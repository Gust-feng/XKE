# XKE.DotNet8 阶段总结报告

> 更新时间：2026-04-26  
> 范围：`Z:/XKE/XKE.DotNet8/`

> 口径更新：移动教务为主数据源，教务系统（验证码/Cookie/抢课）为后置切片；本说明仅针对当前可交付主线。详见 [01_review_findings.md](../review-issues/01_review_findings.md)。

## 0. 2026-04-26 补充总结

截至 2026-04-26，当前主线已经从“本地可信状态桌面应用”推进到“CloudSync / 一键更新 / 私有打包”准备阶段。整体方向保持不变：公开仓库保留可审计的中性代码和占位默认值，本地开发与正式打包通过 ignored 私有配置和发布期注入恢复真实云端能力。

### 已完成

- 本地敏感运行态从仓库固定 AES key 收敛到 DPAPI。
  - `LocalEncryptedFileStore` 改为 Windows 当前用户 DPAPI 保护。
  - 新 envelope 为 `XKEDPAPI1.`，旧 `XKEENC1.` 固定密钥数据不再兼容。
  - `ProtectedJsonFileStore` 原有 credential/session `.json.dpapi` 行为保持不变。
- CloudSync 主线已经建立。
  - 公告、远程策略、更新信息统一从 `cloud-feed.json` 拉取。
  - 公告 Markdown 正文按 `content_path` 单独拉取并缓存。
  - `.cloud/feed.json`、正文缓存和遥感 spool 成为新的运行态边界。
  - 遥感默认开启，先写本地 spool，再尝试上传云函数；失败保留队列。
- 一键自动更新第一阶段已经进入实现基线。
  - `update_info` 扩展为可执行更新元数据。
  - 新增 `.updates` 存储、更新领域服务、WebHost 更新端点、`Xke.Updater` 独立进程、DesktopHost 桥接和前端最小入口。
  - 更新仍按用户主动触发执行，不做后台静默强更。
- 文档和边界已重新收敛。
  - notes 已按 `development-docs`、`roadmap`、`review-issues`、`sensitive-index` 等目录职责整理。
  - 新增 `AGENTS.md`，明确后续开发必须先读 notes，且真实云端点不能提交到 GitHub。
- 私有云端点恢复为“本地可用、公开仓库不可见”的形态。
  - 真实 COS / 云函数地址放在 ignored 的 `开发数据/dotnet8-cloud.local.json`。
  - 公开代码使用 `example.invalid` 和可替换解析逻辑。
  - 运行时优先读取发布目录 `xke.runtime.json`，本地开发再读取 ignored 私有配置。
  - 已增加打包注入工具：`tools/inject-private-cloud-config.ps1` 与 `tools/package-desktop.ps1`。

### 当前暂存区状态

当前暂存区主要是“私有打包注入”这一批：

- `.gitignore`：忽略发布产物和 `xke.runtime.json`。
- `AGENTS.md`：新增项目级协作、notes、敏感数据和私有打包规则。
- `README.md`：补充私有打包命令入口。
- `PrivateCloudEndpointOptions`：支持发布目录 `xke.runtime.json`，并支持中性短字段 `remote.n` / `remote.t`。
- `CloudSyncWebHostTests`：补充注入 runtime config 的解析测试。
- `tools/inject-private-cloud-config.ps1`：从 ignored 私有配置生成发布目录运行时配置。
- `tools/package-desktop.ps1`：发布 DesktopHost 并自动注入私有端点。

### 已验证

- DPAPI 存储相关测试已通过：`tests/Xke.SecurityStorage.Tests/Xke.SecurityStorage.Tests.csproj --no-restore`。
- 私有端点解析相关测试已通过：`tests/Xke.WebHost.Tests/Xke.WebHost.Tests.csproj --no-restore --filter CloudEndpointResolvers`。
- 已用本机 ignored 私有配置验证公告 feed 可拉取且公告条目非空。
- 本轮未重新宣称全 solution 最新通过；历史上 `UpdateWebHostTests.PrepareInstall_OnlySucceedsAfterVerifiedPackage` 曾出现无关残留失败，后续发布前必须现跑确认。

### 后续需要做

1. 完成私有打包注入批次收尾。
   - 复核当前暂存区。
   - 在 PowerShell 环境恢复后实际运行 `tools/package-desktop.ps1`。
   - 用发布目录中的 `xke.runtime.json` 验证 DesktopHost 能拉取公告并正常遥感上报。
2. 补齐发布流程。
   - 正式生成 `XKE-<version>-win-x64.zip`。
   - 计算并写入 `package_sha256`、`package_size`、`release_notes_url`。
   - 上传发布包和 release notes 后更新私有 cloud feed。
   - 跑 `tools/update-smoke.ps1` 或等价更新链路验收。
3. 复跑门禁。
   - 先跑与当前批次相关的 WebHost / Update / Storage 测试。
   - 再跑 `dotnet test XKE.DotNet8.sln --no-restore`。
   - 若仍只有已知 update 测试残留失败，单独记录，不混入私有端点修复。
4. 继续处理既有结构债。
   - `AuthService` 首登异常回滚边界。
   - `ScheduleStore` 当前周本地日期推断。
   - `XkeWebHostApplication` 服务注册、session/context、blocked policy、error envelope 的结构拆分。
   - 建立 `Xke.Core.Tests`，把 normalize/parser/decision 纯逻辑从 WebHost 集成测试中逐步迁出。
5. 保持后置边界不漂移。
   - 传统教务验证码、OCR、Cookie 课表和真实抢课继续留在 Course / future-only 边界。
   - 云端点可以进入打包产物，但不能进入 GitHub 仓库；写权限、密钥、鉴权继续只放服务端或云端环境。

> 以下章节保留 2026-04-24 阶段报告的历史基线；若与本节冲突，以 2026-04-26 补充总结和当前 `development-docs` 为准。

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
