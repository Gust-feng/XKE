# notes 优化记录

## 目标

- 保持 `notes` 只承载当前开发可直接执行的输入。
- 对后续开发保持**可追溯性**：什么是本轮决策、什么是历史沉淀。
- 避免 “移动教务” 与 “教务系统（传统）” 在文档中混用造成判断偏差。

## 本次优化结论

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
- 新增或移动开发项目时，必须同步更新 `../development-docs/README.md`、`../README.md` 和 `00_overview.md`。
- 根目录 `README.md` 是 notes 文件夹职责说明，不能退化成单纯跳转页；每次目录结构或文档职责变化都要同步更新。
- 外部 XKE 文档只允许引用或提炼；新增外部提炼文档时必须同步检查 `02_external_reference_index.md` 和 `../sensitive-index/03_external_reference_rules.md`。
