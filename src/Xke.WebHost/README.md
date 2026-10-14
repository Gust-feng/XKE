# Xke.WebHost

本机 HTTP API 与 C# 后台运行时，装配登录、课表、设置、校园网、桌面能力和受控应用更新入口。旧远程策略、云上传及旧 Updater 已退役。

## 边界

- 业务操作使用 `Xke.Core`；API 统一处理领域错误并保留 HTTP 状态。
- `PersonalRuntimeWorker` 驱动校园网维护、课表自动刷新和课前提醒，独立于 React/WebView 生命周期；测试使用显式临时数据根并可关闭后台任务。
- `AppUpdateWorker` 编排自动检查和下载，具体更新包处理由 DesktopHost 的 Velopack 实现；单独运行 WebHost 不伪造安装能力。
- `LocalApiAccessPolicy` 校验本机 Host 与同源/显式允许的 Origin；桌面账号恢复、机器设置和更新入口还要求原生注入的短时能力证明。前端 session 仅保存在内存。
- `/api/config` 返回本地默认设置和开发诊断，不请求云配置。
- Web 静态资源是可选宿主输入；纯 API 宿主在缺少前端资源时仍可运行。

当前契约见 [03_api_contract.md](../../notes/development-docs/personal-runtime/03_api_contract.md)，更新边界见 [08_release_and_updates.md](../../notes/development-docs/personal-runtime/08_release_and_updates.md)。
