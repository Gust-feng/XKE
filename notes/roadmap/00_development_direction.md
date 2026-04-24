# XKE.DotNet8 后续开发方向图

## 相关文件

- [01_phase_summary.md](./01_phase_summary.md)：当前阶段总结和已完成切片。
- [02_migration_plan_extract.md](./02_migration_plan_extract.md)：旧迁移计划中仍适用的阶段顺序和验收口径。

## 一、开发主线边界

- 主链路：`移动教务` + 本地可信状态。
  - 目标：首次联网初始化、资料入库、课表入库、Notice/设置入库，后续以本地 session/trusted-login 恢复。
  - 约束：不把“传统教务验证码”引入主登录和本地恢复流程。
- 后置链路：`教务系统（传统 JW）` + OCR + 抢课 + Cookie 刷新。
  - 目标：保持 Course 面板内独立占位和 deferred 语义，不影响当前主线。
- 前端策略：本阶段保留现有 API envelope 与字段兼容，不新增前端架构拆分。

## 二、现状状态（今日已固定）

- 已稳定：
  - `DesktopHost`、`Auth`（移动教务）、`Notice`、`Schedule`、`Network`、`Telemetry`、`Feedback`、`Course`框架边界。
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
   - 完成 `AuthService` 首登异常回滚闭环，防止半初始化残留。
   - 完善本地时钟注入和 `ScheduleStore` 周次边界测试。
   - 固化 `active root` 与 legacy 兼容层只读/引用边界。
2. 测试结构收敛（P1）
   - 统一 WebHost 测试启动/取样逻辑。
   - 引入 `Xke.Core.Tests`，抽离 normalize/parser/decision 逻辑到纯逻辑测试。
3. 生产化工程化（P2）
   - `XkeWebHostApplication` 分离服务注册、session/context、blocked policy、error envelope。
   - 统一 deferred 边界注释，清理误导性占位支路。
4. 收尾与复核（P2）
   - 更新 notes 一致性清单、保留活动入口与归档材料划分。
   - 继续通过真实移动教务 smoke 进行格式回归。

## 五、退出/验收条件

- 相关后端门禁保持持续通过：build / test / smoke。
- `roadmap/01_phase_summary.md`、`review-issues/00_current_issues.md` 与 `review-issues/01_review_findings.md` 口径一致。
- 传统验证码与教务系统相关功能只在 `Course` 中出现，不再出现在主登录卡片。
