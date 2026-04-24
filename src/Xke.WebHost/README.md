# Xke.WebHost

本模块是 .NET 8 版本的本地 API 宿主壳。

## 职责
- 承载本地 `/api` 路由
- 复现当前 FastAPI 的关键中间件语义
- 托管现有 `web/` SPA 与静态资源
- 作为 DesktopHost 与前端之间的本地服务桥梁

## 当前阶段
当前已完成：
- loopback-only CORS
- session header 兼容
- `session_id` query 兼容
- no-cache 中间件
- 静态资源与 SPA fallback
- Notice 控制面驱动的 blocked / readonly / fail-closed / beta-access 输出
- Auth / Course / Schedule / Network / Telemetry / Feedback 已按端点域拆分为 partial route 文件
- Updates 与 Desktop settings / activate 宿主路由已独立端点化

## 设计约束
- 先对齐已交付切片的 Python 行为，再做框架层优化
- 本地 session / trusted-login 只恢复本地工作状态，远端 connect / refresh 单独执行
- 不把传统教务验证码链路塞回主登录卡片
- `XkeWebHostApplication` 只保留启动、中间件、壳路由和共用 envelope / session / blocked-policy 辅助逻辑
