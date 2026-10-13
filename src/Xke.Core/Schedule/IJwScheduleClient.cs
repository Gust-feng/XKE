using Xke.Contracts.Requests;
using Xke.Contracts.Models;

namespace Xke.Core.Schedule;

public interface IJwScheduleClient
{
    Task<JwScheduleFetchResult> FetchSemesterAsync(string user, string password, string preloginMode,
        string academicTerm, string sessionId, CancellationToken cancellationToken = default)
        => throw new JwScheduleException("当前来源不提供学期课表", 501);
    Task<JwScheduleFetchResult> FetchPreferredAsync(
        string user,
        string password,
        string preloginMode,
        ScheduleRequest request,
        CancellationToken cancellationToken = default,
        string existingToken = "",
        MobileJwEncryptedCredentialDocument? mobileJw = null);

    Task<JwScheduleFetchResult> FetchCookieAsync(
        string user,
        string password,
        string preloginMode,
        ScheduleRequest request,
        CancellationToken cancellationToken = default,
        string existingToken = "",
        MobileJwEncryptedCredentialDocument? mobileJw = null,
        string sessionId = "");
}

public sealed record JwScheduleFetchResult(
    string SourceKey,
    string SourceLabel,
    string SourceType,
    string WeekLabel,
    int? CurrentWeekNumber,
    int? ViewedWeekNumber,
    IReadOnlyList<Dictionary<string, object?>> AvailableWeeks,
    IReadOnlyDictionary<string, string> WeekDates,
    IReadOnlyList<Dictionary<string, object?>> BlockTimes,
    IReadOnlyList<Dictionary<string, object?>> DataWarnings,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    Dictionary<string, object?> RawPayload,
    string Remarks,
    string FallbackReason,
    MobileJwEncryptedCredentialDocument? MobileJw = null,
    string TermLabel = ""
);
