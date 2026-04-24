# 第一批契约冻结说明

> 状态：历史冻结记录。当前 API 行为以 [12_legacy_api_contract_extract.md](./12_legacy_api_contract_extract.md)、`roadmap/`、`review-issues/` 和代码测试为准。

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
- 本文件保留早期字段冻结和来源对照，作为兼容记录。
- 已完成切片的当前语义不在本文件重复维护。
- 若新增或调整 API 字段，优先更新 `12_legacy_api_contract_extract.md` 和对应测试，再回填本目录索引。
