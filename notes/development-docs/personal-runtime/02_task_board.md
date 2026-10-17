# 当前任务板：第一版对接与云协议版（2026-10-10）

## 2026-10-18 V1.0.0发布验收基线

本轮完成旧移动路径退役、账号昵称/会话隔离、历史学期补齐与设置/动效细节收口。正式Actions已接入PR/main/版本标签验证，发布前草稿资产下载回验，具体运行结果以对应GitHub Actions和Release为准。

本地后端502项通过，固定端口用例因真实桌面运行留给干净CI执行；前端369项通过，另用显式合成宿主完成1项真实HTTP集成。类型检查/零警告lint/依赖审计通过，80项发布内容门禁通过，正式runtime候选六资产校验通过。集成宿主补齐合成原生证明，同时验证无证明的桌面写入仍403，不放宽生产策略。没有真实账号/缓存/私有配置随源码或安装包发布；窗口/真实学校和跨版本升级仍保留原验收边界。

## 2026-10-17 产品名称统一

用户确认对外显示名称统一为XKE。窗口标题、托盘/通知名称、程序集产品信息、SVG可访问名称、浏览器标题和README已同步；旧更新项目文档统一品牌文字，并明确历史方案不再执行、当前以08发布手册为准。技术程序集名、PackageId=XKE、GitHub来源、原数据根、账号缓存和更新渠道保持不变，不引入数据迁移。Git远端本地别名改为XKE，main的既有跟踪关系随Git自动更新；当前开发分支不改向、不合并、不推送。

DesktopHost定向14/14、前端design-sync33/33、类型检查与runtime构建通过。按用户指定信息“调整了部分信息”精确范围本地提交；新候选包和桌面重启证据随后保存于ignored artifacts/repository-preparation/20261017/branding，原有六项未提交材料不夹带。本次改名不是新的公开版本或GitHub发布。

## 2026-10-17 用户授权的分批本地提交

用户后续明确授权分批commit并分散真实执行时间；仅重排暂存区，不恢复/清理工作文件，不设置GIT_AUTHOR_DATE/GIT_COMMITTER_DATE或伪造开发历史。已按清单完成前四批，第五批提交本次必要现状说明与授权边界：

- `95ec2b1`：chore(repo): exclude local archives and runtime state，实际时间2026-10-17T20:01:52+08:00。
- `77304fe`：refactor(runtime): consolidate the personal desktop baseline，实际时间2026-10-17T20:05:12+08:00。
- `9504356`：feat(frontend): integrate the React desktop interface，实际时间2026-10-17T20:07:51+08:00。
- `945cfbf`：build(release): add verified Windows packaging，实际时间2026-10-17T20:09:36+08:00。

本条随第五批`docs: align the current product and release boundaries`归档；精确提交ID/实际时间保存在本地ignored commit-execution.json，不预写本提交自身哈希。六项个人技能或历史审查/敏感索引补记按计划留在工作区，未把所有修改一并提交。没有push/tag/Release，也不修改原运行数据或重启桌面。

本轮补核前端类型检查、lint及4份定向测试122/122；lint无错误、10条既有警告，未为清理仓库扩大重构。发布门禁79项合成通过。仅清理新跟踪源码中的多余EOF空行；THIRD-PARTY-NOTICES原文/字节保留，许可证原有行尾空白单独不作为提交失败。前置独立批次构建证据沿用上条，不新增实际安装/线上升级验收声明。

## 2026-10-17 仓库整理与提交准备完成

只做仓库边界和当前说明同步，不实施上轮建议的业务精简。忽略本地编辑器状态、导入设计包、校园网运行配置、DPAPI文件和环境文件；保留可提交的配置示例、依赖锁与dotnet工具声明。根目录四份已适配设计ZIP及一份SHA256校验文件按用户后续授权删除；其他私有归档、运行数据、构建/发布产物未清理。当前说明纠正React仍mock、提醒未交付、全部更新退役等过期表述；已有审计脚本改为实际frontend/src/tests/build及TypeScript扫描，不再声明已退役Notice存储仍活动。

当前394项变更分类为：规则1、完整运行时基线271、React前端89、发布工具5、必要文档22；另6项个人技能或既有审查/敏感索引补记延后审阅，不默认提交。精确范围及三个重命名的旧路径见ignored artifacts/repository-preparation/20261017/commit-batches.json和各批次paths/pathspec。实施步骤顶部记录依赖关系；119项既有暂存内容保持原样，没有stage/commit/push/tag/Release。压缩包删除原本属于未跟踪本地文件，不形成源码删除提交。

验证：仅第1/2批投影到HEAD的独立源码快照，解决方案构建0警告/0错误；扩展到前端/发布批次的另一快照，React类型检查、lint、runtime构建通过，构建标记08afb9ccd6994761与当前产品一致；79项合成发布门禁通过。快照复用已安装npm依赖，不是全新系统依赖安装验收。快照位于ignored artifacts，lint首次受祖先忽略规则影响未选中文件，随后显式指定源码范围并禁用忽略后通过；不修改产品lint配置。活动导航链接、项目引用、公开候选常见敏感标记及diff空白检查通过，不能把标记扫描当完整保密审计。未读取真实账号/私有配置或运行全量测试、浏览器/学校/安装验证；未改UI或重启桌面。

项目自身许可证仍未指定，不擅自创建；THIRD-PARTY-NOTICES仅维护依赖许可。正式发布前需另行确认源码许可证及提交/推送/发布授权。

## 2026-10-17 移除旧独立启动画面

用户确认当前启动性能足够，撤掉旧设计的独立启动页。MainWindow移除LoadingOverlay/标题/加载说明/不定进度条及BuildLoadingMessage，初始化仅管理浏览器/错误界面可见性；导航完成直接显示既有前端，不新增整页动画或固定延时。原生背景与WebView2首文档底色统一当前one版#f2f1ec，保留错误重试、原生退出、静默启动/托盘恢复、真实账号恢复与认证边界。前端源码未改。

DesktopHost构建0警告/0错误、新候选严格发布校验通过；本轮为原生展示移除，不新增镜像实现的测试、不重跑无关前后端测试或浏览器UI检查。候选artifacts/candidates/1.0.0-direct-startup-20261017，解压至同名desktop-release-preview新目录并重启一次；DesktopHost PID14428、窗口就绪/响应，health app_version=1.0.0/update_supported=true/frontend_build=08afb9ccd6994761、web_root指向新包current/web，JS/CSS HTTP200。DesktopHost.dll SHA256=C30BB919B59389DB8BE1CAC23C88CF26E3FF1FBD741ACD6AEA320B3EFCA1A42F。沿用当前runtime前端（本轮哈希不变）及新DesktopHost构建，保留原账号/数据根；未stage/commit/push或公开发布。

## 2026-10-17 课时完整区域交互

用户要求设置底部课时全部区域可点击。SemesterField改为一个覆盖完整section的原生按钮，标题、点阵和区域内留白均能触发既有汇聚/还原；方点统一作为视觉元素，移除红点小按钮及父层点击处理，避免冒泡双触发。保留Enter/Space、Escape、自动还原、减少动效和数据量不足不汇聚的既有边界，不改点阵排版或汇聚目标。

类型检查、semester-progress/app两份定向测试84/84、runtime构建及新候选包校验通过；未做浏览器UI检查或全量后端测试。新候选artifacts/candidates/1.0.0-semester-hit-area-20261017；桌面从同名desktop-release-preview目录重启一次，PID55868、窗口就绪/响应，health app_version=1.0.0/update_supported=true/frontend_build=08afb9ccd6994761，web_root指向新包current/web，主JS/CSS HTTP200。原数据根与账号保留；未stage/commit/push或公开发布。

## 2026-10-17 无更新结果原位反馈与配色

用户确认取消无更新的角落提示，并要求结果有颜色区分、去掉勾号。手动检查依次在原按钮显示检查更新→检查中→暂无更新，两秒后恢复。结果使用浅青绿底#dbece8/深青绿字#255f55，仅纯文字；标题应用更新、版本信息、自动更新开关与下载中的朱红百分比保持。onNoUpdate只由有效手动check的up_to_date触发，轮询静默；隐藏/关闭/新动作清除结果与计时，组件卸载清理。显式preview复用产品组件，600ms模拟检查只在隔离入口。

类型检查、三份定向测试115/115、runtime构建通过。预览核对检查前/显示结果/恢复后面板和按钮位置尺寸一致（内容458.67×350px、按钮112×40px），结果无SVG、无角落提示。候选包artifacts/candidates/1.0.0-update-check-inline-20261017严格发布校验通过；解压到同名desktop-release-preview新目录并重启一次。DesktopHost PID49724、窗口就绪/响应；health app_version=1.0.0、update_supported=true、frontend_build=47fb16dad2b93b69，web_root为新候选current/web，JS/CSS HTTP200。保留原数据根和账号，未公开发布或stage/commit/push；真实GitHub跨版本更新未在本轮验收。

## 2026-10-17 下载动作视觉强化

用户要求下载中更显眼。在同一112×40px动作按钮内使用朱红底/白字、20px粗体等宽百分比，新增16px下载箭头和1.2秒轻微上下运动；没有新增横条、占位、说明文字或扩大窗口。非下载状态恢复原按钮，下载时禁点但保持完全不透明；prefers-reduced-motion时箭头静止。实际预览三态同460×351.33px、动作同112×40位置；减少动效检查animationName=none。

3份定向114/114、类型/runtime构建与新候选包严格校验通过；新包artifacts/candidates/1.0.0-update-emphasis-20261017。桌面正式便携预览重启一次，PID52296、窗口可见/响应、builda3237c53406208f8、JS/CSS HTTP200。沿用原数据根/账号，不发起真实下载/学校认证、不上传GitHub、不stage/commit/push；旧候选保留。交互演示已切下载中，正式界面同组件但真实状态不伪造。

## 2026-10-17 更新状态复用动作按钮（最新反馈，覆盖预留区方案）

用户拒绝预留进度空间，要求取消多余空白与独立进度条，复用已有组件。本轮删除one-update-feedback/track/download及相关样式；面板由真实header/version/auto/actions四行自然组成，不为未出现内容设占位。右下动作始终112×40px：检查更新/下载更新→下载百分比→重启更新，下载时黑底保持不透明、禁点并以内嵌progressbar语义播报数值，无可见横条。必要错误及设置保存失败复用不参与布局的右下通知，暂无可用更新仍只由显式检查触发一次。

实际预览四态位置/尺寸完全一致：460×351.33 CSS px（含边框），x410/y224.33；动作按钮x729.33/y507、112×40，各态均无track/feedback DOM。比上一版缩短66px，不靠空白撑高。定向3份114/114、类型与runtime构建通过。新候选artifacts/candidates/1.0.0-update-inline-20261017严格校验通过，旧候选保留。桌面正式便携预览重启一次，PID53344、窗口可见/响应、health version1.0.0/update_supported=true/build9c8be173f5c13b08、包内JS/CSS HTTP200。原数据根与账号不搬移，无强制认证、不上传GitHub、不stage/commit/push。演示继续复用同产品组件，模拟新版本仅在独立preview页面。

## 2026-10-17 更新面板固定尺寸与检查结果提示

用户要求所有状态同尺寸、不要在面板内显示“暂无可用更新”。AppUpdateContent统一416px内容高度与固定行轨道，进度/不可用/必要错误共用52px反馈区，自动开关/页脚位置不被条件文本推移。长错误仅在反馈区滚动。up_to_date不增文字；只有手动check成功且结果up_to_date时，observer发出一次右下角2.2秒通知；轮询、下载/安装、隐藏或取消后的旧结果不通知。提示放在dialog顶层内部fixed定位，避免原全局toast被原生modal遮挡，同时不参与弹窗尺寸。

独立演示增加“当前版本”场景，可点击检查观察右下提示；仍不请求GitHub/学校或替换程序。实际浏览器四态尺寸/位置一致：460×417.33 CSS px（含边框，内容416），x410/y191.33；提示位于窗口右下x1134/y726.67，非卡片内。定向3份114/114、类型/runtime构建通过，未重复后端测试。

新候选包artifacts/candidates/1.0.0-update-fixed-20261017校验通过，正式便携预览已重启一次，PID29228、窗口就绪/响应，health app_version1.0.0/update_supported=true/build17f9eccfbb56423d、JS/CSS HTTP200，包内web与当前runtime一致。账号和数据根保留，不伪造真实更新，不覆盖旧候选、不上传GitHub或stage/commit/push。

## 2026-10-17 更新面板文字与层级收敛（用户最新反馈）

用户不接受“合成预览”“已就绪”及“可立即重启安装”等解释段落。本轮AppUpdateContent标题随真实状态改为应用更新/发现新版本/正在下载/下载完成/正在重启，单独突出新版本号、当前版本作次要信息，不重复状态标题。不再显示预览类型标签、后台解释或退出安装说明；保留必要不可用/错误反馈、自动更新开关与下载更新/重启更新动作。安装逻辑不变，不伪造真实可用版本。

独立frontend/update-preview.html仅在显式preview模式运行，复用同一个产品组件；场景选择位于画布底部，产品卡片中不放演示标识。1.0.1为合成状态，无更新网络或真实替换；正式runtime入口不会包含该预览entry，mockApi原默认unsupported保持。预览可点击下载→进度→完成，点重启仅模拟回当前版本。

3份定向112/112、类型检查/runtime构建通过，实际预览检查三状态排版。新候选包存artifacts/candidates/1.0.0-update-ui-20261017，旧artifacts/releases/1.0.0保留未覆盖（旧候选不再是最新UI）；新包严格发布校验通过。当前桌面由新包解压启动一次，PID58340、窗口就绪/响应，health app_version=1.0.0/update_supported=true/build=a7f3bef38ba4fa60、包内JS/CSS HTTP200。沿用原账号数据根、不更改真实更新源或发布GitHub，不stage/commit/push。预览入口http://127.0.0.1:5182/update-preview.html?v=settings&scene=available，服务本轮保留供查看。

## 2026-10-17 正式界面查看与开发所见即所得

用户要求具体查看正式版界面。已核对开发frontend/dist与V1.0.0便携包的frontend-build.json、主JS及CSS逐字节一致（build=0c4b47cdf0a5fa39），没有并行的正式/开发视觉。更新面板差异来自bin/Debug无Velopack布局，update_supported=false，而非旧资源或不同UI。

本轮不修改UI、不重新构建或公开发布。将已有正式便携包解压到artifacts/desktop-release-preview/1.0.0，停止已核对路径的开发进程44236，运行根启动器XKE.exe一次。实际DesktopHost PID28380位于该目录current，窗口可见/响应；health app_version=1.0.0、update_supported=true、web_root明确指向包内current/web、build一致，主JS/CSS HTTP200。沿用默认原数据根，无XKE_DOTNET_DATA_DIR/XKE_WEB_ROOT覆盖，不读取或搬移账号缓存、不改自动更新偏好；未强制学校认证。由于该查看目录在仓库内，既有可选私有开发配置仍可能被正常加载，本次只确认包内前端和正式更新能力，不将其重复宣称为脱离仓库验收（上轮独立临时根验收另有证据）。

查看入口为设置→系统标题右侧v1.0.0。后续用于查看正式效果时应运行受管理便携包，不能将bin/Debug的unsupported状态伪装成安装可用；保持同一runtime前端与真实能力状态。当前GitHub未发布、无新版本时不会凭空显示下载/安装按钮。

## 2026-10-17 V1.0.0 候选包与自动更新最终验证

- 公开仓库Gust-feng/XKE已确认，GitHub无Release时不伪造更新。主版本1.0.0；Velopack1.2.161官方组件，固定win稳定渠道/XKE包身份。系统标题版本面板提供项目页、检查、自动下载、进度与立即重启；关闭到托盘不安装，关闭自动模式不自动应用已下载包。旧云控制/遥测/Updater不恢复。
- 真实SDK Windows测试13/13；WebHost相关83项最终通过（首次其中固定38443端口用例被正在运行的旧桌面占用，停旧桌面后该项单独复跑1/1；不是代码回归）；前端7份214/214、类型与runtime构建通过。发布门禁79项纯合成通过，增加官方setup.ico及第三方许可通知的精准允许/缺失拒绝，不放开通用文件复制。
- 本地已生成artifacts/releases/1.0.0的Setup、Portable、full.nupkg、releases.win.json、assets.win.json、RELEASES六个已验证资产；自包含三runtime8.0.32，官方WebView2 bootstrap。THIRD-PARTY-NOTICES.txt包含18份依赖许可/通知，非项目自身许可证。当前未配置Authenticode签名，不能声称签名发布。
- 独立PackId合成便携程序真实完成1.0.0→1.0.1下载校验、退出替换、重启，外部合成数据保留；升级后DLL哈希与目标1.0.1完全一致。不是实际XKE/GitHub线上跨版本验收。
- 正式候选便携包解压至仓库外临时目录，使用新临时数据根、禁用自动检查并显式空来源，启动成功：app_version=1.0.0、update_supported=true、sessions=0、frontend_build=0c4b47cdf0a5fa39。不借用开发web/配置，不读真实账号、不改真实注册表；正常关闭通过。缺WebView2的新系统安装仍未实测。
- 实际开发桌面仅在最终阶段停止/重启一次，沿用原默认数据根与缓存；PID44236、窗口可见/响应、health=ok、app_version=1.0.0、build=0c4b47cdf0a5fa39、JS/CSS HTTP200。bin/Debug的update_supported=false是预期保护，不能自我覆盖；正式安装/便携包已确认true。使用正式包前应真正退出开发版，避免单实例唤回旧进程。
- 证据与六资产SHA256见ignored artifacts/v1.0.0-verification.json。当前源码与公开main不一致，且既有约定禁止擅自stage/commit/push；本轮没有GitHub上传、创建tag/Release或源码提交。公开发布仍须用户明确允许先审核、提交/推送正确源码，再让v1.0.0对应该commit。

用户操作与发布步骤见08_release_and_updates.md。发布后的数据模型变更要评估迁移/升级门禁，不能继续把开发期“无旧数据兼容”当作已发布用户数据可丢弃的许可；本轮不预造迁移框架。

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

## 2026-10-17：本轮最终验证与桌面启动

本轮实现始于10月16日，最终验证/启动于10月17日完成。后端相关定向217/217、前端8份定向218/218、类型检查/runtime构建通过；DesktopHost主输出0警告/0错误，lint仍为0 errors/8既有warnings。原生WebView2合成页面验证：自身请求正确携带能力，空工作区bootstrap到达原404、desktop/settings=200；普通HTTP无证明=403，已停宿主时正常关闭，修复Closing同步重入。互斥隔离验证11/11。

启动前本项目桌面未在运行；从当前主输出启动一次PID11524，窗口可见/响应正常，health.status=ok，frontend_build=e6e311843b636148与磁盘runtime标记一致；index-CF3IF43p.js、index-Ce8RsIE5.css均HTTP200。沿用原默认数据根及账号缓存，未更改用户提醒开关、网络配置或自启动注册；新版后台按既有偏好运行，未做强制真实认证/通知可见性/像素验收。没有stage/commit/push。

Core SHA256：2BB23434DB98B3866E89817F2197610A3C8D009BA520DDA5A7DEB8F0E632F471；WebHost SHA256：A95A9A4A37A6A0C26C1C30AE68BA9F5C0F4DD00ECC540AE41AA4C6C4EEBFC61F（本次主输出）。当前剩余验收仍按下方APP-01～05摘要，不重开已修缺陷。

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


## 2026-10-16：认证寿命实验准备受阻，设置已恢复

- [x] 经用户授权暂停当前账号自动重认证，后台停止已读回；未改IP/路由/热点。发现手机共享/Mihomo改变默认出口，浏览器旧成功页不足以定基线。
- [x] 绑定有线网卡的单次只读门户探测得到登录页，未确认在线会话，未执行登录或注销，未开始寿命计时。已恢复原自动维护true并确认后台enabled=true，没有遗留监控进程。
- [ ] 取得用户对一次明确经校园网的凭据认证授权后，建立确定成功起点再开展有限时实验。不能由当前结果得出固定TTL或24小时有效期。细节见05手册。


## 2026-10-16：认证生命周期探测准备

- [x] 已区分浏览器认证资料缓存86400秒与网关会话寿命，不能据此承诺24小时在线。本轮193秒被动观察无HTTP/WS，静态在线页不是会话持续有效的证明；没有发现可直接采用的服务器会话TTL字段。
- [ ] 获取允许自然掉线的测试条件，排除自动重认证及其他流量后，分别验证固定会话时限和空闲超时；不能让主动探测刷新空闲计时而误判。本轮未改变真实联网或自动维护设置，未启动长期监控/修改生产代码。


## 2026-10-16：已登录门户实际浏览器观察

- [x] 按用户授权使用Chrome扩展只读解析在线页和已加载脚本，69秒计时窗口未观察到HTTP/WebSocket周期请求；当前访客及扫码计时器未启用，已区分共享脚本中扫码startKeepAlive与真正在线会话续期。结束仍已登录，未注销/重新认证/改IP，未保存原始页面、脚本或凭据。
- [x] 普通drcom登录与其他eportal流程的IP/MAC字段分开记录；同IP跨设备免认证仅为待验证假设，不能据此实现静态IP复用。
- [ ] 核实权威终端在线状态接口及服务器过期规则后再定保活设计；没有续期协议证据不做周期重复登录。本轮没有修改生产代码/UI，NET-01～06仍待修。详见05技术手册最新观察。


## 校园网保活可靠性审查（未修改生产代码）

- [x] 现有网络/调度/命令定向28/28；合成隔离探针5场景完成，无真实网络/账号操作。发现并记录NET-01认证页误判、NET-02最终断网仍记成功、NET-03注销/关保活后在途认证继续发送，见review-issues两份当前文件。
- [ ] 修复上述三项、校准默认代理策略NET-04、隔离课表慢任务NET-05、明确配置变更及时性NET-06，再做真实长时/切网/休眠验收。不能将“已写后台调度”视为无人值守可靠性已验收。
- [x] 核实“直连不使用系统代理”与当前new HttpClient默认装配不一致，手册及交接补充纠正。本轮不修改生产代码/UI、不重启桌面。

## 2026-10-15：注销重登后的课表日历缺失修复

- 用户反馈注销重新登录后日期横杠、下一节卡片消失、当天/目标课高亮缺失，表现像UI整体变更。本轮没有修改UI组件、CSS或最终demo设计。原地只读核对普通课表缓存的日期元数据：较新同学期周快照缺week_dates，同学期较旧周仍有有效日历；未读取凭据或Cookie，未输出或复制真实课程正文到测试或工程。
- Storage.FindSemesterStartDate只在当前学生、显式相同academic_term、指定来源内选择最新有效的周一/周次锚点，不回退空学期或其他账号/学期。ScheduleService在本地读取及新快照落盘前，仅对缺日期的payload补齐该周日期；保留新课程、更新时间和已有来源日期，不改原始响应、不依靠今天推断开学、不以旧课程覆盖新课程。
- 前端combineSchedule分别选择日期锚点和周目录，较新的无日期目录不再把先前有效startDate盖掉；注销仍清空认证/内存，恢复依靠既有持久化普通缓存，不保留其他账号状态。
- 后端Calendar/WeekIdentity/SemesterCache/AuthProtocol定向22/22；新增注销→重新登录、缺日期新抓取落盘、已有来源日期优先及跨学期/账号隔离回归。前端App/合并/加载/适配119/119，包含日期、下一节卡片和当天高亮渲染回归；应用/测试TS、runtime/preview构建通过，lint0错误/9既有warning。未新增浏览器或真实学校注销登录操作。
- DesktopHost重建0警告/0错误并自动重启一次，PID26068、窗口可见，health/runtime build dfbb441a96edceb7一致、入口JS/CSS HTTP200。CSS资源与上轮相同index-BHbPaBj3.css；未stage/commit/push，实际用户重登后效果仍需用户确认。

## 2026-10-15：最终demo前端适配

- [x] 用户确定XKE-frontend-fixed-20261015.zip为最终视觉；仅适配前端，三栏设置/账户卡/560px共享配置卡和课时点阵保留。身份卡与进度固定最新学期currentSchedule，不随历史浏览。
- [x] 新纯显示计数按真实节次/周规则和原row分段去重，完整学期才展示；无时间估算、无缓存总数冒充、无后端DTO/存储/协议变化。细节见frontend-rebuild任务板。
- [x] 前端定向180/180、应用/测试TS、runtime/preview构建通过，lint0错误/9既有warning；限定合成UI核对通过。DesktopHost主输出0警告/0错误并自动重启，PID12768、窗口可见，health/runtime build cc28a1ea4a2ee80c一致、入口资源HTTP200。未做真实学校/校园网或发布验收。

## 2026-10-15：连接设置卡片UI增量

- [x] 连接设置不再在左栏折叠，复用课程源卡片展开/收回；输入焦点边框与心跳数字步进控件修正，后端/凭据/间隔契约未改。具体状态见frontend-rebuild任务板。
- [x] 前端定向150/150、类型与runtime/preview构建通过，限定真实React合成交互11项通过；DesktopHost主输出构建0警告/0错误并重启，health/runtime marker c0861f8c0a55a538一致、窗口可见。未进行真实校园网或学校登录验收。

## 2026-10-15：校园网独立凭据、门户错误与可配置心跳

- [x] 四种接入方式均支持独立账号/密码，campus_account/campus_password进入现有DPAPI网络凭据存储；网络认证与自助摘要不再读取或默认转发教务密码，不修改教务账号。GET配置四组credentials仅返回脱敏密码，顶层campus_account是实际保存值。
- [x] 连接设置统一“账号”“密码”，配置心跳1～1440分钟；不修改密码则同账号保留，更换账号不复用旧密码。修复真实密码含星号/首尾空格被误认为掩码或篡改的问题。未配置校园网凭据先提示，不伪装成账号已经可用。
- [x] 新CampusPortalLoginResult仅解析JSON/JSONP数据，result/HTTP状态不一致不视为成功；IP限制、认证拒绝、无效响应、服务错误和超时按必要消息/错误码反馈。未知ret_code不猜含义，IP限制不继续换运营商后缀重试；另一条外网已在线也保留operation_error，后台软失败记录LastError并遵守间隔。
- [x] attempts删除原始preview，仅保留脱敏用户名/状态码/accepted/error_kind/message；认证失败文案去标记、限长并去除凭据及其标准URL编码形态。网络请求直连不使用系统HTTP代理；不伪造地址、不恢复netsh/手动IP，不保证绕过IP授权或VPN系统路由。
- [x] 校园网凭据修改后摘要丢弃旧身份缓存；前端请求对象/认证代次隔离防止旧摘要回填新账户，静默联网轮询不清除认证拒绝。自助后台仍独立只读白名单，与选中运营商及门户认证会话分离。
- [x] 后端网络/摘要/任务/设置/命令解码定向45/45、Contracts11/11，共56项；覆盖四组独立凭据、教务密码不外发、含星号密码保存、旧密码保留、IP限制及外网在线仍认证失败、无效HTML/HTTP503/错误码、敏感回显脱敏、间隔校验/调度。前端App/样式/适配/HTTP/preview配置136/136，应用及测试类型检查、runtime/独立preview构建通过，lint0错误/9既有warning。
- [x] DesktopHost主输出构建0警告/0错误并重启，窗口可见；health/runtime marker build 85aa6b1750b4668f一致，新JS/CSS HTTP200。未请求学校或以真实校园网账号登录验收，未改真实凭据/系统IP、未stage/commit/push；真实允许IP与门户实际返回仍待用户登录确认。

## 2026-10-15：移除手动IP与系统网卡写入

- [x] 用户明确删除该功能。移除NetworkSettingsDocument/Request的静态地址、子网前缀、网关、DNS，AuthSettingsCatalog相关默认/归一化/patch，NetworkApplyResult及配置apply_result；NetworkService保存只写业务偏好/运营商凭据，删除netsh写地址/DNS与管理员错误处理。保留只读网卡采集/门户IP匹配/实际IP展示/保活。
- [x] 配置保存不再有异步系统操作，规范为SaveConfig同步入口；前端手动IP开关与字段、draft/撤销状态、应用失败通知、DTO/合成fixture及无用样式同步清理。未新增旧业务兼容或迁移，不改真实Windows网卡、不恢复DHCP、不删除运行数据。
- [x] WebHost网络/设置/解码定向19/19、Contracts11/11，共30项后端测试通过。新增退役字段POST验证：配置响应无这些字段及apply_result，且不调用netsh；正常读取IPv4、运营商凭据脱敏、门户与保活仍覆盖。前端4份定向130/130、类型/runtime构建通过。
- [x] API契约、frontend/AGENTS与runtime-integration同步当前边界；历史过程记录保留。DesktopHost主输出构建0警告/0错误并重启，新窗口可见；health/runtime marker build 90cd487faad3fa5f一致，入口JS/CSS HTTP200，生产src/frontend/src不再引用静态IP配置或netsh。

## 2026-10-15：本机IPv4采集的中文命令解码修复

- [x] 用户反馈IP未获取。本机网卡实际有IPv4；旧ProcessNetworkCommandExecutor强制UTF-8读取ipconfig/all，中文OEM输出产生替换字符，网卡标题无法匹配，Interface为空。未用门户值/手动配置IP/演示地址兜底。
- [x] Windows系统网络命令按GetOEMCP返回的实际OEM代码页统一解码stdout/stderr；不根据UI文化猜代码页，非Windows仍保留UTF-8。保持进程隔离、无窗口、超时/取消与既有网卡选择规则，无新增依赖或持久化敏感输出。
- [x] 新增原生命令中英文输出/字段解析回归；NetworkCommandExecutorTests+NetworkWebHostTests+NetworkMaintenanceSchedulerTests定向8/8、0跳过。正式executor+parser本机只读验证识别10个网卡与5个IPv4字段，结果与Windows接口枚举一致、无替换字符；仅输出数量/有无，不保存原始ipconfig或真实地址。
- [x] DesktopHost主输出构建0警告/0错误并重启新进程，确认加载当前Core，窗口可见；前端未改，health/runtime marker仍fc44733cbaae4017。未请求学校、提交认证或修改本机网络配置；没有单独验证带会话HTTP状态响应。

## 2026-10-14：校园网自助摘要实际接入

- [x] 独立客户端login/ → randomCode初始化 → 带Origin/Referer的login/verify → dashboard；独立CookieContainer，只允许预定登录/读取路径，不调用退出/踢下线/修改账户。
- [x] 实际DOM确认状态/套餐/计费方式/周期两列结构，精确白名单解析；周期保留原文，禁止原始响应、个人资料/密码对象/余额/流量进入DTO或缓存。
- [x] GET /api/network/summary及最小契约，已有受控校园凭据；同账号内存5分钟成功缓存，失败保留快照并允许重试，账号/密码变化清空，不耦合status/保活。
- [x] 最终C#客户端真实登录读取五字段有值，同客户端再次读取成功；仅记录结构与有无，不保存原始页面/验证码/凭据值。
- [x] 最终工作区6项自助测试与校园网/保活回归合计11/11，0跳过；隔离构建0警告/0错误，输出artifacts/network-summary-validation。
- [x] 主线程已配置ignored私有endpoint，桌面主输出重新构建并启动；本机summary返回五字段有值，重复读取命中缓存，health/frontend build `865b4699d65ad2d0`一致、窗口可见。左栏真实数据消费已接入，前端100项定向与10项合成布局核对通过。

## 2026-10-13：单一桌面窗口外壳

- [x] WPF WindowChrome去掉系统标题栏，WebView2导航前开启非客户区支持，frameless=true；前端只留一套软件窗口按钮，全认证阶段可用。
- [x] 原生8 DIP缩放边缘，最小1100×656适配前端1080×640；默认尺寸、bridge最小化/最大化/托盘关闭/激活沿用。
- [x] 加载/错误备用退出与成功WebView显示互斥；固定SDK初始化与NavigationCompleted无可见性循环等待门禁。
- [x] .dotnet_cli隔离构建至artifacts/frameless-validation：0警告/0错误；未停止或重启当前进程。
- [ ] 实机确认CSS拖动、边缘缩放、跨屏DPI、最大化任务栏边界、托盘恢复。编译/源码自查不等于桌面验收。

## 2026-10-13 默认学期与前端同步修正

- [x] 本地默认学期按学期ID选择，历史学期后来抓取不抢占启动默认；同学期新鲜度规则不变，定向SemesterCacheTests 4/4。
- [x] 前端按学期保存显示状态、日历仅从同学期本地周快照补充；具体交互修正与验证见frontend-rebuild当前任务板。
- [ ] 真实学校/发布验收不因本轮显示与本地选择修正而标记完成。

## 2026-10-13 校园网自助后台协议整理与数据裁剪

- [x] 使用用户本人 Chrome 确认自助后台登录链路：表单 POST、302 跳转和 `JSESSIONID` 会话；不把它与认证门户混合。
- [x] 记录关键 GET 接口、分页参数和返回形态；确认登录历史、月账单和业务记录存在数组列，未知列不猜语义。
- [x] 根据真实账号返回结果收缩白名单：第一版只保留状态、套餐、计费方式和服务器计费周期；余额、金额、零值用量、空设备列表不进入模型。
- [ ] 实现独立 `CampusSelfServiceClient` 和最小白名单解析器；先补脱敏 fixture，再决定是否接入 WebHost。
- [ ] 冻结 `CampusSelfServiceSummary` 契约后对接前端；当前 frontend 继续使用现有校园网状态契约，不把研究接口伪装成已接入。

详细协议记录见 [07_self_service_protocol.md](07_self_service_protocol.md)。

## 2026-10-11连堂显示与教室提示

- [x] 吸收用户schedule.zip的局部优化，前端在快照合并后按目标周聚合同日相邻/同课程教师类别/同教室/课间0–30分钟课时；保留原分段与周规则，不改后端DTO或JSON/CSV。
- [x] 相邻换教室保持分块并提前提示时间/目的地，课间倒计时按实际分段显示距续课；周课表、左栏和详情共用模型。课块按普通/今天/下一节分层，不恢复候选布局或悬浮提示。
- [x] 前端相关5份测试95/95、严格类型检查/runtime构建通过，lint0 errors/8既有warnings；build `b024c14f3112f0b7` 已由本机桌面服务提供、index引用新bundle、窗口激活成功。未请求学校/改运行数据，无浏览器UI/后端全量验证。窗口仍待F5加载，未重试前次被拒绝的重启。

## 2026-10-11课表与校园网显示收敛

- [x] 前端聚焦当前学生/学期/查看周：历史学期在设置按服务器入学日期筛选，主表移除冗余标题/悬浮提示，顶部保留学期与刷新。时间轴只取查看周真实课程，完整学期裁末尾空周，不改课表存储或合并。
- [x] 校园网连接前后同一布局，页面可见时每30秒只读状态，无检测占位页/按钮，无能力统计隐藏；连接/配置保留，后台保活仍归C#。
- [x] 前端相关6份测试102/102、严格类型检查、runtime构建通过，lint0 errors/8 warnings；当前桌面服务提供新build `418181b59e5dbfd1`及对应bundle，激活接口200。未请求学校或修改运行数据，无浏览器UI/后端全量验证。
- [ ] 当前桌面窗口加载新版：重启命令被自动审批拒绝（仅返回blocked by policy），原进程仍在；需F5重新加载。health新marker不等于旧窗口已加载。

## 2026-10-11课表回看显示修复

- [x] 确认当前周API和学期缓存均有课程，缺失现象来自默认hide与前端全局已结束过滤。
- [x] ui.inactive_course_mode默认dim；hide仅影响当前周，历史周保留；全部隐藏显示已结束状态，与无课/无数据区分。现有个人显示偏好通过API仅patch为dim。
- [x] 前端App/快照31、契约11、设置10项通过，类型检查和runtime构建通过，lint0 errors/9既有warnings；DesktopHost构建0警告/0错误并重启至build `040c1044cf4a2dcd`。不修改课表、不额外请求学校，不做浏览器检查。

## 2026-10-11登录恢复阻断修复

- [x] 实机检查仅业务文件格式前缀，确认旧开发profile/preferences仍为加密文件；未读取/解密真实凭据或Cookie。
- [x] 不可解析业务JSON视为不可用且读取不修改文件；已有凭据通过校验后，本地资料/课表不可恢复则进入学校验证码认证。桌面无完整工作区返回404，不显示解析器原文。
- [x] 学校认证成功生成普通JSON；失败保留原文件并回滚，不实现旧数据兼容/迁移或删除旧目录。未归类API异常用简短提示，诊断留宿主日志。
- [x] Auth/PersonalRuntime定向 **24/24**、AuthStorage **14/14**，DesktopHost构建0警告/0错误。覆盖旧格式/截断JSON/缺资料/缺课表、完整本地恢复及重新初始化失败回滚。
- [x] 重建并重启桌面，health正常、bootstrap404，真实学校验证码入口返回200与图片；探测只取验证码，不提交学校账号密码，完整个人登录仍由用户继续。

## 2026-10-11首次认证与简短操作

- [x] 用户最新确认首次仍需认证；启动自动恢复已有账号，未建立可信工作区则认证，成功后进入主界面；注销返回认证页。
- [x] 删除登录说明、空态操作指引、导航账号状态及网络配置实现说明，按钮表达操作；保留必要标签、状态与错误。
- [x] App/Runtime定向36/36、类型检查、lint（0 errors/9既有warnings）和runtime构建通过，build `cfad6d98cc99e59e`；学校协议与HTTP契约未变。
- [x] 已重启桌面窗口并核对当前资源；未重新实登学校或做完整UI验收。

## 2026-10-10入口探索（已由次日决定覆盖）

- [x] 启动直接显示主界面，后台可信恢复不再触发全屏登录；无账号显示真实课表/校园网空状态。
- [x] 教务账号、验证码、连接与注销集中到设置；取消和注销保留主界面，在途恢复或认证不重新安装旧工作区。
- [x] 定向前端 **43/43**、类型检查、lint（0 errors/9既有warnings）和runtime构建通过；本次隔离HTTP **1/1** 通过，停止202且临时根清理完成。
- [x] 已重启Windows桌面窗口，health正常、窗口可见，加载runtime build `dae5de4824cb9e15`；没有进行完整原生UI或学校实登验收。

## 本轮最终复核

- [x] 学期本地只读恢复与 `cached` 契约完成；前端启动/切换先读本地，在线再刷新，刷新失败保留已有数据。
- [x] 课表按实际获取时间合并；新结果不会被旧学期/单周缓存覆盖，成功空周可以覆盖旧安排。
- [x] `ScheduleDataMapper` 成为桌面与 ProtocolHost 共用的周/学期转换入口，保留服务器时间、未知字段、地点/周次安排和课程元数据。
- [x] 桌面显式 `provider=protocol` 消费 ProtocolHost；仅发送课表 GET，协议服务失败回退直接 JW，不上传桌面学校凭据。
- [x] WebHost 定向测试 **242/242**、ProtocolHost 定向测试 **26/26**、React Vitest **58/58** 通过；runtime 类型检查和构建通过。
- [x] 使用最新源码重建隔离宿主后，React runtime HTTP 联调 **1/1** 通过；资源健康检查、正常停止和临时数据根清理通过。

- [x] 用户锚定产品第一版设计，授权子智能体实现对接及协议版，明确项目未使用、无旧业务兼容。
- [x] 代码核对与登录/数据技术手册、ProtocolHost/部署手册建立。
- [x] ddddocr C#适配与loopback Python进程；实际模型合成图片HTTP识别通过。
- [x] 修复当前学期选项读取、登录账号别名与服务器学号，以及服务层跨地点/周次误合并。
- [x] 前期后端定向69项、Python服务5项、ProtocolHost定向25项和React定向30项均已通过；计数保留作过程记录。
- [x] React runtime对接完成；runtime没有demo数据chunk，preview/runtime产物边界已验证。
- [x] ProtocolHost Linux x64目标发布构建成功；两份systemd unit与私有env模板已提供，未部署。
- [ ] 真实学校密码登录/OCR准确率、Linux运行/云部署、Windows托盘自启动/发布包验收。

本轮最终复核包含 WebHost242、ProtocolHost26、React58、Python服务5 和最新源码真实隔离HTTP联调1；DesktopHost构建0警告/0错误。Linux产物仅目标编译。此前联调曾因宿主未重建而使用旧路由，重建后已通过；没有把fake服务当学校实登。

本轮不部署、不提交，历史计数不代替本轮证据。下方为历史过程。

---

> 2026-10-08 前端整理：frontend/ 现采用用户选定的 React 压缩包优化设计，仅 mock 预览，旧 TS/Vue 运行时集成已归档供后续参考。下文旧 UI 验证为历史记录；真实 React API/桌面接入待完成，C# 功能和数据根未变。

# 个人后端任务板

## 集成前已完成切片（历史记录）

- [x] 新建共同分支，明确前后端目录分工。
- [x] 移除非核心运行模块、API 和云策略依赖。
- [x] 清理契约、旧存储、更新项目、发布工具与退役测试。
- [x] 支持个人时间间隔与校园网地址自定义。
- [x] C# 后台恢复本地工作区并运行校园网保活。
- [x] C# 后台自动刷新课表，过期会话不启动隐藏验证码。
- [x] 默认 JW 首登成功/回滚集成验证及无界面保活验证。
- [x] 统一 API 业务错误格式，记录新前端交接。
- [x] 该切片构建、剩余测试与文档链接复核。

2026-10-07 较早后端收敛切片的历史验证：契约 10、存储 13、WebHost 91，共 114 项通过；桌面项目构建 0 警告 / 0 错误；打包脚本语法检查通过。当时未执行真实网络、Windows 自启动或发布包验收；不代表本轮 Vue/runtime 已完成验证。

## 后续，不在本轮验收范围

- 迁移历史移动兼容测试后删除旧协议实现。
- 交付后台课表提醒发送、忘记本地账号等操作。
- 新前端完成后真实联调及发布包验收。

旧用户数据、私有配置和前端目录保持原样；本轮不推送远端。


## 个人课表服务

- [x] 云端识别/抓取，本机仅 HTTP 获取；未添加 Python 产品依赖。
- [x] API 和 JW 结果共用转换、规范化与缓存入口。
- [x] API 失败/超时/过期/周次不符回退原流程，后台仍不发起验证码。
- [x] 保护较新的本地缓存，不外露服务地址/token/错误正文。
- [x] 使用手册、配置示例、服务器适配文件均放入 ignored 私有目录。
- [ ] 用户提供真实 API 请求方式、鉴权及返回样例后进行联调。

首次账号初始化仍使用登录功能，不把快照当作教务密码校验。

## 当前前端集成：已实现，隔离验证已完成

- [x] 确认 Vue 3 + TypeScript + Vite 界面已实现，不再等待选型/设计。
- [x] 后端负责人确认 UI 契约：`ui.show_weekend` bool 默认 true；`ui.inactive_course_mode` `dim | hide` 默认 hide。
- [x] 协调方回传后端验证：affected 29/29；solution Contracts 11 + SecurityStorage 13 + WebHost 128 = 152/152，0 skipped；含 DesktopHost 的 solution build 0 警告/0 错误。
- [x] 隔离 `tests/Xke.FrontendIntegrationHost` build 0/0 与 HTTP fixture 验证回传：登录成功、课表验证码错误 400/重试 200、刷新失败 503、graceful stop 202；fake 外部/Windows 调用和临时根，不是生产服务或真实外部验收。
- [x] 显式 preview/runtime、同源/`VITE_XKE_API_BASE_URL`、内存 session、分离验证码与真实模型适配的隔离 browser 流程验证。
- [x] 账号偏好与机器设置独立 patch；show_weekend=false 与 startup=true 重载保留，后者为内存 registrar，无真实 Windows 更改。
- [x] 无前端 job scheduler；network on-open/maintain 保持 offline、注销清理课程/用户并启用登录。
- [x] 最终前端 35/35 + 严格 vue-tsc/Vite runtime build、显式 preview build 后恢复 runtime，无 demo chunk/固定验证码；marker/health build 一致，两份 assets HTTP 200/hash 列入。
- [x] 3 DTO rows（不规则 2–4 节/未知字段）、周次标签、challenge 过期禁用提交、取消后刷新 503 保留缓存、第 9 周 404 不显示其他周/第 8 周恢复均覆盖。
- [x] 打包门禁 PS7 语法、11 个模式/元数据 fixture、实际 preview 在创建 output 前拒绝已由协调方回传，未 publish。
- [x] `frontend-build.json` runtime/preview 实际产物与 `tools/frontend-smoke.ps1 -SkipInstall` 35 tests + build 通过；最终恢复 runtime。默认 dist 必须 runtime，自定义旧资源缺标记/旧元数据可兼容，preview/畸形/不支持/非对象拒绝。
- [x] 隔离 host stop 202、进程 exit 0、临时根已清理。
- [ ] 完整鼠标/键盘/视觉验收；IAB 超时后使用实际页面事件点击，无内部状态覆盖，但不能替代完整原生操作。
- [ ] 浏览器错误验证码重试；HTTP harness/状态测试已验证，浏览器因 challenge 过期未验证重试。
- [ ] 真实学校/个人服务、校园网及 Windows/WebView2/托盘/自启动/发布包验收。

当前通过项是协调方与前端负责人回传的最终构建/自动化/隔离流程证据，文档负责人未重跑；集成已实现并完成所述隔离验证，真实环境与完整原生交互验收仍 pending。详情见 [API 验证状态](./03_api_contract.md#vite-静态资源发布模式与验证状态)。本次文档维护仅 `notes/`，不覆盖前后端、根 AGENTS 或私有资料，不提交。

## 私有手册接入核对

- [x] 核对用户给出的私有 API 手册，不复制内容进 Git。
- [x] 适配自签 HTTPS 固定指纹，校验名称与有效期；禁止整体跳过 TLS 校验。
- [x] 请求头 token 采用本机 DPAPI 存储；发布仅显式携带当前用户的受保护副本。
- [x] 实际只读健康检查与鉴权快照转换验证通过，错误 pin 被拒绝；没有触发服务同步。
- [ ] 配置服务器快照对应账号；手册不提供身份，不能猜测绑定学号。

与前期“接口资料待提供”记录相比，请求协议/鉴权/证书已核实，剩余的是个人账号绑定及正式软件联调，不等于服务器或本机端到端全部验收。

## 单人使用确认后的调整

- [x] 用户明确服务器快照全属本人，取消强制学号绑定门槛；此项不再阻塞接入。
- [x] 无身份快照转换到当前本地账号，并使用原缓存和课表模型。
- [x] 仍保留显式身份冲突、日期覆盖与新鲜度检查。

之前“配置账号绑定”的待办现已撤销；不读取旧个人凭据来猜测学号。


## 2026-10-08 JW 协议信息提取研究

- [x] 追踪当前认证、资料、课表首页/按周 HTML 与个人快照的装配链路。
- [x] 建立页面/字段清单，并原地对照归档旧协议源码的相关函数。
- [x] 假页面复现资料错位/select 丢失、旧单元格多课、星期位置、学期/周次/空周/时间推断缺口；现有定向测试 9/9 通过。
- [x] [研究记录](./04_jw_protocol_research.md) 与导航落盘。
- [ ] 确认允许研究的真实协议页面资料范围，核对学期 ID 与切换事件、资料 DOM、课程容器及模式语义。
- [ ] 基于确认后的页面信息补最小强类型提取结果和脱敏回归样例，再落实 JSON/CSV 格式；尚未改代码、迁移数据或取消加密。


## 2026-10-08 浏览器辅助研究后续

- [x] 用户授权的真实页面/脚本/请求观察：学期查询含一次历史学期、资料字段结构、单周获取、月历与调停课表头。
- [x] 三项新增源结构假数据复现：JW-07 失效200 JSON、JW-09 混合资料标签、JW-10 语义 font；临时程序 exit 0，未改生产代码。
- [x] 明确浏览器只用于研究，正式采集不依赖 Chrome/WebView2。
- [ ] 修提取与响应分类，补脱敏定向回归；以纯 C# HTTP 验证真实采集，再冻结 JSON/CSV。调停课记录/状态语义与历史学期周日期仍待核对。


## 2026-10-08 实现验收与后续

- [x] 本人资料 DOM 范围、真实课程结构、空周/失效/错误页区分与源时间保留。
- [x] 学制/入学日期贯通模型、普通 JSON和 API；昵称保存不丢新字段。
- [x] 新学期 GET API与独立快照；新 CSV格式与数组列、原子写入、学期范围读取。
- [x] 取消旧加密业务资料和假 HTML格式兼容；没有自动迁移或删除旧目录。
- [x] 公共假资料回归与真实 C# Cookie GET；真实响应/凭据未进入代码或测试。
- [ ] React按更新契约接入，真实产品联调及桌面/发布验收。
- [ ] 后续处理跨天当前周状态与规范化课表变化提示；暂不实现登录研究、调课原因识别、CSV回写或全学期后台扫描。


## 2026-10-15：前端入口试改与交接

连接设置入口试改未获用户视觉认可，后续交另一agent优化。功能/API不变；前端定向104/104及类型/runtime/preview构建通过，DesktopHost当前build 46e5601500ae26fd、health/资源/可见窗口核对。最新前端更新原同名交接ZIP，详见frontend-rebuild任务板；技术验证不等于视觉验收。
