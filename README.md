# XKE.DotNet8

`XKE.DotNet8` 是 XKE 的 .NET 重构主目录，当前主线已经收敛为：

> 首次联网初始化 -> 本地持久化 -> 后续可信进入并维护本地数据

应用不再定位成反复校验账号密码的登录器。首次手动登录会通过移动教务完成认证、资料落库、偏好落库、当前课表刷新落库；之后优先用本地 session 或前端保存的 trusted account 进入工作区。

## 当前基线

已完成并纳入当前基线的切片：

- `DesktopHost` 开发壳：WPF + WebView2 加载前端并承载本地 WebHost。
- `Auth`：移动教务首次初始化、本地 session、trusted-login、profile/preferences/avatar/logout。
  - 首次登录必须完成移动教务认证、身份拉取和当前课表初始化。
  - 已有本地凭据时只做本地校验与本地工作区恢复，不把远端刷新混入登录动作。
  - 本地初始化数据损坏时可通过 `/api/auth/reinitialize` 显式修复，失败会保留原本地凭据。
- `Notice/控制面`：本地 notice cache、远端控制面、blocked/readonly/fail-closed 投影。
- `Desktop settings`：桌面设置读写，真实系统自启注册后置。
- `Schedule`：移动教务课表刷新、归一化、落库、本地恢复。
- `Network`：本地网络配置、状态检测、心跳闭环，系统命令受控封装。
- `Telemetry`：本地统计与 snapshot，云上传 local-only。
- `Feedback`：本地 outbox，云提交后置。
- `Course`：新框架基础、状态/轮次/预览接口边界，真实传统教务连接和抢课后置。

## 当前推进重心

当前开发重心切到后端长期收敛：

- 优先保证 Auth 首次初始化、本地可信恢复、Schedule 本地缓存、Notice 控制面等活跃链路的行为闭环。
- 前端暂不做结构优化，只保持现有 API envelope 和字段兼容，等待后续整体重构。
- Network 认证历史、Feedback outbox 等隐私落盘增强暂缓；本项目默认运行在用户本机相对可信环境内，当前不引入额外复杂保护。
- 允许保留兼容层，但兼容层只能服务未来迁移和前端重构，不得重新接入 Python legacy runtime。

明确后置的边界：

- 传统教务验证码/OCR/Cookie 课表。
- Course 真实连接、抢课轮询、限流和冷却。
- Telemetry/Feedback 真实云上传。
- 真实开机自启注册与发布打包。

## 数据规则

- 默认数据根为 `XKE.DotNet8/.runtime/data`。
- 可用 `XKE_DOTNET_DATA_DIR` 覆盖运行时数据根。
- 不读取、不写回 Python legacy `data/`、真实 `.env`、真实 token 或生产镜像。
- fixtures 默认使用脱敏样本；真实开发诊断只能留在本机 runtime 或临时目录，不进入版本库。
- 移动教务凭据保存在本机 DPAPI 保护的数据根中，用于后续可信状态维护。

## 目录

- `src/`：.NET 源码。
- `tests/`：Contracts、Storage/Security、WebHost 集成测试。
- `fixtures/`：脱敏样本。
- `tools/`：构建、测试、运行、smoke 脚本。
- `notes/`：阶段总结、问题手册、未来计划。
- `.agents/skills/xke-dotnet8-maintainer/`：项目维护参考，不安装到用户级 skills。

## 门禁

在修改当前切片后至少运行：

```powershell
dotnet build .\XKE.DotNet8.sln
dotnet test .\XKE.DotNet8.sln
powershell -ExecutionPolicy Bypass -File .\tools\schedule-smoke.ps1
powershell -ExecutionPolicy Bypass -File .\tools\frontend-smoke.ps1
```

后端结构收敛优先运行：

```powershell
dotnet build .\XKE.DotNet8.sln
dotnet test .\XKE.DotNet8.sln --no-build
node --check .\tools\mobile-real-smoke.mjs
node --check .\tools\audit-active-links.mjs
```

真实移动教务开发态验证：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\mobile-real-smoke.ps1
```

开发版桌面壳运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-desktop.ps1
dotnet run --project .\src\Xke.DesktopHost\Xke.DesktopHost.csproj
```
