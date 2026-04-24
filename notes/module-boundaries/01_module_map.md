# 模块边界草图

## 目标
把当前 Python 版本的主要职责映射到未来 .NET 8 模块，作为后续建 solution 的边界参考。

## 推荐目标模块
- `Xke.DesktopHost`
- `Xke.WebHost`
- `Xke.Contracts`
- `Xke.Core.Auth`
- `Xke.Core.Schedule`
- `Xke.Core.Course`
- `Xke.Core.NetworkKeepAlive`
- `Xke.Core.NoticePolicy`
- `Xke.Core.Feedback`
- `Xke.Core.Telemetry`
- `Xke.Core.Preferences`
- `Xke.Storage`
- `Xke.Security`
- `Xke.LegacyCompat`

## 当前到未来的主要映射
- `start_xke.py` -> `Xke.DesktopHost`
- `api/app.py` -> `Xke.WebHost`
- `api/models.py` -> `Xke.Contracts`
- `api/routes/auth.py` -> `Xke.Core.Auth`
- `api/routes/schedule.py` -> `Xke.Core.Schedule`
- `api/routes/course.py` -> `Xke.Core.Course`
- `api/routes/network.py` + `core/campus_network.py` -> `Xke.Core.NetworkKeepAlive`
- `api/routes/updates.py` + `core/notice/*` -> `Xke.Core.NoticePolicy` + `Xke.Security`
- `api/routes/feedback.py` + `core/feedback_client.py` -> `Xke.Core.Feedback`
- `api/routes/telemetry.py` + `core/telemetry.py` + `core/remote_sensing/*` -> `Xke.Core.Telemetry` + `Xke.Security` + `Xke.Storage`
- `core/jw_services.py` -> `Xke.Core.Auth` + `Xke.Core.Schedule` + `Xke.Core.Course` + `Xke.Storage`
- `core/runtime_env.py` -> `Xke.Security` + `Xke.DesktopHost`

## 当前原则
- 不在 C# 中复制新的巨石服务
- 先冻结契约与存储边界，再迁移业务
- 先保留 Web 前端，再逐步去巨石化 `web/assets/app.js`
