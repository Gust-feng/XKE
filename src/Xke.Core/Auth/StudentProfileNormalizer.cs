using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Storage;

namespace Xke.Core.Auth;

public static class StudentProfileNormalizer
{
    public static StudentProfileDocument FromUser(string user)
    {
        var studentId = XkeDataRoot.SanitizePathSegment(user);
        return new StudentProfileDocument
        {
            StudentId = studentId,
            Name = user.Trim(),
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
    }

    public static StudentProfileDocument FromJson(string user, JsonElement payload)
    {
        return FromJson(user, [payload]);
    }

    public static StudentProfileDocument FromJson(string user, params JsonElement[] payloads)
    {
        var studentId = Pick(payloads,
            "student_id", "studentID", "studentId", "studentNo", "student_no",
            "userNo", "user_no", "xh", "xgh", "account", "accountNo", "loginName", "username", "学号");
        if (string.IsNullOrWhiteSpace(studentId))
        {
            studentId = user;
        }

        return new StudentProfileDocument
        {
            StudentId = XkeDataRoot.SanitizePathSegment(studentId),
            Name = Pick(payloads, "name", "realName", "real_name", "studentName", "student_name", "xm", "userName", "user_name", "displayName", "姓名"),
            Nickname = Pick(payloads, "nickname", "nickName", "nick_name"),
            UseNickname = PickBool(payloads, "use_nickname", "useNickname"),
            ClassName = Pick(payloads, "className", "class_name", "clsName", "cls_name", "bjmc", "bj", "classNo", "classCode", "class_code", "administrativeClassName", "班级", "班级名称"),
            MajorName = Pick(payloads, "majorName", "major_name", "zymc", "zy", "zyName", "major", "profession", "professionName", "profession_name", "specialty", "specialtyName", "specialityName", "specialty_name", "专业", "专业名称"),
            AcademyName = Pick(payloads, "academyName", "academy_name", "academy", "college", "collegeName", "college_name", "xymc", "yxmc", "yx", "dept", "deptName", "department", "departmentName", "faculty", "facultyName", "院系", "学院", "系部"),
            TrainingLevel = Pick(payloads, "trainingLevel", "training_level", "pycc", "studentType", "student_type", "levelName", "level_name", "educationalLevel", "educationLevel", "eduLevel", "培养层次", "层次", "学历层次"),
            CampusName = Pick(payloads, "campusName", "campus_name", "xqmc", "campus", "campusAreaName", "校区"),
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
    }

    private static string Pick(JsonElement payload, params string[] keys)
    {
        return Pick([payload], keys);
    }

    private static string Pick(IEnumerable<JsonElement> payloads, params string[] keys)
    {
        foreach (var payload in payloads)
        {
            foreach (var source in EnumerateSources(payload))
            {
                foreach (var key in keys)
                {
                    if (!TryGetPropertyIgnoreCase(source, key, out var element))
                    {
                        continue;
                    }

                    var value = ElementToString(element);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }
        }

        return string.Empty;
    }

    private static bool PickBool(JsonElement payload, params string[] keys)
    {
        return PickBool([payload], keys);
    }

    private static bool PickBool(IEnumerable<JsonElement> payloads, params string[] keys)
    {
        foreach (var payload in payloads)
        {
            foreach (var source in EnumerateSources(payload))
            {
                foreach (var key in keys)
                {
                    if (!TryGetPropertyIgnoreCase(source, key, out var element))
                    {
                        continue;
                    }

                    if (element.ValueKind == JsonValueKind.True)
                    {
                        return true;
                    }

                    if (element.ValueKind == JsonValueKind.False)
                    {
                        return false;
                    }

                    var text = ElementToString(element).ToLowerInvariant();
                    if (text is "1" or "true" or "yes" or "on")
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static IEnumerable<JsonElement> EnumerateSources(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in EnumerateObjectSources(payload, depth: 0))
        {
            var key = source.GetRawText();
            if (seen.Add(key))
            {
                yield return source;
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateObjectSources(JsonElement element, int depth)
    {
        if (depth > 3 || element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        yield return element;
        foreach (var wrapperKey in new[]
        {
            "data", "result", "payload", "body", "student", "studentInfo",
            "student_info", "user", "userInfo", "user_info", "info", "profile", "rows", "list", "records", "item"
        })
        {
            if (!TryGetPropertyIgnoreCase(element, wrapperKey, out var nested))
            {
                continue;
            }

            if (nested.ValueKind == JsonValueKind.Object)
            {
                foreach (var source in EnumerateObjectSources(nested, depth + 1))
                {
                    yield return source;
                }
            }
            else if (nested.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in nested.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    foreach (var source in EnumerateObjectSources(item, depth + 1))
                    {
                        yield return source;
                    }

                    break;
                }
            }
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement source, string key, out JsonElement value)
    {
        if (source.TryGetProperty(key, out value))
        {
            return true;
        }

        foreach (var property in source.EnumerateObject())
        {
            if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string ElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object => Pick(element, "name", "label", "text", "value", "title", "mc", "名称"),
            _ => string.Empty,
        };
    }
}
