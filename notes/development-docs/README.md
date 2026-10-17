# 开发文档入口

## 2026-10-17 仓库整理与提交准备

当前 React/C# 第一版、后台提醒、固定 GitHub/Velopack 更新及直接启动窗口均已有实现。正在按实际工程边界整理公开源码：压缩包、编辑器计划、缓存、运行数据与私有配置只保留本地；旧目录和历史过程不删除。提交批次以 personal-runtime/01_implementation_steps.md 顶部为准，精确本地清单放 ignored artifacts/repository-preparation/20261017。整理阶段未暂存或提交；后续已获用户授权按清单分批本地commit，仍不推送或发布，也不顺带实施业务重构。

## 2026-10-17 V1.0.0 发布与自动更新

最新授权与操作手册见[08_release_and_updates.md](./personal-runtime/08_release_and_updates.md)：公开GitHub来源、Velopack客户端、版本1.0.0、自动下载/退出应用、严格无个人数据打包。旧自动更新/遥测退役记录继续保留为历史，不等于恢复旧云模块；GitHub公开发布仍需正确源码提交及明确推送授权。

2026-10-10：产品第一版锚定现第七版合成，推进React真实对接及独立纯HTTP协议服务。
后端先读 [技术手册](./personal-runtime/05_backend_technical_manual.md)，校园网自助后台协议看 [07](./personal-runtime/07_self_service_protocol.md)，部署看 [ProtocolHost/ddddocr](./personal-runtime/06_protocol_host.md)，
状态看两项目任务板。下方mock/旧版集成表格按日期理解；项目未使用，不做旧业务兼容。

本目录按以下结构组织：

```text
development-docs/
  <开发项目>/
    README.md
    00_current_focus.md
    01_implementation_steps.md
    02_task_board.md
```

## 当前执行项目

| 项目 | 入口 | 状态 |
| --- | --- | --- |
| 个人运行时 | [personal-runtime/README.md](./personal-runtime/README.md) | C# 四项核心功能与常驻宿主；历史前端验证保留，当前 React 接入及真实环境待验收 |
| React/TypeScript 前端 | [frontend-rebuild/README.md](./frontend-rebuild/README.md) | 第七版「合成」为唯一正式设计基线；其他资产移出项目，共享假数据，当前 mock 预览，真实集成待完成 |

## 当前阅读顺序

1. 个人运行时四份必读：[README](./personal-runtime/README.md)、[当前焦点](./personal-runtime/00_current_focus.md)、[实施步骤](./personal-runtime/01_implementation_steps.md)、[任务板](./personal-runtime/02_task_board.md)。
2. 前端重做四份必读：[README](./frontend-rebuild/README.md)、[当前焦点](./frontend-rebuild/00_current_focus.md)、[实施步骤](./frontend-rebuild/01_implementation_steps.md)、[任务板](./frontend-rebuild/02_task_board.md)。
3. [API 交接](./personal-runtime/03_api_contract.md)：显式 preview/runtime、内存 session、真实模型、验证码与设置 patch 边界。
4. [当前方向](../roadmap/00_development_direction.md)、[当前问题](../review-issues/00_current_issues.md)；认证/数据相关动作先按 [敏感总览](../sensitive-index/00_overview.md) 读取对应声明。

真实学校、校园网、Windows 托盘/自启动与发布包验收均待执行；历史测试计数不代表本轮已验证。

## 历史开发项目

| 项目 | 入口 | 历史阶段（非当前执行要求） |
| --- | --- | --- |
| CloudSync 云端同步 | [cloud-sync/README.md](./cloud-sync/README.md) | 阶段 1：单包拉取与本地 spool |
| 反馈云上传 | [feedback-cloud/README.md](./feedback-cloud/README.md) | 阶段 1：云端优先 + 本地加密兜底 |
| 一键自动更新 | [auto-update/README.md](./auto-update/README.md) | 阶段 0：切片准备 |
| 传统教务 Cookie 课表 | [traditional-jw-cookie-schedule/README.md](./traditional-jw-cookie-schedule/README.md) | 阶段 1：离线解析 |
| 本地工程结构整理 | [workspace-consolidation/README.md](./workspace-consolidation/README.md) | 已完成：独立仓库、私有归档与旧目录清理 |

## 文件职责

- `README.md`：项目入口、阅读顺序、项目边界
- `00_current_focus.md`：当前切片、目标、完成定义
- `01_implementation_steps.md`：实施步骤、接口行为、验收顺序
- `02_task_board.md`：任务状态、下一步、阻塞项

历史项目文件（保留过程记录，当前执行文件按上述阅读顺序）：

- [cloud-sync/README.md](./cloud-sync/README.md)
- [cloud-sync/00_current_focus.md](./cloud-sync/00_current_focus.md)
- [cloud-sync/01_implementation_steps.md](./cloud-sync/01_implementation_steps.md)
- [cloud-sync/02_task_board.md](./cloud-sync/02_task_board.md)
- [feedback-cloud/README.md](./feedback-cloud/README.md)
- [feedback-cloud/00_current_focus.md](./feedback-cloud/00_current_focus.md)
- [feedback-cloud/01_implementation_steps.md](./feedback-cloud/01_implementation_steps.md)
- [feedback-cloud/02_task_board.md](./feedback-cloud/02_task_board.md)
- [auto-update/README.md](./auto-update/README.md)
- [auto-update/00_current_focus.md](./auto-update/00_current_focus.md)
- [auto-update/01_implementation_steps.md](./auto-update/01_implementation_steps.md)
- [auto-update/02_task_board.md](./auto-update/02_task_board.md)
- [traditional-jw-cookie-schedule/README.md](./traditional-jw-cookie-schedule/README.md)
- [traditional-jw-cookie-schedule/00_current_focus.md](./traditional-jw-cookie-schedule/00_current_focus.md)
- [traditional-jw-cookie-schedule/01_implementation_steps.md](./traditional-jw-cookie-schedule/01_implementation_steps.md)
- [traditional-jw-cookie-schedule/02_task_board.md](./traditional-jw-cookie-schedule/02_task_board.md)

- [workspace-consolidation/README.md](./workspace-consolidation/README.md)
- [workspace-consolidation/00_current_focus.md](./workspace-consolidation/00_current_focus.md)
- [workspace-consolidation/01_implementation_steps.md](./workspace-consolidation/01_implementation_steps.md)
- [workspace-consolidation/02_task_board.md](./workspace-consolidation/02_task_board.md)

## 新增项目规则

新增开发项目时：

1. 在 `development-docs/<project>/` 下创建项目目录
2. 至少包含 `README.md`、`00_current_focus.md`、`01_implementation_steps.md`、`02_task_board.md`
3. 更新本文件的“当前开发项目”表
4. 更新 `../README.md`
5. 更新 `../doc-entry/00_overview.md`
6. 更新 `../doc-entry/01_notes_maintenance.md`

## 边界

- 本目录保存“可执行开发文档”，不是敏感资料正文。
- 真实账号、密码、token、cookie、验证码图片、原始 HTML、原始响应不得进入本目录。
- 真实 URL、云资源地址、发布桶路径、云函数地址和临时验证数据只放在仓库 ignored 的 `开发数据/` 下。
- `development-docs/<project>/` 仍是开发文档固定位置；文档中只写占位符、字段含义、验收步骤，以及“真实资料位置”。
- 每个需要真实资料的开发项目，必须在项目 README 或当前焦点文档中写明对应 `开发数据/<中文目录>/` 路径。
- `开发数据/` 下新增资料目录和说明文档使用中文命名，便于一眼区分真实资料用途；开发文档中的示例 URL 和账号统一使用占位符。
- 需要认证资料时，只引用 `../sensitive-index/` 中的路径索引和规则。

## 本地工程结构整理

[workspace-consolidation/README.md](./workspace-consolidation/README.md)：已完成的仓库独立、私有归档与旧目录清理记录；当时“功能精简和前端重构尚未开始”是历史阶段，不代表当前集成状态。


## 个人本地运行时

当前产品范围收敛见 [personal-runtime/README.md](./personal-runtime/README.md)。在共同分支 `refactor/personal-runtime` 推进四个核心功能及 Windows 常驻宿主集成，旧云功能文档仅作历史参考。

## TypeScript 前端并行开发

前后端在 `Z:\XKE`、同一分支 `refactor/personal-runtime` 按目录开发。`frontend/` 的 React + TypeScript + Vite 第一版已接入 runtime，显式演示/真实运行、业务调用/模型适配与 C# 调度分工明确；runtime 同源或 `VITE_XKE_API_BASE_URL`。所述隔离构建/自动化/browser/assets 验证完成，但页面事件点击不是完整鼠标/键盘/视觉验收，真实学校/校园网/Windows 与发布仍待验收。入口：[frontend-rebuild](./frontend-rebuild/README.md)。


个人课表多来源接入属于 `personal-runtime` 的课表能力，不恢复旧 CloudSync。真实地址、配置和使用手册只保留在 ignored 的私有目录，公开开发状态不包含服务操作资料。

当前前端方向已由用户改为旧 web 界面一比一 TypeScript 迁移；新 Vue 设计退出活动构建。最新进度见 frontend-rebuild，历史验证不能代替本次迁移验收。
