# 当前焦点：一键自动更新第一阶段

> 历史方案：产品显示名称已统一为 XKE，以下旧 Updater/CloudSync 设计不再执行；当前发布结构和更新边界以 [发布与更新手册](../personal-runtime/08_release_and_updates.md) 为准。

## 当前切片

实现“用户主动点击的一键更新”基础链路：

```text
检查更新 -> 下载包 -> 校验包 -> 写安装计划 -> 启动 updater -> 主程序退出 -> 替换文件 -> 重启新版本
```

## 完成定义

- `cloud-feed.json` 的 `update_info` 扩展为可执行更新元数据。
- `/api/updates/feed` 与 `/api/config` 继续兼容现有前端字段。
- 新增更新领域模型，明确当前版本、目标版本、下载地址、包 hash、包大小、包类型。
- 下载包写入 `.runtime/data/.updates/packages/`。
- 下载任务状态写入 `.runtime/data/.updates/jobs/`。
- 安装计划写入 `.runtime/data/.updates/install-plan.json`。
- ZIP 包下载完成后必须校验 SHA256。
- `Xke.Updater` 可以在主程序退出后替换安装目录并重启 `Xke.DesktopHost`。
- 更新失败时保留旧版本可启动，不能破坏 `.runtime/data`。
- 正式发布 ZIP 根目录固定为 `XKE.exe` 启动器 + `XKE/` 程序文件夹，启动器只按相对位置启动文件夹内的 `Xke.DesktopHost.exe`。
- Web 前端只增加最小入口：检查更新、下载更新、立即安装并重启。
- 后续 WinUI 3 UI 可复用同一套更新服务。

## 第一阶段约束

- 默认只支持用户可写安装目录，例如 `%LocalAppData%\Programs\XKE\` 或开发输出目录。
- 如果检测到安装目录不可写，返回“需要手动更新或管理员安装”，不尝试提权。
- 当前只支持完整包替换，不做差分更新。
- 当前只要求 SHA256 校验；包签名作为第二阶段增强。
- 更新包必须是 HTTPS 下载地址。
- 不使用 `.lnk` 作为发布包入口，因为 Windows 快捷方式会固化打包机路径，用户解压到其他位置后会失效。
- 不使用 `.cmd` 作为主入口，避免用户看到脚本、控制台窗口或误判为临时工具。

## 发布包结构规范

手动分发包和云端更新包都必须使用同一 ZIP envelope：

```text
XKE.zip
├─ XKE.exe
└─ XKE/
   ├─ Xke.DesktopHost.exe
   ├─ Xke.Updater.exe
   ├─ xke.runtime.json
   └─ web/
```

- 外层 `XKE.exe` 是可移动启动器，必须按自身所在目录解析 `./XKE/Xke.DesktopHost.exe`。
- `XKE/` 才是实际安装目录；自动更新安装时只替换该目录内的程序文件，不把外层启动器复制成嵌套目录。
- `tools/package-desktop.ps1` 生成正式包时必须启用根启动器生成逻辑，并把启动器和同名程序文件夹一起压入 ZIP。

## 云端字段

`update_info` 第一阶段建议字段：

```json
{
  "updated_at": "2026-04-25T23:30:00+08:00",
  "latest_version": "0.10.0",
  "min_supported_version": "0.9.0",
  "download_url": "https://<RELEASE_BUCKET>/releases/<VERSION>/XKE-<VERSION>-win-x64.zip",
  "package_sha256": "<lowercase-hex-sha256>",
  "package_size": 12345678,
  "package_type": "zip",
  "force_upgrade": false,
  "release_notes_url": "https://<RELEASE_BUCKET>/releases/<VERSION>/release-notes.md"
}
```

真实发布桶、对象路径和临时验证 URL 只放在 `开发数据/公告发布/`，不写入开发文档。

## 风险边界

- 下载链接被篡改时，SHA256 必须拦截安装。
- 下载中断时，只保留失败状态，不写安装计划。
- 替换失败时，updater 必须尝试恢复旧程序目录。
- 安装计划只允许指向程序目录和更新包目录，不能接受任意路径覆盖。
