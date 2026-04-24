# 第一批契约冻结说明

## 当前已冻结到 .NET 的内容
- `APIResponse` -> `src/Xke.Contracts/Api/ApiEnvelope.cs`
- `X-XKE-Session` / `session_id` 语义 -> `src/Xke.Contracts/Api/SessionConstants.cs`
- 基础枚举集合 -> `src/Xke.Contracts/Enums/AppOptions.cs`
- 请求模型：
  - `LoginRequest`
  - `TrustedLoginRequest`
  - `ConnectRequest`
  - `ScheduleRequest`
  - `PreviewRequest`
  - `GrabRequest`
  - `FeedbackRequest`
  - `AppearanceSettingsRequest`
  - `ScheduleSettingsRequest`
  - `AuthSettingsRequest`
  - `NetworkSettingsRequest`
  - `UISettingsRequest`
  - `PreferencesRequest`
  - `ProfileRequest`
  - `TelemetryReportRequest`
- 响应模型：
  - `ScheduleRow`
  - `ScheduleData`
  - `CourseItem`
- WebHost 已对齐的基础行为：
  - `/api/health`
  - `/api/config`
  - loopback-only CORS
  - `X-XKE-Session -> session_id` 中间件兼容

## 主要对照来源
- `Z:/XKE/api/models.py`
- `Z:/XKE/web/assets/modules/constants.js`
- `Z:/XKE/web/assets/modules/http.js`
- `Z:/XKE/web/assets/modules/storage.js`

## 当前策略
- 先冻结字段名和默认值
- 暂未实现完整校验器
- 暂未实现完整路由与返回 DTO
- 下一步继续补：Preferences / Profile / Telemetry / ScheduleRow 等契约
