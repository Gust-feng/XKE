> 2026-10-10：现第七版合成已锚定产品第一版，用户授权React runtime对接。当前接手先读 [后端技术手册](05_backend_technical_manual.md)；独立云协议API见 [06](06_protocol_host.md)。本文件以下API字段仍须按当前代码使用；旧Vue验收/纯mock状态只代表当时记录，不作为本轮证据。

## 当前契约：桌面自动验证码

- 账号偏好auth.auto_captcha_enabled为boolean，默认true；只有已有DPAPI可信token且提交密码与保存密码一致的本机账号可自动认证，偏好不能授予首次登录权限。用户关闭后，普通设置patch/恢复/重认证保留false。
- GET /api/auth/captcha-recognition（既有session鉴权）：{supported,trusted,enabled,state,attempt,message}。state为idle/recognizing/manual_required/disabled/unavailable；只读本机状态，不触发学校请求；不返回账号密码或trusted token。
- 现有captcha_required载荷新增可选message，表达三次识别失败/组件失败后的必要提示；手动验证码入口保留。课表人工验证码被学校拒绝时返回新的captcha_required及新challenge_id，而不是继续复用已释放上下文。
- /api/auth/login未验证账号继续人工；/connect、/reinitialize及周/学期/后台刷新对可信且开启的账号自动续期。自动请求取消可由HTTP中止传播；用户切账号/退出后禁止迟到初始化提交。
- 最多三次总轮次（包含第一次），只对已实测的验证码错误进行服务器重试；密码、未知、限流/网络、原生组件异常不猜测。失败保留本地资料/课表。独立ProtocolHost继续其原有策略，不被桌面功能静默改写。

## 2026-10-19 登录错误区分与自动OCR边界

已实现Core层JwLoginException.Kind：CaptchaRejected、CredentialsRejected、Unknown；目前Auth/Schedule仍按既有400/401映射返回错误，未新增HTTP字段或改变首次captcha_required流程。错误分类以学校响应#showMsg的两条已验证消息为依据，不按HTTP200或历史凭据正确率猜测；原始响应不返回前端。

待实现而非当前接口：首次人工验证成功后按本机账号默认开启自动识别、用户可关闭、设置动态状态、后续最多三次验证码识别/提交轮次与明确凭据错误立即停止。未知错误保留人工处理，不以自动化替代首次人工准入；字段在实际接入时统一更新，不提前宣称已冻结或已支持。

## 2026-10-17 显示昵称消费

POST /api/auth/profile沿用既有{nickname,use_nickname}与{student}响应。前端清空昵称时发空字符串和false，默认学校姓名不被写改；原位编辑只消费昵称与显示偏好。重新认证或持久化JW会话恢复刷新学校资料时保留相同学生本地nickname/use_nickname；未知新学生不跨账号继承。

## 2026-10-17 移动教务退役后的来源边界

周课表持久化键仅为jw_schedule、类型jw；空来源/preferred/cookie/jw/jw_schedule映射到同一缓存，不再读取mobile_schedule或把未知来源映射为移动教务。GET /api/schedule/local使用不支持的source返回400；存储层拒绝不支持来源的新写入。retrieval_channel继续区分direct_jw和快照服务获取渠道，不删除已有缓存、不执行迁移。旧移动名字仅在明确的拒绝/隔离测试中出现；校园网运营商mobile保留。

重新初始化仍走现用表单协议。完整工作区在验证码认证后取课/存储失败时，恢复原凭据、JW会话、个人文件和缓存，保留已有活动会话，原可信登录与离线读取继续可用。

## 2026-10-17 V1.0.0 自动更新增量

用户最新授权检查/自动下载/安装更新，当前实现以Core/Releases＋Velopack1.2.161为准，公开来源Gust-feng/XKE，稳定win渠道与PackageId=XKE；不恢复旧Updater/云控制。新机器设置auto_update_enabled与原生受控/api/app/updates状态/检查/下载/安装入口已接入。HTTP下载仅排队，C#持有并在关闭时排空；校验后退出应用，不强退到托盘的软件。关闭自动更新不会自动安装本地已下载包。

REL-02内容门禁、REL-03 runtime8.0.32发布基线已在新打包流程实现，78份合成门禁验证通过；REL-01自助摘要配置是可选非敏感注入，不再作为纯连接功能的发布前提。详细协议、已验证/待验收边界与公开发布授权要求见08_release_and_updates.md及任务板最新记录；旧“所有更新退役”与旧打包问题保留历史语境。

## 2026-10-16：本机原生入口与后台设置补充

- 数据DTO没有新增平行模型；lesson_reminder_minutes默认30，合法正数下限15（较短值规范为15），关闭不被自动开启，保留现有自定义值能力。
- desktop-webview的POST /api/desktop/auth-bootstrap、GET/POST /api/desktop/settings要求X-XKE-Desktop原生能力；缺失/错误/未配置返回403。能力仅由桌面原生同源请求注入，不由前端或账号会话提供、不对外公开；普通浏览器bootstrap的403/404回手动认证、机器设置不可用。
- WebHost拒绝非loopback Host与非同源/非显式开发Origin（含null）；无Origin的原生普通请求保留，账户接口仍需原session。开发跨源只通过XKE_FRONTEND_ORIGINS显式允许，不能因此绕过原生证明。
- 自动刷新既有daily/interval设置与refresh-status保持契约，409上下文取消不记成功/失败；前端后台观察只读local及semester/local，不再触发学校抓取。ProtocolHost不受本机Origin/能力规则约束。

## 2026-10-16：网络维护语义修正（无JSON结构变化）

GET/POST network接口字段不变。显式maintain仍允许在heartbeat_enabled=false时手动连接；后台入口独立要求有效活动会话及启用开关。会话/配置/凭据变更可使尚未发送的旧维护失效（401/409），不能撤回已发送报文。后台调度正常配置取消不写网络失败。

operation_error、internet_connected和认证历史以同一个最终快照判定；服务端接受认证但外网复查失败不再宣称恢复。检测目标返回普通4xx可视为可达；认证表单/异源/异常重定向/代理认证不作成功。检测地址应无需登录且稳定。代理策略仅禁用系统HTTP代理，不改OS路由/VPN。


## 2026-10-15：网络凭据与认证操作结果增量

- POST /api/network/config支持campus_account/campus_password及现有telecom/unicom/mobile同名后缀字段。GET返回credentials的四个provider；password仅脱敏值，顶层campus_account为实际已保存的校园网账号，不再用教务会话账号冒充。空密码表示同账号保留；更换账号未给新密码时不复用旧账号密码，不新增清空/迁移旧格式接口。
- heartbeat_interval_minutes为1～1440分钟整数；网络config提交无效值返回400且不保存。前端可在保持连接关闭时预先设置间隔，后台任务依照保存值执行。
- 网络status/maintain增加operation_error（默认空字符串），表示本次连接操作失败原因；internet_connected仍仅表示当前实际外网可达，二者不能互相替代。前端必须显示操作错误，不能因HTTP200或其他链路可上网而宣称认证成功。
- attempts仅含脱敏username/status_code/accepted/error_kind/message，删除原始响应preview。服务端错误文案先脱敏、去标记并限长；不公开密码、原始页面或认证请求URL。未知ret_code只显示代码，不猜IP许可范围。校园网摘要也仅用独立campus网络凭据，不读取教务密码。

# 个人后端 API 交接

## 2026-10-14 校园网自助摘要

`GET /api/network/summary`，使用现有 `X-XKE-Session`。返回 `{ok:true,msg:string,data:CampusSelfServiceSummary}`。会话缺失/失效401；后台未配置、连接失败或登录未完成仍为200，依靠data状态呈现，不阻塞status/maintain。

| 字段 | 类型 | 语义 |
| --- | --- | --- |
| configured | boolean | 有效自助后台endpoint已配置 |
| available | boolean | 有可展示白名单摘要（含保留的成功快照），不代表外网在线 |
| cached | boolean | 此次返回已有成功快照；未曾成功时false |
| updated_at | string或null | 最后成功抓取时间ISO8601；失败不更新 |
| message | string | 未配置/暂不可达/更新失败的简短提示；成功为空串 |
| account_status | string或null | 服务器账号状态 |
| plan_name | string或null | 服务器套餐名称 |
| billing_method | string或null | 服务器计费方式 |
| period_start / period_end | string或null | 服务器计费周期起止原文，无时区推测 |

成功快照内存缓存5分钟，账号/密码变化清空；失败保留同账号最后成功快照并带message，未成功则业务字段全null，下一次显式重试重新请求。前端单独请求summary，不与30秒status/后台保活耦合，不根据账号状态推导portal在线状态。没有账号数字、个人信息、余额、流量、IP等扩展字段。配置、实际协议和验收见07，桌面部署接入由主线程完成。

2026-10-13 本地默认学期修正：GET `/semester/local` 未指定学期时按学期ID选最近已保存学期，历史学期更晚抓取不抢占默认；显式学期仍严格匹配，同学期覆盖仍比较原抓取时间。

2026-10-10 数据闭环更新：新增 `GET /api/schedule/semester/local?academic_term=...`，纯本地、缺失404；学期响应增加 `cached`，缓存返回原获取时间。现有GET `/semester`保留为刷新入口，支持云协议源优先/直接JW回退。周快照与学期按获取时间覆盖同周，等时单周优先，失败不更新数据。具体运行说明见技术手册与部署手册。

更新时间：2026-10-07（Vue/runtime 集成已实现，隔离验证已完成；真实环境验收待执行）。共同分支：`refactor/personal-runtime`。代码/公共契约由后端负责人确认，当前文档负责人仅维护 notes；前端可只读引用，不覆盖 `frontend/`。本文区分已实现契约、完成的隔离流程验证与尚未完成的真实端到端/完整原生交互验收，不将 fake 验证扩大为真实验收。

## 当前接口约定

本地 HTTP 服务。桌面目前默认 loopback 38443，开发独立宿主可使用动态端口；runtime 使用同源或 `VITE_XKE_API_BASE_URL` 显式配置的 API base，不写死端口；变量名不是 `VITE_XKE_API_BASE`。会话头为 `X-XKE-Session`，后端兼容 `session_id` query，但新前端不把 session 放进 URL。

### 显式 preview 与 runtime

- 只有 `VITE_XKE_PREVIEW=true`（`dev:preview`）启用模拟数据/固定演示时钟；普通 runtime 不因后端不可用、绑定失败或业务错误切回 mock。
- runtime 的 session 仅存在前端内存；不将密码、session、trusted_login_token 写入 localStorage/sessionStorage 或日志。桌面可信恢复由 `/api/desktop/auth-bootstrap` 提供；首次无可信工作区时须认证，ready后进入主界面，注销返回认证页。设置中的连接操作按需打开账号/验证码面板；按钮表达操作，去掉说明性引导，不伪造成功。
- 注销/401 清理本次内存会话及受保护视图状态，不删除后端保存的账号/课表。离线本地恢复的 `connected=false` 不是登录失败，也不等于联网成功。
- UI 组件消费适配后的 view model，不直接消费 preview mock 或假设成功 payload 与 challenge 同形。

- 成功：`{ "ok": true, "msg": "...", "data": ... }`。
- 业务错误：`{ "ok": false, "msg": "...", "data": null, "detail": "..." }`，并使用对应 HTTP 状态。HTTP 绑定/静态资源等框架响应仍按 HTTP 状态处理。
- `GET /api/config` 直接包含 `prelogin_mode`、`development`、`defaults`；不套业务 envelope，不包含 remote/beta/user/zbid。
- `GET /api/health` 是宿主诊断对象，保留 status、sessions、web_root_found 等字段。

## 核心功能

| 操作 | 接口 | 行为 |
| --- | --- | --- |
| 密码登录 | POST /api/auth/login | 完整本地状态时离线恢复；新账号返回验证码 challenge |
| 强制重新初始化 | POST /api/auth/reinitialize | 启动 JW 验证码流程，失败保留已有账号/设置/课表 |
| 提交登录验证码 | POST /api/auth/captcha | challenge_id + captcha_code；成功返回 AuthPayload |
| 恢复会话 | GET /api/auth/session | 返回账号、设置、缓存；不隐式请求远端 |
| 本地可信登录 | POST /api/auth/trusted-login | user + trusted_login_token；只访问本地 |
| 恢复最近账号 | POST /api/desktop/auth-bootstrap | 桌面模式返回 user/auth；后台恢复不依赖先调用此接口 |
| 连接教务 | POST /api/auth/connect | 复用 JW 会话，否则返回登录 challenge；不调用旧移动接口 |
| 注销 | POST /api/auth/logout | 删除本次 session 并停用其后台任务，不删除已保存账号和课表 |
| 设置 | GET/POST /api/auth/preferences | 返回 settings；POST 用 appearance/schedule/network 等子对象更新 |
| 个人资料/头像 | POST /api/auth/profile、GET/POST /api/auth/avatar | 本地昵称/头像维护；头像上传为 multipart |
| 桌面设置 | GET/POST /api/desktop/settings | launch_at_startup、silent_startup、close_to_tray 等机器设置 |
| 本地课表 | GET /api/schedule/local | 缓存读取；支持 week_label/source query |
| 手动刷新 | POST /api/schedule/preferred | 个人课表服务优先，失败回退 JW；成功仍返回统一课表，回退可返回 captcha_required |
| 课表验证码续跑 | POST /api/auth/jw-captcha/submit | 与登录验证码不同的续跑入口，必须带当前 session |
| 校园网配置 | GET/POST /api/network/config | settings/credentials；仅保存接入/保活/门户配置，不修改系统网卡 |
| 校园网检测 | GET /api/network/status | 执行用户请求的检测 |
| 手动保活 | POST /api/network/maintain | force=false 默认；与后台保活串行 |

旧 `POST /api/schedule/cookie` 保留为直接 JW 刷新入口，不查询个人课表服务。新前端不提供多协议源选择。

## 验证码流程

`data.status="captcha_required"` 时，包含 challenge_id、captcha_image（data URL）、expires_in_seconds。验证码是短时运行期状态，不持久化图片、Cookie 或 challenge 材料。

| 流程 | challenge 来源 | 提交入口 | 会话/成功处理 |
| --- | --- | --- | --- |
| 登录/重新初始化/显式连接 | `/api/auth/login`、`/api/auth/reinitialize`、`/api/auth/connect` | `/api/auth/captcha`，challenge_id + captcha_code | 新账号验证码完成后取得 AuthPayload；已有账号失败保留原工作区 |
| 课表刷新续跑 | `/api/schedule/preferred` 或直接 JW 刷新 | `/api/auth/jw-captcha/submit`，challenge_id + captcha_code | 必须带当前 session；处理实际续跑响应中的课表，不当作首次登录成功 |

两种流程不能混用；前端须记住本次 challenge 的用途，显示后端图片与 TTL，处理错误、过期、取消和重复提交。不用固定演示验证码替代 runtime，取消/刷新失败不得清空已有本地课表。后台自动刷新不创建隐藏验证码。

## 可自定义设置

| 字段 | 范围/含义 |
| --- | --- |
| appearance.theme | 任意非空主题标识，由前端解释；不把未知主题覆盖成默认主题 |
| ui.show_weekend | boolean，默认 true；账号 UI 偏好 |
| ui.inactive_course_mode | `dim` 或 `hide`，默认 `dim`（2026-10-11）；账号 UI 偏好。hide仅隐藏当前周已结束课程，历史周保留 |
| schedule.auto_refresh_enabled/mode | mode 为 daily 或 interval |
| schedule.auto_refresh_daily_time | HH:mm，本机时区 |
| schedule.auto_refresh_interval_hours | 1～8760 小时整数 |
| schedule.lesson_reminder_minutes | 1～1440 分钟整数，仅保留设置；提醒发送本轮尚未实现 |
| network.heartbeat_enabled | 启用后由 C# 后台执行 |
| network.heartbeat_interval_minutes | 1～1440 分钟整数 |
| network.portal_url | HTTP/HTTPS 门户地址，默认沿用当前校园网，无内嵌用户凭据 |
| network.connectivity_check_urls | 至少一个有效 HTTP/HTTPS 检测地址；无效地址过滤、重复地址去重 |

无效数值保留已有设置，不套用旧固定列表。有效时间和地址边界仍需检查。隔离浏览器验证确认 `ui.show_weekend=false` 在页面重载后保留；字段默认值/枚举由后端确认。此证据不等于真实账号或 Windows 环境验收。

### 账号设置、桌面设置与 patch 语义

- `/api/auth/preferences` 是当前账号的 settings：`appearance`、`schedule`、`network`、`ui` 等嵌套子对象。UI 的扁平表单仅是 view model，不按原样 POST。
- `/api/desktop/settings` 是机器级 `launch_at_startup`、`silent_startup`、`close_to_tray`，不是账号 preferences，不混进同一个设置请求。普通 WebHost 的支持/返回结果与实际 Windows 宿主区分；成功保存不自动证明自启动注册或托盘行为已真实验收。
- 两者 POST 按 patch 使用：只发送变更字段；未提供字段应保留已有值，不用表单默认值/完整空对象覆盖无关设置。显式 `false` 是有效修改，不可因 falsy 丢弃。前端使用响应中的规范化值回填。
- `null`/空字符串不等于所有字段的统一重置命令。清空网络地址、账号、密码等须服从该字段的明确后端行为；只改保活开关/间隔时不附带空凭据，不把未读取的密码提交为空。清凭据能力不能从 preview 或缺字段推断为已支持，当前片段 patch/清空回归由后端确认。
- 分别处理账号/桌面/网络配置保存错误（手动IP与apply_result已于2026-10-15移除），不能因为一个请求成功就显示所有设置已保存；未完成部分保留用户编辑状态与失败提示。后台提醒发送未实现，不显示已生效的承诺。

## 后台状态

- `GET /api/network/maintenance`：data.enabled、next_run_at、last_run_at、last_message、last_error。
- `GET /api/schedule/refresh-status`：data.enabled、next_run_at、last_run_at、requires_login、last_error。

均需要 session。后台每 5 秒读取一次当前设置，到期执行。注销、关闭设置或切换账号在下一轮更新调度。课表后台优先读取个人课表快照，不依赖本机 JW 会话；服务不可用时只复用有效 JW 会话，失效时保留缓存、标记 requires_login，不发起隐藏验证码。界面只展示状态，不自行启动保活或刷新任务。

## 已移除与实际限制

公告、遥测、反馈、云策略、选课、自动更新路由返回 404，旧数据不清理。

旧移动教务代码与测试开关已退役；当前课表提醒由 C# 后台执行。学校真实联通性、本机托盘/自启动和发布产物未进行本轮真实验收，不能由模拟测试或隔离 HTTP 测试替代。当前 backend/frontend/guard 与隔离 browser 验证证据及限制见本文末尾；不能扩大为完整鼠标、键盘、视觉或真实环境验收。


## 课表多链路统一输出

个人服务与直接 JW 的成功结果共用后端规范化、存储及课表响应模型，前端只读取 rows、week_dates、available_weeks、term、student 等既有字段，不解析服务原始 JSON。验证码 challenge 是原有交互状态，与课表成功数据分开。

`retrieval_channel` 为来源元数据（personal_api / direct_jw），不决定渲染结构；`source_key` 仍统一为 jw_schedule。`updated_at` 保存实际快照抓取时间，下载不把旧数据标成今天的新数据；较新的本地同周同学期缓存保留。

服务配置、地址、鉴权和使用手册只在 ignored 的私有目录维护，不通过 /api/config 或课表 payload 交给前端。本接口读取不发送教务密码；首次本地账号初始化仍由登录功能负责，快照 API 不承担学校密码验证。真实服务仍待部署/配置与联调。

## 真实模型到 UI 的适配约定

| 真实后端模型 | UI 适配要求 |
| --- | --- |
| AuthPayload `student` | `name` → 学生姓名，`academy_name` → 院系，`major_name` → 专业，`training_level` → 培养层次；不要直接假设后端返回 preview 的 student_name/college/major |
| AuthPayload `settings`、`cached_schedule`、`connected` | settings 是嵌套账号设置；cached_schedule 按实际课表模型适配；本地已登录和远端已连接分开展示 |
| Schedule `rows` | 从 course_name、weekday、section、time_range/block_time、weeks、room、teacher、credit 等真实字段构建课程项，不期待后端直接返回 preview courses；未知或缺失值不填样例教师/学分/考核 |
| Schedule `week_dates`、`block_times`、`available_weeks` | 周日期、节次时间、可选周来自实际 payload；区分 viewed/current week，不用固定演示时钟；空周为有效界面状态 |
| Schedule `term`、`student`、`cached`、`updated_at`、`data_origin` | 显示实际学期/账号/缓存/抓取时间；不可把读取缓存时间当新抓取时间，缓存失败不伪造成功 |
| NetworkStatus `campus_connected`、`internet_connected`、`interface`、`settings`、`message` | 校园内网/外网连通分别展示，IP 从真实 interface 提取，门户来自配置；不直接套 preview online/ip_address |
| Network maintenance 与 Schedule refresh-status | enabled、next_run_at、last_run_at、last_error/requires_login 等是后台执行状态，和用户主动检测结果不同；可有限状态轮询，不启动业务调度 |

映射属于类型化调用/模型层，不把业务协议散落到 UI 组件；真实 payload 与 challenge 应按形状显式区分，失败显示业务/HTTP 错误，不用演示 payload 掩盖不匹配。

## Vite 静态资源、发布模式与验证状态

- 前端资源以 `frontend/dist/index.html` 及其中引用的 `assets/` 为准，health 检查实际入口/资源，不依赖旧 web 的 app.js/profile.js/http.js 固定清单。协调方验证 runtime marker 的 build 与 `/api/health` 的 `frontend_build` 相同，两份 emitted Vite assets 均 HTTP 200 并列入 `static_asset_hashes`；生产 JS 无 `DEMO-0001`/`7k8x`。build id 随构建变化，不在文档固化。资源响应仍应检查内容/MIME，避免 SPA fallback 的 200 假阳性。
- 产物模式标记 `frontend-build.json`：`{build, mode: runtime | preview}`。默认 `frontend/dist` 必须有 runtime 标记；显式自定义旧资源可兼容无标记/旧元数据，但显式 preview、畸形/不支持/非对象元数据拒绝打包。runtime 与显式 preview build 均通过，最终恢复 runtime 产物，排除 demo chunk/固定验证码；这不是发布包验收。
- 协调方已回传打包门禁：PS7 语法检查通过、11 个隔离模式/元数据 fixture 通过；实际脚本对临时 preview root 在创建 output 前拒绝，没有 publish。
- 协调方已回传后端验证：affected 29/29；solution Contracts 11 + SecurityStorage 13 + WebHost 128 = 152/152，0 skipped；含 DesktopHost 的 solution build 0 警告/0 错误。
- `tests/Xke.FrontendIntegrationHost` 为独立隔离验证 executable：实际 WebHost、fake 外部教务/网络/Windows 调用、临时根，不是生产服务。build 0/0；fixture HTTP 登录成功、课表验证码错误 400/重试 200、刷新失败 503、graceful stop 202 已回传；协调方另确认停止后进程 exit 0、临时根已清理。
- 协调方最终回传：前端 35/35 tests；严格 vue-tsc + Vite runtime build 通过；显式 preview build 通过，最终恢复 runtime，不包含 demo chunk/固定验证码。`tools/frontend-smoke.ps1 -SkipInstall` 通过 35 项测试 + build，不再以旧 web smoke 为本轮入口。

### 隔离浏览器流程实测（实际 WebHost，fake 外部服务）

协调方回传以下实际页面事件流程；文档负责人未重跑，不将 fake 数据/内存系统替身当作真实学校或 Windows 调用：

- 登录并提交真实接口返回的 challenge 后显示 3 条实际 DTO rows，包括不规则 2–4 节与未知字段保留；页面重载恢复账号；数字周次选项标签已修正。
- 手动刷新返回课表 challenge；过期后提交按钮禁用。取消后再刷新得到 503，显示错误并保留缓存课程。选择第 9 周得到实际 404，不展示其他周课表；返回第 8 周恢复课程。
- `ui.show_weekend=false` 重载后保留；桌面 startup=true 重载后保留，但 registrar 是内存替身，未更改真实 Windows 自启动。
- 网络面板打开即检测及手动 maintain 后仍显示 offline，不假装联网成功。注销清理课程与用户并启用登录。

### 操作方式、覆盖与真实验收限制

- IAB 点击/截图因 viewport 不一致间歇超时；设置 1440×900 后恢复截图和一次鼠标点击。后续使用 locator.evaluate(el => el.click()) 触发实际页面事件，没有直接覆盖内部状态。因此这些是隔离流程验证，不是完整鼠标、键盘或视觉验收。
- 错误验证码重试由 HTTP harness 与自动化状态测试验证。浏览器 challenge 因工具延迟在提交前过期，因此不能宣称浏览器实测了错误验证码重试。
- 当前结论：Vue/runtime 集成已实现，所述构建、自动化与隔离流程验证已完成；真实学校/个人课表服务、校园网、Windows/WebView2/托盘/自启动与发布包/发布仍未验收，未真实发布。历史测试计数继续保留为历史切片记录。

## 私有服务对齐状态

服务读取的是单人当前周缓存，不触发教务登录；后台只读拉取，不调用服务器同步端点。实际传输采用受控 HTTPS 证书校验与请求头令牌，这些私有配置不会出现在前端配置或课表结果。

服务响应缺少账号/学期字段时，由私有配置显式绑定，不自动采用当前任意账号；周次、日期和实际抓取时间仍统一校验。真实只读验证已通过，但个人账号绑定尚未提供，正式来源未启用。

## 单人来源约定

用户已确认 API 快照仅服务本人。私有 student_id 可留空，无需前端增加学号绑定步骤；快照归入请求使用的本地账号，student 等资料仍由本地账号提供。显式配置或响应中的身份冲突会回退 JW，其他周次/日期/新鲜度规则保持。此项取代之前强制绑定的描述。

## 周次显示标识修正（2026-10-07）

明确的数字/中文周标签统一为 `第N周`，包括 payload.week_label、current_week_label、课程行 week_label 和 available_weeks[].label。available_weeks[].value 保留教务查询值（可能是周一 ISO 日期），前端适配器应以 label 解析周次，不把该查询日期当作课程周号。

旧缓存缺少 week_dates 时，仅从与所显示周标签一致的 option.value 周一日期补齐七天；不从别的周或机器今天推测。真正不同周的结果仍不可冒充用户选择的周。


## 2026-10-08 新数据契约（后端已实现，React未接入）

### 用户基本资料

student 新增 program_duration_years（整数或 null）、enrollment_date（来源可识别时 yyyy-MM-dd，否则保留字符串）；学习层次读取学校“学习层次”字段。student_id/name/academy_name/major_name/class_name/training_level来自本人区域；昵称为本地设置。校区无明确值则空，不根据课程地点推断。

### 课表条目

rows新增 section_numbers（整数数组）、week_numbers（整数数组）、time_segments（字符串数组）、hours_text（原文）及 hours_variants（不同原文数组）。同一安排只一行，完整节次保留，时间段直接来自各大节。small_section_1/2对仅有大节时间的来源为空，break_minutes为0；不得继续以其缺失视作解析失败。不同地点不被合成单一房间冲突对象。

### 学期接口

GET /api/schedule/semester?academic_term=<TERM>，使用既有 session 传递方式；缺学期取学校当前学期。返回 term、student、rows、block_times、available_terms、notes、supplementary_courses、updated_at、saved_files。使用直接 JW 来源，不用仅提供当前周的个人快照服务。需已连接的 JW 会话，401不产生验证码；源请求均为GET。获取成功会更新本地学期快照，不修改学校数据。

原 GET /api/schedule/local 新增可选 academic_term，显式指定时只查该学期；不以其它学期同周缓存替代。指定历史学期的单周请求要有 manual_week；历史日期没有来源时保持缺失。

### 文件约定

profile.json 和 preferences.json 为普通 JSON，不做旧加密格式读取/迁移。学期输出 students/<student_id>/schedules/<academic_term>/semester/semester_schedule.json 与 semester_schedule.csv；按周仍放同学期 weeks/<week>/。CSV列为 student_id,academic_term,week_label,weekday,course_name,teacher,room,section_numbers,week_numbers,time_segments,weeks,credit,hours_text；数组以分号连接，保留源周次/未解释学时原文。CSV由统一结果生成，不支持编辑后回写。

密码、Cookie、运行会话的既有 DPAPI保护未在本轮改动。没有提供传入 Cookie 的公共本地 API，也没有为旧文件新增兼容接口。前端不实现认证续期、采集或后台扫描。


## 2026-10-10 第一版React对接验收

当前React已实现HTTP/DTO适配、内存session、首登/连接/两类验证码/注销、学期和最新单周覆盖、设置patch、校园网与现有桌面桥接。当前学期常规单周请求保留目录和日期；历史学期明确manual_week，不从历史缓存回填本周状态。

前端30/30定向测试、严格类型检查与三种构建通过，lint 0 errors/9 warnings。主负责人真实隔离WebHost HTTP 1/1通过，资源200/health标记一致。未做浏览器UI、真实学校密码登录、校园网、Windows常驻或发布验收。独立ProtocolHost鉴权/路由不同，不能直接当React API base，见[部署手册](06_protocol_host.md)。
