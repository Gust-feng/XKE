# 当前方向（2026-10-10）

用户确认项目从未使用，现第七版合成设计锚定产品第一版，授权子智能体完成真实React/C#对接及独立云协议版。

当前主线为稳定轻量的个人数据链路：默认学校表单登录 → 资料/学期/周GET → 规范化JSON/CSV → 桌面runtime。ProtocolHost共享同一Core，ddddocr本机识别，不复活旧云同步/选课/遥测。

同日后续完成数据闭环：学期缓存先读、最新获取时间决定同周安排、共享转换入口、桌面protocol消费及JW回退。既有表单登录协议按用户确认的可用基线保持。云自动重登/定时采集与真实Windows/Linux验收继续分开推进。

2026-10-11修复实机登录阻断：旧开发业务文件不可恢复时回到学校验证码认证，不引入旧数据兼容/迁移；认证成功重建普通JSON，失败回滚。38项定向及桌面构建通过，真实验证码获取已确认；下一步由用户完成个人账号认证，不将验证码可达等同完整登录验收。

同日用户已进入真实课表；本周/历史空白确认为显示过滤而非采集遗漏。默认淡化保留已结束课程，隐藏只限当前周，历史可回看；52项定向及桌面构建通过。后续以真实使用暴露的链路问题为优先，避免把视觉空白直接误判为数据缺失。

接手入口：[技术手册](../development-docs/personal-runtime/05_backend_technical_manual.md)、[部署手册](../development-docs/personal-runtime/06_protocol_host.md)。本轮完成证据看两项目任务板；后续优先真实学校/OCR、Linux与Windows宿主验收，之后再决定自动重登录/定时云采集。不迁移旧业务数据、不删除旧目录。下方为历史阶段。

---

> 2026-10-09 当前前端：第七版「合成」为唯一正式设计基线，其他设计资产独立退出工程；统一假数据，当前仍为 mock，真实 React/C# 集成待完成。下方 Vue/旧 TS 验证属于历史阶段。执行入口见 frontend-rebuild 与 frontend/docs/backend-handoff.md。

# 2026-10-07 当前产品范围

当前活动后端为个人本地运行时：登录、课表、设置、校园网及 Windows 常驻宿主。公告、遥测、反馈、云策略、选课和自动更新已退出装配，历史资料保留但不参与当前决策。最新执行状态见 [个人运行时](../development-docs/personal-runtime/README.md)。

## 当前优先级：Vue/runtime 集成已实现，隔离验证已完成

2026-10-07：`frontend/` 已有 Vue 3 + TypeScript + Vite 界面，已完成获批集成与所述隔离验证，不再等待设计。已对齐显式 preview/runtime、内存 session、登录/课表两套验证码、真实模型、账号/机器设置 patch、UI 偏好和 Vite assets/health；C# 继续独占课表刷新与校园网保活调度。执行入口：[personal-runtime](../development-docs/personal-runtime/README.md)、[frontend-rebuild](../development-docs/frontend-rebuild/README.md)、[API 交接](../development-docs/personal-runtime/03_api_contract.md)。

协调方回传后端 152/152（0 skipped）、build 0/0、隔离 HTTP 与打包 preview 门禁证据；最终前端 35/35、严格 vue-tsc/runtime build、preview build 后恢复 runtime、smoke -SkipInstall 35 tests + build、health marker/hash/两份 assets HTTP 200 与隔离 browser 流程已回传。IAB 间歇超时后使用页面事件点击，不是完整鼠标/键盘/视觉验收；验证码重试仅 HTTP/状态测试覆盖，浏览器因 challenge 过期未验证重试。发布门禁与隔离 WebHost 测试不是产品发布或真实验收。真实学校/个人服务、校园网、Windows/WebView2/托盘/自启动与发布包仍 pending。

---

以下是历史阶段记录，包括较早同日“未裁剪/前端计时器/不推进 UI”的口径；与当前产品范围冲突时，以个人运行时代码及当前开发文档为准，不将历史术语/测试结果当成当前状态。

# XKE.DotNet8 后续开发方向图

## 相关文件

- [01_phase_summary.md](./01_phase_summary.md)：当前阶段总结和已完成切片。
- [02_migration_plan_extract.md](./02_migration_plan_extract.md)：旧迁移计划中仍适用的阶段顺序和验收口径。

## 一、开发主线边界

- 主链路：`JW 表单协议` + 本地可信状态；旧移动实现仅作为测试兼容 seam，不参与生产运行。
  - 目标：首次联网初始化、资料入库、课表入库、Notice/设置入库，后续以本地 session/trusted-login 恢复。
  - 约束：验证码只存在于短时运行期 challenge，不持久化 Cookie 或验证码材料。
- 后置链路：`教务系统（传统 JW）` + OCR + 抢课 + Cookie 刷新。
  - 目标：保持 Course 面板内独立占位和 deferred 语义，不影响当前主线。
- 前端策略：本阶段保留现有 API envelope 与字段兼容，不新增前端架构拆分。
- 桌面更新策略：一键自动更新进入准备切片，优先做用户触发的下载、校验、独立 updater 替换和重启；不做后台静默强更。

## 二、现状状态（今日已固定）

- 已稳定：
  - `DesktopHost`、`Auth`（移动教务）、`Notice`、`Schedule`、`Network`、`Telemetry`、`Feedback`、`Course`框架边界。
  - `Notice` 正在向 `CloudSync` 收敛：公告、远程策略、更新信息绑定为单包拉取，遥感走本地 spool + 云端上传。
  - `Feedback` 已从纯本地 outbox 扩展为云端优先上传，失败时写入本地加密 outbox。
- 关键行为约束：
  - 首次初始化必须完整拿到身份与非空课表才算成功。
  - 已有本地凭据只做本地校验，不触发远端重置式登录。
  - 远端刷新失败不清空 trusted session/local cache。
  - Course 抢课、`Cookie`、传统验证码保持 deferred。

## 三、统一术语表（必须写进代码注释/文档）

- `移动教务`：Mobile JW。用于 `auth/login`、`schedule`、profile、notice 的主数据来源。
- `教务系统`：传统教务门户。当前仅为未来 `Course` 真实连通和验证码/OCR 链路预留。
- `本地可信状态`：本机 DPAPI 凭据 + 本地 cache 可恢复工作区，不等价于每次远端重新登录。

## 四、下一阶段迭代（建议按这个顺序）

1. 后端结构收敛第一层（P0）
   - 完成 `CloudSync` 单包拉取、`.cloud` 缓存、远程策略全局拦截和遥感本地 spool。
   - 完成 `Feedback` 云端优先上传与本地加密兜底。
   - 完成 `AuthService` 首登异常回滚闭环，防止半初始化残留。
   - 完善本地时钟注入和 `ScheduleStore` 周次边界测试。
   - 固化 `active root` 与 legacy 兼容层只读/引用边界。
2. 测试结构收敛（P1）
   - 统一 WebHost 测试启动/取样逻辑。
   - 引入 `Xke.Core.Tests`，抽离 normalize/parser/decision 逻辑到纯逻辑测试。
3. 生产化工程化（P2）
   - `XkeWebHostApplication` 分离服务注册、session/context、blocked policy、error envelope。
   - 统一 deferred 边界注释，清理误导性占位支路。
   - 建立一键自动更新第一阶段：扩展 `update_info`、新增 `.updates` 存储、`Xke.Core.Update`、`/api/update/*` 和 `Xke.Updater`。
4. 收尾与复核（P2）
   - 更新 notes 一致性清单、保留活动入口与归档材料划分。
   - 继续通过真实移动教务 smoke 进行格式回归。

## 五、退出/验收条件

- 相关后端门禁保持持续通过：build / test / smoke。
- `roadmap/01_phase_summary.md`、`review-issues/00_current_issues.md` 与 `review-issues/01_review_findings.md` 口径一致。
- 传统验证码与教务系统相关功能只在 `Course` 中出现，不再出现在主登录卡片。

## 2026-10-07 工程整理与产品范围确认

- 当前仓库根为 `Z:\XKE`，保留既有 Git 关系，当前业务模块尚未裁剪。
- 后续产品核心确认为登录、课表、必要设置、校园网及 Windows 常驻/自启动。全面精简与前端优化另行确定范围，本轮仅整理本地目录。
- 当前 JW 表单协议登录/课表以代码和对应开发文档为准；上文的移动教务主线与纯后置验证码口径属于历史阶段说明。
- 校园网定时保活仍依赖前端计时器，静默启动跳过 WebView 加载；后续常驻调度需要独立于界面生命周期。
- 执行记录见 [workspace-consolidation/README.md](../development-docs/workspace-consolidation/README.md)。


## 当前提交边界

2026-10-07 用户授权保存 C# 后端及必要验证/文档的本地基线；旧前端有意保留本地，不推进 UI 重构。独立 Python 验证工具不作为 C# 产品依赖。默认 JW 首登端到端验收与常驻后台调度仍需后续切片。


## 2026-10-08 后端数据获取方向

正式采集采用现有 C# HTTP 协议与软件自己的会话，浏览器只用于研究，不成为产品依赖。已观察到学期课表 GET/POST 的完整周规则来源，不必默认逐周扫描。先修实际响应/提取问题，再形成学生→学期元数据/排课规则→按周物化结果；JSON 为权威数据、CSV 为导出。具体证据与纯 HTTP 验收条件见 [JW 研究第10、11节](../development-docs/personal-runtime/04_jw_protocol_research.md#11-正式采集的边界与稳定性要求)。尚未改生产实现或迁移文件。


## 2026-10-08 数据提取实现后的方向

第一轮已落地固定学校纯 HTTP/DOM 数据源、普通业务 JSON和新 CSV、用户基本资料及学期快照；不做旧数据兼容/迁移。当前优先修规范化数据的使用与跨天状态，再交接 React 真实接入；Cookie真实 GET已验证，但密码/验证码、长期续期、Windows产品运行和发布仍分开验收。详见 personal-runtime/03_api_contract.md 与研究第12节。
