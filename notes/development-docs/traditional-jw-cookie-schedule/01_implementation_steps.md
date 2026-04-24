# 传统教务 Cookie 课表开发步骤

## 阶段 1：离线解析

目标：不依赖真实账号、验证码或 Cookie，先把传统教务 HTML 解析为统一课表数据。

输入：

- `fixtures/schedule/sample_cookie_schedule.html`
- `fixtures/schedule/sample_available_weeks.json`

输出：

- `rows`
- `week_label`
- `available_weeks`
- `current_week_label`
- `block_times`
- `remarks`
- `source_key=jw_schedule`
- `source_type=jw`
- `source_label=教务系统课表`

建议落点：

- 新增传统教务 HTML parser，优先放在 `src/Xke.Core/Schedule/`
- 为 parser 增加单元测试，优先放在 `tests/Xke.Core.Tests/` 或现有等价测试项目

验收：

- fixture 至少解析出 1 行有效课程
- 每行至少包含 `course_name`、`weekday`、`section`
- 教师、教室、周次缺失时允许为空，但必须稳定输出字段

## 阶段 2：客户端结果封装

目标：让传统教务 Cookie 课表结果能进入现有 `IJwScheduleClient.FetchCookieAsync` 边界。

实现要求：

- `NativeJwScheduleClient.FetchCookieAsync` 返回 `JwScheduleFetchResult`
- 离线测试阶段可以先用 fixture/fake client 证明返回结构
- `cookie_rq` 只作为课表日期/周次请求参数，不作为 Cookie
- `RawPayload` 可保存脱敏结构，不保存真实 HTML 正文到文档

验收：

- `JwScheduleFetchResult.SourceKey == "jw_schedule"`
- `JwScheduleFetchResult.SourceType == "jw"`
- `JwScheduleFetchResult.Rows.Count > 0`
- 空 HTML 或无法解析时返回明确异常

## 阶段 3：WebHost 与缓存闭环

目标：让 `/api/schedule/cookie` 从 deferred 变成可测试的成功路径。

实现要求：

- `ScheduleService.FetchCookieAsync` 调用 `IJwScheduleClient.FetchCookieAsync`
- 成功后写入 `XkeScheduleStore`
- 返回 `data_origin=live_fetch`
- `GET /api/schedule/local?source=cookie` 能读取本地 `jw_schedule`

验收：

- Cookie 刷新成功返回 `200`
- 本地读取返回缓存课表
- 无 session 返回会话错误
- 本地凭据缺失返回认证错误
- 移动教务 `schedule/preferred` 不受影响

## 阶段 4：传统教务预登录与登录态

目标：迁移旧 `JwSsoClient` 的最小真实登录链路。

必须迁移的行为：

- `get_login_page`
- `prepare_prelogin_bootstrap`
- `fetch_captcha_text`
- `login_portal`
- `bootstrap_jw_session`
- `fetch_schedule_html`

硬性规则：

- 预登录是必经阶段
- Cookie 不是登录入口
- `capture` 缺少 `prelogin_capture.xml` 时必须返回可读错误
- `coded` 可以生成或刷新运行态预登录录包
- 未拿到 JW 域名下 `/jsxsd` 路径登录态 Cookie 时，不允许请求课表
- Cookie 失效时允许重走完整预登录 + 登录 + JW bootstrap 一次

验收：

- `capture` 缺录包失败可读
- `coded` 可完成预登录准备
- 登录失败不写课表缓存
- JW bootstrap 失败不请求课表
- Cookie 失效可触发完整重登一次

## 阶段 5：真实开发态 smoke

目标：用测试账号验证真实链路。

环境变量：

```powershell
$env:user="<TEST_USER>"
$env:password="<TEST_PASSWORD>"
$env:prelogin_mode="capture"
$env:cookie_rq="<YYYY-MM-DD>"
```

验收顺序：

1. 登录拿到 session
2. 调用 `/api/schedule/cookie`
3. 调用 `/api/schedule/local?source=cookie`
4. 回归 `/api/schedule/preferred`
5. 回归 `/api/schedule/local?source=preferred`

禁止事项：

- 不把真实测试账号写入文档
- 不提交真实 `prelogin_capture.xml`
- 不提交真实 Cookie header
- 不提交真实 HTML 原文
