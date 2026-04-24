# notice feed 本地组装进展

## 对照来源
- `Z:/XKE/core/notice/service_v2.py:1537`
- `Z:/XKE/core/notice/service_v2.py:1594`
- `Z:/XKE/core/notice/service_v2.py:1637`
- `Z:/XKE/core/notice/service_v2.py:1650`
- `Z:/XKE/core/notice/service_v2.py:1011`

## 当前已落地
### Storage
- `LegacyNoticeFeedReader`
- `LegacyNoticeFeedBuilder`
- `LegacyNoticeJson`
- `LegacyNoticePaths`
- `LegacyNoticeBundleRecord`
- `LegacyNoticeFeedResult`

## 当前已具备的能力
- 从本地公告索引开始读取 notice feed
- 读取并校验 notice / version_policy / update_info / access_policy payload
- 选择匹配 channel 的 manifest
- 读取 bundle 列表并加载本地 bundle payload
- 读取 markdown 正文并回填 `content_markdown` / `content`
- 聚合 warnings

## 当前仍缺
- markdown content 的 sha256 校验
- 更完整地对齐 Python 的 normalize 细节
- access decision / readonly / blocked 的高层输出
- version policy / update info / access policy 的完整归一化与 merge 语义
