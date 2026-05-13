# XKE·软大课务

.NET 8 重写版。先前使用 Python 开发，但代码质量不行了，技术债越来越重，并且受限于性能问题，所以进行了重构。目前后端拆开，但前端还没动

## 构建和运行

需要 .NET 8 SDK。

```
dotnet restore XKE.DotNet8.sln
dotnet build XKE.DotNet8.sln
dotnet test XKE.DotNet8.sln
dotnet run --project src\Xke.DesktopHost
```

tools 目录下有对应脚本，作用和上面一样。

## 项目结构

```
src/
├── Xke.Contracts     — 请求/响应的模型定义
├── Xke.Core          — 业务逻辑：认证、课表、通知、反馈、更新、遥测
├── Xke.Storage       — 本地数据读写，兼容旧版 Python 数据格式
├── Xke.Security      — 加解密和签名校验，基于 Windows DPAPI
├── Xke.WebHost       — ASP.NET Core 服务，所有 API 端点
├── Xke.DesktopHost   — WPF + WebView2 桌面壳
├── Xke.Updater       — 独立更新进程
└── Xke.LegacyCompat  — 前端兼容常量，后续会移除
tests/
tools/
fixtures/             — 脱敏测试样本
notes/                — 开发文档、路线图、已知问题
```

技术栈：.NET 8、ASP.NET Core、WPF、WebView2、DPAPI、xUnit。

## 数据存储

默认路径 `%LOCALAPPDATA%\.XKE\data`，可通过 `XKE_DOTNET_DATA_DIR` 环境变量覆盖。

凭据和敏感配置走 DPAPI 加密。课表缓存和头像明文存储。

## 当前状态

已完成：移动教务登录与课表、公告系统、校园网保活、遥测与反馈。

进行中：自动更新。

暂未开放：选课抢课、手机端提醒。

## 参与开发

从 [notes/doc-entry/00_overview.md](./notes/doc-entry/00_overview.md) 开始阅读开发文档。

Bug 或建议提 Issue
