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
- 真实资料只放在仓库 ignored 的 `开发数据/`，新增资料目录和说明文档用中文命名；`development-docs/<project>/` 只保留开发说明和真实资料位置指引。
- 开发文档中的 URL、账号、token、云资源地址、发布桶路径、云函数地址必须使用占位符；如需真实值，只写“见 `开发数据/<中文目录>/...`”。
- 新增或移动开发项目时，必须同步更新 `../development-docs/README.md`、`../README.md` 和 `00_overview.md`。
- 根目录 `README.md` 是 notes 文件夹职责说明，不能退化成单纯跳转页；每次目录结构或文档职责变化都要同步更新。
- 外部 XKE 文档只允许引用或提炼；新增外部提炼文档时必须同步检查 `02_external_reference_index.md` 和 `../sensitive-index/03_external_reference_rules.md`。

## 2026-04-25 CloudSync 文档调整

- 新增 `../development-docs/cloud-sync/`，作为公告、远程策略、遥感本地队列的当前开发项目文档。
- CloudSync 文档必须留在 `notes/development-docs/cloud-sync/`，不能放在仓库根级 `development-docs/`。
- 旧父项目公告/遥感文档只作为字段参考，不再作为 .NET 重构目标设计。

## 2026-04-25 WinUI 3 原生 UI 预案

- 新增 `../future-plans/01_winui3_native_ui.md`，记录后续可能从 WebView2 前端迁移到 WinUI 3 原生桌面 UI 的方向。
- 该预案属于 future-only 边界，不改变当前 `WPF + WebView2 + WebHost + Web 前端` 的可用基线。
- 当前阶段仍以后端结构收敛、存储/API 契约稳定和现有前端兼容为优先级。

## 2026-04-25 一键自动更新开发项目

- 新增 `../development-docs/auto-update/`，把一键自动更新从讨论方案转为可执行开发切片。
- 第一阶段仅覆盖用户主动点击后的下载、校验、安装计划、独立 updater 替换和重启，不做后台静默强更。
- 更新临时文件必须写入 `.runtime/data/.updates/` 或覆盖数据根下，不能触碰学生数据、凭据和课表缓存。

## 2026-04-26 应用原生提醒迁移记录

- 在 `../future-plans/01_winui3_native_ui.md` 补充应用原生提醒边界。
- 明确当前课程提醒触发仍依赖 Web 前端运行，`Xke.DesktopHost` 只提供托盘气泡发送通道。
- 后续迁移目标是由 `DesktopHost/Core` 承担提醒设置读取、课表候选计算、定时触发、去重和发送记录；托盘和开机静默启动场景不能依赖 WebView 加载。

## 2026-04-26 反馈云上传开发项目

- 新增 `../development-docs/feedback-cloud/`，记录反馈云函数整理、客户端 transport、私有端点注入和本地兜底边界。
- 真实云函数地址、COS 配置和原始 ZIP 只保留在 ignored 的 `开发数据/` 下。
- 当前完成范围是提交时云端优先上传，失败写入本地加密 outbox；后台补传仍为后续项目。
