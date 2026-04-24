# XKE.DotNet8 阶段总结报告

> 更新时间：2026-04-24  
> 范围：`Z:/XKE/XKE.DotNet8/`

> 口径更新：移动教务为主数据源，教务系统（验证码/Cookie/抢课）为后置切片；本说明仅针对当前可交付主线。详见 [01_review_findings.md](../review-issues/01_review_findings.md)。

## 1. 当前阶段结论

当前重构已经从“可启动壳”推进到“本地可信状态桌面应用”阶段。

现在的主线不是把软件继续做成反复输入账号密码的登录器，而是：

1. 首次联网初始化。
2. 将资料、偏好、课表、控制面等数据落到本地 `.NET` 数据根。
3. 后续优先通过本地 session 或 trusted-login 恢复工作区。
4. 远端验证和数据刷新只在用户主动连接或后台维护时发生。

## 2. 已完成切片

截至当前，已完成并通过 solution 门禁的切片如下：

- `DesktopHost` 开发壳。
  - WPF + WebView2 承载前端。
  - `DesktopHost` 负责拉起 `WebHost`、窗口激活、开发壳运行。
- `Auth`。
  - 移动教务登录、trusted-login、session restore、connect。
  - profile / preferences / avatar / logout。
  - 首次登录自动完成本地初始化。
- `Notice/控制面`。
  - notice cache、远端控制面、blocked/readonly/fail-closed。
- `Desktop settings`。
  - 本地设置读写，真实系统自启注册后置。
- `Schedule`。
  - 移动教务课表刷新、归一化、落库、本地恢复。
- `Network`。
  - 本地配置、状态探测、心跳维护、受控系统命令封装。
- `Telemetry`。
  - 本地 runtime/profile/overview 闭环，云上传仍 local-only。
- `Feedback`。
  - 本地 outbox 闭环，云提交后置。
- `Course` 新框架基础切片。
  - 状态、轮次、预览、缓存边界已建。
  - 传统教务验证码连接与抢课继续后置。

## 3. 本轮收敛变化

本轮完成的关键收敛：

- `Auth` 首次手动登录不再只建 session。
  - 会同步执行移动教务认证、资料落库、偏好落库、当前课表刷新并落库。
  - 当前课表初始化失败时，首登整体失败并回滚本次凭据、session 和半成品目录。
- `Auth` 登录语义拆成双路径。
  - 未初始化账号：必须真实移动教务认证成功，并拿到有效 token、身份信息和非空课表。
  - 已初始化账号：只校验本地 DPAPI 凭据，校验通过后只恢复本地 profile、settings、cached schedule。
  - 本地 profile 或 schedule 缺失时返回“本地初始化数据不完整”，不创建新工作区 session。
- `AuthPayload.cached_schedule` 正式承担首登与本地恢复时的课表种子数据。
- 前端 trusted 状态已从单一 `xke-remembered-login` 切换到：
  - `xke-trusted-accounts`
  - `xke-last-account`
- 启动进入顺序已固定为：
  1. 本地 `session_id`
  2. 单 trusted account 自动 trusted-login
  3. 多 trusted accounts 显示账号选择器
4. 无 trusted accounts 显示手动登录表单

## 4. 今日真实链路验证

今天用真实移动教务账号完成了开发态 smoke 验证：

- `auth/login` 首登成功，返回 `cached_schedule` 非空。
- 真实课表归一化结果：当前周 `第8周`，课程行数 11。
- 第二次同账号 `auth/login` 命中已有本地凭据路径，`connected=false`，不重新做远端校验，并返回本地课表。
- 宿主重启后 `/api/auth/session` 可恢复本地工作状态，`connected=false`。
- 主动 `/api/auth/connect` 可重新验证移动教务并返回 `connected=true`。
- 真实资料字段命中 6/7，缺失 `campus_name`，不阻断主链路。

真实验证诊断目录保留在本机临时目录，仅用于开发排查，不进入版本库。

## 5. 当前门禁状态

当前基线门禁：

```powershell
dotnet build .\XKE.DotNet8\XKE.DotNet8.sln
dotnet test .\XKE.DotNet8\XKE.DotNet8.sln
powershell -ExecutionPolicy Bypass -File .\XKE.DotNet8\tools\schedule-smoke.ps1
powershell -ExecutionPolicy Bypass -File .\XKE.DotNet8\tools\frontend-smoke.ps1
```

本轮复核结果：

- `dotnet build` 通过。
- `dotnet test` 通过。
- `schedule-smoke.ps1` 通过。
- `frontend-smoke.ps1` 通过。

## 6. 当前 deferred 边界

当前允许保留的 deferred 边界只剩：

- 传统教务验证码 / OCR / Cookie 课表。
- Course 真实抢课执行链路。
- Telemetry / Feedback 云上传。

这些边界需要继续保留明确注释，不再保留误导性的空壳逻辑。

## 7. 下一步建议

下一步不应再扩张新的业务面，而应围绕后端长期结构做收敛：

- 优先闭合 Auth 首登初始化原子性、Schedule 本地日期、Legacy reference-only 隔离这些行为边界。
- 继续验证真实移动教务返回格式，把必要变体收敛进解析器与后端测试。
- 后端逐步拆 `XkeWebHostApplication` 的服务注册、session/context、错误 envelope 和 blocked policy；前端暂不拆，只保持兼容。
- 隐私落盘增强暂缓，Network history、Feedback outbox、云上传相关保护后续单独规划。
- 把根仓库活动入口、参考材料、未来计划分区整理，减少后续开发噪音。
