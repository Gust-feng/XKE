# notes 优化记录

## 2026-10-17 分批源码归档

整理后获得用户本轮commit授权，根AGENTS、当前焦点/实施步骤及活动导航同步为“精确范围的本地分批提交，推送和发布另行授权”。先前无提交记录保留为整理阶段历史事实；提交证据只在当前任务板记录，六项延后历史补记不夹带。没有新建notes文件或重复提交本地执行报告。

## 2026-10-17 仓库整理同步

原地同步 README、Contracts/WebHost 说明、模块地图和当前 notes 导航，纠正 React 仍 mock、提醒未交付及全部更新退役等过期口径。历史开发过程继续保留；不新增开发项目或说明文件。提交准备清单只放 ignored artifacts，不把临时审计报告和压缩包引入源码。

## 2026-10-17 V1.0.0 发布入口

新增personal-runtime/08_release_and_updates.md记录用户最新自动更新授权、官方组件、机器设置、无个人数据发布边界及未公开Release状态；同步README/doc-entry/development-docs/项目入口、当前焦点、任务板与API，不复活旧云模块，不写入真实凭据/云端点。

## 2026-10-16 当前状态收敛

原地同步personal-runtime、frontend交接与review-issues，统一APP-01～05及当前待验收摘要；历史记录保留并明确不覆盖顶部现状。同步入口/数据边界，没有新建notes项目或目录；不含真实账号、IP、Cookie、原始响应或能力令牌。

## 2026-10-13 自助后台字段裁剪

- 根据用户本人 Chrome 中账号的实际返回结果复核空值和零值：余额、金额、近期用量、设备列表等当前没有可靠业务意义。
- 将自助后台第一版白名单收缩为账号状态、套餐、计费方式和服务器计费周期；保留其他接口作为后续观察资料，不提前写入前端契约或 JSON/CSV。
- 未读取或保存个人资料、密码字段、Cookie、原始响应；未修改远端账户、项目代码或 frontend。

## 2026-10-12 校园网自助后台协议整理

- 新增 `personal-runtime/07_self_service_protocol.md`，记录用户 Chrome 只读观察到的后台登录、Cookie、GET 接口和不规则响应结构。
- 明确自助后台与认证门户分离；第一版不提取个人资料、不保存完整响应、不读取密码相关字段，不立即把未确认数组列语义写入前端契约。
- 同步 notes 根入口、development-docs 入口和 personal-runtime 阅读顺序；本轮只整理协议边界，未修改 frontend、未接入真实账号配置、未保存原始响应。

## 2026-10-10 数据闭环优化

- 原地更新05/06技术手册、03 API契约、两项目状态文档及导航，记录学期缓存读取、真实获取时间优先级、共享转换与protocol客户端。
- 用户确认既有学校表单协议此前实测可用，保持登录基线，区别于新增OCR/宿主组合验收；历史开发过程继续保留。
- 未新建开发项目或文档目录，未复制真实配置、凭据或原始响应。

## 2026-10-10 技术手册与云协议版

- 新增 `personal-runtime/05_backend_technical_manual.md`：十分钟接手、模块入口、默认表单/旧SSO区别、登录状态、数据与前端规则；保留为当前技术手册。
- 新增 `personal-runtime/06_protocol_host.md`：无界面协议API、ddddocr接口、独立数据根和Linux部署；保留为当前部署手册，未部署事实明确标注。
- 同步 notes/README、doc-entry/00_overview、development-docs索引、两项目四份状态文档、roadmap与review-issues。历史开发过程不删除/压缩，当前状态放首部避免后续Agent误读。
- 本轮没有提取私有归档正文或新增真实值；长期教务声明增加当前入口以纠正旧后置/SSO唯一主线表述，不删除参考库。

## 目标

- 保持 `notes` 只承载当前开发可直接执行的输入。
- 对后续开发保持**可追溯性**：什么是本轮决策、什么是历史沉淀。
- 避免 “移动教务” 与 “教务系统（传统）” 在文档中混用造成判断偏差。

## 历史导航优化结论（旧移动主线阶段）

以下结论保留当时治理过程；当前默认 JW、Vue/runtime 集成与阅读优先级以文末最新维护记录、活动入口和 personal-runtime/API 为准。

- 活跃文档已从根目录散文件收敛为分类目录：
  - `development-docs/`：按“开发文档 / 开发项目 / 具体内容”组织当前实际开发项目，目录入口为 `../development-docs/README.md`。
  - `roadmap/`：后续方向与阶段总结。
  - `review-issues/`：当前问题与审查清单。
  - `future-plans/`：future-only 传统教务计划。
  - `doc-entry/`：README 与本文件。
- 归档目录保留历史 00/12/13/18/19/未来目标 等材料，按 `legacy-` 前缀归类并统一入口。
- 统一将“移动教务”定义为主线链路，传统教务仅在前向预案与 `Course` 边界讨论。
- 将隐私增强与前端拆分移出本轮主线，避免与后端结构收敛目标冲突。

## 继续执行要求

- 若发现新报告（如 18 号类）与当前决策不一致，先归并到 `../review-issues/01_review_findings.md` 的“边界状态”。
- 任何后续新增 notes 文件，先在本文件登记“保留理由/冻结理由”后再落盘。
- 当前正在开发的项目只进入 `development-docs/<project>/`，避免污染 `roadmap/` 和 `sensitive-index/`。
- 真实资料只放在仓库 ignored 的 `开发数据/`，新增资料目录和说明文档用中文命名；`development-docs/<project>/` 只保留开发说明和真实资料位置指引。
- 开发文档中的 URL、账号、token、云资源地址、发布桶路径、云函数地址必须使用占位符；如需真实值，只写“见 `开发数据/<中文目录>/...`”。
- 新增或移动开发项目时，必须同步更新 `../development-docs/README.md`、`../README.md` 和 `00_overview.md`。
- 根目录 `README.md` 是 notes 文件夹职责说明，不能退化成单纯跳转页；每次目录结构或文档职责变化都要同步更新。
- 外部 XKE 文档只允许引用或提炼；新增外部提炼文档时必须同步检查 `02_external_reference_index.md` 和 `../sensitive-index/03_external_reference_rules.md`。

## 2026-04-25 CloudSync 文档调整

- 新增 `../development-docs/cloud-sync/`，作为公告、远程策略、遥感本地队列的当前开发项目文档。
- CloudSync 文档必须留在 `notes/development-docs/cloud-sync/`，不能放在仓库根级 `development-docs/`。
- 旧父项目公告/遥感文档只作为字段参考，不再作为 .NET 重构目标设计。

## 2026-04-25 WinUI 3 原生 UI 预案

- 新增 `../future-plans/01_winui3_native_ui.md`，记录后续可能从 WebView2 前端迁移到 WinUI 3 原生桌面 UI 的方向。
- 该预案属于 future-only 边界，不改变当前 `WPF + WebView2 + WebHost + Web 前端` 的可用基线。
- 当前阶段仍以后端结构收敛、存储/API 契约稳定和现有前端兼容为优先级。

## 2026-04-25 一键自动更新开发项目

- 新增 `../development-docs/auto-update/`，把一键自动更新从讨论方案转为可执行开发切片。
- 第一阶段仅覆盖用户主动点击后的下载、校验、安装计划、独立 updater 替换和重启，不做后台静默强更。
- 更新临时文件必须写入 `.runtime/data/.updates/` 或覆盖数据根下，不能触碰学生数据、凭据和课表缓存。

## 2026-04-26 应用原生提醒迁移记录

- 在 `../future-plans/01_winui3_native_ui.md` 补充应用原生提醒边界。
- 明确当前课程提醒触发仍依赖 Web 前端运行，`Xke.DesktopHost` 只提供托盘气泡发送通道。
- 后续迁移目标是由 `DesktopHost/Core` 承担提醒设置读取、课表候选计算、定时触发、去重和发送记录；托盘和开机静默启动场景不能依赖 WebView 加载。

## 2026-04-26 反馈云上传开发项目

- 新增 `../development-docs/feedback-cloud/`，记录反馈云函数整理、客户端 transport、私有端点注入和本地兜底边界。
- 真实云函数地址、COS 配置和原始 ZIP 只保留在 ignored 的 `开发数据/` 下。
- 当前完成范围是提交时云端优先上传，失败写入本地加密 outbox；后台补传仍为后续项目。

## 2026-10-07 本地工程结构整理

- 活动仓库从旧嵌套目录提升为 `Z:\XKE`，保留原有 Git 历史、分支、远端和未提交文件。
- 新增 `development-docs/workspace-consolidation/` 四份执行文档；保留理由是记录真实物理迁移、归档边界、校验结果与未完成清理项。
- 同步 notes README、活动入口、开发项目索引和外部资料规则。
- 历史 Python 文档与私有资料保持原文，存入 ignored 的 `开发数据/历史工程/旧Python工程.zip`；notes 只记录路径和用途。
- 根目录原始页面收纳到 ignored 的 `开发数据/历史页面样本/`，不当作前端源码或公开测试样本。
- 课表验证脚本改用仓库自己的 `web/`，不再读取旧父目录前端。
- 删除旧临时目录和空嵌套目录被执行策略拦截，任务板明确保留待清理项。
- 本轮不实施功能裁剪，不重做前端，不打包，不提交或推送。

- 最终验证：桌面构建 0 警告 / 0 错误，6 个宿主测试通过，两个归档的全部文件内容校验通过。旧自启动登记已备份并清除；旧目录自动删除和升级审批均被拒绝，实际待清理路径保留在任务板。


## 2026-10-07 管理员清理 GUI

- 新增 `tools/cleanup-legacy-workspace.ps1` 与 `.cmd` 启动器，由用户在本机显式运行。
- GUI 仅列出四个既定目录，清理旧工程前校验归档；日志保存在 ignored 的 `.runtime/maintenance/`。
- 同步当前工程整理文档、任务板和导航入口；本轮没有代用户执行删除。
- 本轮核查发现三处占位/发布目录已不存在，只有 `Z:\XKE-legacy` 仍需清理。


## 2026-10-07 本地清理完成复核

- 用户在本机完成删除，复核确认 `Z:\XKE-legacy`、旧嵌套目录、空文档目录、旧发布目录与 `nul` 残留文件均已不存在。
- 两个临时清理 GUI 脚本已删除，移除活动文档中的失效链接和执行说明。
- 当前仓库仍位于 `Z:\XKE`，Git HEAD、`main` 分支及原 upstream 保持不变；两份私有归档 SHA256 与清理前一致。
- 工程整理任务板已标记完成；本轮仅同步收尾记录，未修改业务实现。


## 2026-10-07 基线提交范围整理

- C# 实现、必要测试、开发状态和迁移记录纳入本地提交；前端按用户原意保留在本地排除规则中。
- Python 协议辅助工具及测试保留本地，相关操作与交接说明收纳到 ignored 的 `开发数据/本地协议工具说明/`，不删除当前 C# 开发文档。
- 新的 `.dotnet-cli-home/` 与 Python 缓存进入忽略规则；冗余缓存自动删除仍被 `blocked by policy` 拦截，不将本地依赖提交到 Git。
- 提交前修正本地模式云上传装配与过期验证码客户端释放，验证只运行相关切片。

- 定向验证实际结果：WebHost/核心 79、认证存储 16、契约 14 项通过；桌面宿主与更新器构建通过，均无警告和错误。


## 2026-10-07 个人运行时重构

新增 `development-docs/personal-runtime/` 四份执行文档；保留原因是用户明确将产品转为个人本地软件。前端独立重做，后端移除公告、遥测、反馈、云控制、选课和自动更新。用户数据和私有资料保留，旧开发文档不删除。

## 2026-10-07 同分支前端开发准备

按用户澄清，前后端在同一分支、同一工作区并行：新增 `frontend/` 开发约束与后端交接，以及 `development-docs/frontend-rebuild/` 四份执行文档。保留原因是明确新前端从头设计的范围和协作方式；当前没有旧前端代码、框架、页面或依赖。前端独占 `frontend/`，后端负责 C#、公共契约及导航资料。刚建立的单独前端 worktree 和分支予以撤销。


## 2026-10-07 个人后端实现与交接

- 后端装配收敛为四项核心功能和桌面生命周期，删除退役代码和测试，旧私有资料/运行数据不删除。
- 新增个人后端 `03_api_contract.md`，作为前端只读交接入口；同步 README、doc-entry、development-docs、module-boundaries 和当前协作指引。
- 当前路线和问题文档顶部说明新的产品范围，历史云模块文档保留原有过程角色。
- C# 后台恢复本地可信工作区，执行校园网保活及课表自动刷新；页面不承担计时器。
- 本轮未修改 `frontend/`，未打包、未推送，保持同分支按目录并行。

- 已退出功能的 course/feedback/notice/telemetry 脱敏 fixtures 同步删除，历史版本仍在 Git 可追溯；长期敏感资料索引和当前开发文档未删除。

- 最终实际验证：Contracts 10 / SecurityStorage 13 / WebHost 91，共 114 项测试通过；桌面构建无警告/错误，打包工具语法检查通过。当前改动未提交或推送。


## 2026-10-07 个人课表 API 多链路

- 在 personal-runtime 内增加服务快照获取器，共用既有课表规范化/持久化接口，失败回退 JW；不新增桌面 OCR 运行依赖。
- 服务实际配置、使用手册和服务器部署材料保存在 ignored 的 `开发数据/课表服务/`，未复制真实响应或认证材料进 Git。
- 同步 README、开发入口和公开前端数据契约；只说明产品行为，私有手册不进入 notes。
- 真实服务尚待用户提供接口资料，未进行部署或真实调用。

- 本次服务接入验证：WebHost 114 / Contracts 10 通过，DesktopHost 构建无警告/错误；新增 23 项来源回归覆盖。私有手册和配置不在 Git index；服务器适配仅语法验证，未真实部署或联调。

## 2026-10-07 获批 Vue/runtime 集成与导航纠偏（最终已实现，隔离验证完成）

- 只更新既有 `notes/` 文件，无新增、移动、归档或删除；不修改根 AGENTS、前端/后端代码、私有配置/真实数据，不提交或推送。历史开发项目与 `sensitive-index/教务系统` 长期资料库保留。
- 阅读 AGENTS、notes 导航、personal-runtime/frontend-rebuild 各四份必读、API、路线/问题和敏感声明后，发现导航仍把 cloud/feedback/update 置于活跃顺序，并将已有 Vue 实现描述为空目录；用当前公开代码与协调方回传确认不一致，更新 notes 而不覆盖其他负责人的文件。
- notes README、doc-entry overview、development-docs README 的活动顺序改为 personal-runtime → frontend-rebuild → API → roadmap/review → 对应敏感声明；旧云项目与工程整理保留历史入口。术语更新为默认 JW 表单登录、个人课表快照优先/直接 JW 回退、移动实现仅历史兼容，不广泛改写敏感资料正文。
- frontend-rebuild 四份、personal-runtime 四份与 API 同步显式 `VITE_XKE_PREVIEW=true` / `dev:preview`、同源/`VITE_XKE_API_BASE_URL` 配置 runtime base（不是 `_BASE`）、内存 session、独立验证码、真实模型适配、账号/机器设置 patch、无前端 job scheduler、Vite index/assets health。UI 值由后端负责人确认：`ui.show_weekend` bool 默认 true，`ui.inactive_course_mode` `dim | hide` 默认 hide。
- 发布采用 `frontend-build.json` 的 `{build, mode: runtime | preview}`；默认 dist 必须 runtime，显式自定义旧资源缺标记/旧元数据可兼容，preview/畸形/不支持/非对象元数据拒绝。协调方回传 PS7 语法、11 个模式/元数据 fixture 与实际 preview 在 output 创建前拒绝，未 publish；最终 runtime/preview 标记产物已构建验证，preview 后恢复 runtime，不包含 demo chunk/固定验证码。
- 当前协调方回传：affected 29/29；solution Contracts 11 + SecurityStorage 13 + WebHost 128 = 152/152，0 skipped；含 DesktopHost 的 build 0 警告/0 错误。`tests/Xke.FrontendIntegrationHost` build 0/0，fixture HTTP 登录成功、课表验证码错误 400/重试 200、刷新失败 503、graceful stop 202。该独立验证 executable 使用实际 WebHost、fake 外部/Windows 调用与临时根，不是生产服务或真实验收；文档负责人未重跑。
- 最终前端负责人回传 35/35 tests、严格 vue-tsc + Vite runtime build、runtime metadata；显式 preview build 后恢复 runtime 通过。协调方 `tools/frontend-smoke.ps1 -SkipInstall` 通过 35 tests + build。runtime marker 与 health.frontend_build 一致，两份 emitted assets HTTP 200/hash 列入，生产 JS 无 DEMO-0001/7k8x；不固化临时 build id。隔离 host stop 202/exit 0/临时根清理通过。
- 实际 WebHost + fake 外部服务的 browser 流程：challenge 登录显示 3 实际 DTO rows（不规则 2–4 节/未知字段）、重载恢复账号、数字周次标签修正；课表 challenge 过期禁用提交，取消后刷新 503 保留缓存，第 9 周实际 404 不展示其他周/第 8 周恢复；show_weekend=false 和 startup=true 重载保留（内存 registrar，无真实 Windows 修改），网络 on-open/maintain 保持 offline，注销清理用户/课程并启用登录。
- IAB 点击/截图因 viewport 不一致间歇超时，1440×900 恢复截图/一次鼠标点击；后续 locator.evaluate(el => el.click()) 触发实际事件，无内部状态覆盖。不宣称完整鼠标/键盘/视觉验收。错误验证码重试仅 HTTP harness/自动化状态测试验证；浏览器因工具延迟 challenge 过期未验证重试。文档负责人未重跑。
- roadmap/current issues 更新为集成已实现、所述隔离验证完成，仍保留真实验收与原生交互 pending；不重写历史问题体系。真实学校/个人服务产品端到端、校园网、Windows/WebView2/托盘/自启动与发布包/发布未验收。前期后端/个人服务/preview 测试记录保留历史角色，既有私有手册核对追加记录原样保留，不扩大为产品端到端验收。
- 本轮导航链接检查：15 份本次维护文档、142 个本地 Markdown 链接（含锚点）全部可达；15 份文档空白检查与范围内 diff --check 无错误，0 个失效；不以链接可达代替功能验收。

## 2026-10-07 私有 API 手册对齐

仅在 personal-runtime 既有文件登记脱敏接入状态，不新增公开操作手册。真实 API 手册保持 ignored；真实端点、指纹、token 和运维资料未复制至公开代码/fixtures/notes。具体本机接入记录和配置仍放私有目录。

## 2026-10-07 单人课表服务语义

用户确认服务器课表全部属于本人，取消强制学号绑定，只更新来源适配器、回归测试和当前开发状态。私有手册仍 ignored，配置和前端实现不改动；旧个人凭据未读取。

## 2026-10-07 周次误拦截修正

当前后端周标签统一为 `第N周`，来源 option 查询日期保留；缺日期的旧缓存仅按匹配周的周一补齐。更新个人运行时现状及接口说明，不改前端实现或重新打包。相关 35 项测试和桌面构建通过，运行实例已更新。

## 旧前端一比一 TS 迁移

用户明确拒绝新设计并授权子智能体，以本地 web 为视觉基线；备份新设计，更新 frontend-rebuild 和入口。HTML/CSS/辅助模块先完成，控制器和动态整合仍在验证。旧私有资料和原 web 未修改，不引入真实数据进测试。

## 旧版TS整合验证完成

当前前端改为旧web外观和实际TypeScript控制器/辅助模块，vanilla Vite构建。用户授权的3个子智能体写集已整合；严格检查0诊断、41项测试通过，隔离WebHost核心交互通过。同步frontend-rebuild及frontend验证文档，旧设计私有归档可恢复，旧web未修改。字体fallback、完整视觉范围与未交付后台提醒的限制如实保留。


## 2026-10-08 前端版本与设计标准统一

- 用户明确选择压缩包第二版及其优化设计，统一活动目录 frontend/（React + TypeScript + Vite）。
- 原 web、旧 TS 迁移、早先 Vue 与独立 demo 归入 ignored 的 开发数据/历史前端/20261008-baseline-consolidation/；附原始 ZIP、整理前快照、文件清单及 SHA-256。
- 保留 frontend-rebuild 四份执行文档历史过程，在顶部明确新标准与 mock/真实集成边界；同步 notes README、doc-entry、开发项目索引、AGENTS 与工程 README。
- 预览构建输出 dist-preview/、preview/ 并标記 mode=preview；更新 smoke 为类型/lint/构建，不将旧测试计数当作新 React 验收。
- 当前只有设计标准变更与工程整理，真实 API 适配与桌面桥接尚未完成；私有手册及真实用户运行数据不变。


## 2026-10-08 JW 协议信息提取研究

- 保留理由：用户明确将后端研究重点转向已有 JW 链路中的信息提取；新增 [研究记录](../development-docs/personal-runtime/04_jw_protocol_research.md)，放入现有个人运行时项目，不新增并行开发项目。
- 记录当前代码、脱敏 fixture、归档旧协议源码的来源关系，以及假页面复现、字段证据和后续页面样本需求；旧推断规则仅作历史参考。
- 本轮没有读取真实运行数据、账号配置、Cookie/token 缓存或原始录包，没有访问真实教务；归档只原地读取 core/jw_services.py 的相关函数，不解压、不复制源码。
- 同步 notes/README、doc-entry/00_overview、personal-runtime/README/当前焦点/任务板与 review-issues 两份入口；没有修改生产代码或前端。


## 2026-10-08 真实协议研究补充

在既有 personal-runtime/04_jw_protocol_research.md 第10、11节追加用户授权的浏览器辅助观察与源结构复现，保留初轮历史证据；不新增开发项目。同步焦点、任务板、导航、问题/路线与长期教务索引。浏览器不进入产品获取链路；纯 HTTP 真实验收仍 pending。真实资料和截图不进入公开文档，生产代码/前端/认证缓存未改动。


## 2026-10-08 后端数据提取第一轮实现

用户授权修改后端，随后明确项目一直处于开发状态、不需要兼容旧数据。更新既有 personal-runtime 文档与 API 交接，不新建开发项目；同步入口、路线、问题和存储说明。代码采用纯 HTML DOM 解析与 HTTP GET，资料/偏好为普通 JSON，课表按统一模型生成 JSON/CSV。没有旧加密业务资料转换或历史格式双写分支；账号密码、Cookie 与运行会话的既有 DPAPI 边界未改。真实验证仅通过内存 Cookie 做只读 C# 请求，公共测试全部使用假资料；不提交 Git、不修改 frontend/、不读写真实运行数据。

## 2026-10-09：第七版正式设计锚定与素材独立

用户确定第七版「合成」为唯一设计基线，保留其布局与第四版继承动效；活动 frontend/ 集中共享假数据，仍为 mode=preview。其他设计素材移出项目，按独立类别处理，不保留来源清单、压缩包或工程关联。同步前端说明/约束/交接、四份执行文档、notes 导航、路线与当前问题；既有开发过程文档保留为明确标记的历史记录，旧路径不作为运行入口。
