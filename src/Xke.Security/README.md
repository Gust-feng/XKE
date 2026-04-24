# Xke.Security

当前阶段目标：
- 对齐 Python 版本中的 secure-json / hybrid secret / DPAPI / 签名相关能力边界
- 先优先落地 secure-json 与 notice DPAPI 的真实读取兼容

当前已落地：
- `TelemetryKeyProfile`
- `SecureJsonEnvelope`
- `SecureJsonDecryptor`
- `NoticeDpapi`
- `NoticeCanonicalJson`
- `NoticeHash`
- `NoticeSignatureVerifier`
- `NoticePublicKeyLoader`
- `SecurityAssetPaths`（基于 appRoot 解析，而不是开发机绝对路径）

当前限制：
- 还未实现 hybrid secret 兼容
- 当前公告索引验签链已建入口，但仍需进一步确认与 Python Ed25519 输出的完全一致性
- 尚未加入 purpose 常量与更细粒度的强类型错误
