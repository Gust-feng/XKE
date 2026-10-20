using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Xke.Core.Schedule;
using Xke.Storage;

namespace Xke.Core.Auth.Jw;

/// <summary>One shared, bounded renewal policy for trusted desktop accounts.</summary>
public sealed class AutomaticCaptchaLogin(XkeDataRoot data, ICaptchaRecognizer? recognizer = null)
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public bool CanUse(string user, string password)
    {
        if (recognizer?.IsAvailable != true || _entries.TryGetValue(user, out var entry) && entry.Blocked) return false;
        var credential = data.Credentials().Load(user);
        // This protected token records prior explicit CAPTCHA initialization.
        // A preference alone never authorizes first login; this local policy is
        // not an attestation against a malicious client controlled by the user.
        return credential is not null && !string.IsNullOrWhiteSpace(credential.TrustedLoginToken)
            && credential.Password == password
            && data.ForStudent(string.IsNullOrWhiteSpace(credential.StudentId) ? credential.User : credential.StudentId).LoadPreferences()?.Auth.AutoCaptchaEnabled != false;
    }

    public CaptchaRecognitionStatus GetStatus(string user)
    {
        var credential = data.Credentials().Load(user);
        var trusted = !string.IsNullOrWhiteSpace(credential?.TrustedLoginToken);
        var enabled = credential is not null && data.ForStudent(string.IsNullOrWhiteSpace(credential.StudentId) ? credential.User : credential.StudentId).LoadPreferences()?.Auth.AutoCaptchaEnabled != false;
        var state = _entries.TryGetValue(user, out var entry) ? Volatile.Read(ref entry.State) : new Progress("idle", 0, "");
        return new(recognizer?.IsAvailable == true, trusted, enabled,
            !enabled ? "disabled" : recognizer?.IsAvailable != true ? "unavailable" : state.State,
            state.Attempt, state.Message);
    }

    public void Reset(string user)
    {
        if (!_entries.TryGetValue(user, out var entry)) return;
        lock (entry.Sync)
        {
            entry.Active?.Cancel();
            entry.Blocked = false;
            Volatile.Write(ref entry.State, new("idle", 0, ""));
        }
    }

    public void RequireManual(string user, string message)
    {
        var entry = _entries.GetOrAdd(user, _ => new Entry());
        entry.Blocked = true;
        Volatile.Write(ref entry.State, new("manual_required", 0, message));
    }

    public void Cancel(string user)
    {
        if (!_entries.TryGetValue(user, out var entry)) return;
        lock (entry.Sync)
        {
            entry.Active?.Cancel();
            Volatile.Write(ref entry.State, new("idle", 0, ""));
        }
    }

    public async Task<bool> TryLoginAsync(string user, string password, DirectJwLoginProtocol protocol, string image,
        CancellationToken cancellationToken, Action? validate = null)
    {
        if (!CanUse(user, password)) return false;
        var entry = _entries.GetOrAdd(user, _ => new Entry());
        await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (entry.Sync) entry.Active = operation;
        try
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                Validate();
                Volatile.Write(ref entry.State, new("recognizing", attempt, ""));
                string code;
                try { code = await recognizer!.RecognizeAsync(image, operation.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { Block("识别组件不可用，请手动输入验证码", attempt); return false; }
                Validate();
                if (Regex.IsMatch(code, "^[A-Za-z0-9]{4}$", RegexOptions.CultureInvariant))
                {
                    try
                    {
                        await protocol.CompleteAsync(code, operation.Token).ConfigureAwait(false);
                        Validate();
                        Volatile.Write(ref entry.State, new("idle", 0, ""));
                        return true;
                    }
                    catch (JwLoginException error) when (error.Kind == JwLoginFailureKind.CaptchaRejected)
                    {
                        // Only this confirmed server error permits another submission.
                    }
                    catch (JwScheduleException error)
                    {
                        Block(error.Message, attempt);
                        throw;
                    }
                }
                if (attempt < 3)
                {
                    Validate();
                    image = await protocol.BeginAsync(operation.Token).ConfigureAwait(false);
                }
            }
            Block("自动识别三次未通过，请手动输入验证码", 3);
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!entry.Blocked) Volatile.Write(ref entry.State, new("idle", 0, ""));
            return false;
        }
        catch (OperationCanceledException)
        {
            if (!entry.Blocked) Volatile.Write(ref entry.State, new("idle", 0, ""));
            throw;
        }
        catch (Exception error) when (error is JwScheduleException { StatusCode: 409 } or AuthException { StatusCode: 409 })
        {
            if (!entry.Blocked) Volatile.Write(ref entry.State, new("idle", 0, ""));
            throw;
        }
        catch
        {
            if (!entry.Blocked) Block("自动认证未完成，请手动验证", Volatile.Read(ref entry.State).Attempt);
            throw;
        }
        finally
        {
            lock (entry.Sync) if (ReferenceEquals(entry.Active, operation)) entry.Active = null;
            entry.Gate.Release();
        }

        void Validate()
        {
            operation.Token.ThrowIfCancellationRequested();
            validate?.Invoke();
            if (!CanUse(user, password)) throw new OperationCanceledException(operation.Token);
        }
        void Block(string message, int attempt)
        {
            entry.Blocked = true;
            Volatile.Write(ref entry.State, new("manual_required", attempt, message));
        }
    }

    private sealed class Entry
    {
        public object Sync { get; } = new();
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public CancellationTokenSource? Active;
        public volatile bool Blocked;
        public Progress State = new("idle", 0, "");
    }
    private sealed record Progress(string State, int Attempt, string Message);
}

public sealed record CaptchaRecognitionStatus(
    [property: JsonPropertyName("supported")] bool Supported,
    [property: JsonPropertyName("trusted")] bool Trusted,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("attempt")] int Attempt,
    [property: JsonPropertyName("message")] string Message);
