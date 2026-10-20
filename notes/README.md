# XKE.DotNet8 notes

## 本轮自动验证码验收入口

桌面已接入可信账号本地OCR（首次人工、后续可关闭、验证码错误最多三次）、动态设置状态及模型/CRT随包方案。当前结果与未验收边界以personal-runtime的最新任务板/API/技术手册为准；旧“桌面仅人工”“尚未接入”是历史阶段，不代表当前候选。2026-10-20用户已明确授权V1.0.2发布；必须先验证，再按必要范围提交并由Actions校验后公开。

## 2026-10-18 正式发布流程

本轮已获必要源码提交、main合并及GitHub Actions发布授权。当前发布入口为 [发布与更新手册](./development-docs/personal-runtime/08_release_and_updates.md) 顶部：先验证，再草稿上传及下载回验，完成后公开。旧日期的未授权/未发布记录保留历史语境；真实账号/缓存/私有配置不入源码或发布包。

## 2026-10-17 移动教务退役收尾

当前来源与验证见[技术手册](./development-docs/personal-runtime/05_backend_technical_manual.md)、[API契约](./development-docs/personal-runtime/03_api_contract.md)、[任务板](./development-docs/personal-runtime/02_task_board.md)顶部。旧移动实现/开关/缓存回退和顶层fixtures已退出；合成样本由对应测试项目管理。下方旧兼容入口及fixtures记录保留历史角色。

## 2026-10-17 仓库整理与提交准备

当前 React/C# 第一版、后台提醒、固定 GitHub/Velopack 更新及直接启动窗口均已有实现。正在按实际工程边界整理公开源码：压缩包、编辑器计划、缓存、运行数据与私有配置只保留本地；旧目录和历史过程不删除。提交批次以 personal-runtime/01_implementation_steps.md 顶部为准，精确本地清单放 ignored artifacts/repository-preparation/20261017。整理阶段未暂存或提交；后续已获用户授权按清单分批本地commit，仍不推送或发布，也不顺带实施业务重构。

## 2026-10-17 V1.0.0 发布与自动更新

最新授权与操作手册见[08_release_and_updates.md](./development-docs/personal-runtime/08_release_and_updates.md)：公开GitHub来源、Velopack客户端、版本1.0.0、自动下载/退出应用、严格无个人数据打包。旧自动更新/遥测退役记录继续保留为历史，不等于恢复旧云模块；GitHub公开发布仍需正确源码提交及明确推送授权。

## 2026-10-16 当前状态入口

整体可靠性、课前提醒与后台更新APP-01～05的当前实现/唯一待验收摘要见[个人运行时任务板](./development-docs/personal-runtime/02_task_board.md)顶部。下方旧日期待办仅作历史，不重复将已修NET或未接提醒作为现状；提醒回执为普通业务日期/哈希，不含凭据。

## 2026-10-10 快速接手

产品第一版沿用现第七版「合成」；已授权真实 React/C# 对接及独立云协议版，项目未投入使用、无旧业务数据兼容。
后续Agent先读 [后端技术手册](./development-docs/personal-runtime/05_backend_technical_manual.md)，
再读 [API契约](./development-docs/personal-runtime/03_api_contract.md)、[校园网自助后台协议](./development-docs/personal-runtime/07_self_service_protocol.md)、[协议服务与ddddocr部署](./development-docs/personal-runtime/06_protocol_host.md) 和两项目任务板。
下方 mock、Vue、SSO/OCR后置说明均按其记录日期理解，不覆盖本轮授权和代码。实际验证结果在任务板维护。

本日后续优化已补齐学期离线读取、最新成功数据覆盖、共享转换与桌面读取协议服务；配置及接口分别见上述05/06手册。既有表单登录按用户确认的此前可用基线复用，新增OCR组合验收单独记录。

> 以下为截至2026-10-09的导航与历史过程，当前状态先看上方接手入口。

本目录是 `XKE.DotNet8` 重构开发文档区。根目录只保留此 README，所有开发文档必须放入对应子目录。

## 目录职责

| 目录 | 内容 | 作用 | 更新时机 |
| --- | --- | --- | --- |
| [doc-entry](./doc-entry/00_overview.md) | 文档入口、维护记录 | 给后续开发者和 AI 明确阅读顺序、术语、文档治理规则 | 每次新增、移动、归档文档时更新 |
| [development-docs](./development-docs/README.md) | 开发文档 / 开发项目 / 具体内容 | 承载可执行开发项目文档，当前项目为个人运行时与 React/TypeScript 前端集成；工程整理及旧云模块文档保留历史角色 | 每次开始、完成或调整开发项目时更新 |
| [roadmap](./roadmap/00_development_direction.md) | 后续开发方向、阶段总结 | 决定当前主线、已完成切片、下一阶段优先级 | 每次切片完成、主线调整、验收口径变化时更新 |
| [review-issues](./review-issues/00_current_issues.md) | 当前问题手册、审查结论 | 记录仍需修复的问题、暂缓事项、审查排序 | 每次代码审查、问题关闭、优先级变化时更新 |
| [future-plans](./future-plans/00_traditional_jw_manual_login.md) | 后置功能计划 | 保存 Course 验证码、OCR、真实抢课、WinUI 3 原生桌面 UI 等 future-only 边界 | 只在后置边界变化或准备重启该切片时更新 |
| [core-index](./core-index/00_overview.md) | 运行时、API、领域、前端复用、文档入口索引 | 快速定位系统结构和主要入口 | 核心模块边界或入口路径变化时更新 |
| [api-parity](./api-parity/00_overview.md) | 契约冻结、legacy 数据模型、notice 兼容、WebHost 中间件记录 | 保存迁移兼容过程和契约对齐依据 | API 契约、兼容策略、notice 机制变化时更新 |
| [module-boundaries](./module-boundaries/01_module_map.md) | 模块边界草图 | 说明后端模块职责，防止职责回流到巨型入口 | 模块拆分、依赖方向、服务边界变化时更新 |
| [sensitive-index](./sensitive-index/00_overview.md) | 敏感边界、路径清单、移动教务/教务系统资料索引 | 记录敏感输入、运行数据、两套教务链路的边界 | 认证材料、数据落盘、真实链路资料变化时更新 |

## 当前开发优先级

当前以 [个人运行时](./development-docs/personal-runtime/README.md)、[前端重做](./development-docs/frontend-rebuild/README.md) 和 [API 交接](./development-docs/personal-runtime/03_api_contract.md) 为准。当前 React 第七版已接入真实 C# API，preview 仍显式隔离；当前验收边界看两项目任务板顶部。旧 Vue、CloudSync、反馈、选课及旧 Updater 文档保留历史角色；新应用更新以 personal-runtime/08 为准。

## 当前工程整理

活动仓库根已调整为 `Z:\XKE`。本轮记录见 [本地工程结构整理](./development-docs/workspace-consolidation/README.md)。Git 关系和业务行为保持不变，旧工程资料仅作私有归档。旧目录清理已完成，临时清理脚本已撤销。

## 外部文档如何使用

- 旧 `docs/项目说明/` 已归档到 `开发数据/历史工程/旧Python工程.zip`，只作为用户视角参考，不迁入 notes 正文。
- 旧 `重构计划/` 保存在同一私有归档中，只允许提炼仍适用于当前项目的结论。
- 归档中的 `重构计划/90_受限资料/` 只能做路径级索引，不将真实账号、密码、token、Cookie、`.env`、录包或原始响应复制到公开代码或 notes。
- 旧 `reference-archive/` 保存在同一私有归档中，只作离线参考，不作为活动决策源。
- 外部引用细则见 [doc-entry/02_external_reference_index.md](./doc-entry/02_external_reference_index.md) 与 [sensitive-index/03_external_reference_rules.md](./sensitive-index/03_external_reference_rules.md)。

## 当前阅读顺序

1. [doc-entry/00_overview.md](./doc-entry/00_overview.md) 与 [development-docs/README.md](./development-docs/README.md)。
2. 个人运行时四份必读：[README](./development-docs/personal-runtime/README.md)、[当前焦点](./development-docs/personal-runtime/00_current_focus.md)、[实施步骤](./development-docs/personal-runtime/01_implementation_steps.md)、[任务板](./development-docs/personal-runtime/02_task_board.md)。
3. 前端重做四份必读：[README](./development-docs/frontend-rebuild/README.md)、[当前焦点](./development-docs/frontend-rebuild/00_current_focus.md)、[实施步骤](./development-docs/frontend-rebuild/01_implementation_steps.md)、[任务板](./development-docs/frontend-rebuild/02_task_board.md)。
4. [API 交接](./development-docs/personal-runtime/03_api_contract.md)。
5. [当前方向](./roadmap/00_development_direction.md) 与 [当前问题](./review-issues/00_current_issues.md)；审查还须读 [审查发现](./review-issues/01_review_findings.md)。
6. 涉及认证、验证码、运行数据或私有服务时，先读 [敏感总览](./sensitive-index/00_overview.md)、[边界规则](./sensitive-index/01_boundary_rules.md) 和 [教务系统声明](./sensitive-index/教务系统/00_声明.md)；维护旧移动兼容代码再读 [移动教务声明](./sensitive-index/移动教务/00_声明.md)。
7. 历史开发项目按 [development-docs 历史入口](./development-docs/README.md#历史开发项目) 查阅；长期 [教务资料索引](./sensitive-index/教务系统/01_接入资料索引.md) 与 [WinUI 3 预案](./future-plans/01_winui3_native_ui.md) 按需阅读，不作为当前验收要求。

## 术语边界

- **JW 表单协议（传统 Web 教务）**：默认首次联网初始化、显式连接和直接课表刷新链路；登录验证码与课表续跑验证码是两种独立交互。
- **个人课表服务**：课表快照优先来源，失败回退直接 JW；不承担学校密码校验，不恢复旧 CloudSync。
- **本地可信状态**：DPAPI 保护的本地凭据/设置与持久化课表缓存；离线恢复不等于当前远端连接成功。浏览器运行期 session 仅保存在内存。
- **移动教务**：历史兼容测试入口，默认宿主不使用。长期资料中的旧“移动主线/传统后置”表述是历史语境，不覆盖当前运行时与 API 交接；OCR、本机选课及已退役云功能不回接主线。

## 更新规则

- 每次完成后端切片、修复关键边界、调整 deferred 策略，都必须同步更新 `roadmap` 与 `review-issues`。
- 每次推进当前开发项目，都必须同步更新对应 `development-docs/<project>/00_current_focus.md` 或 `development-docs/<project>/02_task_board.md`。
- 每次新增、移动、归档 notes 文档，都必须同步更新本 README 与 `doc-entry/01_notes_maintenance.md`。
- 每次从外部文档提炼结论，都必须同步检查 `doc-entry/02_external_reference_index.md` 与 `sensitive-index/03_external_reference_rules.md`。
- 每次触及移动教务或传统教务资料，必须同步检查 `sensitive-index/移动教务` 与 `sensitive-index/教务系统` 是否需要更新。
- 不在根目录新增散文件；临时分析材料先判断是否进入对应子目录，若只具备历史价值则进入 `reference-archive/notes`。

## 归档入口

- 历史 notes 索引：`开发数据/历史工程/旧Python工程.zip` 内的 `reference-archive/notes/notes-archive-index.md`。


## 个人本地运行时

当前产品范围收敛见 [personal-runtime/README.md](./development-docs/personal-runtime/README.md)。在共同分支 `refactor/personal-runtime` 推进四个核心功能及 Windows 常驻宿主的前后端集成；真实学校、校园网及 Windows 验收仍待执行，旧云功能文档仅作历史参考。

## TypeScript 前端并行开发

前后端在 `Z:\XKE`、同一分支 `refactor/personal-runtime` 按目录开发。历史阶段 `frontend/` 的 Vue 3 + TypeScript + Vite 界面与 runtime 集成已实现，显式演示/真实运行、内存 session、真实数据适配、独立验证码、设置 patch 与 Vite health/assets 的所述隔离验证已完成。runtime 同源或 `VITE_XKE_API_BASE_URL`；最终 runtime 无 demo chunk/固定验证码。完整原生交互、真实学校/校园网/Windows 和发布仍未验收，不把页面事件点击或 fake 服务当真实验收。入口：[frontend-rebuild](./development-docs/frontend-rebuild/README.md)。


个人课表多来源接入属于 `personal-runtime` 的课表能力，不恢复旧 CloudSync。真实地址、配置和使用手册只保留在 ignored 的私有目录，公开开发状态不包含服务操作资料。

历史阶段曾采用旧 web 界面一比一 TypeScript 迁移；该阶段与新 Vue 设计均已退出活动构建。最新进度见 frontend-rebuild，历史验证不能代替本次迁移验收。


## 2026-10-08 后端协议信息研究

新增 [JW 协议信息提取研究](./development-docs/personal-runtime/04_jw_protocol_research.md)：记录代码/假样例研究及用户授权的真实页面、请求观察；最新证据和纯 HTTP 生产边界见第10、11节。生产代码与数据格式尚未改动，纯 HTTP 新来源真实验收待完成。


## 2026-10-08 数据提取第一轮实现入口

当前实现看 personal-runtime/04_jw_protocol_research.md 第12节和 03_api_contract.md 新数据契约。纯 C# Cookie GET、基本资料与学期 API、新 JSON/CSV已交付本轮代码；明确无旧业务数据兼容、没有前端接入或发布。初轮“尚未修改/真实未验证”为历史状态。
