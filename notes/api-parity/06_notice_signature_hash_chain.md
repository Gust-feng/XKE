# 公告索引验签与哈希校验进展

## 对照来源
- `Z:/XKE/core/notice/signing.py`
- `Z:/XKE/core/notice/service_v2.py`
- `Z:/XKE/core/runtime_env.py`

## 当前已落地
### Security
- `NoticeCanonicalJson`
- `NoticeHash`
- `NoticeSignatureVerifier`
- `NoticePublicKeyLoader`
- `SecurityAssetPaths` 已从开发机绝对路径改为基于 `appRoot` 解析

### Storage
- `LegacyNoticeValidationResult`
- `LegacyNoticeCacheReader.ReadAndValidateIndex()`
- `LegacyNoticeCacheReader.ReadPayloadJsonValidated()`
- `LegacyRootReader.ReadAndValidateNoticeIndex()`

## 当前已对齐的语义
- notice index canonical JSON
- notice index 签名读取与验签入口
- payload canonical sha256 校验入口
- 本地公告缓存路径仍按 Python v2 的 hash 分桶规则

## 当前仍缺
- 需要进一步确认 .NET 验签实现与 Python Ed25519 结果完全一致
- notice manifest / bundle / markdown 的高层组装
- fail-closed / readonly 的上层策略输出
