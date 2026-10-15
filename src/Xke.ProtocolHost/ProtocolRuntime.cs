using System.Globalization;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xke.Storage;

namespace Xke.ProtocolHost;

/// <summary>One school connection per process. Only ordinary business files are persisted.</summary>
public sealed class ProtocolRuntime(
    XkeDataRoot dataRoot,
    ICaptchaRecognizer captchaRecognizer,
    Func<string, string, DirectJwLoginProtocol> loginProtocolFactory) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TraditionalJwScheduleHtmlParser _parser = new();
    private TraditionalJwClient? _client;
    private StudentProfileDocument? _profile;

    public async Task<ProtocolConnectionData> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.User) || string.IsNullOrEmpty(request.Password))
            throw new JwScheduleException("账号和密码不能为空", 400);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var protocol = loginProtocolFactory(request.User.Trim(), request.Password);
            var image = await protocol.BeginAsync(cancellationToken).ConfigureAwait(false);
            var code = await captchaRecognizer.RecognizeAsync(image, cancellationToken).ConfigureAwait(false);
            await protocol.CompleteAsync(code, cancellationToken).ConfigureAwait(false);
            var candidate = protocol.TransferAuthenticatedSession();
            try
            {
                var profile = await candidate.FetchStudentProfileAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                dataRoot.ForStudent(profile.StudentId).SaveProfile(profile);
                ClearConnection();
                _client = candidate;
                _profile = profile;
                return new ProtocolConnectionData(true, profile);
            }
            catch
            {
                candidate.Dispose();
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public Task<ProtocolConnectionData> GetConnectionAsync(CancellationToken cancellationToken)
        => UseConnectionAsync((_, profile, _) => Task.FromResult(new ProtocolConnectionData(true, profile)), cancellationToken);

    public Task<StudentProfileDocument> FetchProfileAsync(CancellationToken cancellationToken)
        => UseConnectionAsync(async (client, _, token) =>
        {
            var profile = await client.FetchStudentProfileAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            dataRoot.ForStudent(profile.StudentId).SaveProfile(profile);
            _profile = profile;
            return profile;
        }, cancellationToken);

    public Task<Dictionary<string, object?>> FetchWeekAsync(int? week, string academicTerm, CancellationToken cancellationToken)
    {
        if (week is <= 0) throw new JwScheduleException("week 必须是正整数", 400);
        var request = new ScheduleRequest
        {
            Mode = week.HasValue ? "manual" : "current",
            ManualWeek = week?.ToString(CultureInfo.InvariantCulture) ?? "",
            AcademicTerm = academicTerm,
        };
        return UseConnectionAsync(async (client, profile, token) =>
        {
            var result = await client.FetchScheduleAsync(request, _parser, token).ConfigureAwait(false);
            var payload = ScheduleDataMapper.FromWeek(result, profile, request);
            token.ThrowIfCancellationRequested();
            dataRoot.ForStudent(profile.StudentId).Schedules().SaveSourceSchedule(payload, [], result.SourceKey,
                payload["week_label"]?.ToString() ?? result.WeekLabel);
            return payload;
        }, cancellationToken);
    }

    public Task<SemesterScheduleData> FetchSemesterAsync(string academicTerm, CancellationToken cancellationToken)
        => UseConnectionAsync(async (client, profile, token) =>
        {
            var result = await client.FetchSemesterScheduleAsync(academicTerm, _parser, token).ConfigureAwait(false);
            var payload = ScheduleDataMapper.FromSemester(result, profile);
            token.ThrowIfCancellationRequested();
            return dataRoot.ForStudent(profile.StudentId).Schedules().SaveSemesterSchedule(payload);
        }, cancellationToken);

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { ClearConnection(); }
        finally { _gate.Release(); }
    }

    private async Task<T> UseConnectionAsync<T>(Func<TraditionalJwClient, StudentProfileDocument, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is null || _profile is null)
                throw new JwScheduleException("尚未连接教务，请重新登录", 401);
            try { return await action(_client, _profile, cancellationToken).ConfigureAwait(false); }
            catch (JwScheduleException ex) when (ex.StatusCode == 401)
            {
                ClearConnection();
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    private void ClearConnection()
    {
        _client?.Dispose();
        _client = null;
        _profile = null;
    }

    public void Dispose()
    {
        ClearConnection();
        _gate.Dispose();
    }
}
