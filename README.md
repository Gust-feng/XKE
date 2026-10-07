# XKE·软大课务

.NET 8 重写版。先前使用 Python 开发，但代码质量不行了，技术债越来越重，并且受限于性能问题，所以进行了重构。目前后端已按职责分层，前端保留在本地，准备后续重做

## 构建和运行

项目目标运行时为 .NET 8，开发 SDK 版本以根目录 `global.json` 为准。

```
dotnet restore XKE.DotNet8.sln
dotnet build XKE.DotNet8.sln
dotnet test XKE.DotNet8.sln
dotnet run --project src\Xke.DesktopHost
```

tools 目录下有对应脚本，作用和上面一样。

## 本地工程位置

当前独立仓库根为 `Z:\XKE`，不再依赖旧 Python 父工程。`XKE.DotNet8.sln` 和项目名称保持不变，Git 历史、当前分支和远端配置保持原关系。

旧 Python 工程与早期 C# 骨架的源码、文档和私有资料保存于被忽略的 `开发数据/历史工程/` 压缩归档；它们仅用于历史查阅，不参与构建或运行。开发原始页面样本也留在 `开发数据/`，不属于发布资源。

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
└── Xke.LegacyCompat  — 历史兼容项目，当前未纳入 solution
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

当前实现：JW 表单协议登录与课表、本地可信状态、公告、校园网接口、遥测与反馈。校园网定时保活仍由前端调度，静默启动下的后台保活需要后续调整。

进行中：自动更新。

暂未开放：选课抢课、手机端提醒。

## 前端交付边界

当前 `web/` 有意保留为本地开发资源，未纳入此仓库版本管理。桌面运行及发布需要本地提供 `web/index.html` 和配套资源；从 Git 获取后端并不等于取得完整桌面发布内容。未来前端重做另行确定交付方式。

## 参与开发

从 [notes/doc-entry/00_overview.md](./notes/doc-entry/00_overview.md) 开始阅读开发文档。

Bug 或建议提 Issue
