# Legacy data 目录模型冻结

## 对照来源
- `Z:/XKE/core/jw_services.py` 中 `StudentDataStore`
- `Z:/XKE/重构计划/05_数据与安全/02_本地目录与文件模型.md`

## 当前已在 .NET 中固定的类型
- `src/Xke.Storage/LegacyDataRoot.cs`
- `src/Xke.Storage/LegacyTelemetryRoot.cs`
- `src/Xke.Storage/LegacyNoticeCacheRoot.cs`
- `src/Xke.Storage/LegacyStudentRoot.cs`
- `src/Xke.Storage/LegacyDataPaths.cs`

## 已覆盖的目录/文件职责
### 全局目录
- `data/.telemetry/`
- `data/.notice_cache/v1/`

### 学号目录
- `data/<student_id>/.env`
- `data/<student_id>/settings.json`
- `data/<student_id>/preferences.json`
- `data/<student_id>/prelogin_capture.xml`
- `data/<student_id>/cache/*`
- `data/<student_id>/info/*`
- `data/<student_id>/info/network/*.json`
- `data/<student_id>/schedules/<term>/weeks/<week>/*`

## 当前阶段限制
- 只固定路径与目录职责
- 还未实现 JSON 解析与安全读取
- 还未实现新旧格式迁移

## 下一步
- 建只读 JSON/文本加载器
- 固定 `settings` / `basic_info` / `app_runtime` / `network_auth_history` / `remote_sensing_snapshot` 的读取入口
