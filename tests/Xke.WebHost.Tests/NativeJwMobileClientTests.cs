using System.Net;
using System.Text;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class NativeJwMobileClientTests
{
    [Fact]
    public async Task NativeJwAuthClient_InitializeUsesEncryptedMobileHandshake()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/hnrjzyxyhd/getKey", request.RequestUri!.AbsolutePath);
                Assert.Equal("20260001", ParseQuery(request.RequestUri)["userNo"]);
                return Json(new
                {
                    code = 1,
                    data = "1234567890abcdef",
                });
            },
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/hnrjzyxyhd/login", request.RequestUri!.AbsolutePath);
                var query = ParseQuery(request.RequestUri);
                Assert.Equal("20260001", query["userNo"]);
                Assert.Equal("1", query["encode"]);
                Assert.Equal(string.Empty, query["captchaData"]);
                Assert.Equal(string.Empty, query["codeVal"]);
                Assert.NotEqual("secret-pass", query["pwd"]);
                Assert.False(string.IsNullOrWhiteSpace(query["pwd"]));
                Assert.Equal("null", HeaderValue(request, "token"));
                return Json(new
                {
                    code = 1,
                    data = new
                    {
                        token = "mobile-token",
                    },
                });
            },
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/hnrjzyxyhd/student/my", request.RequestUri!.AbsolutePath);
                Assert.Equal("mobile-token", HeaderValue(request, "token"));
                return Json(new
                {
                    code = 1,
                    data = new
                    {
                        xh = "20260001",
                        xm = "测试同学",
                        bjmc = "软件一班",
                        zymc = "软件技术",
                        xymc = "信息工程学院",
                        pycc = "专科",
                        xqmc = "主校区",
                    },
                });
            },
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwAuthClient(client);

        var result = await jw.InitializeAsync("20260001", "secret-pass", "capture");

        Assert.Equal("20260001", result.Profile.StudentId);
        Assert.Equal("测试同学", result.Profile.Name);
        Assert.Equal("软件一班", result.Profile.ClassName);
        Assert.Equal("软件技术", result.Profile.MajorName);
        Assert.Equal("信息工程学院", result.Profile.AcademyName);
        Assert.Equal("专科", result.Profile.TrainingLevel);
        Assert.Equal("主校区", result.Profile.CampusName);
        Assert.False(string.IsNullOrWhiteSpace(result.MobileJw?.EncryptedPassword));
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task NativeJwAuthClient_InitializeReturnsReadableRemoteFailure()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            _ => Json(new
            {
                code = 1,
                data = "1234567890abcdef",
            }),
            _ => Json(new
            {
                code = 0,
                msg = "账号或密码错误",
            }),
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwAuthClient(client);

        var error = await Assert.ThrowsAsync<JwAuthException>(() => jw.InitializeAsync("20260001", "bad-pass", "capture"));

        Assert.Equal("账号或密码错误", error.Message);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void StudentProfileNormalizer_HandlesNestedRealShapeFixture()
    {
        using var document = JsonDocument.Parse(FixtureText("sample_student_my_nested.json"));

        var profile = StudentProfileNormalizer.FromJson("2026XXXX01", document.RootElement);

        Assert.Equal("2026XXXX01", profile.StudentId);
        Assert.Equal("测试同学", profile.Name);
        Assert.Equal("软件一班", profile.ClassName);
        Assert.Equal("软件技术", profile.MajorName);
        Assert.Equal("信息工程学院", profile.AcademyName);
        Assert.Equal("专科", profile.TrainingLevel);
        Assert.Equal("主校区", profile.CampusName);
    }

    [Fact]
    public async Task NativeJwAuthClient_InitializeHandlesNestedRealShapeProfileAndToken()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            _ => Json(new
            {
                success = true,
                result = "1234567890abcdef",
            }),
            _ => Json(new
            {
                code = 200,
                result = new
                {
                    access_token = "mobile-token",
                },
            }),
            request =>
            {
                Assert.Equal("mobile-token", HeaderValue(request, "token"));
                return JsonText(FixtureText("sample_student_my_nested.json"));
            },
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwAuthClient(client);

        var result = await jw.InitializeAsync("2026XXXX01", "secret-pass", "capture");

        Assert.Equal("2026XXXX01", result.Profile.StudentId);
        Assert.Equal("测试同学", result.Profile.Name);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task NativeJwAuthClient_MergesLoginProfileFieldsWhenStudentMyOmitsThem()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            _ => Json(new
            {
                code = 1,
                data = "1234567890abcdef",
            }),
            _ => Json(new
            {
                code = 1,
                data = new
                {
                    token = "mobile-token",
                    userNo = "20260001",
                    name = "测试同学",
                    academyName = "信息工程学院",
                    majorName = "软件技术",
                    clsName = "软件一班",
                    trainingLevel = "专科",
                },
            }),
            request =>
            {
                Assert.Equal("mobile-token", HeaderValue(request, "token"));
                return Json(new
                {
                    code = 1,
                    data = new
                    {
                        xh = "20260001",
                        xm = "测试同学",
                    },
                });
            },
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwAuthClient(client);

        var result = await jw.InitializeAsync("20260001", "secret-pass", "capture");

        Assert.Equal("20260001", result.Profile.StudentId);
        Assert.Equal("测试同学", result.Profile.Name);
        Assert.Equal("信息工程学院", result.Profile.AcademyName);
        Assert.Equal("软件技术", result.Profile.MajorName);
        Assert.Equal("软件一班", result.Profile.ClassName);
        Assert.Equal("专科", result.Profile.TrainingLevel);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task NativeJwScheduleClient_FetchPreferredUsesManualWeekAndMobileHandshake()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            request =>
            {
                Assert.Equal("/hnrjzyxyhd/getKey", request.RequestUri!.AbsolutePath);
                return Json(new
                {
                    code = 1,
                    data = "1234567890abcdef",
                });
            },
            request =>
            {
                var query = ParseQuery(request.RequestUri);
                Assert.Equal("/hnrjzyxyhd/login", request.RequestUri!.AbsolutePath);
                Assert.Equal("20260001", query["userNo"]);
                Assert.Equal("1", query["encode"]);
                Assert.Equal(string.Empty, query["captchaData"]);
                Assert.Equal(string.Empty, query["codeVal"]);
                Assert.NotEqual("secret-pass", query["pwd"]);
                Assert.Equal("null", HeaderValue(request, "token"));
                return Json(new
                {
                    code = 1,
                    data = new
                    {
                        token = "schedule-token",
                    },
                });
            },
            request =>
            {
                var query = ParseQuery(request.RequestUri);
                Assert.Equal("/hnrjzyxyhd/student/curriculum", request.RequestUri!.AbsolutePath);
                Assert.Equal("9", query["week"]);
                Assert.Equal(string.Empty, query["kbjcmsid"]);
                Assert.Equal("schedule-token", HeaderValue(request, "token"));
                return Json(new
                {
                    code = 1,
                    data = new object[]
                    {
                        new
                        {
                            currentWeek = "8",
                            date = new object[]
                            {
                                new
                                {
                                    mxrq = "2026-04-20",
                                    xqid = "1",
                                    zc = "9",
                                },
                                new
                                {
                                    mxrq = "2026-04-26",
                                    xqid = "7",
                                    zc = "9",
                                },
                            },
                            courses = new object[]
                            {
                                new
                                {
                                    courseName = "高等数学",
                                    teaName = "张老师",
                                    classroomName = "1-101",
                                    classTime = "10102",
                                    startTime = "08:00",
                                    endTIme = "09:40",
                                    classWeekDetails = "9",
                                    credit = "4",
                                },
                            },
                        },
                    },
                });
            },
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwScheduleClient(client);

        var result = await jw.FetchPreferredAsync("20260001", "secret-pass", "capture", new ScheduleRequest
        {
            Mode = "manual",
            ManualWeek = "9",
        });

        Assert.Equal("mobile_schedule", result.SourceKey);
        Assert.Equal("mobile", result.SourceType);
        Assert.Equal("第9周", result.WeekLabel);
        Assert.Equal(8, result.CurrentWeekNumber);
        Assert.Equal(9, result.ViewedWeekNumber);
        Assert.Single(result.Rows);
        Assert.Equal("高等数学", result.Rows[0]["course_name"]);
        Assert.Equal("星期一", result.Rows[0]["weekday"]);
        Assert.Equal("第1-2节", result.Rows[0]["section"]);
        Assert.Equal("08:00-09:40", result.Rows[0]["time_range"]);
        Assert.Equal("08:00-09:40", result.Rows[0]["block_time"]);
        Assert.Equal("08:00-08:45", result.Rows[0]["small_section_1"]);
        Assert.Equal("08:55-09:40", result.Rows[0]["small_section_2"]);
        Assert.Equal(10, result.Rows[0]["break_minutes"]);
        Assert.Equal("2026-04-20", result.WeekDates["星期一"]);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task NativeJwScheduleClient_DropsMismatchedWeekDatesForManualWeek()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            request =>
            {
                var query = ParseQuery(request.RequestUri);
                Assert.Equal("/hnrjzyxyhd/student/curriculum", request.RequestUri!.AbsolutePath);
                Assert.Equal("9", query["week"]);
                Assert.Equal("schedule-token", HeaderValue(request, "token"));
                return Json(new
                {
                    code = 1,
                    data = new object[]
                    {
                        new
                        {
                            currentWeek = "8",
                            date = new object[]
                            {
                                new
                                {
                                    mxrq = "2026-04-20",
                                    xqid = "1",
                                    zc = "8",
                                },
                            },
                            courses = new object[]
                            {
                                new
                                {
                                    courseName = "高等数学",
                                    teaName = "张老师",
                                    classroomName = "1-101",
                                    classTime = "10102",
                                    startTime = "08:00",
                                    endTIme = "09:40",
                                    classWeekDetails = "9",
                                },
                            },
                        },
                    },
                });
            },
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwScheduleClient(client);

        var result = await jw.FetchPreferredAsync("20260001", "secret-pass", "capture", new ScheduleRequest
        {
            Mode = "manual",
            ManualWeek = "9",
        }, existingToken: "schedule-token");

        Assert.Equal("第9周", result.WeekLabel);
        Assert.Equal(8, result.CurrentWeekNumber);
        Assert.Equal(9, result.ViewedWeekNumber);
        Assert.Empty(result.WeekDates);
        Assert.Single(result.Rows);
    }

    [Fact]
    public async Task NativeJwScheduleClient_FetchPreferredReusesExistingMobileToken()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            request =>
            {
                var query = ParseQuery(request.RequestUri);
                Assert.Equal("/hnrjzyxyhd/student/curriculum", request.RequestUri!.AbsolutePath);
                Assert.Equal(string.Empty, query["week"]);
                Assert.Equal("existing-mobile-token", HeaderValue(request, "token"));
                return JsonText(FixtureText("sample_curriculum_real_shape.json"));
            },
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwScheduleClient(client);

        var result = await jw.FetchPreferredAsync("2026XXXX01", "secret-pass", "capture", new ScheduleRequest
        {
            Mode = "current",
        }, existingToken: "existing-mobile-token");

        Assert.Equal("第9周", result.WeekLabel);
        Assert.Single(result.Rows);
        Assert.Equal("高等数学", result.Rows[0]["course_name"]);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task NativeJwScheduleClient_FetchPreferredReusesFreshEncryptedCredentialWithoutGetKey()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            request =>
            {
                var query = ParseQuery(request.RequestUri);
                Assert.Equal("/hnrjzyxyhd/login", request.RequestUri!.AbsolutePath);
                Assert.Equal("cached-encrypted-password", query["pwd"]);
                Assert.Equal("1", query["encode"]);
                return Json(new
                {
                    code = 1,
                    data = new
                    {
                        token = "schedule-token",
                    },
                });
            },
            request =>
            {
                Assert.Equal("/hnrjzyxyhd/student/curriculum", request.RequestUri!.AbsolutePath);
                Assert.Equal("schedule-token", HeaderValue(request, "token"));
                return JsonText(FixtureText("sample_curriculum_real_shape.json"));
            },
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwScheduleClient(client);
        var cached = new MobileJwEncryptedCredentialDocument
        {
            EncryptedPassword = "cached-encrypted-password",
            GeneratedAt = DateTimeOffset.Now.AddMinutes(-10).ToString("O"),
            ExpiresAt = DateTimeOffset.Now.AddMinutes(50).ToString("O"),
        };

        var result = await jw.FetchPreferredAsync("2026XXXX01", "secret-pass", "capture", new ScheduleRequest
        {
            Mode = "current",
        }, mobileJw: cached);

        Assert.Equal("cached-encrypted-password", result.MobileJw?.EncryptedPassword);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task NativeJwScheduleClient_FetchPreferredRefreshesEncryptedCredentialAfterCachedLoginFailure()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            request =>
            {
                var query = ParseQuery(request.RequestUri);
                Assert.Equal("/hnrjzyxyhd/login", request.RequestUri!.AbsolutePath);
                Assert.Equal("stale-encrypted-password", query["pwd"]);
                return Json(new
                {
                    code = 0,
                    msg = "密文已失效",
                });
            },
            request =>
            {
                Assert.Equal("/hnrjzyxyhd/getKey", request.RequestUri!.AbsolutePath);
                return Json(new
                {
                    code = 1,
                    data = "1234567890abcdef",
                });
            },
            request =>
            {
                var query = ParseQuery(request.RequestUri);
                Assert.Equal("/hnrjzyxyhd/login", request.RequestUri!.AbsolutePath);
                Assert.NotEqual("stale-encrypted-password", query["pwd"]);
                return Json(new
                {
                    code = 1,
                    data = new
                    {
                        token = "schedule-token",
                    },
                });
            },
            request =>
            {
                Assert.Equal("/hnrjzyxyhd/student/curriculum", request.RequestUri!.AbsolutePath);
                return JsonText(FixtureText("sample_curriculum_real_shape.json"));
            },
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwScheduleClient(client);
        var cached = new MobileJwEncryptedCredentialDocument
        {
            EncryptedPassword = "stale-encrypted-password",
            GeneratedAt = DateTimeOffset.Now.AddMinutes(-10).ToString("O"),
            ExpiresAt = DateTimeOffset.Now.AddMinutes(50).ToString("O"),
        };

        var result = await jw.FetchPreferredAsync("2026XXXX01", "secret-pass", "capture", new ScheduleRequest
        {
            Mode = "current",
        }, mobileJw: cached);

        Assert.NotNull(result.MobileJw);
        Assert.NotEqual("stale-encrypted-password", result.MobileJw!.EncryptedPassword);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task NativeJwScheduleClient_FetchPreferredHandlesRealShapeFixture()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            _ => Json(new
            {
                code = 200,
                data = "1234567890abcdef",
            }),
            _ => Json(new
            {
                success = true,
                payload = new
                {
                    token = "schedule-token",
                },
            }),
            request =>
            {
                Assert.Equal("schedule-token", HeaderValue(request, "token"));
                return JsonText(FixtureText("sample_curriculum_real_shape.json"));
            },
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwScheduleClient(client);

        var result = await jw.FetchPreferredAsync("2026XXXX01", "secret-pass", "capture", new ScheduleRequest
        {
            Mode = "current",
        });

        Assert.Equal("第9周", result.WeekLabel);
        Assert.Single(result.Rows);
        Assert.Equal("高等数学", result.Rows[0]["course_name"]);
        Assert.Equal("星期一", result.Rows[0]["weekday"]);
        Assert.Equal("第1-2节", result.Rows[0]["section"]);
        Assert.Equal("2026-04-20", result.WeekDates["星期一"]);
        Assert.Empty(result.DataWarnings);
    }

    [Fact]
    public async Task NativeJwScheduleClient_FetchPreferredHandlesCaseVariantKeys()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            _ => Json(new
            {
                Code = 200,
                Data = "1234567890abcdef",
            }),
            _ => Json(new
            {
                Success = true,
                Result = new
                {
                    AccessToken = "schedule-token",
                },
            }),
            request =>
            {
                Assert.Equal("schedule-token", HeaderValue(request, "token"));
                return Json(new
                {
                    Code = 200,
                    Data = new
                    {
                        Week = "9",
                        Dates = new[]
                        {
                            new
                            {
                                Date = "2026-04-20",
                                WeekDay = "1",
                            },
                        },
                        CourseList = new[]
                        {
                            new
                            {
                                CourseName = "高等数学",
                                TeacherName = "张老师",
                                ClassroomName = "1-101",
                                ClassTime = "10102",
                                CourseTime = "08:00-09:35",
                                ClassWeekDetails = "9",
                            },
                        },
                    },
                });
            },
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwScheduleClient(client);

        var result = await jw.FetchPreferredAsync("2026XXXX01", "secret-pass", "capture", new ScheduleRequest
        {
            Mode = "current",
        });

        Assert.Equal("第9周", result.WeekLabel);
        Assert.Single(result.Rows);
        Assert.Equal("高等数学", result.Rows[0]["course_name"]);
        Assert.Equal("星期一", result.Rows[0]["weekday"]);
        Assert.Equal("第1-2节", result.Rows[0]["section"]);
    }

    [Fact]
    public async Task NativeJwScheduleClient_FetchPreferredReturnsWarningsForPartialRealShape()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            _ => Json(new
            {
                code = "1",
                data = "1234567890abcdef",
            }),
            _ => Json(new
            {
                code = "1",
                data = new
                {
                    token = "schedule-token",
                },
            }),
            _ => JsonText(FixtureText("sample_curriculum_missing_fields.json")),
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwScheduleClient(client);

        var result = await jw.FetchPreferredAsync("2026XXXX01", "secret-pass", "capture", new ScheduleRequest
        {
            Mode = "current",
        });

        Assert.Equal("第10周", result.WeekLabel);
        Assert.Single(result.Rows);
        Assert.Equal("大学英语", result.Rows[0]["course_name"]);
        Assert.Contains(result.DataWarnings, warning => string.Equals(warning["type"]?.ToString(), "mobile-course-section-missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NativeJwScheduleClient_FetchPreferredReturnsReadableRemoteFailure()
    {
        using var handler = new ScriptedHttpMessageHandler(
        [
            _ => Json(new
            {
                code = 1,
                data = "1234567890abcdef",
            }),
            _ => Json(new
            {
                code = 1,
                data = new
                {
                    token = "schedule-token",
                },
            }),
            _ => Json(new
            {
                code = 0,
                msg = "移动教务暂不可用",
            }),
        ]);

        using var client = new HttpClient(handler);
        var jw = new NativeJwScheduleClient(client);

        var error = await Assert.ThrowsAsync<JwScheduleException>(() => jw.FetchPreferredAsync("20260001", "secret-pass", "capture", new ScheduleRequest
        {
            Mode = "current",
        }));

        Assert.Equal("移动教务暂不可用", error.Message);
        Assert.Equal(3, handler.Requests.Count);
    }

    private static HttpResponseMessage Json(object payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage JsonText(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static string FixtureText(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "fixtures", "mobile", fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate mobile fixture {fileName}");
    }

    private static string HeaderValue(HttpRequestMessage request, string name)
    {
        return request.Headers.TryGetValues(name, out var values)
            ? values.SingleOrDefault() ?? string.Empty
            : string.Empty;
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(Uri? uri)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var raw = uri?.Query ?? string.Empty;
        foreach (var segment in raw.TrimStart('?').Split('&', StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(segment))
            {
                continue;
            }

            var pair = segment.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0]);
            var value = pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }

    private sealed class ScriptedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _steps;

        public ScriptedHttpMessageHandler(IEnumerable<Func<HttpRequestMessage, HttpResponseMessage>> steps)
        {
            _steps = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(steps);
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_steps.Count == 0)
            {
                throw new Xunit.Sdk.XunitException($"Unexpected request: {request.Method} {request.RequestUri}");
            }

            Requests.Add(request);
            var response = _steps.Dequeue().Invoke(request);
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var request in Requests)
                {
                    request.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
