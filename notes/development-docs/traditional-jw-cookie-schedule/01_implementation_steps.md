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
- 用户侧只有 `auto` 自动化预登录；`capture` / `coded` 只作为旧配置别名归一化为 `auto`
- 自动化预登录失败时必须返回可读 `409` 诊断，不要求用户补充或切换录包
- 未拿到 JW 域名下 `/jsxsd` 路径登录态 Cookie 时，不允许请求课表
- Cookie 失效时允许重走完整预登录 + 登录 + JW bootstrap 一次

验收：

- `auto` 可完成预登录准备，且不读取或生成 `prelogin_capture.xml`
- 自动化预登录失败返回明确错误
- 登录失败不写课表缓存
- JW bootstrap 失败不请求课表
- Cookie 失效可触发完整重登一次

## 阶段 4A：人工验证码 challenge

目标：先用 Web 界面完成人工验证码识别，同时为后续 OCR 接入保留稳定扩展点。

### 交互流程

```text
用户触发 /api/schedule/cookie
-> 后端执行 get_login_page + prepare_prelogin_bootstrap
-> 后端获取验证码图片
-> 后端创建 challenge_id 并保存预登录上下文
-> 返回 captcha_required
-> Web 前端展示验证码图片和输入框
-> 用户输入 captcha_code
-> 前端提交 challenge_id + captcha_code
-> 后端继续 login_portal -> bootstrap_jw_session -> fetch_schedule_html
```

### 后端职责

- 保存 challenge 状态，包括登录页上下文、预登录结果、验证码图片来源、初始会话和过期时间。
- 生成一次性 `challenge_id`。
- 校验 challenge 是否属于当前 session。
- 验证码提交后继续传统教务登录流程。
- 登录成功或失败后清理 challenge。
- 不把 Cookie、请求头、验证码原图或预登录录包正文返回给前端。

### 前端职责

- 展示验证码图片。
- 收集用户输入。
- 提交 `challenge_id` 和 `captcha_code`。
- 显示验证码错误、过期、重试等状态。
- 不保存 Cookie、请求头、预登录上下文或真实响应。

### 推荐接口形态

首次请求：

```text
POST /api/schedule/cookie?session_id=<SESSION_ID>
```

需要人工验证码时返回：

```json
{
  "msg": "需要输入教务系统验证码",
  "data": {
    "status": "captcha_required",
    "challenge_id": "<CHALLENGE_ID>",
    "captcha_image": "data:image/png;base64,<REDACTED>",
    "expires_in_seconds": 120
  }
}
```

提交验证码：

```text
POST /api/auth/jw-captcha/submit?session_id=<SESSION_ID>
```

请求体：

```json
{
  "challenge_id": "<CHALLENGE_ID>",
  "captcha_code": "<CODE>"
}
```

成功后返回 Cookie 课表刷新结果，或者返回可继续重试的验证码错误。

### OCR 兼容点

验证码求解必须通过后端抽象，不能写死在 Web UI 或登录流程中。

建议抽象：

```csharp
public interface IJwCaptchaSolver
{
    Task<JwCaptchaSolveResult> SolveAsync(
        JwCaptchaChallenge challenge,
        CancellationToken cancellationToken);
}
```

第一阶段实现：

- `ManualCaptchaSolver`
  - 返回 `captcha_required`
  - 等待 Web 前端提交验证码

后续实现：

- `OcrCaptchaSolver`
  - 自动识别验证码
  - 成功时直接返回 `captcha_code`
  - 失败或低置信度时回退 `ManualCaptchaSolver`

### 注释要求

代码注释要明确：

- 验证码不是独立功能，而是传统教务预登录状态机的一环。
- 人工识别和 OCR 只是 `captcha_code` 的不同来源。
- Cookie 不是入口；验证码提交后仍必须继续 `login_portal -> bootstrap_jw_session`。
- 前端不得持有传统教务 Cookie、请求头或预登录上下文。

### 验收

- 未提交验证码时，后端返回 `captcha_required`，不阻塞请求线程等待用户输入。
- `challenge_id` 过期后提交返回明确错误。
- 验证码错误时可重新生成或重新提交，具体行为需保持一致。
- 登录成功后 challenge 被清理。
- OCR 未实现时，不影响人工验证码流程。
- 后续 OCR 失败时，仍能回退人工验证码输入。

## 阶段 5：真实开发态 smoke

目标：用测试账号验证真实链路。

环境变量：

```powershell
$env:user="<TEST_USER>"
$env:password="<TEST_PASSWORD>"
$env:prelogin_mode="auto"
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
- 不提交历史真实 `prelogin_capture.xml`
- 不提交真实 Cookie header
- 不提交真实 HTML 原文
