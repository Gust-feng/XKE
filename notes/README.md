# XKE.DotNet8 notes

本目录是 `XKE.DotNet8` 重构开发文档区。根目录只保留此 README，所有开发文档必须放入对应子目录。

## 目录职责

| 目录 | 内容 | 作用 | 更新时机 |
| --- | --- | --- | --- |
| [doc-entry](./doc-entry/00_overview.md) | 文档入口、维护记录 | 给后续开发者和 AI 明确阅读顺序、术语、文档治理规则 | 每次新增、移动、归档文档时更新 |
| [development-docs](./development-docs/README.md) | 开发文档 / 开发项目 / 具体内容 | 承载可执行开发项目文档，当前项目为传统教务 Cookie 课表接入 | 每次开始、完成或调整开发项目时更新 |
| [roadmap](./roadmap/00_development_direction.md) | 后续开发方向、阶段总结 | 决定当前主线、已完成切片、下一阶段优先级 | 每次切片完成、主线调整、验收口径变化时更新 |
| [review-issues](./review-issues/00_current_issues.md) | 当前问题手册、审查结论 | 记录仍需修复的问题、暂缓事项、审查排序 | 每次代码审查、问题关闭、优先级变化时更新 |
| [future-plans](./future-plans/00_traditional_jw_manual_login.md) | 后置功能计划 | 保存 Course 验证码、OCR、真实抢课等 future-only 边界 | 只在后置边界变化或准备重启该切片时更新 |
| [core-index](./core-index/00_overview.md) | 运行时、API、领域、前端复用、文档入口索引 | 快速定位系统结构和主要入口 | 核心模块边界或入口路径变化时更新 |
| [api-parity](./api-parity/00_overview.md) | 契约冻结、legacy 数据模型、notice 兼容、WebHost 中间件记录 | 保存迁移兼容过程和契约对齐依据 | API 契约、兼容策略、notice 机制变化时更新 |
| [module-boundaries](./module-boundaries/01_module_map.md) | 模块边界草图 | 说明后端模块职责，防止职责回流到巨型入口 | 模块拆分、依赖方向、服务边界变化时更新 |
| [sensitive-index](./sensitive-index/00_overview.md) | 敏感边界、路径清单、移动教务/教务系统资料索引 | 记录敏感输入、运行数据、两套教务链路的边界 | 认证材料、数据落盘、真实链路资料变化时更新 |

## 外部文档如何使用

- `Z:/XKE/docs/项目说明/` 是产品/用户说明，只作为用户视角参考，不迁入 notes 正文。
- `Z:/XKE/重构计划/` 是旧规划和事实冻结资料，只允许提炼仍适用于 `.NET` 重构的结论。
- `Z:/XKE/重构计划/90_受限资料/` 只能做路径级索引，不复制真实账号、密码、token、Cookie、`.env`、录包或原始响应。
- `Z:/XKE/reference-archive/` 是历史归档，只作离线参考，不作为活动决策源。
- 外部引用细则见 [doc-entry/02_external_reference_index.md](./doc-entry/02_external_reference_index.md) 与 [sensitive-index/03_external_reference_rules.md](./sensitive-index/03_external_reference_rules.md)。

## 当前阅读顺序

1. [doc-entry/00_overview.md](./doc-entry/00_overview.md)
2. [development-docs/README.md](./development-docs/README.md)
3. [development-docs/traditional-jw-cookie-schedule/README.md](./development-docs/traditional-jw-cookie-schedule/README.md)
4. [development-docs/traditional-jw-cookie-schedule/00_current_focus.md](./development-docs/traditional-jw-cookie-schedule/00_current_focus.md)
5. [roadmap/00_development_direction.md](./roadmap/00_development_direction.md)
6. [review-issues/00_current_issues.md](./review-issues/00_current_issues.md)
7. [review-issues/01_review_findings.md](./review-issues/01_review_findings.md)
8. [sensitive-index/教务系统/01_接入资料索引.md](./sensitive-index/教务系统/01_接入资料索引.md)

## 术语边界

- **移动教务**：当前主链路，负责身份与课表数据源。本地可信状态、首次初始化、课表维护默认都按移动教务理解。
- **教务系统（传统）**：验证码/OCR/Cookie/选课真实执行等后置链路。当前只能出现在 `future-plans`、`sensitive-index/教务系统` 或 Course deferred 边界中。
- 后续文档不得把移动教务的身份/课表链路写成传统教务连接状态。

## 更新规则

- 每次完成后端切片、修复关键边界、调整 deferred 策略，都必须同步更新 `roadmap` 与 `review-issues`。
- 每次推进当前开发项目，都必须同步更新对应 `development-docs/<project>/00_current_focus.md` 或 `development-docs/<project>/02_task_board.md`。
- 每次新增、移动、归档 notes 文档，都必须同步更新本 README 与 `doc-entry/01_notes_maintenance.md`。
- 每次从外部文档提炼结论，都必须同步检查 `doc-entry/02_external_reference_index.md` 与 `sensitive-index/03_external_reference_rules.md`。
- 每次触及移动教务或传统教务资料，必须同步检查 `sensitive-index/移动教务` 与 `sensitive-index/教务系统` 是否需要更新。
- 不在根目录新增散文件；临时分析材料先判断是否进入对应子目录，若只具备历史价值则进入 `reference-archive/notes`。

## 归档入口

- [reference-archive/notes/notes-archive-index.md](../../reference-archive/notes/notes-archive-index.md)
