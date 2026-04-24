# notice access / fail-closed 进展

## 对照来源
- `Z:/XKE/core/notice/service_v2.py:686`
- `Z:/XKE/core/notice/service_v2.py:732`
- `Z:/XKE/core/notice/service_v2.py:1149`
- `Z:/XKE/core/notice/service_v2.py:1174`

## 当前已落地
### Storage
- `LegacyNoticeAccessPolicy`
- `LegacyNoticeFeedBuilder.ApplyFailClosedDefaults()`
- `LegacyNoticeFeedReader` 已在 notice index 验签失败时输出 fail-closed 骨架

## 当前已具备的能力
- normalize access policy
- resolve access decision
- blocked account 基础判断
- notice integrity failure 时的 readonly 骨架输出

## 当前仍缺
- 更完整的 version policy / update info merge 语义
- `app_control` / `status_notice` 与 Python 的细节完全对齐
- 远端 sync + 本地 sync 的统一控制平面
