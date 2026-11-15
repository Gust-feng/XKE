# 本轮联调审查结论（2026-10-10）

## 2026-11-10 课表学期身份修复

JW-20：ScheduleService.LoadLocal在日期到周号的转换中丢失学期身份，较新历史同周可影响Auth恢复、学期列表和刷新。改用LoadScheduleForDate返回完整记录；默认周读取先定学期，缺周404；runtime总传所选学期，JW显式当前学期保留首页日历。回归覆盖日期/无本周缓存/历史同周/显式历史读取和请求参数，结果见personal-runtime任务板。最初真实启动的完整时序未知，稳定复现与历史数据仍在的证据分开记录。

## 2026-11-03 天气、专属提醒与弹窗交互

当前验证：前端393通过/1项既有默认跳过，TypeScript及严格lint零警告；天气服务/真实HTTP契约9通过，桌面/提醒相关54通过（包含天气服务7项），DesktopHost19通过，runtime构建成功。固定38443端口测试初轮被旧桌面占用，正常退出后复跑全部通过；独立HTTP/DOM与构建不替代真实学校、天气精确性或多屏验收。运行时启动标记和最终原生预览结果由本轮收尾另记。

本轮新增天气与通知仅记录已验证边界，未做全仓审计；既有历史审查修改保持原样。

## 2026-10-17 V1.0.0 候选包与自动更新最终验证

- 公开仓库Gust-feng/XKE已确认，GitHub无Release时不伪造更新。主版本1.0.0；Velopack1.2.161官方组件，固定win稳定渠道/XKE包身份。系统标题版本面板提供项目页、检查、自动下载、进度与立即重启；关闭到托盘不安装，关闭自动模式不自动应用已下载包。旧云控制/遥测/Updater不恢复。
- 真实SDK Windows测试13/13；WebHost相关83项最终通过（首次其中固定38443端口用例被正在运行的旧桌面占用，停旧桌面后该项单独复跑1/1；不是代码回归）；前端7份214/214、类型与runtime构建通过。发布门禁79项纯合成通过，增加官方setup.ico及第三方许可通知的精准允许/缺失拒绝，不放开通用文件复制。
- 本地已生成artifacts/releases/1.0.0的Setup、Portable、full.nupkg、releases.win.json、assets.win.json、RELEASES六个已验证资产；自包含三runtime8.0.32，官方WebView2 bootstrap。THIRD-PARTY-NOTICES.txt包含18份依赖许可/通知，非项目自身许可证。当前未配置Authenticode签名，不能声称签名发布。
- 独立PackId合成便携程序真实完成1.0.0→1.0.1下载校验、退出替换、重启，外部合成数据保留；升级后DLL哈希与目标1.0.1完全一致。不是实际XKE/GitHub线上跨版本验收。
- 正式候选便携包解压至仓库外临时目录，使用新临时数据根、禁用自动检查并显式空来源，启动成功：app_version=1.0.0、update_supported=true、sessions=0、frontend_build=0c4b47cdf0a5fa39。不借用开发web/配置，不读真实账号、不改真实注册表；正常关闭通过。缺WebView2的新系统安装仍未实测。
- 实际开发桌面仅在最终阶段停止/重启一次，沿用原默认数据根与缓存；PID44236、窗口可见/响应、health=ok、app_version=1.0.0、build=0c4b47cdf0a5fa39、JS/CSS HTTP200。bin/Debug的update_supported=false是预期保护，不能自我覆盖；正式安装/便携包已确认true。使用正式包前应真正退出开发版，避免单实例唤回旧进程。
- 证据与六资产SHA256见ignored artifacts/v1.0.0-verification.json。当前源码与公开main不一致，且既有约定禁止擅自stage/commit/push；本轮没有GitHub上传、创建tag/Release或源码提交。公开发布仍须用户明确允许先审核、提交/推送正确源码，再让v1.0.0对应该commit。

用户操作与发布步骤见08_release_and_updates.md。发布后的数据模型变更要评估迁移/升级门禁，不能继续把开发期“无旧数据兼容”当作已发布用户数据可丢弃的许可；本轮不预造迁移框架。

## 2026-10-17 V1.0.0 自动更新增量

用户最新授权检查/自动下载/安装更新，当前实现以Core/Releases＋Velopack1.2.161为准，公开来源Gust-feng/XKE，稳定win渠道与PackageId=XKE；不恢复旧Updater/云控制。新机器设置auto_update_enabled与原生受控/api/app/updates状态/检查/下载/安装入口已接入。HTTP下载仅排队，C#持有并在关闭时排空；校验后退出应用，不强退到托盘的软件。关闭自动更新不会自动安装本地已下载包。

REL-02内容门禁、REL-03 runtime8.0.32发布基线已在新打包流程实现，78份合成门禁验证通过；REL-01自助摘要配置是可选非敏感注入，不再作为纯连接功能的发布前提。详细协议、已验证/待验收边界与公开发布授权要求见08_release_and_updates.md及任务板最新记录；旧“所有更新退役”与旧打包问题保留历史语境。

## 2026-10-17：正式打包前核查（REL-01～04，尚未正式出包）

本节覆盖前一轮“当前待办摘要”的发布部分：APP-01～05业务修复仍保持完成，但暂不将当前打包链路视为可直接正式分发。本轮仅只读核查代码及隔离Release构建/合成文件验证，未改业务/打包脚本、未读取真实私有配置正文、未重启实际桌面、未生成正式包。

- **REL-01 / P1，待修：校园网自助摘要缺发布注入。** package-desktop.ps1只有ScheduleApiConfigPath，没有CampusSelfService配置入口；CampusSelfServiceOptions要求可执行目录xke.campus-self-service.json，开发时可回退ignored开发目录。当前只核对元数据：开发配置存在、显式环境变量未设、主输出未带该文件。离开仓库且未人工放文件/设环境变量时，将退化为“尚未配置自助后台”，不是单纯UI问题。建议增加受校验的endpoint/timeout专用注入，不复制整个开发目录；此问题与05手册已有待办一致。
- **REL-02 / P1，待修：前端来源和输出内容门禁不封闭。** 默认缺metadata及显式preview能拒绝，但显式-WebRoot缺metadata仍通过；Sync-WebRuntime递归复制所有文件，合成.env和未知.dpapi会进入web，输出根外的旧杂文件没有清理门禁。使用合成文件单独执行preflight/Sync函数已复现，未运行正式publish。当前真正frontend/dist未发现这些可疑文件名，不能将门禁风险表述为真实秘密已泄露。建议要求所有正式来源具备runtime标记/完整资源，限制发布文件集合，使用专用空/临时输出而非在旧目录覆盖。
- **REL-03 / P1，待修发布基线：自包含选择旧.NET补丁。** 当前SDK10.0.202的ProcessFrameworkReferences（仅目标求值，没有publish）为win-x64/self-contained解析NETCore、WindowsDesktop、AspNetCore三套runtime pack均8.0.26；微软2026-10-13公布的.NET8最新补丁为8.0.32。先更新发布工具链或显式固定并验证三套一致的受支持补丁，不能以NuGet无漏洞替代共享框架/runtime补丁核对。.NET8于2026-11-10结束支持，应另行安排受支持LTS迁移，不在只读核查中擅自改目标框架。
- **REL-04 / P2，发布方案/实机验收待定：默认不是免依赖包。** 默认SelfContained=false；Release runtimeconfig实际声明NETCore、WindowsDesktop和AspNetCore三个共享框架，不能只要求.NET Desktop Runtime。项目只引用WebView2 SDK，没有发布时Runtime检测/安装分发流程；新机需要另行保证WebView2 Runtime。建议普通分发采用已更新补丁的自包含.NET包，并提供WebView2预检/安装路径；离开仓库、临时数据根的成品冒烟仍待正式候选包生成后执行。若只是本机自用，缺依赖风险不同，但不能称通用绿色包。DPAPI凭据不作为跨用户/跨设备的通用配置发放。

### 本轮验证与证据

- 隔离Release编译0警告/0错误；AuthProtocol/LocalApiAccess/LessonReminder/ScheduleMaintenance/Worker定向38/38，0跳过，未做学校/断网测试。
- npm生产依赖audit报告0已知漏洞；DesktopHost NuGet含传递依赖audit未报告已知漏洞，两者均不覆盖上述runtime补丁与部署配置风险。
- 打包脚本语法0错误，合成preflight/复制验证见ignored artifacts/prepackage-review-063dc5fcc4b14ddf877e0c778d35680a/results.json；runtime-selection.json、verification-summary.json只含合成/构建元数据，不含私有配置值。
- 当前runtime前端标记e6e311843b636148；dist仅运行资源，疑似凭据/.env/demo-data文件名为0；对136份公共源码/产物的有限私钥/特定云地址模式扫描未命中。这不是完整敏感信息零泄露证明，最终包仍须按清单核验。
- 官方依据：[.NET支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy)、[WebView2 Runtime分发](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)，核对日期2026-10-17。

建议顺序：修REL-01/02 → 更新并锁定发布runtime补丁 → 确定依赖/配置清单 → 在专用目录生成候选包并脱离仓库验收。暂不正式出包，不继续重做UI或扩展校园网策略。

## 2026-10-17 验证完成

10月16日APP-01～05本轮实现已完成最终定向验证与新版桌面启动；后端217/217、前端218/218、DesktopHost构建0警告/0错误、原生隔离访问/关闭及互斥验证通过。实际窗口可见、health/runtime build=e6e311843b636148，详细证据及唯一当前待验收摘要见个人运行时02_task_board.md顶部；下方历史内容不再代表未修状态。

## 2026-10-16：整体可靠性与课前提醒闭环（当前状态）

本节为当前状态，后续按日期保留的旧待办/未修复结论不再独立代表现状；不删除历史过程。

- APP-01 已实现：WebHost默认只接受本机Host；API显式Origin只接受实际同源或独立HTTP宿主显式配置的开发来源。桌面可信恢复及机器设置读写需要进程内随机能力令牌，由WebView2原生仅向自身API注入，不进JS/URL/磁盘/配置响应；外部浏览器回到显式手动认证，不授予原生设置能力。原生bridge/主导航限定本应用来源。不是对同用户恶意原生进程的隔离沙箱。
- APP-02 已实现：提醒选项关闭/15/30/45分钟，底层默认30、有效值下限15，保留大于等于15的自定义值，不自动启用或迁移用户偏好。C#独立后台循环从本地真实日期/时间及周规则生成提醒；新版单周（包括空课表）优先覆盖旧学期，历史浏览不抢占当前学期，不编造缺失时间。连堂短课间去重、跨午夜缓冲、注销/配置变化及实际原生展示前复核；过期超过2分钟的提醒不补发。
- APP-03 已实现：正式设置中提供自动更新开关/每日时间/间隔小时；原三栏保留，后台更新配置放系统栏，避免将课表栏堆高。原同步位置显示需连接/失败状态，不重复Toast。可见时30秒读取任务，成功后只读本地周/学期，保持历史浏览与选中周；隐藏/注销丢弃旧响应，不增加学校轮询。当前HTTP取消为逻辑取消与批次排空，不宣称已经发出的fetch被传输级撤回。
- APP-04 已实现：课表后台任务在来源请求前后、JW续跑与保存关键点复核会话/活动账号/配置；注销、切账号、关闭或修改自动刷新后旧任务不续跑、不回写。预期409不记录成刷新失败或成功。手动刷新不受后台开关限制；已发送请求不能撤回。
- APP-05 已实现：用户+数据根摘要命名互斥选主，次实例有限等候HTTP唤窗；退出先异步停止宿主再关闭窗口，互斥由拥有线程释放。修复宿主已停时Closing内同步重入Close的问题；不同数据根的多实例固定端口冲突不在本轮支持范围。

验证：后端13个相关切片定向217/217；前端8份定向218/218、类型检查及runtime构建通过；lint 0 errors/8既有warnings。命名互斥隔离进程验证11/11；真实WebView2隔离合成页面确认原生证明可通过、无证明调用403及宿主已停时窗口正常关闭，未用真实账号或学校网络。桌面最终构建/启动记录另见本节后续补充，不把编译通过等同系统通知可见或像素验收。

### 当前仍待验收（唯一当前待办摘要）

- Windows系统通知是否可见及免打扰/系统通知设置影响；托盘/静默启动、休眠与跨屏DPI的实际使用验收。提醒需要应用进程运行，不承诺应用退出后发送，也不补发已过提醒窗口的历史通知。
- 发布包、自启动及独立ProtocolHost/Linux/OCR组合按原任务单单独验收；既有学校表单登录继续沿用用户确认基线。
- 用户已取消认证寿命长测；NET-01～06已修复，不重新列为待开发，不扩展为未经证实的续租协议。

## 2026-10-16：NET-01～06局部修正已完成

用户确认现有“门户可达、外网不通才重认证”策略够用；本轮只修可靠性边界，不引入续租/心跳协议、不做寿命长测、不改UI或系统网络配置。

- NET-01：已覆盖此前普通登录form误判的复现。沿用现有AngleSharp依赖解析实际认证表单，忽略脚本中的模板字符串；拒绝已识别的认证页、异源落点、代理认证及未跟随的重定向。保留目标自身401/403/404等4xx可达判定，避免网页业务错误引发不必要重登。不是通用门户识别器，仍需稳定且无需登录的检测目标。
- NET-02：删除认证后重复检测的双重成功判据；唯一最终快照同时决定返回状态、operation_error和成功历史。认证接受但外网仍不可用时记录失败，不继续换账号后缀重发。
- NET-03：背景与手动入口明确分开；维护前后、每次发送凭据前、后缀重试前校验会话、活动后台账号、配置与凭据。注销、切账号、关保活、改门户/运营商/凭据均可阻止旧任务继续认证；调用方取消释放维护锁。关闭后台保活不禁止显式手动连接。已经发出的请求不能撤回，变化后停止后续探测/重试并不记录陈旧成功。
- NET-04：NetworkHttpTransport默认UseProxy=false、AllowAutoRedirect=false，DI实际使用该策略；不更改系统路由、Mihomo/TUN、网卡/IP或热点，不声称能绕开VPN系统路由或服务器权限。
- NET-05：同一后台服务内网络/课表用各自串行循环并行运行，慢课表不能占住网络循环，宿主停止同时取消两者，无新调度框架。
- NET-06：调度比较完整网络配置和选中凭据文件的变更标记，下一次网络检查时提前执行，不等待旧的分钟间隔。正常空闲检查约5秒；执行中的操作仍受逐步校验/超时约束，不承诺即时终止。凭据修订号只读文件元数据，不为每轮检查解密密码；真正认证前另核对当前凭据。预期配置取消不标为网络错误，并在下个检查重新评估。

验证：网络/维护/命令/课表调度/后台独立性/认证协议定向69/69，本轮全部可靠性测试为临时根、合成账号、假HTTP/命令；阻塞课表实测后台任务仍可连续运行并可取消。未做真实断网、强制真实认证、全量solution或浏览器UI测试。

DesktopHost主输出重建0警告/0错误并重启一次，PID47144、窗口可见；加载的Xke.Core.dll位于本次DesktopHost输出。health=ok，前端build仍dfbb441a96edceb7且原JS/CSS HTTP200，证明前端资源未替换。Core SHA256 D38CA497257ADDEF64C14DC7A2686616DA352C7BB4635B8786BBBE7FB3806E52（以运行时核对输出为准）；应用后台按既有偏好运行，未改账号数据，不stage/commit/push。

真实校园网长稳/休眠/切网仍不是上述测试已验收内容；用户已取消寿命长测，本轮不安排。下方历史NET待修记录保留审查当时语境，以本节为当前修复状态。


## 校园网保活可靠性核对（历史复现，已由2026-10-16修复覆盖）

范围为当前工作区NetworkService/NetworkMaintenanceScheduler/PersonalRuntimeWorker与现有网络测试，不把历史测试或单次真实连通当长稳验收。现有网络/调度/命令定向28/28通过；额外隔离控制台探针位于ignored artifacts/network-maintenance-audit，仅使用合成账号、假HTTP和假命令，无真实网络/认证请求。结果audit-results.json已复现：

- NET-01（优先修复）：外网检测只排除配置门户host及Dr.COM/注销页特征，其他HTTP<500响应可能被视为已联网。假200登录form返回internet_connected=true、loginRequests=0，保活被跳过。默认HTTPS能排除部分非受信劫持，但不等同响应语义正确；本轮为假网络边界复现，不宣称真实校园网页面已触发。
- NET-02（优先修复）：认证后第一次外网检测成功、随后完整状态复查失败时，NetworkService仍返回“外网访问已恢复”、operation_error空，historySuccess=true，但最终internet_connected=false。需统一最终判定来源，避免假成功和错误的下次重试安排。
- NET-03（优先修复）：MaintainCore只在开始读取会话/配置；探针在检测挂起期间删除该会话并关闭保活，释放后仍发送一次认证。需会话失效/后台配置变更的取消与认证前校验，不能简单禁止HeartbeatEnabled=false下的手动连接。
- NET-04：默认DI为new HttpClient()，未显式UseProxy=false，与手册“直连不使用系统代理”不符。不能当作已完成；系统路由/VPN与服务器IP权限仍是独立边界。
- NET-05：网络和课表刷新由同一后台worker串行await，慢课表请求会拖住下次保活；网络间隔为任务完成后再加配置分钟数，不是硬周期或即时断线恢复。
- NET-06：调度只以sessionId/interval判断配置变化；provider/门户/凭据变化不会主动提前已排定的下一次检查。假时钟推进8小时后仅补跑一次，符合不积压原则；provider变更时尚未到期则不执行。此为时钟模拟，不是真实Windows休眠唤醒验收。

正确边界继续保留：本地后台运行而非依赖网页、维护串行锁、操作逐次超时、显式门户拒绝/IP限制、DPAPI网络凭据及失败不忙循环。仍缺真实校园网长时、实机休眠/切网/代理组合验收。本轮只审查/复现及修正文档，不修改生产实现/UI，不重启桌面、不stage/commit/push。

2026-10-11追加JW-19：SwissWeek按真实结束时间过滤全部已结束课程，默认hide导致历史课表及周日当前周空白。数据本身存在；改默认dim，hide限制当前周，历史周始终渲染，全部隐藏用已结束状态区分。52项定向通过，课表文件未改，当前账号只更新显示偏好。

2026-10-11追加JW-18：XkeStudentStore直接反序列化旧开发加密业务文件，AuthService有凭据就固定走本地恢复，导致认证表单无法进入学校验证码。业务文件解析失败现视为不可用，读取不修改；无完整工作区bootstrap404，默认协议登录可重建，完整本地恢复行为保持。学校初始化失败仍回滚旧文件和凭据；未归类API错误不透传底层异常。Auth/PersonalRuntime24、AuthStorage14项通过，桌面重建及真实验证码入口确认，未代用户完成真实密码登录。

同日后续归并为JW-14至17：学期有保存但无读取、周快照无条件覆盖更新的学期、桌面与协议宿主重复转换、桌面快照客户端无法直接消费新协议服务。已对应补齐本地读取/按获取时间覆盖/共享转换/显式protocol客户端。协议消费使用真实本机HTTP连假学校宿主，验证GET周/学期与401后JW回退，未发送学校凭据到云接口。

实际源码核对发现并处理以下数据流问题，归并为JW-11至13：

1. `ScheduleService.NormalizeScheduleRows`旧MergeDuplicateScheduleRows合并不同地点/周次，覆盖parser保留信息。改为完整结构去重；后端测试覆盖不同room、同room不同weeks及真实重复副本，本地JSON复读保持三条安排。
2. `AuthService.SubmitProtocolCaptchaAsync`将登录账号与服务器学号强制相等。支持登录别名，StudentId使用服务器资料；凭据按原登录账号保存，初始失败回滚保持。
3. `JwScheduleContextParser.ParseHome`多学期选择器取第一项而忽略selected。改取选中项，避免单周落入错误学期目录。

另统一学校保护路径HTTP401为会话失效，由ProtocolHost/桌面既有错误链处理；不把所有HTTP错误当Cookie失效。后端相关69项通过；OCR Python HTTP5项及实际模型合成识别通过。ProtocolHost/React最终结果在任务板登记。

当前测试不证明学校真实密码登录、验证码准确率、Linux运行或Windows产品发布。下方为历史审查结论。

---

# 2026-10-07 当前产品范围

当前活动后端为个人本地运行时：登录、课表、设置、校园网及 Windows 常驻宿主。公告、遥测、反馈、云策略、选课和自动更新已退出装配，历史资料保留但不参与当前决策。最新执行状态见 [个人运行时](../development-docs/personal-runtime/README.md)。

---

以下是历史阶段记录；与当前产品范围冲突时，以个人运行时代码及开发文档为准。

# XKE.DotNet8 重构审查合并报告

> 更新时间：2026-04-24  
> 范围：`Z:/XKE/XKE.DotNet8/`  
> 来源：合并 `18_重构问题.md`（归档于 `Z:/XKE/reference-archive/notes/legacy-notes/legacy-18-review-issue-memo.md`）与历史修订后的当前执行版。  
> 使用规则：本文件是后续修复排序的唯一主报告；`18_重构问题.md` 与 `19_重构审查收敛报告.md`（分别归档于 `Z:/XKE/reference-archive/notes/legacy-notes/legacy-18-review-issue-memo.md`、`Z:/XKE/reference-archive/notes/legacy-notes/legacy-19-convergence-memo.md`）只作为历史材料，不再作为独立决策依据。  

## 0. 名词边界（统一）

- 移动教务（Mobile JW）：作为当前唯一主链路数据源，覆盖身份、课表、首登初始化、定期维护。
- 教务系统（Legacy JW）：指传统教务站点与验证码/OCR/Cookie 场景，当前仅保留未来边界，**不进入主登录和本次恢复链路**。
- 传统教务功能若出现术语歧义，以“教务系统”为准；涉及课表/身份同步的实际优先级以“移动教务”为准。

## 1. 总体判断

当前重构方向总体正确，已经形成 `Contracts / Core / Storage / Security / WebHost / DesktopHost` 的基本边界，并且保持了按垂直切片推进的路线。当前推进重心已经从隐私落盘增强切到后端长期结构收敛。主要风险不是“能不能构建运行”，而是活跃链路边界和长期维护结构还没有完全固化：

- 首登初始化原子性还有异常缺口。
- 后端测试和 WebHost 支持层还需要继续去重。
- Legacy 兼容层必须保持 reference-only，不能重新接回 Python runtime。
- 部分业务边界条件还依赖隐含约定，后续容易漂移。
- `WebHost` 仍有继续膨胀成新中心模块的风险；前端暂不拆分，只保留兼容。

## 2. P1 问题

### P1-1：首登回滚不完整

- 位置：`src/Xke.Core/Auth/AuthService.cs:404-438`
- 影响：首登初始化已经写入 profile、preferences、credentials、session，并把 session 标记为 connected；但后续只对 `AuthException` 和 `ScheduleException` 做回滚。
- 风险：如果课表初始化期间发生 `OperationCanceledException`、`IOException`、`JsonException` 或其它未归一化异常，可能留下半初始化本地状态，破坏“首登必须完整落库才算成功”的主线。
- 建议：把远端初始化后的所有失败都纳入统一回滚边界。可以保留 cancellation 的语义，但仍应清理本次 session、connected token、临时凭据和本次学生目录变更。
- 测试建议：补一个 fake schedule/client 抛普通异常或取消异常的 WebHost 测试，断言 credentials、sessions、student directory 都没有残留。

### P1-2：认证历史仍落明文账号

- 位置：`src/Xke.Core/Network/NetworkService.cs:486-491`
- 当前裁决：暂缓。
- 原因：本轮默认用户电脑处于相对可信环境，隐私落盘不作为核心优先级。该问题保留为后续隐私增强项，不阻塞当前后端结构收敛。
- 后续建议：如恢复隐私增强切片，再统一清洗 attempts 或重做 network auth history schema。

## 3. P2 问题

### P2-1：反馈云上传补传策略仍未完成

- 位置：`src/Xke.Core/Feedback/HttpFeedbackTransport.cs`
- 当前状态：反馈 outbox 已使用本地加密文件保存；默认 transport 会优先上传云函数，失败后写入本地加密 outbox。
- 剩余风险：本轮没有后台常驻补传器；如果云端短时不可用，反馈会保留在本地，需要后续切片决定是否扫描 outbox 并补传。
- 后续建议：如需要“最终一定到云端”，再设计 outbox 枚举、重试节流、成功删除和用户隐私保留周期。

### P2-2：当前周使用 UTC 日期

- 位置：`src/Xke.Storage/XkeScheduleStore.cs:103-111`
- 影响：`FindScheduleWeekLabelForDate` 默认使用 `DateTime.UtcNow` 推断当前日期。
- 风险：桌面应用面向本地用户，香港/中国时区凌晨会比 UTC 早一天，周切换边界可能选到上一周课表。
- 建议：使用本地日期，或引入可注入时钟，让测试能覆盖本地时区边界。
- 测试建议：补周日/周一凌晨边界测试，明确当前周推断基于本地日期。

### P2-3：LegacyCompat 编译边界需要保持明确

- 位置：`XKE.DotNet8.sln:6-27`
- 当前裁决：保留目录，不加入活跃 solution 门禁。
- 原因：`Xke.LegacyCompat` 是后续前端重构和迁移参考层，不应重新扩大到 active runtime。
- 测试建议：保留 active `.NET` 数据根不读取 `XKE_DATA_DIR` / Python legacy `data/` 的隔离测试。

### P2-4：运营商凭据清空语义不清

- 位置：`src/Xke.Core/Network/NetworkService.cs:540-568`
- 影响：空 account 会保留旧 account，空 password 或任何包含 `*` 的 password 都会触发保留逻辑。
- 风险：用户无法可靠清空某个运营商凭据，也无法设置包含星号的真实密码。前端展示用 masked password 与后端保存语义耦合过紧。
- 建议：把请求语义改成显式字段，例如 `password_unchanged`、`clear_provider`，或每个 provider 使用独立 patch 模型。不要用 `*` 推断是否保留密码。
- 测试建议：补三类测试：保留旧密码、清空 provider、设置包含 `*` 的新密码。

## 4. 模块化和质量观察

- `XkeWebHostApplication` 已经用 partial endpoint 文件拆开，但服务注册、通用错误处理、session 解析、blocked policy 仍集中在同一个静态入口里。
- `Xke.Contracts` 同时承载 HTTP DTO 和本地持久化 document。短期减少映射成本，但长期会让 API 契约和存储 schema 互相牵制。
- `NetworkService`、`AuthService`、`NoticeCompatibility`、`NativeJwScheduleClient` 文件偏大，后续新增功能前应先拆稳定边界，避免继续形成新巨石。
- Telemetry/Feedback 已进入云上传链路；后台补传、管理侧查询权限和更细隐私保留周期后续单独推进。
- 前端本轮不做结构优化，只要求后端保留现有 envelope 和字段兼容，方便后续前端整体重构。

## 5. 对 18 号报告的合并裁决

18 号报告覆盖面较广，但部分优先级偏重。合并后的执行口径如下：

- `XkeDpapi / NoticeDpapi` 的 P/Invoke 重复属于长期维护债，不是当前 P0。两个保护域使用不同 entropy，后续可抽公共底层 helper，但不能破坏现有已加密数据兼容。
- `AuthEndpoints` 重复 try/catch、`XkeWebHostApplication` 偏大、缺少 `Xke.Core.Tests` 都成立，但优先级低于首登原子性、敏感数据落盘和真实数据链路闭环。
- `global.json` 使用 SDK 10 与 `net8.0/net8.0-windows` 目标并存，是当前工具链兼容策略，不作为缺陷处理；如后续需要团队/CI 固化，再单独调整。
- `StoredCredentialDocument.Password` 不是普通明文落盘，因为凭据文件经 DPAPI 保护；内存中明文生命周期可以后续优化，但不应误判为当前最高风险。
- 移动教务 AES-ECB 是外部协议约束，不能自行替换算法；应保留边界注释，说明这是提交加密字段的兼容实现。
- `XkeDataRoot` 每次创建轻量 store 实例暂不处理，当前收益低于复杂度。
- `Dictionary<string, object?>` 应逐步限制在 Notice、Legacy、真实移动教务格式兼容等动态边界；新业务 DTO 优先强类型，但不做机械大替换。
- `XkeStudentStore.WriteJson` 非原子写入是有效长期风险，可在完成 P1/P2 行为边界后统一处理。

## 6. 已验证门禁

本次审查后已验证：

```text
dotnet build XKE.DotNet8.sln --nologo
结果：通过，0 warning / 0 error

dotnet test XKE.DotNet8.sln --no-build
结果：通过，71 tests

tools/frontend-smoke.ps1
结果：通过，26 tests

tools/schedule-smoke.ps1
结果：通过，15 tests

dotnet build src/Xke.LegacyCompat/Xke.LegacyCompat.csproj --nologo
结果：通过，0 warning / 0 error
```

## 7. 建议处理顺序

1. 修 `AuthService` 首登回滚边界，并补异常回滚测试。
2. 修当前周本地日期推断。
3. 固化 active `.NET` 数据根与 Legacy reference-only 的隔离测试和注释。
4. 扩展后端测试支持层，减少 WebHost 测试启动样板重复。
5. 启动 WebHost 注册、错误处理、session/context 加载、blocked policy 的长期结构拆分。
6. 建立 `Xke.Core.Tests`，把 normalize/parser/decision 纯逻辑逐步迁出 WebHost 集成测试。
7. Network 认证历史脱敏、Feedback 后台补传、运营商凭据清空语义作为后续隐私/设置增强切片处理。

## 8. 收束方向图（用于后续开发排队）

### 8.1 当前主线（不改变现有 API）
- 首先保证数据主链路稳定：Auth 首登/本地恢复、Schedule 初始化与恢复、Notice 兼容、Desktop settings、Network/Telemetry/Feedback 本地与云端兜底闭环。
- 优化后端结构：集中排查活跃链路边界、测试重复、错误面和 session/blocked 边界，不新增前端字段。
- 将文档、测试、注释全部按照“移动教务主线 + 教务系统后置”统一标注。

### 8.2 下一步开发优先级
- P0：`AuthService` 首登异常回滚测试与实现、`NetworkService` 运营商清空语义与文档澄清。
- P1：后端测试支持层统一、`Xke.Core.Tests` 建立、`XkeWebHostApplication` 结构性拆分。
- P2：`Course` 维持 deferred 入口，补齐课程框架说明与占位文档；不回填验证码/抢课逻辑。
- P3：`Mobile JW` 与前端数据契约对齐：若真实格式差异继续出现，先补解析器与 fixtures，再看需要。
- P4：在确认稳定后，再启用任何云端或隐私落盘加固切片。

### 8.3 术语与边界约束（跨文档一致）
- 所有文档和注释默认先写“移动教务”，只有明确与验证码/Cookie/传统课程抢课相关时才写“教务系统”。
- 任何实现计划若要提到“登录恢复”，必须区分：
  - 移动教务：可信本地状态恢复 + 可选重连；
  - 教务系统：人工 challenge / OCR / 抢课链路，属于后置切片。
- 本文件更新时保持 `../roadmap/01_phase_summary.md`、`./00_current_issues.md`、`../future-plans/00_traditional_jw_manual_login.md` 的指向一致，不产生并行口径。


## 2026-10-07 提交前核对

- 当前生产 Auth/Schedule 使用 JW 表单协议，旧移动路径只作为兼容测试入口。当前公共测试宿主仍显式使用旧移动路径，默认协议入口新增了无远程调用的参数/challenge 错误验收；首登成功和回滚的完整 API 验证仍需后续补齐，不能由兼容测试通过代替。
- 本轮修正 `LocalOnly` 只禁公告/更新却仍装配云上传 transport 的缺口；明确本地模式采用本地 telemetry 与 feedback transport，并增加装配回归测试。
- 登录 challenge 过期提交时释放其协议客户端，避免从待处理字典移除后遗留资源。
- 前端由用户明确选择本地保留，不纳入当前提交。后端仓库可构建，桌面资源需本地提供；该交付边界写入 README。
- 校园网后台调度仍依赖前端；生产默认 JW 首登失败/取消与重复初始化的全路径验收属于下一阶段，当前提交不宣称这些已完成。


## 2026-10-08 JW 提取研究归并

本轮新证据与上一轮来源审查统一记录于 [04_jw_protocol_research.md](../development-docs/personal-runtime/04_jw_protocol_research.md)，使用 JW-01 至 JW-08 标识，避免复述旧移动阶段结论为当前事实。个人资料空值过滤导致字段错位、select 项丢失、旧单元格第二门课程丢失与星期列位置假设为本轮额外复现；学期、周次、空周、时间问题沿用同一问题体系。现有相关测试 9/9，通过不能覆盖这些边界。真实校历/作息/课程 ID/调停课接口仍无证据；研究完成不等于修复或真实链路验收。


## 2026-10-08 浏览器观察与代码证据归并

在用户的真实教务会话中观察接口/结构，并以临时 C# 假数据程序验证：JW-07 补充200失效 JSON 漏识别；新增 JW-09 单格标签/值的学籍头部误配、JW-10 font title 字段名被读成数据和多课程遗漏。最新复现/接口范围见 [研究记录第10节](../development-docs/personal-runtime/04_jw_protocol_research.md#10-真实页面与请求观察2026-10-08)。原 JW-02/select 与 JW-04/其它表头变体仍为假输入边界，不把它们声称为真实学校现状。没有生产修复或纯 HTTP 新来源验收。


## 2026-10-08 第一轮实现验证补充

研究中的 JW-01至JW-10对应修复已实现：DOM配对、按表头星期、同格多课和显示副本、200失效JSON/非课表、不可查询周404及服务器大节时间；附带修复显式多节次列表只取末两节的问题。新学期GET API和资料字段全链路回归、普通JSON/新CSV及独立快照已验证。用户明确开发状态无需旧数据兼容，生产不保留旧加密业务读取或假title-value布局解析。验证计数与范围以personal-runtime最终验收记录为准，不将Cookie只读访问当作完整登录/发布验收。
