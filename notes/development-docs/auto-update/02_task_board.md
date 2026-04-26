# 一键自动更新任务板

## In Progress

- 运行构建、后端测试、更新 smoke 与前端 smoke，确认本轮实现可用。

## Pending

- 后续发布流水线仍需要生成完整 ZIP 包、计算 hash 并上传到正式 HTTPS 发布目录。
- 第二阶段再考虑包签名、差分包、管理员提权或 WinUI 3 原生更新界面。

## Done

- 确认当前项目已有 CloudSync `update_info`、版本提示、下载链接展示基础。
- 确认一键更新必须使用独立 updater，主程序不直接覆盖自己。
- 确认第一阶段只做用户触发更新，不做后台静默强更。
- 确认更新临时文件必须留在 `.runtime/data/.updates/` 或覆盖数据根下。
- 扩展 `update_info` 字段透传：`package_sha256`、`package_size`、`package_type`、`release_notes_url`。
- 在 `XkeDataRoot` 和存储层加入 `.updates` 本地目录。
- 新增 `Xke.Core.Update` 领域模型和服务。
- 新增 `/api/update/*` WebHost 端点。
- 新增 `Xke.Updater` 独立进程项目。
- 扩展 DesktopHost 桥接方法 `start_update_install`。
- 在 Web 前端关于页增加两步更新入口，移除下载地址展示。
- 新增 `tools/update-smoke.ps1` 和临时包验收测试。
- 修复桌面打包入口，发布 `Xke.DesktopHost` 时同步发布并校验 `Xke.Updater` 产物。

## Blockers / Decisions

- 需要确定正式安装目录是否为用户可写目录。第一阶段默认不支持需要管理员权限的 `Program Files`。
- 需要确定发布包格式。第一阶段建议完整 ZIP 包。
- 需要确定包下载地址。建议使用私有部署侧维护的公开 HTTPS 静态下载目录，路径形如 `releases/<version>/`。
