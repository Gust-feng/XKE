# Contracts JSON 映射进展

## 当前已补齐
- `ApiEnvelope` 已显式固定 `ok/msg/data`
- `LoginRequest` 已固定 `prelogin_mode`
- `GrabRequest` 已固定 `jx0404id`
- 已补缺失模型：
  - `DesktopSettingsRequest`
  - `NetworkConfigRequest`
  - `NetworkActionRequest`

## 下一步
- 继续给其余 Request / Response 补 `JsonPropertyName`
- 再补 DataAnnotation 或等价验证层
