# 当前模块边界

日期：2026-10-17。当前产品为个人 Windows 本地工具；六个桌面运行时项目及一个独立协议宿主。

| 模块 | 职责 | 边界 |
| --- | --- | --- |
| Contracts | 登录、课表、设置、网络、桌面及应用更新模型 | 不做 I/O |
| Security | Windows 当前用户 DPAPI 存储基础 | 不处理业务协议 |
| Storage | 账号、会话、设置、课表缓存、提醒回执与桌面运行态 | 不依赖前端，不清理旧用户数据 |
| Core | 认证、课表、网络、设置与后台调度，以及 Releases 更新状态编排 | 不依赖 WebView、WPF、ASP.NET 或 Velopack |
| WebHost | 本机 API、访问边界、后台任务与受控更新入口 | 不实现云控制或上传，不在 HTTP 请求中替换运行文件 |
| DesktopHost | WPF/WebView2、托盘、自启动、通知、窗口控制及 Velopack 更新实现 | 不把页面计时器作为后台调度，不替换用户数据 |
| ProtocolHost | 独立跨平台学校协议 API，复用 Core/Storage 与本机 OCR | 独立数据根，学校登录材料仅驻内存，不调用桌面 DPAPI |

唯一活动前端为 `frontend/` 的 React + TypeScript；runtime 接本机 API，合成数据仅在显式 preview 中使用。Core 采用 namespace 模块化，不为每个功能拆分 csproj。

旧移动协议仅保留测试兼容入口；默认 Auth/Schedule 使用 JW 表单协议。旧云控制、公告、遥测、反馈、选课及旧 Updater 不恢复。后续退出兼容路径时先保留相应覆盖，不在仓库整理中顺带改写认证链路。
