# notes 活动入口

## 2026-10-17 仓库整理与提交准备

当前 React/C# 第一版、后台提醒、固定 GitHub/Velopack 更新及直接启动窗口均已有实现。正在按实际工程边界整理公开源码：压缩包、编辑器计划、缓存、运行数据与私有配置只保留本地；旧目录和历史过程不删除。提交批次以 personal-runtime/01_implementation_steps.md 顶部为准，精确本地清单放 ignored artifacts/repository-preparation/20261017。整理阶段未暂存或提交；后续已获用户授权按清单分批本地commit，仍不推送或发布，也不顺带实施业务重构。

## 2026-10-17 V1.0.0 发布与自动更新

最新授权与操作手册见[08_release_and_updates.md](../development-docs/personal-runtime/08_release_and_updates.md)：公开GitHub来源、Velopack客户端、版本1.0.0、自动下载/退出应用、严格无个人数据打包。旧自动更新/遥测退役记录继续保留为历史，不等于恢复旧云模块；GitHub公开发布仍需正确源码提交及明确推送授权。

## 2026-10-16 当前状态入口

整体可靠性、课前提醒与后台更新APP-01～05的当前实现/唯一待验收摘要见[个人运行时任务板](../development-docs/personal-runtime/02_task_board.md)顶部。下方旧日期待办仅作历史，不重复将已修NET或未接提醒作为现状；提醒回执为普通业务日期/哈希，不含凭据。

2026-10-12当前接手入口：[后端技术手册](../development-docs/personal-runtime/05_backend_technical_manual.md)、[校园网自助后台协议](../development-docs/personal-runtime/07_self_service_protocol.md)
和[协议服务/ddddocr部署](../development-docs/personal-runtime/06_protocol_host.md)。先读当前手册和项目任务板，
再按本文查历史过程；产品第一版沿用第七版合成，真实React/C#已接入，实机/发布验收分开记录。

同日后续数据闭环见上述05/06及最新任务板：整学期离线读取、同周按时间覆盖、共享转换、protocol数据源消费。当前真实React已接入；下方“仍mock”属于历史阶段。

> 下方2026-10-09及更早的mock/旧集成状态为历史记录，不覆盖本轮手册与任务板。

`Z:/XKE/notes` 按开发用途组织目录。根目录 README 负责说明每个文件夹的内容和功能；本文件负责给出活动阅读顺序与维护约束。

## 当前执行入口

先读 [个人运行时](../development-docs/personal-runtime/README.md)、[前端重做](../development-docs/frontend-rebuild/README.md) 各四份执行文档及 [API 交接](../development-docs/personal-runtime/03_api_contract.md)。当前React第一版已接真实C# API，最终demo为视觉基线，preview仍显式隔离；旧Vue/mock过程仅作历史，当前实现与实机/发布验收以两项目任务板顶部为准。旧云模块开发文档保留历史角色，不作为当前实现要求。

## 当前结构整理

当前活动仓库根为 `Z:\XKE`。[workspace-consolidation/README.md](../development-docs/workspace-consolidation/README.md) 保存已完成的结构整理和旧目录清理记录，按需查阅，不再排在当前集成项目之前。

## 活跃文档（按优先级阅读）

1. [development-docs/README.md](../development-docs/README.md)。
2. 个人运行时：[README](../development-docs/personal-runtime/README.md)、[当前焦点](../development-docs/personal-runtime/00_current_focus.md)、[实施步骤](../development-docs/personal-runtime/01_implementation_steps.md)、[任务板](../development-docs/personal-runtime/02_task_board.md)。
3. 前端重做：[README](../development-docs/frontend-rebuild/README.md)、[当前焦点](../development-docs/frontend-rebuild/00_current_focus.md)、[实施步骤](../development-docs/frontend-rebuild/01_implementation_steps.md)、[任务板](../development-docs/frontend-rebuild/02_task_board.md)。
4. [API 交接](../development-docs/personal-runtime/03_api_contract.md) 与 [校园网自助后台协议](../development-docs/personal-runtime/07_self_service_protocol.md)。
5. [当前方向](../roadmap/00_development_direction.md)、[当前问题](../review-issues/00_current_issues.md)；审查同时读 [审查发现](../review-issues/01_review_findings.md)。
6. 涉及认证、运行数据或私有服务时先读 [敏感总览](../sensitive-index/00_overview.md)、[边界规则](../sensitive-index/01_boundary_rules.md)、[教务声明](../sensitive-index/教务系统/00_声明.md)；旧移动兼容维护另读 [移动声明](../sensitive-index/移动教务/00_声明.md)。
7. [历史开发项目](../development-docs/README.md#历史开发项目) 和 [长期教务资料](../sensitive-index/教务系统/01_接入资料索引.md) 按需引用，保留原有过程和参考角色。

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

- **JW 表单协议（传统 Web 教务）**：默认联网登录、显式连接及直接课表刷新；登录与课表续跑分别使用验证码入口。
- **个人课表服务**：自动/手动课表的优先快照来源，失败回退直接 JW；不是密码校验或旧 CloudSync。
- **本地可信状态**：由 C# 受控存储恢复的本地账号和缓存，不等同远端连接成功；新前端 session 仅驻内存。
- **移动教务**：仅旧兼容测试入口。长期索引的旧主线表述保留历史语境，当前行为以 personal-runtime/API 为准；本机 OCR、选课及云模块不进入默认装配。

## 文档更新规则

- 活跃文件允许修改，但必须保持边界一致性：当前维护个人运行时与 React 第一版集成、正式发布边界，不重新接入退役能力。
- 历史项目文档和长期资料保留原位置与角色；用明确历史标记隔离过期口径，不随意删除或压缩。
- `future-plans/00_traditional_jw_manual_login.md` 保留历史后置讨论，不能据此把当前 JW 登录验证码认定为未实现；当前边界以 API 交接为准。
- 本文件中的优先级顺序是当前执行入口；任何新增文档先评估是否必须纳入活跃目录。
- 当前实际开发任务优先记录到 `development-docs/<project>/`，路线图只保留阶段方向。
- `README.md` 是目录职责清单，必须随文档结构变化同步更新。
- 外部 XKE 文档只允许引用或提炼，具体规则见 `02_external_reference_index.md`。

## 归档入口

- 历史 notes 索引保存在 `开发数据/历史工程/旧Python工程.zip` 内的 `reference-archive/notes/notes-archive-index.md`。


## 个人本地运行时

当前产品范围收敛见 [personal-runtime/README.md](../development-docs/personal-runtime/README.md)。在共同分支 `refactor/personal-runtime` 推进四个核心功能及 Windows 常驻宿主集成；真实学校、校园网与 Windows 验收待执行，旧云功能文档仅作历史参考。

## TypeScript 前端并行开发

前后端在 `Z:\XKE`、同一分支 `refactor/personal-runtime` 按目录开发。历史阶段 `frontend/` 已有 Vue 3 + TypeScript + Vite 界面与 runtime 集成，显式 preview/runtime、真实模型、内存 session、独立验证码、设置 patch 与 Vite health/assets 的所述隔离验证已完成；runtime 同源或 `VITE_XKE_API_BASE_URL`。页面事件点击不等于完整鼠标/键盘/视觉验收，错误验证码重试为 HTTP/自动化证据，真实学校/校园网/Windows 与发布仍未验收。入口：[frontend-rebuild](../development-docs/frontend-rebuild/README.md)。


个人课表多来源接入属于 `personal-runtime` 的课表能力，不恢复旧 CloudSync。真实地址、配置和使用手册只保留在 ignored 的私有目录，公开开发状态不包含服务操作资料。

历史阶段曾采用旧 web 界面一比一 TypeScript 迁移；该阶段与新 Vue 设计均已退出活动构建。最新进度见 frontend-rebuild，历史验证不能代替本次迁移验收。


## 2026-10-08 JW 研究入口

后端协议与数据提取研究先读 [04_jw_protocol_research.md](../development-docs/personal-runtime/04_jw_protocol_research.md)。已完成所述范围的代码/假样例研究及真实页面、请求观察；纯 C# HTTP 新来源实现与验收仍待完成。旧 SSO/移动主线索引只作历史参考。


## 2026-10-08 后端实现状态

最新执行入口为 personal-runtime/04_jw_protocol_research.md 第12节、03_api_contract.md 新数据契约、当前焦点和任务板。已完成第一轮后端实现与所述验证；无旧文件兼容/迁移，React接入和产品验收仍待做。
