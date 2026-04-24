# 传统教务 Cookie 课表开发文档

本目录是 `development-docs/traditional-jw-cookie-schedule/`，按“开发文档 / 开发项目 / 具体内容”组织。

本目录只维护传统教务 Cookie 课表项目的当前开发过程；长期路线图与敏感资料索引分别由 `roadmap/` 和 `sensitive-index/` 维护。

长期协议资料和敏感边界不在本目录展开；需要时跳转到 [../../sensitive-index/教务系统/01_接入资料索引.md](../../sensitive-index/教务系统/01_接入资料索引.md)。

## 当前主线

当前推进主线：

```text
传统 Web 教务预登录 -> SSO/JW 登录态 -> Cookie 课表 HTML -> jw_schedule 缓存
```

当前第一阶段只做离线 HTML 课表解析和 `jw_schedule` 结果封装；传统教务登录态接入排在后续阶段。移动教务保持现有主链路稳定，课程查询和抢课继续后置。

## 阅读顺序

1. [00_current_focus.md](./00_current_focus.md)
   - 当前切片、边界、完成定义
2. [01_implementation_steps.md](./01_implementation_steps.md)
   - 具体开发步骤和验收顺序
3. [02_task_board.md](./02_task_board.md)
   - 当前任务板
4. [../../sensitive-index/教务系统/01_接入资料索引.md](../../sensitive-index/教务系统/01_接入资料索引.md)
   - 传统教务资料索引和预登录流程边界
5. [../../sensitive-index/移动教务/01_认证流程索引.md](../../sensitive-index/移动教务/01_认证流程索引.md)
   - 移动教务回归边界

## 使用规则

- 每次开始一个开发切片，先更新 `00_current_focus.md`。
- 每次拆解或完成任务，更新 `02_task_board.md`。
- 每次触及真实认证资料，先检查 `../../sensitive-index/`。
- 不在本目录保存真实账号、密码、token、cookie、验证码图片、原始 HTML 或真实响应。

## 当前第一个可执行切片

先做离线解析：

```text
fixtures/schedule/sample_cookie_schedule.html
-> 传统教务 HTML parser
-> JwScheduleFetchResult
-> source_key=jw_schedule
```

这个切片不依赖真实账号、验证码或 Cookie，是后续接入真实预登录链路前必须稳定的基础。
