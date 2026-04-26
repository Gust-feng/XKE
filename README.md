# XKE.DotNet8

XKE 的 .NET 8 重构版。当前主线是本地 WebHost + 桌面壳 + 移动教务课表能力，传统 Web 教务系统接入仍在后续开发中。

## 快速开始

```powershell
dotnet build .\XKE.DotNet8.sln
dotnet test .\XKE.DotNet8.sln
```

启动开发版桌面壳：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-desktop.ps1
```

## 目录

- `src/`：应用源码
- `tests/`：测试项目
- `fixtures/`：脱敏样本
- `tools/`：开发、smoke、审计脚本
- `notes/`：设计记录、审查问题、路线图、敏感资料索引
- `.runtime/`：开发/测试覆盖数据目录，默认不应提交

## 关键文档

- `notes/README.md`：notes 入口
- `notes/roadmap/00_development_direction.md`：后续开发方向
- `notes/review-issues/00_current_issues.md`：当前问题入口
- `notes/sensitive-index/00_overview.md`：敏感资料边界
- `notes/sensitive-index/教务系统/01_接入资料索引.md`：传统 Web 教务系统接入资料
- `notes/sensitive-index/移动教务/01_认证流程索引.md`：移动教务认证与课表流程

## 数据边界

- 默认数据根：`%LOCALAPPDATA%\.XKE\data`
- 可用 `XKE_DOTNET_DATA_DIR` 覆盖数据根
- AppData 运行数据默认加密落盘；头像和课表缓存保持普通文件，便于查看与调试
- 不提交真实账号、密码、token、cookie、`.env`、原始录包或真实运行数据
- 真实开发验证资料只保留在本机运行目录或受限资料原路径

## 常用验证

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\schedule-smoke.ps1
powershell -ExecutionPolicy Bypass -File .\tools\frontend-smoke.ps1
```

## 私有打包

真实云端点只放在本机 `开发数据/dotnet8-cloud.local.json`，公开仓库不提交。打包桌面版时使用：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\package-desktop.ps1
```

脚本会发布桌面宿主，并把私有端点注入发布目录的 `xke.runtime.json`。
