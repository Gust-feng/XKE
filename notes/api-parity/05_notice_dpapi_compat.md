# DPAPI 公告缓存兼容进展

## 对照来源
- `Z:/XKE/core/notice/dpapi.py`
- `Z:/XKE/core/notice/service_v2.py`

## 当前已落地
### Security
- `src/Xke.Security/NoticeDpapi.cs`

### Storage
- `src/Xke.Storage/LegacyNoticeCacheRoot.cs`
- `src/Xke.Storage/LegacyNoticeCacheReader.cs`
- `src/Xke.Storage/LegacyRootReader.cs`

## 已对齐的关键语义
- DPAPI(CurrentUser) 读取
- 固定 entropy：`XKE|notice-cache|v1|2026-03-21`
- cache key → hash 路径：`sha256("salt|version|cacheKey")`
- index / index-signature 不是字面文件，而是 hash 分桶 `.bin`
- payload 路径使用 `asset:<normalized-relative-path>` 生成 cache key

## 当前仍缺
- Ed25519 公告索引验签
- payload / markdown sha256 校验
- 高层 notice feed 组装
- fail-closed / readonly 输出模型
