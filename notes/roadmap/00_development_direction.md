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
