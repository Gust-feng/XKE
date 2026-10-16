# 产品第一版前后端交接

## 2026-10-17 V1.0.0 应用更新

系统标题旁新增独立版本面板，不挤占三栏设置行。版本/状态来自本机/api/app/updates；前端只读进度、发起检查/下载/应用，不自行比较版本或执行包。auto_update_enabled是机器偏好；preview明确合成、bin开发态不安装。后台校验完成后可立即重启，或自动模式在真正退出/下次启动应用；关闭自动模式保留包不自行安装。面板可见串行轮询，关闭/隐藏废弃旧响应，下载由C#继续持有，真正退出取消排空；dialog内按键不触发1/2/3底层导航。项目链接限制Gust-feng/XKE并由WebView用户点击打开系统浏览器。

## 2026-10-16：提醒、后台更新与原生访问边界

保持最终demo布局/动效；提醒改关闭/15/30/45，默认后端30，较短正数至少15，自定义>=15显示当前选项，不自动开提醒。自动更新开关/每天时间/小时配置在原系统栏，避免挤高课表栏。原同步位置报告需连接/失败，不重复Toast。可见每30秒读取job，成功只读local/semester/local回填、保持所选周和历史浏览；隐藏/注销/选周代次使旧结果失效，已有HTTP批次排空，不声称fetch传输级取消。

WebView2由原生给同源API注入能力头，前端不保存令牌；普通浏览器bootstrap 403/404回手动认证，desktop/settings不可用时禁用提醒/启动设置并保留已保存偏好。C#独立后台通知不依赖窗口可见或WebView时钟；真实通知显示受系统策略影响。前端8份定向218/218、类型/runtime通过、lint0错误/8既有warning；隔离原生HTTP证明测试通过，不是像素或真实学校验收。

## 2026-10-16：网络维护后端修正已加载，前端未改

后台NET-01～06已按现策略局部修正：最终快照统一成功/错误、认证页识别、旧会话/配置终止、独立网络循环和配置修订重排。默认网络HTTP客户端现已UseProxy=false且不自动跟随跳转（不影响OS/Mihomo路由），覆盖下方历史代理待修说明。手动连接与关后台保活分开，API JSON结构不变；无界面、样式、前端源代码或构建资源变更。后端相关69/69与桌面构建通过并重启；详细边界在personal-runtime/05与任务板，未做真实校园网长测。


## 保活审查补充：代理说明更正（当前代码事实）

下方历史记录中的“门户请求和联网检查直连、不使用系统HTTP代理”不符合当前实现：XkeWebHostApplication默认装配new HttpClient()，未显式禁用代理；不能据此保证校园网检测和认证避开系统代理。该项归入NET-04待修，实际是否使用代理取决于系统及环境配置。其余系统路由/VPN、服务器IP授权边界不变。本轮仅核对，无真实认证或网络设置修改。完整保活可靠性问题见notes/review-issues/00_current_issues.md的NET-01～06。

## 2026-10-15：最终demo适配已完成

用户确认新返包为最终视觉。前端按最新学期currentSchedule展示身份学期/同步和课时进度，不跟随历史浏览；新增显示层计数，按source.section_numbers及同row实际时间分段去重，完整学期才展示总数。没有新增后端字段、变更现有DTO、密码/会话存储、学校认证、网络协议或保活任务。原始安排/JSON/CSV不改写。

连接入口统一设置页、560px共享卡片，四种独立账号/密码、1～1440分钟心跳、IP只读与operation_error边界保持。定向180/180及类型/双构建通过，DesktopHost已重启并核对build cc28a1ea4a2ee80c与窗口；详细技术及限定合成UI核对见runtime-integration。

## 2026-10-15：连接设置卡片交互与控件修正

- 取消连接设置入口的左栏内联折叠，NetworkConfigPop与CoursePop共用CardPopover源卡变形/共享文字/收回流程；保存API、四组账号和密码、心跳间隔及高级字段保持原逻辑。设置页入口不再先切到校园网页，直接从本行展开。
- 输入焦点环移入边框盒并消除原边框叠线；心跳间隔由原生上下箭头改为减号/数值/加号，保留type=number键盘步进、直接输入及1～1440边界。
- 网络表单420px优先宽度，长内容内部滚动；异步读取/高级字段变化平滑调整卡片尺寸，窗口缩放保留草稿。Escape、点击空白关闭，关闭期间inert，网络表单Tab循环并回收焦点；减少动效静态到位。课程默认几何与文字终态交接不变。

## 2026-10-15：四种接入方式凭据与可配置心跳

- 校园网/电信/联通/移动都在连接设置显示“账号”“密码”，各自保存到当前学生的DPAPI网络凭据文件，不自动复制或转发教务密码。校园网未独立配置时明确提示，教务工作区与登录资料不修改。
- 自动保持连接的心跳间隔在同一编辑器设置，1～1440分钟整数；后台C#按配置检查、必要时重认证，不依赖前端定时认证。更改配置不等于服务器已经认证成功。
- 认证JSON/JSONP按result/msg/ret_code提取结果，只解析数据、不执行脚本；operation_error独立于internet_connected，另一条链路有外网不代表此次认证通过。IP权限文本、认证拒绝、响应异常、服务错误/超时分别保留必要原因，不回显原始响应或未脱敏凭据；只有服务器明确提供的信息才用于归类，不猜测允许网段或错误码含义。
- 门户请求和联网检查直连、不使用系统HTTP代理；系统路由/VPN和服务器IP授权仍可能影响结果。不能靠改网卡/伪造IP绕过限制，手动IP功能保持删除。

## 2026-10-14：自助摘要已接入左栏

`RuntimeApi.getNetworkSummary()`调用独立`GET /api/network/summary`，消费03契约的平铺白名单。Store维护独立摘要/忙碌/错误，账号切换清理，失败保留同账号成功值；进入net及连接状态变化时读取，30秒网络status轮询不带summary。available不等于联网成功，cached失败数据只显示必要“暂未更新”。后台服务/门户认证/保活各自保持边界。

左栏以真实套餐元数据为主，连接设置原位展开，默认隐藏高级字段。课程详情只显示课程名、地点、具体时间、教师，原始周规则不删除但不展示；复制动作全部移除。窗口按钮与导航已分成独立层。前端100项定向通过，10项合成Edge布局检查通过；独立C#实取白名单通过，桌面最终加载与后端最终测试计数见任务板。

## 2026-10-13：单一桌面窗口外壳

公共WindowControls已移至App外层，认证/加载阶段也可用，既有bridge方法未变。宿主WindowChrome去掉系统标题栏，导航前启用`IsNonClientRegionSupportEnabled`，`frameless=true`；前端顶部与空白toolbar使用`app-region:drag`，nav/buttons为no-drag。窗口关闭继续调用原托盘逻辑，不能直接替换为强制退出。正常显示时无第二套WPF按钮，加载/错误仅保留备用退出。

连接等待恢复25%中档，仅由真实netBusy控制；日期与详情精简不涉及HTTP、后台任务或原始课程字段。详情使用真实边界变形，宽源卡不被360px默认宽度截断；原始课时/周规则保留。前端定向70/70、类型/lint/runtime/preview构建通过，宿主隔离构建见任务板。未重启实机或做额外UI验收。

## 2026-10-13：校园网左栏层级与信息降噪

用户指出原图左侧未很好还原，且数字、IP、月份旁时钟属于冗余。此次保留原图的日期/分区标题/分隔线/列表节奏，而非复制不受真实接口支持的演示统计。

- 全局日期栏移除月份旁时钟；日期栏按分钟更新，下一节倒计时和课中进度保持按秒更新。校园网页只保留点阵HH:MM。
- 主页去掉账号数字、IP、在线时长和游戏分数；原三栏metadata改为单行必要状态提示，边线和同布局状态切换保留。
- 左栏以真实“自动保持连接”和“连接配置”构成连接分区，不因没有流量数据只剩底部孤立链接；移除流量数字和设备数量，设备有真实能力时只保留名称/本机/下线操作，无能力不显示空占位。
- 设置页的同一开关改称“自动保持连接”，对应已有heartbeat_enabled，而不是错误暗示另一套开机自动登录功能；移除旁边重复校园网账号。实际DTO、配置输入中的IP、凭据和后端协议不改。

验证：App/样式覆盖/DTO定向 **68/68**，严格类型检查、lint（0 errors / 9既有warnings）、runtime/preview构建通过；runtime build `294f290246a7fef2`。没有启动/重启桌面，没有额外浏览器UI检查；这里是代码与渲染结构验证，不宣称像素或完整交互验收。


## 2026-10-13：学期尺与切周操作解耦

修复普通切周也触发整把尺子刷新动画、周快照加入后重算全部横条长度的问题。Store新增仅供视图使用的`semesterSchedule`（现有整学期快照引用）和`refreshing`（主动刷新状态）；不新增持久化缓存或后端字段。

- 课表主体仍按较新单周快照覆盖学期规则；学期尺长度/比例只读完整学期快照，明确表示“学期安排”，不随浏览周次重新归一化。
- 普通周读取仍使用`syncing`协调请求，但不播放扫描动画、不显示“更新中”；主动刷新才置`refreshing`。取消、认证恢复和切学期清理该标记。
- 切周只改变选中标记/周数字；完整学期数据更新时才重算概览。尚无整学期数据时显示课量未知，不把当前单周数据伪装成全学期分布。

验证：App/学期范围/快照定向 **68/68**，严格类型检查和runtime/preview构建通过；lint 0 errors / 9既有warnings。runtime build `5f5fbdd4b5c7b260`。未请求真实学校、未新增浏览器检查、未操作或重启桌面。


## 2026-10-13：同步优化返包 XKE-frontend-fixed-20261013.zip

以用户指定返包的9个前端源码增量为准，不把旧交接包的参考文档、AGENTS或过期SHA清单覆盖回工作区。当前源码与交接基线没有并行差异；源码归一化换行后合入，原改前文件备份留在ignored的artifacts/frontend-handoff/。

- 修复无服务器学期ID的显式preview找不到课表；演示账号补入学日期，作息表只补到合成数据，runtime仍遵循服务器block_times。
- 导航恢复右上；校园网离线滑块显示“未连接”，三栏信息行恢复；点阵时钟和preview时钟共享time.now()。
- 普通/已结束/实践课块均为实心，不用整体透明度；实践用短标签，正在上课显示已过时间的暗面和纸色切线。入场揭开替代文字纵向拉伸，底部裁切小时标签移除。
- 详情采用独立的打开/关闭层、先变形后显示内容，切换课程不复用同一个裁切容器；深色/红色卡次要字和操作沿卡片前景色，源课块让位。
- 集成修正只补生命周期：移除/键盘切换时源课块恢复，关闭回调保持稳定，静态详情不阻断旧层退场；只从可点击课块捕获来源。课中进度按秒更新；连堂课间按真实分段决定是否显示时间线。

验证：相关5份测试 **103/103**、严格类型检查、lint **0 errors / 9既有warnings**、runtime/preview构建通过；runtime build `66b499a7b293d94c`。真实API、鉴权、存储、后台任务和后端源码未改。不启动/重启桌面，不做新增浏览器UI或真实学校请求；返包中的逐帧检查属于对方记录，不当作本轮已执行验收。


2026-10-13：第七版「合成」为产品第一版，当前 React 已接入 C# HTTP runtime。全部修改位于 frontend/；后端契约/工具/notes 由主 agent 维护，没有提交、暂存或切换分支。


## 2026-10-13 同步修正：以压缩包 2 的完整交互为准

此前 v22 同步遗漏学期弹层 CSS，并把正在浏览的学期复用为当前安排；构建通过不能代表设计同步完成。本轮已补齐：

- 学期标题/弹层/更新按钮/尺子扫描和可重试短提示使用完整样式，不再出现浏览器默认编号列表。
- `semesters` 按学期缓存，`currentSchedule` 与浏览中的 `schedule` 分离。回看历史从第1周开始，左栏日期/下一节仍使用当前安排，`T` 返回本学期；菜单始终能选回当前学期。
- 本地学期缺少日历时只读取同学期已有周快照补充日期，不臆造历史开学日期；未保存的学期不替换当前画面。资料文件和原始课程不改写。
- 无明确学期参数的本地读取按学期 ID 顺序选最近已保存学期；同学期新鲜度仍按实际 `updated_at`，避免后来抓取历史学期抢占启动默认。
- `R` 刷新、更新扫描和失败6秒重试提示接入现有认证/取消边界；删除“已有课表保留”的常驻重复错误。
- 校园网页左栏改为网络侧栏，不再放下一节卡片/周尺；只显示真实支持的统计，配置仍为二级抽屉。只读状态轮询不触发认证动画。
- 点阵保留 v22 时钟/离线彩蛋；减少动效时联网立即到位，时钟按分钟重绘，尺寸变化会重绘，退出清理 RAF/定时器；不每帧重建字形数组。
- 课程就地展开、配置抽屉从右进入、配置恢复原值后保存禁用；关闭动画取消后不能回头关闭新打开的课程。

最后代码收尾复核：App/学期状态/加载/CSS定向 **73/73**，严格类型检查和runtime构建通过，最终runtime build `6a9add2ef3415dfc`；preview另行构建。用户要求只改代码，未再次启动、重启或操作界面。

验证：前端 unit **153 passed / 1 skipped**；类型检查、lint（0 errors / 9 warnings）、runtime/preview 构建通过；`SemesterCacheTests` **4/4**。已完成隔离 Chromium + 合成 HTTP 的10项交互检查（弹层计算样式、历史浏览、T/R、失败保留、详情、网络侧栏、减少动效时钟、配置撤销），无 pageerror，不访问真实学校。桌面在此前构建上确认恢复到“大三上学期 / 第6周”和当前下一节；用户随后要求只改代码，最终后续小改动不再重载窗口。不把它写成完整学校/发布验收。

## 2026-10-13：同步 schedule (2).zip v22

- 已同步压缩包最新的连堂合并、换教室提醒、课程详情就地展开与校园网矩阵优化。
- `CourseBlock` 仍是前端显示模型；后端仍接收/保存规范化课程 rows，不接受 `parts` 或 `moveFrom` 回写。
- 校园网在线矩阵只展示当前时间 `HH:MM`，不把余额、流量或在线时长作为矩阵信息载体；runtime 未提供的能力仍保持 null/false，不显示空占位。状态和配置仍走真实 C# API。
- 发现并修复 `prefers-reduced-motion` 下 Canvas 首次静态绘制后不再被连接状态唤醒的问题；这不改变正常动效路径。

验证已重新执行：`npm run check` 通过；Vitest **133 passed / 1 skipped**；lint **0 errors / 9 warnings**；runtime build 与 preview build 通过。真实学校、真实校园网、Windows 发布包仍不是本次自动验证范围。

## 2026-10-11吸收schedule.zip的连堂与教室优化

已只读查看压缩包CHANGES-merge、merge、one组件及样式，沿用当前合成布局，吸收连堂显示、换教室提示和时间层级。修正包内按全学期JSON周规则/同名直接合并、忽略教师及整学期查换教室的不足；不导入其他布局/mock，不在接口取数时改写原课程。

新增lib/course-blocks.ts：在快照新鲜度合并之后按目标周生成CourseBlock，原课时通过parts完整保留。只合并同日相邻、名称/教师/类别相同、同一已知教室及0–30分钟课间的课程；不跨重叠或其他课程。不同教室保持分块，目的地显示提示，左栏提前显示时间和房间。换教室提示也覆盖相邻不同科目，不跨周或长间隔。课间倒计时按真实分段，详情保留每段节次/周规则/来源元数据；周尺计次课。移除已无调用的旧双套nextOccurrence/upcoming，统一使用实际周规则找下一次课程，长假不误报结束。

普通课块浅底、今天墨色、下一节/正在上红色，课块突出课程名/教室；无重复悬浮提示。共享preview样本改为12个合成课时，含同教室连堂和虚构换教室，不进入runtime。

验证：App、course-blocks、schedule-view、schedule-state、adapters共 **95/95**，严格类型检查/runtime及显式preview构建通过，lint **0 errors/8既有warnings**；两种产物模式隔离，runtime无演示值。runtime build `b024c14f3112f0b7`；桌面本机health/index确认新版资源、窗口激活成功。未请求真实学校/改写运行数据，无浏览器UI或后端全量验证。窗口未重启，沿用前次自动审批拒绝后的F5加载方式；服务资源已更新不等于旧窗口已加载。

## 2026-10-11课表与校园网显示收敛

- 主课表移除学期下拉、“周课表/本地缓存”和重复悬浮提示；简短学期和刷新放到顶部操作栏，历史学期在设置中按服务器入学日期筛选。
- 时间轴优先取后端 `block_times` 的真实作息；缺失时取已加载课程的真实时间范围，不能由前端固定 8/10 点覆盖服务器作息。完整学期已加载时裁末尾无课周，保留中间空周/当前周/所选周。新增内部semesterLoaded覆盖标记，与所选周scope来源独立；后端DTO、课表文件及合并规则未变。
- 用户确认校园网为独立页面、连接前后同一布局并自动更新。前端仅可见时每30秒读status，进入/恢复可见立即读取，请求不重叠；不调度登录/保活。连接和配置保留，无能力统计隐藏，失败就地显示并保留状态。

验证：6份相关测试 **102/102**，严格类型检查与runtime构建通过，lint **0 errors/8 warnings**。build `418181b59e5dbfd1` 已由当前桌面本机服务提供，index引用新bundle；未请求真实学校/修改运行数据，也未做浏览器UI检查。现有桌面进程未重启：自动审批拒绝重启命令（仅返回blocked by policy）；激活接口200。窗口须F5重新加载，health的新marker只证明资源已更新，不代表旧窗口已加载。

## 最新增量：首次认证与简短操作（2026-10-11）

同日课表回看修复：原hide默认值与全局时间过滤导致周日当前周及历史周空白。后端默认改dim；前端hide仅作用于当前周，历史周始终保留，全部隐藏显示“本周课程已结束”。现有个人账号通过preferences API仅patch该显示偏好，课表数据不改。App/快照31、契约11、设置10项通过，check/runtime build通过，lint仍为0 errors/9既有warnings；runtime build `040c1044cf4a2dcd`。桌面已重建重启；未做额外浏览器UI检查。

用户撤回无认证主界面方案：启动自动恢复已有账号，无可信工作区时须认证，ready后显示主界面，注销回认证页。连接/验证码仍在设置按需打开；取消保留已有工作区，首次取消只返回认证表单，不能跳过认证。

去掉登录说明、空态操作指引、导航账号状态与网络配置实现说明；保留字段、必要状态和错误，由按钮表达操作。学校协议、后台恢复、HTTP鉴权、preview/runtime边界与既有课表动效不变。

本次App/Runtime定向36/36、类型检查、lint（0 errors/9既有warnings）与runtime构建通过，build `cfad6d98cc99e59e`。仅组件入口与提示文案改动，未重跑真实学校或完整UI验收。

同日错误反馈增量：认证错误改为按钮旁12px次级墨色，不重复Toast；传输提示收敛为“暂时无法连接”“连接超时”“响应异常”。修复默认fetch被作为HttpClient方法调用导致Chromium/WebView2 Illegal invocation的问题，默认传输绑定globalThis；学校401与本地会话核验边界不变。定向39/39、类型检查、lint（0 errors/9既有warnings）通过，runtime build `dc834ff81bb184fa`。针对该浏览器调用问题额外做隔离Chromium验证：启动无错误，合成账号错误/断连/异常响应三种反馈均只显示一次，未访问学校或真实账号。

## 2026-10-10入口探索（已由次日决定覆盖）

- App 不再按 loading/anonymous 切到全屏登录；合成主界面启动即显示，后台 bootstrap 继续恢复已有资料。无账号保持 schedule/settings=null，课表与校园网显示真实空态，设置展示同样样式的连接卡。
- 连接/注销从导航移入设置。账号/验证码使用 authOpen + captcha 控制的小弹窗，loading 不弹；无账号也能随时取消，支持按钮、Esc、遮罩，关闭卸载内存输入；注销仍留主界面。
- openAuth/cancelAuth/注销同时使 React 与 HTTP/认证在途代次失效，旧 bootstrap、连接和 captcha 不抢回工作区。bootstrap 开始读取 config 前设 retryConfig=true，修复“配置尚未返回时取消 → 后续登录误判桌面能力”的竞态。
- 无 session 的课表、设置和校园网操作在前端停止或引导设置；不开放无账号校园网、不改变后端鉴权。后台回调使用稳定的“已有工作区”依赖，避免会话核验回填触发循环请求。无账号 connect/logout 不向受保护后端发送请求。
- 已有账号/课表的第七版布局、切周/课程/详情动效保持；preview 仍显式，不加入运行失败 mock 回退，没有新增依赖。

本轮文件：src/App.tsx、src/lib/store.tsx、src/api/runtime.ts、src/components/AuthPanel.tsx、src/editions/one/{OneLayout,SwissWeek,SwissSettings,SwissNet}.tsx、src/styles/runtime.css、tests/{app.test,runtime.test}.ts，以及 README/AGENTS 和 docs/{runtime-integration,backend-handoff}.md。

验证：针对入口 SSR、HTTP/取消竞态和学期加载的 43/43 通过；npm run check、runtime build 通过，lint 0 errors/9 既有 warnings。测试不使用新依赖或浏览器。

本次最终产物为 mode=runtime、build=`dae5de4824cb9e15`。主负责人已在此 build 完成后用全新隔离宿主 `3412` 执行 runtime HTTP 联调 1/1 通过，停止返回 202；随后重启桌面 `34304`，确认加载本次新 marker。此处 1/1 是本次个人使用入口 build 后的新执行结果，不是上轮引用；依据主负责人回传记录，前端 agent 本次仅更新文档，没有重复测试或构建。已确认桌面重载，不扩大为完整 UI、真实学校或发布验收。

学期离线闭环的新端点 HTTP 集成现已由主负责人在新鲜合成宿主执行 1/1 通过（含本地学期与注销后离线恢复）；此前待重跑说明撤销。该结果为回传证据，不是前端 agent 本轮另行运行或真实学校验证。

## 本轮增量：学期离线闭环与快照新鲜度

- 新增 getLocalSemester，对接 `GET /api/schedule/semester/local?academic_term=...`；SemesterDto/adaptSemester 支持 cached（缺省 false），不改写 updated_at。已有周上下文明确传 term，无上下文只使用最新保存学期，不推断学校当前学期/日期。
- 登录、bootstrap、离线恢复先发布本地学期，已连接时再刷新；失败保留已有数据。切换学期本地命中直接使用，仅缺失且已连接时刷新；离线切周缺周文件继续显示学期规则，不自动请求学校。
- 合并依据有效 syncedAt：新学期覆盖旧周，较新周与等时周覆盖该周，最新成功空数据生效。已知优先于未知，双方未知时单周优先且时间保持 null。saveWeek/saveSemester 防止较旧/未知时间缓存替换较新数据；同账号重新连接也保留较新的内存数据。
- 所选周 scope/cached/syncedAt 跟随真正使用的数据；按账号、学期、周及操作代次隔离，切周不取消同学期启动读取，取消/注销/切学期丢弃迟到响应。
- 第一版组件布局、样式、动效未修改；没有引入浏览器业务缓存或前端后台任务。

本轮修改文件（相对 frontend/）：

| 范围 | 文件 |
| --- | --- |
| API/契约 | src/api/{types,dto,adapters,runtime,mock}.ts |
| 状态/加载 | src/lib/{store.tsx,schedule-state.ts,schedule-loading.ts}（新增加载模块） |
| 定向测试 | tests/{runtime.test.ts,schedule-state.test.ts,schedule-loading.test.ts,runtime.integration.test.ts}（新增两份状态/加载测试） |
| 交接 | docs/{runtime-integration,backend-handoff}.md |

验证：定向 58/58、npm run check、runtime npm run build 通过；lint 0 errors/9 既有 warnings。主负责人已在新鲜合成 Xke.FrontendIntegrationHost 执行扩展的新端点及注销后离线学期读取 test:integration，1/1 通过；之前待执行记录已撤销。没有请求真实学校/私有数据、浏览器 UI 检查或写入 frontend/ 外。

下方第一版首次对接记录保留，上轮验证数字不代表本次新端点验收。

## 已实现

| 能力 | 前端入口与行为 |
| --- | --- |
| 会话/登录 | api/http.ts 与 api/runtime.ts；X-XKE-Session 仅驻内存，首次登录与桌面 bootstrap 使用真实接口 |
| 验证码 | components/AuthPanel.tsx；登录提交 /auth/captcha，课表续跑提交 /auth/jw-captcha/submit；TTL、取消、错误、单次登录 challenge 处理独立 |
| 课表 | api/adapters.ts、lib/schedule-state.ts、SwissWeek；学期 rows 与最新单周覆盖按学期/周隔离，空周有效，失败保留已有快照 |
| 学期与周 | /schedule/semester/local 离线读取，/schedule/semester 刷新；日期和周目录取实际单周，历史明确学期和周号，缺日期显示不可用 |
| 设置 | preferences 与 desktop/settings 分开 patch；false 不丢失，只发送修改字段，读规范化响应回填 |
| 校园网 | status、maintain、config、maintenance；内外网分开，不以维护请求成功推断联网；密码仅内存，未修改凭据不发送 |
| 桌面 | lib/desktop.ts 连接现有 pywebview API 与窗口状态事件；普通 WebHost 不假定机器级能力可用 |
| 构建 | dist runtime 无 demo；dist-preview 与 preview 显式 preview；仅 preview 可覆盖时钟 |

学校登录/连接 401 不能直接注销本地工作区。受保护接口 401 先核验本地 /auth/session，确认无效才清 auth、课程和 session。取消/注销的代次检查阻止在途响应恢复旧用户。

## 实际后端缺口与当前处理

- 设备列表、流量统计、踢设备、校园网断开没有当前产品 API：runtime 显示不可用，禁止 mock 成功替代；预览仍可演示原交互。
- 提醒发送尚未实现：只保存 reminder 配置，界面明确说明。
- 显式 academic_term + manual_week 的当前学期分支仍可能缺少日期/目录，历史规范化仍可能回填 current week。已反馈主 agent；前端当前学期刷新走常规单周链路，历史 current week 显示未知，不编造日期。
- 首轮缺少本地学期读取的缺口已由本轮闭环接入：本地学期纯读取，网络刷新失败保留已有数据；后端来源、转换和持久化由主负责人维护。
- 云端 ddddocr 属独立协议 host，由主 agent/协议 agent 负责；桌面人工 captcha 保留。

## 验证边界

前端定向适配/HTTP/缓存/取消状态测试、类型检查、lint 与三种构建通过。真实隔离 Xke.FrontendIntegrationHost 的 HTTP 集成由主 agent 执行并回传 1/1 通过，覆盖两条验证码、错误重试、503 保留、学期、设置、offline 和注销；真实 index/assets 的 HTTP 检查由主 agent 完成。

隔离 host 使用合成学校/网络/Windows 依赖及临时数据根，不是学校或 Windows 真实验收。本轮没有浏览器 UI 检查，没有发布或 Git 操作。

## 本轮文件清单

| 范围 | 新增/修改文件（相对 frontend/） |
| --- | --- |
| HTTP/模型 | src/api/{http,dto,adapters,runtime,network-patch,types,index,mock}.ts；src/env.d.ts |
| 工作区/时间/桌面 | src/lib/{store.tsx,schedule-state.ts,desktop.ts,time.ts} |
| 产品视图 | src/App.tsx；src/components/{AuthPanel,NetworkConfig,Sheet}.tsx；src/editions/one/{OneLayout,SwissWeek,SwissSettings,SwissNet}.tsx；src/styles/runtime.css |
| 构建/类型 | package.json、package-lock.json、vite.config.ts、vite.preview.config.ts、vitest.config.ts、tsconfig.json、tsconfig.node.json、tsconfig.tests.json、build/frontend-metadata.ts；原 build/preview-metadata.ts 已由新通用标记器替代 |
| 定向测试 | tests/{fixtures,adapters.test,runtime.test,runtime.integration.test}.ts |
| 交接文档 | README.md、AGENTS.md、docs/{design-baseline,backend-handoff,runtime-integration}.md |

最终 check 通过，unit 30/30；未设置变量时 integration 1 项跳过，主 agent 在新鲜隔离 host 另执行 1/1 通过。lint 0 errors/9 warnings，三种构建通过，runtime demo 字符/文件检查与文档链接检查通过。

接手：[运行时技术说明](./runtime-integration.md)、[后端契约](../../notes/development-docs/personal-runtime/03_api_contract.md)、[后端技术手册](../../notes/development-docs/personal-runtime/05_backend_technical_manual.md)。
