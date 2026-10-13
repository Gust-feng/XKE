using System.Security.Cryptography;
using System.Text;
using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Storage;

namespace Xke.Core.Network;

public sealed class CampusSelfServiceSummaryService : IDisposable
{
    private readonly XkeDataRoot _root;
    private readonly CampusSelfServiceOptions _options;
    private readonly TimeProvider _time;
    private readonly Func<ICampusSelfServiceClient> _createClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ICampusSelfServiceClient? _client;
    private string? _identity;
    private CampusSelfServiceSummary? _last;
    private DateTimeOffset _nextFetch;

    public CampusSelfServiceSummaryService(XkeDataRoot root, CampusSelfServiceOptions options,
        TimeProvider time, Func<ICampusSelfServiceClient>? createClient = null)
    {
        _root = root;
        _options = options;
        _time = time;
        _createClient = createClient ?? (() => new CampusSelfServiceClient(options));
    }

    public async Task<CampusSelfServiceSummary> GetSummaryAsync(string sessionId, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var session = string.IsNullOrWhiteSpace(sessionId) ? null : _root.Sessions().Load(sessionId);
            if (session is null) throw new AuthException(401, "会话不存在或已过期，请重新登录");
            if (!_options.Configured) return new() { Message = _options.InvalidConfiguration || _options.Endpoint.Length > 0
                ? "自助后台配置不可用" : "尚未配置自助后台" };
            var studentId = string.IsNullOrWhiteSpace(session.StudentId) ? session.User : session.StudentId;
            var credential = _root.ForStudent(studentId).NetworkCredentials().Load("campus");
            if (credential is null || string.IsNullOrWhiteSpace(credential.Account) || string.IsNullOrWhiteSpace(credential.Password))
            {
                _client?.Dispose(); _client = null; _last = null; _identity = null; _nextFetch = default;
                return new() { Configured = true, Message = "请先配置校园网账号和密码" };
            }
            var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(studentId + "\0" + credential.Account + "\0" + credential.Password)));
            if (_identity != identity)
            {
                _client?.Dispose();
                _client = null;
                _last = null;
                _identity = identity;
                _nextFetch = default;
            }
            var now = _time.GetUtcNow();
            if (_last is not null && now < _nextFetch)
                return _last with { Cached = _last.Available };
            try
            {
                _client ??= _createClient();
                var summary = await _client.GetSummaryAsync(credential.Account, credential.Password, token).ConfigureAwait(false);
                _last = summary with { Configured = true, Available = true, Cached = false,
                    UpdatedAt = _time.GetUtcNow().ToString("O"), Message = string.Empty };
                _nextFetch = _time.GetUtcNow().AddMinutes(5);
            }
            catch (CampusSelfServiceException ex)
            {
                _client?.Dispose();
                _client = null;
                _last = (_last ?? new CampusSelfServiceSummary { Configured = true }) with
                {
                    Cached = _last?.Available == true,
                    Message = _last?.Available == true ? "更新未完成，保留上次套餐信息" : ex.Message,
                };
                // No failure cooldown: the page's explicit retry must perform a
                // new request. Summary is not polled by the connectivity worker.
                _nextFetch = default;
            }
            return _last;
        }
        finally { _gate.Release(); }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _gate.Dispose();
    }
}
