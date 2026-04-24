# secure-json 兼容进展

## 对照来源
- `Z:/XKE/core/remote_sensing/crypto.py`
- `Z:/XKE/core/remote_sensing/storage.py`
- `Z:/XKE/重构计划/05_数据与安全/03_加密机制详解.md`

## 当前已落地
### Security
- `TelemetryKeyProfile`
- `SecureJsonEnvelope`
- `SecureJsonDecryptor`

### Storage
- `LegacyFileReader` 已接入 `SecureJsonDecryptor`
- `LegacyStudentReader` / `LegacyRootReader` 默认带 `dataRoot` 读取上下文

## 当前已实现的关键兼容行为
- 从 `data/.telemetry/telemetry_aes_key.json` 读取 `key_b64`
- 识别 `xke-secure-json`
- 复现 `AES-256-GCM` 解密流程
- 按 `xke-secure-json|purpose|fingerprint` 构造 AAD
- 校验 `plaintext_sha256`
- 明文 JSON 与 secure-json 同路径兼容读取

## 当前限制
- 还未补 hybrid secret
- 还未补 DPAPI(CurrentUser)
- 还未补公钥验签
- 还未补 purpose 常量统一定义
