# 开发任务板

## 当前状态

当前切片：`jw-cookie-schedule-v1`

当前阶段：阶段 1，传统教务 HTML 离线解析。

## Ready

- 梳理 `fixtures/schedule/sample_cookie_schedule.html` 的结构
- 梳理 `fixtures/schedule/sample_available_weeks.json` 的周次结构
- 设计传统教务 HTML parser 的输出字段
- 决定 parser 放置位置和测试项目位置

## Doing

- 暂无

## Next

- 实现传统教务 HTML parser
- 增加 fixture 解析单元测试
- 将 parser 输出映射到 `JwScheduleFetchResult`
- 增加 Cookie 课表 fake client 测试

## Later

- 接入 `ScheduleService.FetchCookieAsync`
- 打通 `/api/schedule/cookie` 成功路径
- 打通 `/api/schedule/local?source=cookie`
- 迁移传统 SSO 预登录和 JW bootstrap
- 增加真实开发态 smoke

## Blocked

- 真实传统教务登录链路依赖预登录录包或 `coded` 模式实现
- 真实 smoke 依赖本机测试账号环境，不写入仓库文档

## Done

- 建立 `notes/development-docs/traditional-jw-cookie-schedule/` 开发项目目录
- 明确第一阶段从离线解析开始
- 明确 Cookie 不是登录入口，预登录是传统教务必经阶段
