# 开发任务板

## 当前状态

当前切片：`jw-cookie-schedule-v1`

当前阶段：阶段 1，传统教务 HTML 离线解析。

## Ready

- 梳理 `fixtures/schedule/sample_cookie_schedule.html` 的结构
- 梳理 `fixtures/schedule/sample_available_weeks.json` 的周次结构
- 设计传统教务 HTML parser 的输出字段
- 决定 parser 放置位置和测试项目位置
- 设计人工验证码 challenge 状态模型
- 设计 `ManualCaptchaSolver` 与后续 `OcrCaptchaSolver` 的抽象边界

## Doing

- 暂无

## Next

- 实现传统教务 HTML parser
- 增加 fixture 解析单元测试
- 将 parser 输出映射到 `JwScheduleFetchResult`
- 增加 Cookie 课表 fake client 测试
- 增加人工验证码 challenge 接口设计
- 增加验证码过期、错误、重试的验收用例

## Later

- 接入 `ScheduleService.FetchCookieAsync`
- 打通 `/api/schedule/cookie` 成功路径
- 打通 `/api/schedule/local?source=cookie`
- 迁移传统 SSO 预登录和 JW bootstrap
- 实现 Web 验证码弹窗
- 后续接入 OCR，并在 OCR 失败时回退人工验证
- 增加真实开发态 smoke

## Blocked

- 真实传统教务登录链路依赖预登录录包或 `coded` 模式实现
- OCR 后置；第一阶段只实现人工验证码，不阻塞 Cookie 课表主线
- 真实 smoke 依赖本机测试账号环境，不写入仓库文档

## Done

- 建立 `notes/development-docs/traditional-jw-cookie-schedule/` 开发项目目录
- 明确第一阶段从离线解析开始
- 明确 Cookie 不是登录入口，预登录是传统教务必经阶段
- 明确验证码 v1 走 Web 人工输入，后续 OCR 失败可回退人工
