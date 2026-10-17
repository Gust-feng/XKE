# 当前入口：产品第一版对接与协议服务（2026-10-10）

## 2026-10-17 仓库整理与提交准备

当前 React/C# 第一版、后台提醒、固定 GitHub/Velopack 更新及直接启动窗口均已有实现。正在按实际工程边界整理公开源码：压缩包、编辑器计划、缓存、运行数据与私有配置只保留本地；旧目录和历史过程不删除。提交批次以 personal-runtime/01_implementation_steps.md 顶部为准，精确本地清单放 ignored artifacts/repository-preparation/20261017。整理阶段未暂存或提交；后续已获用户授权按清单分批本地commit，仍不推送或发布，也不顺带实施业务重构。

## 2026-10-17 V1.0.0 发布与自动更新

最新授权与操作手册见[08_release_and_updates.md](./08_release_and_updates.md)：公开GitHub来源、Velopack客户端、版本1.0.0、自动下载/退出应用、严格无个人数据打包。旧自动更新/遥测退役记录继续保留为历史，不等于恢复旧云模块；GitHub公开发布仍需正确源码提交及明确推送授权。

项目未投入使用，无旧业务数据兼容。产品第一版沿用第七版合成；React真实对接与独立ProtocolHost已实现，已具备学期离线读取、数据新鲜度合并、共享转换和桌面消费协议服务；当前整理源码提交边界。既有表单登录按用户确认的可用基线复用。

接手顺序：[技术手册](05_backend_technical_manual.md) → [当前焦点](00_current_focus.md) → [实施步骤](01_implementation_steps.md) → [任务板](02_task_board.md) → [API契约](03_api_contract.md) → [校园网自助后台协议](07_self_service_protocol.md) → [云协议版部署](06_protocol_host.md)。

以下截至2026-10-08的状态保留作历史过程，旧Vue/移动/纯mock结论不覆盖当前实现。

---

> 2026-10-08 前端整理：frontend/ 现采用用户选定的 React 压缩包优化设计，仅 mock 预览，旧 TS/Vue 运行时集成已归档供后续参考。下文旧 UI 验证为历史记录；真实 React API/桌面接入待完成，C# 功能和数据根未变。

# 个人本地运行时

仅保留登录、课表、设置、校园网及本机 API/Windows 常驻宿主。公告、遥测、反馈、云控制、选课和自动更新已退出当前装配。前端在同分支 `frontend/` 独立重做，后端不修改该目录。旧用户数据与私有资料不删除。

## 阅读顺序

1. [00_current_focus.md](./00_current_focus.md)
2. [01_implementation_steps.md](./01_implementation_steps.md)
3. [02_task_board.md](./02_task_board.md)
4. [03_api_contract.md](./03_api_contract.md)
5. [04_jw_protocol_research.md](./04_jw_protocol_research.md)：JW 信息提取研究；第10、11节为真实请求观察、源结构问题及纯 HTTP 生产边界。

## 当前阶段：前端集成已实现，隔离验证已完成

2026-10-07：`frontend/` 已有 Vue 3 + TypeScript + Vite 界面，不再处于工作区准备阶段。已与 [frontend-rebuild](../frontend-rebuild/README.md) 对齐显式 preview/runtime、内存 session、分开的登录/课表验证码、真实模型适配、账号偏好与机器设置的 patch 保存，以及 Vite 静态资源 health。

preview 仅 `VITE_XKE_PREVIEW=true` / `dev:preview` 显式启用；runtime 使用同源/`VITE_XKE_API_BASE_URL` 配置 API base，不自动回退演示。前端不持久化密码/session/trusted token，不承接 C# 的课表刷新或校园网 job scheduler。

UI 持久化字段由后端负责人确认：`ui.show_weekend` boolean 默认 true；`ui.inactive_course_mode` 为 `dim | hide`，默认 hide。隔离浏览器已验证 show_weekend=false 与 startup=true 重载保留；桌面 registrar 为内存替身，未修改真实 Windows。

协调方已回传后端 affected 29/29、solution 152/152（0 skipped）、含 DesktopHost build 0/0；`tests/Xke.FrontendIntegrationHost`（实际 WebHost、fake 教务/网络/Windows 调用、临时根）build 0/0 与 fixture HTTP 检查已通过，该工具不是生产服务。发布门禁 PS7 语法、11 个模式/元数据 fixture 与实际 preview 拒绝通过，未 publish；默认 dist 要求 `frontend-build.json` runtime 标记，显式自定义旧资源缺标记/旧元数据可兼容，preview/畸形/不支持/非对象元数据拒绝。

最终前端 35/35 tests、严格 vue-tsc/Vite runtime build、显式 preview build 后恢复 runtime（无 demo chunk/固定验证码）、smoke -SkipInstall 35 tests + build 通过。runtime marker 与 health build 一致，两份 emitted assets HTTP 200/hash 列入；隔离 host stop 202/exit 0/临时根清理通过。实际 WebHost + fake 外部服务的 browser 流程已验证，但后续使用页面事件点击，不是完整鼠标/键盘/视觉验收；错误验证码重试仅 HTTP/状态测试覆盖，浏览器 challenge 过期未验证重试。详细证据见 [API 验证状态](./03_api_contract.md#vite-静态资源发布模式与验证状态)。历史后端/preview 测试结果只覆盖各自当时切片，真实学校、校园网、Windows/WebView2/托盘/自启动与发布包仍待验收。

## 配置原则

个人时间间隔、主题标识、校园网门户及检测地址支持自定义；保留时间、正数和 URL 的必要检查。教务协议保持当前学校实现，多学校适配不由任意 URL 配置替代。


## 2026-10-08 数据源切片

纯 DOM与 C# GET 已用于基本资料、学期和单周数据；新增学期API/独立快照、新CSV及普通业务JSON。用户确认无旧数据兼容需要。最新字段与路径见 03_api_contract.md，实际验证与边界见研究第12节；不把本切片当 React/桌面/登录生命周期/发布全部完成。
