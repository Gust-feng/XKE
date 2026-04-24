# 目标架构提炼

本文件从旧重构规划中提炼仍适用于 `XKE.DotNet8` 的结构性结论，不复制旧文档原文。

## 当前有效结论

- 后端继续按 `Contracts / Core / Storage / Security / WebHost / DesktopHost` 分层推进。
- `Xke.Core` 承载业务用例和外部协议编排，避免把业务决策塞回 WebHost endpoint。
- `Xke.Storage` 只负责 `.runtime/data` 或 `XKE_DOTNET_DATA_DIR` 下的本地数据读写，不读取 Python legacy runtime。
- `Xke.WebHost` 保持现有 API envelope 和路由兼容，长期目标是拆出服务注册、session/context、blocked policy、错误 envelope。
- `Xke.DesktopHost` 只负责桌面宿主、WebHost 生命周期、WebView2 壳和桌面桥，不承载业务逻辑。

## 模块边界要求

- Auth：首次初始化、本地凭据校验、trusted-login、session restore、移动教务 connect。
- Schedule：移动教务课表刷新、本地缓存读取、未来传统教务 Cookie 课表入口。
- Course：传统教务验证码、轮次、预览、抢课的 future-only 边界。
- Network：本地配置、状态探测、心跳维护和受控系统命令封装。
- Telemetry / Feedback：当前 local-only，云上传后置。
- Notice：控制面、blocked policy、公告 feed 和 fail-closed 边界。

## 不迁入的旧结论

- 不保留双运行时作为当前目标架构。
- 不把 Python legacy data 作为 .NET 运行时依赖。
- 不把旧前端结构作为当前必须兼容的长期架构，只保留现有 API envelope 兼容。

## 维护要求

模块依赖方向变化时，必须同步更新本文件和 `01_module_map.md`。
