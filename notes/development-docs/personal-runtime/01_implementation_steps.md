# 当前实施顺序（2026-10-10）

## 本轮闭环已实现，下一步为用户验收

1. 已复用现有受保护可信token，未另造一套普通JSON信任标记；第一次账号密码认证仍返回人工验证码，成功初始化后默认开启。
2. 已接入共享AutomaticCaptchaLogin：三次总识别/提交轮次，只对CaptchaRejected重试；关闭/取消/注销停止提交，密码/未知错误停下；同进程失败后暂停自动认证，人工成功或显式关闭再开启恢复。
3. 已接入Windows模型包装器和官方app-local CRT；构建资源固定来源/摘要、发布门禁逐文件校验，组件不可用不影响人工登录。
4. 已接设置开关、动态状态和人工失败换图；请求取消同时中止HTTP传输。重新输入已有账号密码走显式reinitialize，避免本地旧密码校验挡住学校密码更新。
5. 已完成定向回归、合成HTTP、真实隔离自动初始化及后台续期、应用目录原生模块路径验证和本地候选打包。候选只供验收，不等于GitHub正式发布或全新系统安装已验收。

## 2026-10-19 自动验证码接入顺序（最新）

1. 已完成：以用户授权的保存账号确认学校两种失败消息，并确认C#模型可通过正确登录；只提取#showMsg，未知页面不推断。
2. 已完成：Core结构化区分CaptchaRejected/CredentialsRejected/Unknown，保留HTTP401契约并补隔离回归；不把HTTP200/401作为重试依据。
3. 待实现：桌面app-local模型/字符表/原生运行库资源、固定来源/校验及正式包内容门禁；不得复制System32 DLL冒充可再分发来源。
4. 待实现：本机账号的人工认证信任门槛与独立偏好。首次人工验证码成功后默认开启，不在全局默认值或未验证账号上开放自动识别；注销/切账号隔离，用户关闭不被会话恢复重新开启。
5. 待实现：共享登录/课表续期自动识别流程。最多三个总轮次（含首次），只在明确验证码失败时刷新图片/上下文；凭据错误、未知/限流/网络和组件故障不猜测重试。三次失败提醒并退回人工；取消/关开关时停止后续提交。
6. 待实现：设置内验证码自动识别开关和动态状态，保留手动验证码入口；定向回归首次人工、可信恢复、关闭、三次失败、凭据错误、迟到结果与课表后台，再作新机安装及真实链路验收。当前发布版与线上Release不在本轮自动更新。

## 2026-10-19 本地OCR验证顺序

1. 确认本机ddddocr版本、默认模型、字符表和预处理；将合成图片/模型/对照输出放ignored artifacts，不读账号缓存。
2. 独立Windows C#工具复用ICaptchaRecognizer，固定CPU ONNX Runtime；比较完整图片识别与Python预处理张量推理，区分缩放差异与模型解码差异。
3. 验证取消、非法图片、重复字符及并发，统计首次/稳定耗时；对比同版本Velopack包加入必要win-x64依赖和模型后的体积。
4. 仅在证据通过后准备生产接入：共享认证续期、有限重试、人工回退和模型资源发布门禁需要一起处理；本轮不把OCR注册到生产或自动提交学校认证。

## 2026-10-18 正式发布执行顺序

本轮用户已授权完成必要源码提交、main合并、GitHub同步及Actions发布。先核对源文件/敏感边界与已存在测试，再分批提交后端退役及回归、前端交互及工具链、正式构建/必要说明；不全仓库暂存。冻结的六项本地文档/技能及个人协议研究材料保持不动，不上传归档或运行产物。

PR在干净Windows环境执行完整验证（包含本机被真实桌面占用的固定端口用例）；通过后合入main，再创建与Version一致的v1.0.0标签。Actions负责构建发布、下载回验及公开，最后独立核对main/tag/commit、运行结论和七个Release资产。任何阻碍不以“已完成”代替，也不提前让电脑休眠。

## 2026-10-17 退役收尾实施范围

1. 在现用表单协议补完整工作区重新认证后的学校取课/意外异常回滚测试，先验证原行为。
2. 用拒绝/隔离测试复现移动缓存仍被默认读取及允许新写入的问题，再将存储来源收敛到JW；API非法来源返回400。仅改变代码，不处理现存用户文件。
3. 删除无引用资料转换器和可选缓存方法，统一mapper默认来源与当前契约/存储样本。
4. 同步当前说明与审计脚本，定向验证认证/缓存/提醒/协议消费者及解决方案构建。工作区其他既有改动不覆盖，不在此轮打包或提交。

## 2026-10-17 仓库整理与分批提交方案

准备完成后用户已授权重排暂存区并分批本地提交，仍不执行 push/tag/Release；用户要求非必要内容不提交，设计压缩包不提交；根目录四份已完成适配的设计ZIP及一份校验文件按用户后续授权删除。精确路径和状态清单生成在 ignored `artifacts/repository-preparation/20261017/commit-batches.json` 及各批次 `.paths`；它们是本地审阅材料，不提交进项目。

| 顺序 | 建议提交主题 | 必要范围与依赖 |
| --- | --- | --- |
| 1 | `chore(repo): exclude local archives and runtime state` | 仅忽略规则；根目录指定旧设计包已按授权删除，其他本地材料不清理。 |
| 2 | `refactor(runtime): consolidate the personal desktop baseline` | Contracts/Core/Storage/Security/WebHost/DesktopHost/ProtocolHost、配套测试/fixture、解决方案与版本属性、协议/OCR工具、旧模块删除。上述实现跨共享文件耦合；同一批纳入以维持构建，不伪造逐功能的历史提交。包含更新业务及原生实现，打包工具另归第4批。 |
| 3 | `feat(frontend): integrate the React desktop interface` | 唯一 React 前端源码、依赖锁、配置、合成fixture和测试，以及frontend-smoke。依赖第2批的真实API；preview与runtime隔离。 |
| 4 | `build(release): add verified Windows packaging` | 本地dotnet工具声明、Velopack打包/校验脚本、合成门禁、第三方通知。依赖第2/3批，不包含任何实际安装包或更新feed产物。 |
| 5 | `docs: align the current product and release boundaries` | 根README/AGENTS、当前API与技术/部署/更新手册及活动导航。只纳入审阅确认的必要说明；开发日记、历史审查记录和个人工作流修改另列延后，不默认一并提交。 |

原暂存区含旧删除和重命名，不能直接追加第1批后commit，否则会夹带既有暂存内容。本轮已取得重排暂存区授权，先保留原索引内容摘要，再仅清除原暂存路径的index状态，逐批精确暂存；不对工作文件执行restore/reset/clean。重命名必须包含旧路径删除及新路径，不能只加入目标文件。

先做公开候选内容、忽略规则、项目引用、空白检查和批次快照构建。此类验证不替代真实学校/新机安装/线上升级验收。批次使用实际author/committer时间，随机间隔只影响真实执行节奏，不伪造日期。项目自身许可证尚未选定，不擅自生成；第三方通知不能替代项目许可证。

## 2026-10-17 V1.0.0 自动更新增量

用户最新授权检查/自动下载/安装更新，当前实现以Core/Releases＋Velopack1.2.161为准，公开来源Gust-feng/XKE，稳定win渠道与PackageId=XKE；不恢复旧Updater/云控制。新机器设置auto_update_enabled与原生受控/api/app/updates状态/检查/下载/安装入口已接入。HTTP下载仅排队，C#持有并在关闭时排空；校验后退出应用，不强退到托盘的软件。关闭自动更新不会自动安装本地已下载包。

REL-02内容门禁、REL-03 runtime8.0.32发布基线已在新打包流程实现，78份合成门禁验证通过；REL-01自助摘要配置是可选非敏感注入，不再作为纯连接功能的发布前提。详细协议、已验证/待验收边界与公开发布授权要求见08_release_and_updates.md及任务板最新记录；旧“所有更新退役”与旧打包问题保留历史语境。

## 2026-10-16：本轮闭环执行

APP-01～05按Core/Storage后台业务→WebHost受控入口→DesktopHost能力→React状态消费接入，未引入调度框架或前端学校轮询。提醒使用原始真实时间而非显示块跨度，15/30/45与底层自定义值下限一致；后台更新只读回填不改变用户浏览。当前结果及剩余验收见[任务板](02_task_board.md)顶部，不再按下方历史未勾选项重复开发。

本轮数据闭环：先提取共享转换与学期本地API；前端先本地后按需刷新，并按获取时间合并；新增显式protocol客户端和学期源接口，再验证真实本机HTTP消费/失败回退。更新手册与当前任务板，不更改既有学校登录协议。

1. 核对当前前端交接、实际API和登录代码；当前默认表单协议与历史SSO入口分清。
2. 子智能体接React；独立子智能体实现ProtocolHost，共享解析/存储，写集不重叠。
3. 主负责人实现ddddocr客户端与loopback Python服务，整理登录/数据技术手册和部署示例。
4. 联调修复服务器选中学期读取、登录别名使用服务器学号，以及服务层跨地点/周次误合并。
5. 定向xUnit、前端类型/状态测试/runtime构建及真实隔离WebHost HTTP验证；不做无关UI/全量检查。
6. 核对结果，同步导航、当前风险与接手任务。真实学校密码登录、验证码准确率和Linux部署单列待验收。

当前手册：[技术](05_backend_technical_manual.md)、[部署](06_protocol_host.md)。下方为历史过程。

---

> 2026-10-08 前端整理：frontend/ 现采用用户选定的 React 压缩包优化设计，仅 mock 预览，旧 TS/Vue 运行时集成已归档供后续参考。下文旧 UI 验证为历史记录；真实 React API/桌面接入待完成，C# 功能和数据根未变。

# 个人后端实施步骤

## 已实施的后端收敛基线

以下为集成前切片的实施顺序，历史测试结果见当前焦点；不等同本轮前端集成已验收。

1. 从已提交基线在共同分支推进，前端写入范围为 `frontend/`。
2. 删除退役服务、路由、契约和更新器；移除核心路由中的远程访问策略。
3. 保留账号、设置、课表和运行数据的存储路径；重命名通用 JSON 转换器，避免残留公告命名。
4. 自定义设置采用值域规范化，不再使用固定时间/主题选项。
5. 由 C# 生命周期托管本地恢复、保活及自动刷新；用可控时钟和 fake 网络/系统调用验证。
6. API 统一业务异常处理，前端通过本地状态接口展示后台任务，不复制调度。
7. 构建和测试均使用临时测试数据根及 repo-local CLI 缓存。
8. 更新产品 README、API 交接和开发入口；该历史切片不启动 GUI、真实登录或发布打包。

## 已实现并完成隔离验证的 Vue/runtime 集成步骤

1. 明确 `VITE_XKE_PREVIEW=true` / `dev:preview` 与普通 runtime；runtime 同源/`VITE_XKE_API_BASE_URL` 配置 API base，API 错误不切回 mock。
2. 对齐实际 AuthPayload、Schedule rows/日期/时间段与 Network 模型，前端只在内存保存 session；桌面 bootstrap 与浏览器区分，不持久化密码/session/trusted token。
3. 登录 `/api/auth/captcha` 与课表 `/api/auth/jw-captcha/submit` 分开续跑，后者必须带当前 session；覆盖错误/过期/取消与原缓存保留。
4. 账号偏好与机器设置独立 patch 保存，未提交字段保留、清空按具体字段契约；UI 值已确认：`ui.show_weekend` boolean 默认 true、`ui.inactive_course_mode` `dim | hide` 默认 hide。
5. C# 调度校园网保活/课表刷新，前端只读状态与用户手动请求，不实现 job scheduler。
6. health 检查 Vite `dist/index.html` 实际引用的 assets，不依赖旧固定 app.js；发布引入 `frontend-build.json` 的 `{build, mode: runtime | preview}` 标记，拒绝 preview，默认 dist 必须有 runtime 标记，显式自定义旧资源无标记可兼容。协调方已回传 PS7 语法、11 个模式/元数据 fixture 与实际 preview 拒绝通过，未 publish；runtime/preview build 通过后恢复 runtime metadata/产物，无 demo chunk/固定验证码，marker/health build 一致、两份 assets HTTP 200/hash 列入已验证。显式 preview/畸形/不支持/非对象元数据拒绝。
7. 使用 `tests/Xke.FrontendIntegrationHost` 实际 WebHost + fake 外部/Windows 调用 + 临时根做隔离验证，该 executable 非生产服务。协调方已回传后端/HTTP 验证结果；实际 WebHost + fake 外部服务的 browser/runtime 流程验证已完成，stop 202/exit 0/临时根清理通过，详见当前焦点。
8. 最终前端 35/35 + 严格 vue-tsc/Vite runtime build、显式 preview build 后恢复 runtime 与 smoke -SkipInstall 35 tests + build 已回传。保留前期测试为历史切片；IAB 间歇超时后的事件点击不是完整鼠标/键盘/视觉验收，错误验证码重试仅 HTTP/状态测试覆盖，浏览器因 challenge 过期未验证重试。真实学校/个人服务产品端到端、校园网权限、Windows/WebView2/托盘/自启动和发布包/发布须另行验收。


## 2026-10-08 数据提取实现切片

1. 纯 DOM 替换课程树结构和个人资料的正则配对；保留服务器原文及明确时间段，不引入浏览器运行时。
2. 补齐学制/入学日期，并贯通昵称保存、API 和普通 JSON。
3. 实现学期/历史周 GET，新增独立学期 API 与快照路径；按周 API不使用全学期文件作为缓存。
4. 写新 CSV 列和数组格式，保留 JSON 元数据/统一快照；用户确认不做旧文件兼容或迁移。
5. 定向假数据回归及无浏览器的真实 C# Cookie GET 验证；登录/OCR/前端/发布不在本切片。
