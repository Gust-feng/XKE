# Legacy 只读加载层

## 对照来源
- `Z:/XKE/core/jw_services.py`
- `Z:/XKE/core/remote_sensing/crypto.py`
- `Z:/XKE/重构计划/05_数据与安全/02_本地目录与文件模型.md`
- `Z:/XKE/重构计划/05_数据与安全/03_加密机制详解.md`

## 当前已在 .NET 中落地的类型
- `LegacyReadResult`
- `LegacySecureJsonDetector`
- `LegacyFileReader`
- `LegacyStudentReader`
- `LegacyRootReader`
- `LegacyDataRootFactory`

## 当前行为
- 能读取明文 JSON
- 能读取明文文本
- 能识别 `xke-secure-json` envelope
- 已接入 `Xke.Security`，可在提供 `dataRoot` 时尝试真实解密 secure-json
- 若解密失败，则保留 envelope 标记但不返回明文
- 能优先读取新路径，并在部分场景回退旧路径（如 `basic_info.json` / `preferences.json`）

## 当前覆盖的读取入口
### Student
- basic info
- preferences
- app runtime
- network auth history
- remote sensing snapshot（仅 envelope 识别）
- course grab guard
- cookie schedule params
- account credentials（仅 envelope 识别）
- cookie header text
- prelogin capture text

### Global
- notice index
- notice index signature
- telemetry installation
- telemetry AES key

## 下一步
- 为 secure-json 增加真实解密能力
- 为 `.notice_cache/v1/*` 增加 DPAPI 兼容层
- 把读取结果映射为强类型 DTO，而不仅仅是 `JsonElement`
