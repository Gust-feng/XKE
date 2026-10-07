# notes 活动入口

`XKE.DotNet8/notes` 已按开发用途收敛为目录。根目录 README 负责说明每个文件夹的内容和功能；本文件负责给出活动阅读顺序与维护约束。

## 当前结构整理

当前活动仓库根为 `Z:\XKE`。本轮先读 [workspace-consolidation/README.md](../development-docs/workspace-consolidation/README.md)；原有开发项目仍按以下入口查阅。本地结构整理和旧目录清理已完成，复核结果见该项目 README。

## 活跃文档（按优先级阅读）

1. [development-docs/README.md](../development-docs/README.md)
2. [cloud-sync/README.md](../development-docs/cloud-sync/README.md)
3. [cloud-sync/00_current_focus.md](../development-docs/cloud-sync/00_current_focus.md)
4. [feedback-cloud/README.md](../development-docs/feedback-cloud/README.md)
5. [feedback-cloud/00_current_focus.md](../development-docs/feedback-cloud/00_current_focus.md)
6. [auto-update/README.md](../development-docs/auto-update/README.md)
7. [auto-update/00_current_focus.md](../development-docs/auto-update/00_current_focus.md)
8. [traditional-jw-cookie-schedule/README.md](../development-docs/traditional-jw-cookie-schedule/README.md)
9. [00_current_focus.md](../development-docs/traditional-jw-cookie-schedule/00_current_focus.md)
10. [02_task_board.md](../development-docs/traditional-jw-cookie-schedule/02_task_board.md)
11. [00_development_direction.md](../roadmap/00_development_direction.md)
12. [00_current_issues.md](../review-issues/00_current_issues.md)
13. [01_接入资料索引.md](../sensitive-index/教务系统/01_接入资料索引.md)

## 本目录文件

- [00_overview.md](./00_overview.md)：活动入口和阅读顺序。
- [01_notes_maintenance.md](./01_notes_maintenance.md)：notes 维护规则和变更记录。
- [02_external_reference_index.md](./02_external_reference_index.md)：外部 XKE 文档引用规则。

## 目录分组

- `doc-entry/`：目录入口、文档治理说明、外部文档引用索引。
- `development-docs/`：按“开发文档 / 开发项目 / 具体内容”组织的可执行开发文档，入口见 `development-docs/README.md`。
- `roadmap/`：后续开发方向、阶段总结。
- `review-issues/`：当前问题手册、重构审查清单。
- `future-plans/`：Course 验证码、OCR、真实抢课、WinUI 3 原生 UI 等 future-only 计划。
- `core-index/`：运行时、API、领域、前端复用和文档入口索引。
- `api-parity/`：契约冻结、legacy 兼容、notice 兼容和 WebHost 迁移记录。
- `module-boundaries/`：后端模块职责与依赖方向草图。
- `sensitive-index/`：敏感边界、路径清单、移动教务与传统教务资料索引。

## 术语统一

- **移动教务**：当前主链路，负责身份与课表数据源。
- **教务系统（传统）**：验证码/OCR/Cookie/选课执行等传统逻辑，当前后置。
- 默认一切“课表、身份、本地恢复、自动刷新”相关问题优先按“移动教务”语义理解。

## 文档更新规则

- 活跃文件允许修改，但必须保持边界一致性：后端长期收敛优先、前端兼容保留。
- 冗余、重复或历史口径的内容应优先移入归档，不在 `notes` 根目录累积噪音。
- `future-plans/00_traditional_jw_manual_login.md` 只描述后置边界与入口，不影响主登录与自动化验收。
- 本文件中的优先级顺序是当前执行入口；任何新增文档先评估是否必须纳入活跃目录。
- 当前实际开发任务优先记录到 `development-docs/<project>/`，路线图只保留阶段方向。
- `README.md` 是目录职责清单，必须随文档结构变化同步更新。
- 外部 XKE 文档只允许引用或提炼，具体规则见 `02_external_reference_index.md`。

## 归档入口

- 历史 notes 索引保存在 `开发数据/历史工程/旧Python工程.zip` 内的 `reference-archive/notes/notes-archive-index.md`。
