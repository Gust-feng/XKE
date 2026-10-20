# XKE

## 工作区验收候选：本地验证码自动识别

当前工作区已接入Windows本地OCR：首次人工验证码，可信账号后续默认开启且可关闭；只有明确验证码错误才换图重试，总共最多三次，其他错误立即停止并保留人工。模型和必要的官方VC++ DLL随包，无Python或额外运行库下载安装步骤。设置显示真实状态，周/学期/后台刷新共享续期策略。

本地V1.0.2候选仅供用户验收，尚未作为GitHub正式版本发布；公开版本仍以Releases为准。真实隔离链路、测试、包体和新机验收限制见[当前任务板](notes/development-docs/personal-runtime/02_task_board.md)。

## V1.0.1 与更新

公开仓库：[Gust-feng/XKE](https://github.com/Gust-feng/XKE)；发布页：[Releases](https://github.com/Gust-feng/XKE/releases)。正式版本由GitHub Actions验证、构建并发布，以Releases中的已发布资产为准。

v1.0.0候选标签因CI发现凭据变更识别缺口而未发布，保留原标签；修复版以v1.0.1发布，自动更新仍只接受更高版本。

设置→系统标题旁的版本入口可检查更新、自动下载、立即重启安装；自动模式不强退使用中的程序，在真正退出或下次启动应用。使用Velopack正式安装/便携包，开发bin不自我覆盖。数据保留在原本机目录，不随更新替换，不上传账号/课表/校园网凭据。

构建：先`npm --prefix frontend run build`，再`pwsh -NoProfile -File tools/package-desktop.ps1`。默认生成artifacts/releases/1.0.2，严格禁止个人数据/私有密钥注入。详细发布资产及行为见[发布与更新手册](notes/development-docs/personal-runtime/08_release_and_updates.md)。

个人课表与校园网工具。Windows版由.NET8后端与WPF/WebView2宿主运行，React + TypeScript负责界面。
现第七版「合成」设计锚定为产品第一版，保持原布局与动效。新增独立ProtocolHost用于未来云服务器部署，复用同一学校HTTP协议和数据解析。

当前主版本为 V1.0.2，正式标签对应已合入main的源码。已有实现、定向测试和真实使用的验证边界以任务板记录为准；开发期不新增旧业务文件迁移，正式发布后的数据变更须评估升级兼容。

## 接手入口

- [后端技术手册](notes/development-docs/personal-runtime/05_backend_technical_manual.md)：模块地图、默认表单登录/旧SSO区别、会话、数据模型与接手顺序。
- [桌面API契约](notes/development-docs/personal-runtime/03_api_contract.md)和[协议服务部署](notes/development-docs/personal-runtime/06_protocol_host.md)。
- [后端任务板](notes/development-docs/personal-runtime/02_task_board.md)、[前端任务板](notes/development-docs/frontend-rebuild/02_task_board.md)、[前端交接](frontend/docs/backend-handoff.md)。

## 仓库与提交边界

源码、合成测试、依赖锁文件和必要技术说明参与版本管理。设计压缩包、编辑器状态、运行数据、构建/发布产物及真实配置只保留在本地；忽略文件不等于打包白名单，正式发布仍由独立门禁校验。

必要源码按依赖分批提交，缓存、产物、真实配置及个人研究材料不提交。合入main并完成检查后，推送与Directory.Build.props版本一致的正式标签（例如v1.0.2）触发Actions；流程先验证，再生成草稿发布，上传资产重新下载校验后才公开。构建配置中的校园网自助地址通过Actions最小配置secret注入，不写入源码；不允许注入账号、密码、Cookie或token。

## 构建与本地运行

.NET SDK见global.json；验证统一使用仓库内.dotnet_cli。

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD ".dotnet_cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
npm --prefix frontend ci
npm --prefix frontend run build
# 真实前端资源为frontend/dist；无界面API运行：
dotnet run --project src/Xke.WebHost
```

React开发可指定 `VITE_XKE_API_BASE_URL` 指向本机API。默认runtime同源，只有显式preview启用合成数据；runtime不因连接失败回退mock。
设计预览使用前端README所列preview命令，产物必须标记mode=preview，桌面打包继续拒绝preview。

```powershell
# 定向前端检查，不启动浏览器
./tools/frontend-smoke.ps1 -SkipInstall
# 实际WebHost + 假学校/校园网/Windows调用，临时数据根：
./tools/test-frontend-integration.ps1
```

隔离HTTP验证使用合成宿主，自动启动/关闭并清理其临时数据，不创建WPF窗口或读取真实密码。Windows宿主构建：`dotnet build src/Xke.DesktopHost`。
独立协议服务需配置 `XKE_PROTOCOL_API_KEY`、`XKE_PROTOCOL_DATA_DIR`、`XKE_DDDDOCR_URL`，启动步骤见部署手册。

## 工程结构

| 项目 | 职责 |
| --- | --- |
| Xke.Contracts | 登录、课表、设置、校园网的数据契约 |
| Xke.Core | 学校HTTP协议、DOM解析、桌面业务服务与后台调度 |
| Xke.Storage | 学生索引、业务JSON/CSV、桌面凭据/会话存储入口 |
| Xke.Security | Windows当前用户DPAPI |
| Xke.WebHost | 本机HTTP API、后台任务、前端资源服务 |
| Xke.DesktopHost | WPF/WebView2、托盘、自启动、窗口控制 |
| Xke.ProtocolHost | 跨平台无界面纯协议API，内存学校连接，ddddocr识别 |

仓库为 `Z:/XKE`，共同分支 `refactor/personal-runtime`。前端写frontend/，后端写src/、后端tests/、tools/和notes，按目录分工，不全仓库暂存或覆盖另一方变更。

## 当前业务边界

- 桌面首次登录使用学校表单协议和人工验证码；完整本地工作区可离线恢复，连接与刷新才访问学校。
- 桌面启动自动恢复已有本地账号；首次使用需要认证，成功后进入主界面。重新连接从设置打开，界面使用简短按钮而非说明性引导。
- 云协议版使用ddddocr本机CPU识别进程，不要求浏览器环境；每进程一个学校连接，重启或失效后重新登录。
- 资料、当前/历史学期与单周课表使用纯HTTP GET，登录自身需要学校规定的POST。当前学校固定协议不扩展为通用爬虫。
- 课表由服务器周次、节次和时间规范化，保留多时间段、原始学时说明、补充课程；正常空周有效。
- 桌面个人快照服务优先，失败回退JW；学校失败保留缓存。校园网保活与课表刷新由C#后台执行，不依赖React页面生命周期。
- 桌面课前提醒由 C# 后台发送，使用已知课程日期与真实时间，支持关闭及 15/30/45 分钟提前量；普通业务回执用于防止重启后重复提醒。
- 当前校园网不支持主动断开、设备列表、流量统计或踢设备；设计预览不代表这些能力已经实现。
- 应用更新使用固定 GitHub 来源和 Velopack；旧 Updater、公告、遥测、反馈、云控制/同步和选课不进入默认装配。旧移动教务实现、测试开关和缓存回退已退役；当前仅保留 JW 表单/Cookie 协议及已配置的课表快照来源。

## 数据

桌面默认 `%LOCALAPPDATA%/.XKE/data`，可由 `XKE_DOTNET_DATA_DIR` 覆盖。
普通profile/preferences为JSON，课表按 `students/<student_id>/schedules/<academic_term>/` 保存周与学期JSON/CSV；这是持久化物化副本，非浏览器缓存。
密码、Cookie、桌面运行会话保留现有DPAPI。协议服务使用独立 `XKE_PROTOCOL_DATA_DIR`，学校登录材料只在内存，不调用DPAPI。

私有配置/真实凭据/原始响应留在ignored开发数据或明确受限位置。公开技术手册仅记录协议结构、路径和脱敏测试；不部署、不提交、不删除旧目录。
