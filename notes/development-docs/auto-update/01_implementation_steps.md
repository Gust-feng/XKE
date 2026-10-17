# 一键自动更新实施步骤

> 历史方案：产品显示名称已统一为 XKE，以下旧 Updater/CloudSync 设计不再执行；当前发布结构和更新边界以 [发布与更新手册](../personal-runtime/08_release_and_updates.md) 为准。

## 0. 发布与元数据准备

- 打包完整程序为 ZIP；正式用户包和云端更新包都采用固定 envelope：

```text
<package>.zip
├─ XKE.exe
└─ XKE/
   ├─ Xke.DesktopHost.exe
   ├─ Xke.Updater.exe
   ├─ xke.runtime.json
   └─ web/
```

- 外层 `XKE.exe` 是根启动器，不是 Windows `.lnk` 快捷方式；它必须使用相对路径启动 `./XKE/Xke.DesktopHost.exe`。
- 发布包不得再使用 `.lnk` 或 `.cmd` 作为主启动入口。
- 计算 ZIP 的 SHA256。
- 上传到公开 HTTPS 静态下载地址：
  - `releases/<version>/<package>.zip`
  - `releases/<version>/release-notes.md`
- 更新 `cloud-feed.json.update_info`。
- 客户端继续通过 CloudSync 拉取 `cloud-feed.json`。

## 1. CloudSync 字段扩展

- 扩展 `RemotePolicyService.NormalizeUpdateInfo(...)`：
  - 保留现有 `latest_version`
  - 保留现有 `min_supported_version`
  - 保留现有 `download_url`
  - 保留现有 `force_upgrade`
  - 新增 `package_sha256`
  - 新增 `package_size`
  - 新增 `package_type`
  - 新增 `release_notes_url`
- `/api/config` 与 `/api/updates/feed` 继续透出这些字段。
- 旧字段缺失时保持空值，不影响公告和远程策略。

## 2. 存储层

- 在 `XkeDataRoot` 增加 `.updates` 路径：
  - `.updates/packages/`
  - `.updates/jobs/`
  - `.updates/backups/`
  - `.updates/install-plan.json`
- 新增 `XkeUpdateStore`：
  - 保存下载任务状态
  - 保存下载包路径
  - 保存安装计划
  - 清理过期失败任务
- 所有更新临时文件必须留在 `.runtime/data` 或 `XKE_DOTNET_DATA_DIR` 下。

## 3. 核心更新服务

- 新增 `Xke.Core.Update` 命名空间：
  - `UpdateManifest`
  - `UpdateStatus`
  - `UpdateDownloadJob`
  - `UpdateService`
  - `UpdatePackageVerifier`
- `UpdateService` 职责：
  - 从 CloudSync 当前 feed/config 解析更新信息
  - 比较当前版本和最新版本
  - 校验下载 URL 是 HTTPS
  - 创建下载任务
  - 下载到临时文件后原子重命名
  - 校验 SHA256 和包大小
  - 生成安装计划

## 4. WebHost API

- 新增 `UpdateEndpoints.cs`。
- 第一阶段端点：

```text
GET  /api/update/status
POST /api/update/download
GET  /api/update/jobs/{job_id}
POST /api/update/prepare-install
```

- `/api/update/status` 返回当前版本、最新版本、是否可更新、是否强制更新。
- `/api/update/download` 启动或复用下载任务。
- `/api/update/jobs/{job_id}` 查询下载进度和校验结果。
- `/api/update/prepare-install` 只写安装计划，不直接替换运行中的程序。

## 5. DesktopHost 桥接

- 扩展桌面桥：
  - `start_update_install`
- 桥接方法读取 `.updates/install-plan.json`。
- 启动 `Xke.Updater.exe`，传入：
  - 主进程 PID
  - 安装计划路径
  - 主程序启动路径
- DesktopHost 设置允许退出并关闭当前进程。

## 6. Updater 独立进程

- 新增 `src/Xke.Updater/Xke.Updater.csproj`。
- `Xke.Updater` 步骤：
  - 读取安装计划
  - 等待主进程退出
  - 校验安装目录和包路径都在允许边界
  - 备份旧程序文件
  - 解压新包到临时目录
  - 如果 ZIP 是 `XKE.exe` + `XKE/` envelope，只把 `XKE/` 作为真正 payload
  - 替换安装目录中的程序文件
  - 启动新的 `Xke.DesktopHost.exe`
  - 成功后标记安装完成
  - 失败时恢复备份并写失败状态

## 7. 前端最小入口

- 在关于 / 更新区域增加：
  - 检查更新
  - 下载更新
  - 立即安装并重启
- 当前 Web 前端只调用 API 和桌面桥，不直接处理文件系统。
- 如果运行在浏览器而不是 DesktopHost，安装按钮显示为不可用，并保留下载链接。

## 8. 测试与验收

- Core 测试：
  - 版本比较
  - manifest 字段归一化
  - SHA256 校验
  - 非 HTTPS 链接拒绝
  - 路径越界拒绝
- Storage 测试：
  - `.updates` 路径在数据根内
  - 任务状态保存和读取
- WebHost 测试：
  - `/api/update/status`
  - `/api/update/download`
  - `/api/update/prepare-install`
- Updater 测试：
  - 使用临时目录模拟安装目录
  - 替换成功
  - envelope 包不会安装成嵌套目录
  - 替换失败回滚
- Smoke：
  - 新增 `tools/update-smoke.ps1`
  - 本地生成测试 ZIP，走完整下载、校验、安装计划链路

## 9. 第二阶段增强

- 更新包签名校验。
- 差分更新。
- 静默下载但用户确认安装。
- 失败后在 UI 中显示可恢复状态。
- 与 WinUI 3 原生界面复用同一 UpdateService。
