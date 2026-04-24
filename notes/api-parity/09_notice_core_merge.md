# notice 核心合并语义进展

## 对照来源
- `Z:/XKE/core/notice/service_v2.py:929`
- `Z:/XKE/core/notice/service_v2.py:1011`
- `Z:/XKE/core/notice/service_v2.py:1149`
- `Z:/XKE/core/notice/service_v2.py:1174`

## 当前已落地
### Storage
- `LegacyNoticeConfigMerger`
- `LegacyNoticeAccessPolicy`
- `LegacyNoticeFeedBuilder`
- `LegacyNoticeFeedReader`

## 当前已具备的能力
- config 基础字段合并
- notice / manifest / bundle 汇总
- access policy normalize
- access decision resolve
- integrity failure 时的 fail-closed readonly 骨架
- 被封禁账号时输出 blocked / access-policy-blocked 骨架

## 当前仍缺
- version policy 的更完整 normalize / 匹配逻辑
- update info 的更完整 normalize 语义
- panel_items / reminder_items / items 的真正投影
- 与 Python `build_notice_feed_payload()` 的字段细节完全对齐
