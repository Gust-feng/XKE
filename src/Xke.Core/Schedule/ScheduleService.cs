using System.Text.RegularExpressions;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth.Jw;
using Xke.Storage;

namespace Xke.Core.Schedule;

public sealed class ScheduleService
{
    private readonly XkeDataRoot _dataRoot;
    private readonly IJwScheduleClient _scheduleClient;
    private readonly MobileSessionRuntimeStore _mobileSessionRuntime;
    private readonly JwCaptchaChallengeStore _captchaChallengeStore;
    private readonly IJwCaptchaSolver _captchaSolver;

    public ScheduleService(
        XkeDataRoot dataRoot,
        IJwScheduleClient scheduleClient,
        MobileSessionRuntimeStore mobileSessionRuntime,
        JwCaptchaChallengeStore captchaChallengeStore,
        IJwCaptchaSolver captchaSolver)
    {
        _dataRoot = dataRoot;
        _scheduleClient = scheduleClient;
        _mobileSessionRuntime = mobileSessionRuntime;
        _captchaChallengeStore = captchaChallengeStore;
        _captchaSolver = captchaSolver;
    }

    public (string Message, Dictionary<string, object?> Payload) LoadLocal(string sessionId, string weekLabel, string source)
    {
        var context = LoadContext(sessionId);
        var sourceKeys = XkeScheduleStore.ResolveSourceKeys(source);
        Dictionary<string, object?>? cached;
        if (!string.IsNullOrWhiteSpace(weekLabel))
        {
            cached = context.ScheduleStore.LoadScheduleForWeek(weekLabel, sourceKeys);
        }
        else
        {
            var inferredCurrentWeek = context.ScheduleStore.FindScheduleWeekLabelForDate(null, sourceKeys);
            cached = !string.IsNullOrWhiteSpace(inferredCurrentWeek)
                ? context.ScheduleStore.LoadScheduleForWeek(inferredCurrentWeek, sourceKeys)
                : null;
            cached ??= context.ScheduleStore.LoadLatestSourceSchedule(sourceKeys);
        }

        if (cached is null)
        {
            throw new ScheduleException(404, "当前账号暂无匹配的本地课表缓存，请使用密码登录重新完成初始化");
        }

        var payload = NormalizeSchedulePayload(
            cached,
            context,
            cached: true,
            dataOrigin: "local_cache",
            sourceKey: cached.GetValueOrDefault("source_key")?.ToString() ?? string.Empty,
            sourceLabel: cached.GetValueOrDefault("source_label")?.ToString() ?? cached.GetValueOrDefault("source")?.ToString() ?? "课表数据",
            sourceType: cached.GetValueOrDefault("source_type")?.ToString() ?? string.Empty,
            fallbackReason: string.Empty,
            request: null);

        return ("本地课表已加载", payload);
    }

    public async Task<(string Message, Dictionary<string, object?> Payload)> FetchPreferredAsync(
        string sessionId,
        ScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        try
        {
            _mobileSessionRuntime.TryGetToken(sessionId, out var existingToken);
            var result = await _scheduleClient.FetchPreferredAsync(
                context.Session.User,
                context.Credentials.Password,
                context.Session.PreloginMode,
                request,
                cancellationToken,
                existingToken,
                context.Credentials.MobileJw).ConfigureAwait(false);
            SaveMobileJwCredentialIfNeeded(context, result.MobileJw);
            var payload = SaveLivePayload(context, result, request, fallbackReason: string.Empty);
            return ("课表获取成功", payload);
        }
        catch (JwScheduleException ex)
        {
            throw new ScheduleException(500, ex.Message);
        }
    }

    public async Task<(string Message, Dictionary<string, object?> Payload)> FetchCookieAsync(
        string sessionId,
        ScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        try
        {
            var result = await _scheduleClient.FetchCookieAsync(
                context.Session.User,
                context.Credentials.Password,
                context.Session.PreloginMode,
                request,
                cancellationToken,
                existingToken: string.Empty,
                mobileJw: context.Credentials.MobileJw,
                sessionId: context.SessionId).ConfigureAwait(false);
            var payload = SaveLivePayload(context, result, request, fallbackReason: string.Empty);
            return ("Cookie 课表获取成功", payload);
        }
        catch (JwCaptchaRequiredException ex)
        {
            var challenge = _captchaChallengeStore.Create(context.SessionId, request, ex);
            return ("需要输入教务系统验证码", BuildCaptchaPayload(challenge));
        }
        catch (JwScheduleException ex)
        {
            throw new ScheduleException(ex.StatusCode, ex.Message);
        }
    }

    public async Task<(string Message, Dictionary<string, object?> Payload)> SubmitJwCaptchaAsync(
        string sessionId,
        JwCaptchaSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ScheduleException(400, "验证码提交参数不能为空");
        }

        var context = LoadContext(sessionId);
        return await _captchaChallengeStore.UseAsync(
            request.ChallengeId,
            context.SessionId,
            async (challenge, token) =>
            {
                var solved = await _captchaSolver.ResolveAsync(
                    new JwCaptchaSolveContext(
                        challenge.ChallengeId,
                        challenge.CaptchaImage,
                        request.CaptchaCode),
                    token).ConfigureAwait(false);
                if (!solved.HasCode)
                {
                    throw new ScheduleException(400, solved.FailureReason);
                }

                if (challenge.ContinueAsync is null)
                {
                    throw new ScheduleException(409, "验证码已提交，但真实教务登录续跑尚未接入；请使用开发态 HTML fixture 或等待下一切片");
                }

                try
                {
                    var result = await challenge.ContinueAsync(solved.Code, token).ConfigureAwait(false);
                    var payload = SaveLivePayload(context, result, challenge.Request, fallbackReason: string.Empty);
                    return ("Cookie 课表获取成功", payload);
                }
                catch (JwScheduleException ex)
                {
                    throw new ScheduleException(ex.StatusCode, ex.Message);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> BuildCaptchaPayload(JwCaptchaChallenge challenge)
    {
        var payload = new JwCaptchaChallengePayload(
            Status: "captcha_required",
            ChallengeId: challenge.ChallengeId,
            CaptchaImage: challenge.CaptchaImage,
            ExpiresInSeconds: challenge.RemainingSeconds(DateTimeOffset.Now));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = payload.Status,
            ["challenge_id"] = payload.ChallengeId,
            ["captcha_image"] = payload.CaptchaImage,
            ["expires_in_seconds"] = payload.ExpiresInSeconds,
        };
    }

    private Dictionary<string, object?> SaveLivePayload(
        ScheduleContext context,
        JwScheduleFetchResult result,
        ScheduleRequest request,
        string fallbackReason)
    {
        var payload = BuildPayloadFromFetchResult(result);
        payload = NormalizeSchedulePayload(
            payload,
            context,
            cached: false,
            dataOrigin: "live_fetch",
            sourceKey: result.SourceKey,
            sourceLabel: result.SourceLabel,
            sourceType: result.SourceType,
            fallbackReason: fallbackReason,
            request: request);

        context.ScheduleStore.SaveSourceSchedule(
            payload,
            result.RawPayload,
            payload.GetValueOrDefault("source_key")?.ToString() ?? result.SourceKey,
            payload.GetValueOrDefault("week_label")?.ToString() ?? result.WeekLabel);
        return payload;
    }

    private static Dictionary<string, object?> BuildPayloadFromFetchResult(JwScheduleFetchResult result)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["source"] = result.SourceLabel,
            ["source_label"] = result.SourceLabel,
            ["source_key"] = result.SourceKey,
            ["source_type"] = result.SourceType,
            ["rows"] = result.Rows.Cast<object?>().ToList(),
            ["week_label"] = result.WeekLabel,
            ["current_week_label"] = ResolveCurrentWeekLabel(result),
            ["available_weeks"] = result.AvailableWeeks.Cast<object?>().ToList(),
            ["week_dates"] = result.WeekDates.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal),
            ["current_week_number"] = result.CurrentWeekNumber,
            ["viewed_week_number"] = result.ViewedWeekNumber,
            ["remarks"] = result.Remarks,
            ["block_times"] = result.BlockTimes.Cast<object?>().ToList(),
            ["fallback_reason"] = result.FallbackReason,
            ["data_warnings"] = result.DataWarnings.Cast<object?>().ToList(),
        };
    }

    private static string ResolveCurrentWeekLabel(JwScheduleFetchResult result)
    {
        return result.CurrentWeekNumber is int currentWeekNumber && currentWeekNumber > 0
            ? $"第{currentWeekNumber}周"
            : result.WeekLabel;
    }

    private Dictionary<string, object?> NormalizeSchedulePayload(
        Dictionary<string, object?> rawPayload,
        ScheduleContext context,
        bool cached,
        string dataOrigin,
        string sourceKey,
        string sourceLabel,
        string sourceType,
        string fallbackReason,
        ScheduleRequest? request)
    {
        var payload = new Dictionary<string, object?>(rawPayload, StringComparer.Ordinal);
        var resolvedSourceKey = XkeScheduleStore.NormalizeSourceKey(sourceKey);
        var resolvedSourceType = NormalizeSourceType(sourceType, resolvedSourceKey);
        var resolvedSourceLabel = string.IsNullOrWhiteSpace(sourceLabel)
            ? ResolveSourceLabel(resolvedSourceKey)
            : sourceLabel.Trim();
        var weekLabel = (payload.GetValueOrDefault("week_label")?.ToString() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(weekLabel))
        {
            weekLabel = "当前周";
        }

        var currentWeekNumber = ReadInt(payload.GetValueOrDefault("current_week_number"));
        var viewedWeekNumber = ReadInt(payload.GetValueOrDefault("viewed_week_number"));
        var currentWeekLabel = (payload.GetValueOrDefault("current_week_label")?.ToString() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(currentWeekLabel) && currentWeekNumber is int resolvedCurrentWeekNumber && resolvedCurrentWeekNumber > 0)
        {
            currentWeekLabel = $"第{resolvedCurrentWeekNumber}周";
        }

        if (string.IsNullOrWhiteSpace(currentWeekLabel))
        {
            currentWeekLabel = weekLabel;
        }

        currentWeekNumber ??= ParseWeekNumber(currentWeekLabel);
        viewedWeekNumber ??= ParseWeekNumber(weekLabel);
        var dataWarnings = NormalizeList(payload.GetValueOrDefault("data_warnings"));

        payload["source"] = resolvedSourceLabel;
        payload["source_label"] = resolvedSourceLabel;
        payload["source_key"] = resolvedSourceKey;
        payload["source_type"] = resolvedSourceType;
        payload["rows"] = NormalizeScheduleRows(payload.GetValueOrDefault("rows"), dataWarnings);
        payload["week_label"] = weekLabel;
        payload["available_weeks"] = NormalizeAvailableWeeks(payload.GetValueOrDefault("available_weeks"), weekLabel, currentWeekLabel);
        payload["week_dates"] = NormalizeStringDictionary(payload.GetValueOrDefault("week_dates"));
        payload["current_week_label"] = currentWeekLabel;
        payload["current_week_number"] = currentWeekNumber;
        payload["viewed_week_number"] = viewedWeekNumber;
        payload["remarks"] = (payload.GetValueOrDefault("remarks")?.ToString() ?? (cached ? "当前显示本地缓存课表" : "课表刷新成功")).Trim();
        payload["block_times"] = NormalizeList(payload.GetValueOrDefault("block_times"));
        payload["fallback_reason"] = string.IsNullOrWhiteSpace(fallbackReason)
            ? (payload.GetValueOrDefault("fallback_reason")?.ToString() ?? string.Empty)
            : fallbackReason;
        payload["data_warnings"] = DeduplicateScheduleWarnings(dataWarnings);
        payload["term"] = NormalizeTerm(payload.GetValueOrDefault("term"), request);
        payload["student"] = BuildStudentPayload(context.Profile);
        payload["cached"] = cached;
        payload["data_origin"] = dataOrigin;
        payload["updated_at"] = NormalizeTimestamp(payload.GetValueOrDefault("updated_at"));
        payload["file_updated_at"] = payload.GetValueOrDefault("_file_updated_at")?.ToString()
            ?? payload.GetValueOrDefault("file_updated_at")?.ToString()
            ?? string.Empty;

        if (cached)
        {
            var savedFiles = NormalizeList(payload.GetValueOrDefault("saved_files"));
            var sourceFile = payload.GetValueOrDefault("_file")?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sourceFile))
            {
                savedFiles = new List<object?> { _dataRoot.RelativePath(sourceFile) };
            }

            payload["saved_files"] = savedFiles;
        }
        else if (!payload.ContainsKey("saved_files"))
        {
            payload["saved_files"] = new List<object?>();
        }

        payload.Remove("_file");
        payload.Remove("_file_updated_at");
        return payload;
    }

    private static int? ReadInt(object? value)
    {
        return value switch
        {
            int directInt => directInt,
            long directLong when directLong >= int.MinValue && directLong <= int.MaxValue => (int)directLong,
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt32(out var numberInt) => numberInt,
            JsonElement { ValueKind: JsonValueKind.String } element when int.TryParse(element.GetString(), out var stringInt) => stringInt,
            _ => int.TryParse(value?.ToString(), out var parsed) ? parsed : null,
        };
    }

    private void SaveMobileJwCredentialIfNeeded(ScheduleContext context, MobileJwEncryptedCredentialDocument? mobileJw)
    {
        if (mobileJw is null || string.IsNullOrWhiteSpace(mobileJw.EncryptedPassword))
        {
            return;
        }

        _dataRoot.Credentials().Save(new StoredCredentialDocument
        {
            User = context.Credentials.User,
            Password = context.Credentials.Password,
            StudentId = context.Credentials.StudentId,
            TrustedLoginToken = context.Credentials.TrustedLoginToken,
            MobileJw = mobileJw,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });
    }

    private static Dictionary<string, object?> BuildStudentPayload(StudentProfileDocument profile)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["student_id"] = profile.StudentId,
            ["name"] = string.IsNullOrWhiteSpace(profile.Name) ? profile.StudentId : profile.Name,
            ["class_name"] = profile.ClassName,
            ["major_name"] = profile.MajorName,
            ["academy_name"] = profile.AcademyName,
            ["training_level"] = profile.TrainingLevel,
            ["campus_name"] = profile.CampusName,
        };
    }

    private ScheduleContext LoadContext(string sessionId)
    {
        var cleanedSessionId = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanedSessionId))
        {
            throw new ScheduleException(400, "会话不能为空");
        }

        var session = _dataRoot.Sessions().Load(cleanedSessionId)
            ?? throw new ScheduleException(401, "会话不存在或已过期，请重新登录");
        var credentials = _dataRoot.Credentials().Load(session.User)
            ?? throw new ScheduleException(401, "本地凭据不存在，请重新登录");
        var studentId = string.IsNullOrWhiteSpace(session.StudentId) ? session.User : session.StudentId;
        var studentStore = _dataRoot.ForStudent(studentId);
        var profile = studentStore.LoadProfile() ?? new StudentProfileDocument
        {
            StudentId = studentId,
            Name = studentId,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };

        return new ScheduleContext(
            SessionId: cleanedSessionId,
            Session: session,
            Credentials: credentials,
            StudentStore: studentStore,
            ScheduleStore: studentStore.Schedules(),
            Profile: profile);
    }

    private static List<object?> NormalizeList(object? value)
    {
        return value switch
        {
            null => [],
            List<object?> list => list,
            IEnumerable<object?> values => values.ToList(),
            System.Collections.IEnumerable values => values.Cast<object?>().ToList(),
            _ => [],
        };
    }

    private static List<object?> NormalizeScheduleRows(object? value, List<object?> dataWarnings)
    {
        var rows = NormalizeList(value);
        var normalized = new List<object?>();
        var skipped = 0;

        foreach (var item in rows)
        {
            var row = NormalizeObjectDictionary(item);
            if (row.Count == 0)
            {
                skipped++;
                continue;
            }

            var courseName = FirstText(row, "course_name", "course", "name", "title");
            var weekday = NormalizeWeekdayText(FirstText(row, "weekday", "day", "week_day"));
            var section = FirstNormalizedSectionText(row, "section", "sections", "class_time", "time_text");
            if (string.IsNullOrWhiteSpace(courseName)
                || string.IsNullOrWhiteSpace(weekday)
                || string.IsNullOrWhiteSpace(section))
            {
                skipped++;
                continue;
            }

            row["course_name"] = courseName;
            row["weekday"] = weekday;
            row["section"] = section;
            row["source"] = FirstText(row, "source");
            row["week_label"] = FirstText(row, "week_label");
            row["time_range"] = FirstText(row, "time_range");
            row["block_time"] = FirstText(row, "block_time");
            row["small_section_1"] = FirstText(row, "small_section_1");
            row["small_section_2"] = FirstText(row, "small_section_2");
            row["teacher"] = TrimPrefix(FirstText(row, "teacher", "teacher_name"), "教师：");
            row["room"] = FirstText(row, "room", "classroom", "location");
            row["weeks"] = FirstText(row, "weeks", "week", "week_info");
            row["credit"] = FirstText(row, "credit");
            if (!row.ContainsKey("break_minutes"))
            {
                row["break_minutes"] = 0;
            }

            var timeText = FirstText(row, "time_text");
            if (string.IsNullOrWhiteSpace(timeText))
            {
                row["time_text"] = string.Join(" / ", new[]
                {
                    weekday,
                    section,
                    row.GetValueOrDefault("time_range")?.ToString() ?? string.Empty,
                }.Where(part => !string.IsNullOrWhiteSpace(part)));
            }

            normalized.Add(row);
        }

        if (skipped > 0)
        {
            dataWarnings.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "schedule-row-invalid",
                ["message"] = $"已跳过 {skipped} 条缺少 weekday/section/course_name 的不可渲染课表行",
            });
        }

        return MergeDuplicateScheduleRows(normalized, dataWarnings);
    }

    private static List<object?> MergeDuplicateScheduleRows(List<object?> rows, List<object?> dataWarnings)
    {
        var mergedRows = new List<Dictionary<string, object?>>();
        var bySlot = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);

        foreach (var item in rows)
        {
            if (item is not Dictionary<string, object?> row)
            {
                continue;
            }

            var key = string.Join(
                "\u001f",
                row.GetValueOrDefault("weekday")?.ToString() ?? string.Empty,
                row.GetValueOrDefault("section")?.ToString() ?? string.Empty,
                row.GetValueOrDefault("course_name")?.ToString() ?? string.Empty,
                row.GetValueOrDefault("teacher")?.ToString() ?? string.Empty);
            if (!bySlot.TryGetValue(key, out var existing))
            {
                bySlot[key] = row;
                mergedRows.Add(row);
                continue;
            }

            var existingRooms = SplitRoomCandidates(existing.GetValueOrDefault("room")?.ToString());
            var incomingRooms = SplitRoomCandidates(row.GetValueOrDefault("room")?.ToString());
            var addedRooms = incomingRooms
                .Where(room => !existingRooms.Contains(room, StringComparer.Ordinal))
                .ToList();
            if (addedRooms.Count > 0)
            {
                existingRooms.AddRange(addedRooms);
                existing["room"] = string.Join(" / ", existingRooms);
                existing["conflict"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "room_conflict",
                    ["kept_room"] = existingRooms[0],
                    ["discarded_rooms"] = existingRooms.Skip(1).Cast<object?>().ToList(),
                };
                dataWarnings.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["type"] = "duplicate_slot",
                    ["course_name"] = existing.GetValueOrDefault("course_name")?.ToString() ?? string.Empty,
                    ["weekday"] = existing.GetValueOrDefault("weekday")?.ToString() ?? string.Empty,
                    ["section"] = existing.GetValueOrDefault("section")?.ToString() ?? string.Empty,
                    ["kept_room"] = existingRooms[0],
                    ["kept_weeks"] = existing.GetValueOrDefault("weeks")?.ToString() ?? string.Empty,
                    ["discarded"] = addedRooms
                        .Select(room => (object?)new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["room"] = room,
                        })
                        .ToList(),
                    ["conflict"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["kind"] = "room_conflict",
                    },
                    ["message"] = "检测到同课程同时间存在多个教室，已合并为一条课表并保留全部教室",
                });
            }

            MergeScheduleRowText(existing, row, "weeks", " / ");
            MergeScheduleRowText(existing, row, "credit", " / ");
            MergeScheduleRowText(existing, row, "time_range", " / ");
            MergeScheduleRowText(existing, row, "block_time", " / ");
            FillScheduleRowText(existing, row, "small_section_1");
            FillScheduleRowText(existing, row, "small_section_2");
            FillScheduleRowText(existing, row, "source");
            FillScheduleRowText(existing, row, "week_label");
        }

        return mergedRows.Cast<object?>().ToList();
    }

    private static List<object?> DeduplicateScheduleWarnings(List<object?> warnings)
    {
        var result = new List<object?>();
        var byKey = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var primitiveKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var warning in warnings)
        {
            var dictionary = NormalizeObjectDictionary(warning);
            if (dictionary.Count == 0)
            {
                var primitiveKey = warning?.ToString() ?? string.Empty;
                if (primitiveKeys.Add(primitiveKey))
                {
                    result.Add(warning);
                }

                continue;
            }

            var key = BuildScheduleWarningKey(dictionary);
            if (!byKey.TryGetValue(key, out var existing))
            {
                byKey[key] = dictionary;
                result.Add(dictionary);
                continue;
            }

            MergeScheduleWarningDiscardedRooms(existing, dictionary);
        }

        return result;
    }

    private static string BuildScheduleWarningKey(Dictionary<string, object?> warning)
    {
        var type = (warning.GetValueOrDefault("type")?.ToString() ?? string.Empty).Trim();
        if (string.Equals(type, "duplicate_slot", StringComparison.Ordinal))
        {
            return string.Join(
                "\u001f",
                type,
                warning.GetValueOrDefault("course_name")?.ToString() ?? string.Empty,
                warning.GetValueOrDefault("weekday")?.ToString() ?? string.Empty,
                warning.GetValueOrDefault("section")?.ToString() ?? string.Empty,
                warning.GetValueOrDefault("kept_room")?.ToString() ?? string.Empty);
        }

        return string.Join(
            "\u001f",
            type,
            warning.GetValueOrDefault("course_name")?.ToString() ?? string.Empty,
            warning.GetValueOrDefault("weekday")?.ToString() ?? string.Empty,
            warning.GetValueOrDefault("section")?.ToString() ?? string.Empty,
            warning.GetValueOrDefault("kept_room")?.ToString() ?? string.Empty,
            warning.GetValueOrDefault("kept_weeks")?.ToString() ?? string.Empty,
            warning.GetValueOrDefault("message")?.ToString() ?? string.Empty);
    }

    private static void MergeScheduleWarningDiscardedRooms(Dictionary<string, object?> target, Dictionary<string, object?> source)
    {
        var rooms = new List<string>();
        foreach (var item in NormalizeList(target.GetValueOrDefault("discarded")).Concat(NormalizeList(source.GetValueOrDefault("discarded"))))
        {
            var room = NormalizeObjectDictionary(item).GetValueOrDefault("room")?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(room) && !rooms.Contains(room, StringComparer.Ordinal))
            {
                rooms.Add(room);
            }
        }

        if (rooms.Count > 0)
        {
            target["discarded"] = rooms
                .Select(room => (object?)new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["room"] = room,
                })
                .ToList();
        }
    }

    private static List<string> SplitRoomCandidates(string? value)
    {
        return (value ?? string.Empty)
            .Split(["/", "／", "|", "、"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(room => room.Trim())
            .Where(room => !string.IsNullOrWhiteSpace(room))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void MergeScheduleRowText(Dictionary<string, object?> target, Dictionary<string, object?> source, string key, string separator)
    {
        var current = target.GetValueOrDefault(key)?.ToString() ?? string.Empty;
        var incoming = source.GetValueOrDefault(key)?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(incoming) || current.Contains(incoming, StringComparison.Ordinal))
        {
            return;
        }

        target[key] = string.IsNullOrWhiteSpace(current) ? incoming : $"{current}{separator}{incoming}";
    }

    private static void FillScheduleRowText(Dictionary<string, object?> target, Dictionary<string, object?> source, string key)
    {
        if (!string.IsNullOrWhiteSpace(target.GetValueOrDefault(key)?.ToString()))
        {
            return;
        }

        var incoming = source.GetValueOrDefault(key)?.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(incoming))
        {
            target[key] = incoming;
        }
    }

    private static Dictionary<string, object?> NormalizeObjectDictionary(object? value)
    {
        if (value is Dictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        if (value is System.Collections.IDictionary dictionaryObject)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (System.Collections.DictionaryEntry entry in dictionaryObject)
            {
                if (entry.Key is string key)
                {
                    result[key] = entry.Value;
                }
            }

            return result;
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                result[property.Name] = JsonElementToObject(property.Value);
            }

            return result;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => NormalizeObjectDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            _ => null,
        };
    }

    private static string FirstText(Dictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value))
            {
                var text = value?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }

    private static string FirstNormalizedSectionText(Dictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value))
            {
                continue;
            }

            var normalized = NormalizeSectionText(value?.ToString() ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return string.Empty;
    }

    private static string NormalizeWeekdayText(string value)
    {
        var cleaned = (value ?? string.Empty).Trim();
        var match = Regex.Match(cleaned, @"星期[一二三四五六日天]");
        if (match.Success)
        {
            return match.Value == "星期天" ? "星期日" : match.Value;
        }

        return cleaned switch
        {
            "周一" or "1" => "星期一",
            "周二" or "2" => "星期二",
            "周三" or "3" => "星期三",
            "周四" or "4" => "星期四",
            "周五" or "5" => "星期五",
            "周六" or "6" => "星期六",
            "周日" or "周天" or "0" or "7" => "星期日",
            _ => cleaned,
        };
    }

    private static string NormalizeSectionText(string value)
    {
        return ScheduleSectionNormalizer.NormalizeSectionText(value);
    }

    private static List<int> ExtractSectionNumbers(string? value)
    {
        return ScheduleSectionNormalizer.ExtractSectionNumbers(value);
    }

    private static string TrimPrefix(string value, string prefix)
    {
        var cleaned = (value ?? string.Empty).Trim();
        return cleaned.StartsWith(prefix, StringComparison.Ordinal)
            ? cleaned[prefix.Length..].Trim()
            : cleaned;
    }

    private static Dictionary<string, object?> NormalizeStringDictionary(object? value)
    {
        if (value is Dictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static List<object?> NormalizeAvailableWeeks(object? value, string weekLabel, string currentWeekLabel)
    {
        var weeks = NormalizeList(value);
        if (weeks.Count == 0 && !string.IsNullOrWhiteSpace(weekLabel))
        {
            weeks.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = ParseWeekNumber(weekLabel)?.ToString() ?? weekLabel,
                ["label"] = weekLabel,
                ["selected"] = string.Equals(weekLabel, currentWeekLabel, StringComparison.Ordinal),
            });
        }

        return weeks;
    }

    private static Dictionary<string, object?> NormalizeTerm(object? value, ScheduleRequest? request)
    {
        var term = value is Dictionary<string, object?> dictionary
            ? new Dictionary<string, object?>(dictionary, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!term.ContainsKey("label"))
        {
            term["label"] = request?.TermLabel?.Trim() ?? string.Empty;
        }

        if (!term.ContainsKey("academic_term"))
        {
            term["academic_term"] = request?.AcademicTerm?.Trim() ?? string.Empty;
        }

        return term;
    }

    public static int? ParseWeekNumber(string? weekLabel)
    {
        var cleaned = Regex.Replace(weekLabel ?? string.Empty, @"\s+", string.Empty);
        var match = Regex.Match(cleaned, @"\d+");
        if (match.Success)
        {
            return int.Parse(match.Value);
        }

        var chineseMatch = Regex.Match(cleaned, @"第?([零〇一二两三四五六七八九十百]+)周?");
        return chineseMatch.Success ? ParseChineseNumber(chineseMatch.Groups[1].Value) : null;
    }

    private static int? ParseChineseNumber(string value)
    {
        var total = 0;
        var current = 0;
        foreach (var ch in value ?? string.Empty)
        {
            if (TryParseChineseDigit(ch, out var digit))
            {
                current = digit;
                continue;
            }

            var unit = ch switch
            {
                '十' => 10,
                '百' => 100,
                _ => 0,
            };
            if (unit == 0)
            {
                return null;
            }

            total += (current == 0 ? 1 : current) * unit;
            current = 0;
        }

        total += current;
        return total > 0 ? total : null;
    }

    private static bool TryParseChineseDigit(char value, out int digit)
    {
        digit = value switch
        {
            '零' or '〇' => 0,
            '一' => 1,
            '二' or '两' => 2,
            '三' => 3,
            '四' => 4,
            '五' => 5,
            '六' => 6,
            '七' => 7,
            '八' => 8,
            '九' => 9,
            _ => -1,
        };
        return digit >= 0;
    }

    private static string NormalizeSourceType(string sourceType, string sourceKey)
    {
        var cleaned = (sourceType ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            return cleaned;
        }

        return sourceKey == "jw_schedule" ? "jw" : "mobile";
    }

    private static string ResolveSourceLabel(string sourceKey)
    {
        return sourceKey == "jw_schedule" ? "教务系统课表" : "移动教务课表";
    }

    private static string NormalizeTimestamp(object? value)
    {
        var raw = value?.ToString() ?? string.Empty;
        return string.IsNullOrWhiteSpace(raw) ? DateTimeOffset.Now.ToString("O") : raw.Trim();
    }

    private sealed record ScheduleContext(
        string SessionId,
        StoredSessionDocument Session,
        StoredCredentialDocument Credentials,
        XkeStudentStore StudentStore,
        XkeScheduleStore ScheduleStore,
        StudentProfileDocument Profile);
}
