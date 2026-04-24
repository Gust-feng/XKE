# XKE.DotNet8 目录整理说明

## 本次整理目标
- 去掉源码目录中不应该长期保留的构建产物噪音
- 为关键模块补充 README 和文件级注释
- 让 `XKE.DotNet8` 目录结构更适合持续自主迭代

## 已完成整理
### 1. 清理构建产物目录
已移除以下目录中的生成产物：
- `src/Xke.Storage/bin`
- `src/Xke.Storage/obj`
- `src/Xke.Security/bin`
- `src/Xke.Security/obj`
- `src/Xke.WebHost/bin`
- `src/Xke.WebHost/obj`

### 2. 补充模块 README
- `src/Xke.Contracts/README.md`
- `src/Xke.WebHost/README.md`
- `notes/README.md`

### 3. 补充关键文件文件头注释
- `src/Xke.Storage/LegacyNoticeFeedReader.cs`
- `src/Xke.Security/SecureJsonDecryptor.cs`
- `src/Xke.WebHost/Program.cs`
- `src/Xke.Contracts/Requests/NetworkConfigRequest.cs`

## 当前目录仍建议保持的组织方式
- 根目录：solution、根级配置、总 README、审查报告
- `src/`：各 .NET 项目源码
- `notes/`：事实索引、parity 记录、模块台账
- `fixtures/`：后续脱敏样本
- `tools/`：后续对比和验证工具

## 后续建议
- 为 `src/Xke.Core/`、`src/Xke.DesktopHost/`、`src/Xke.LegacyCompat/` 继续补 README
- 后续接入 `dotnet build` 后，把所有生成目录统一加入忽略规则或保持清理
- 对关键入口文件继续保持文件级注释，减少后续理解成本
