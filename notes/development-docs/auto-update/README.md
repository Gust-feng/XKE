# 一键自动更新

> 历史方案：产品显示名称已统一为 XKE，以下旧 Updater/CloudSync 设计不再执行；当前发布结构和更新边界以 [发布与更新手册](../personal-runtime/08_release_and_updates.md) 为准。

## 项目定位

本项目把当前“版本提示 / 下载链接”升级为可执行的一键更新链路。

第一阶段目标是用户主动点击更新后，客户端完成下载、校验、退出、替换和重启；不做后台静默自动替换，不要求管理员权限安装。

## 阅读顺序

1. [00_current_focus.md](./00_current_focus.md)
2. [01_implementation_steps.md](./01_implementation_steps.md)
3. [02_task_board.md](./02_task_board.md)

## 目标边界

- 继续复用 CloudSync 的 `update_info` 作为云端版本入口。
- 更新包放在私有部署侧维护的公开 HTTPS 下载地址，例如 `releases/<version>/...` 路径。
- 发布 ZIP 的用户可见根目录必须是一个外层启动器 `XKE.exe` 加一个同名程序文件夹 `XKE/`；不要再把 `.lnk` 或 `.cmd` 作为主启动入口。
- 客户端必须校验 `package_sha256` 后才能进入安装阶段。
- 主程序不直接覆盖自己，安装替换交给独立 `Xke.Updater` 进程。
- 更新只替换程序目录，不触碰 `.runtime/data`、学生数据、凭据、课表缓存和 WebView2 用户数据。
- Web 前端和未来 WinUI 3 都只调用同一套更新能力。

## 非目标

- 不做无感后台静默强更。
- 不把下载链接等同于一键更新。
- 不在第一阶段支持 `Program Files` 下的管理员提权安装。
- 不把私钥、云端管理密钥或云存储写权限放入客户端。
- 不重构当前 Web UI；前端只增加最小更新动作入口。

## 与现有模块关系

- `CloudSync`：提供 `update_info`，是更新元数据入口。
- `Xke.Core.Update`：新增更新领域，负责版本判断、下载计划、校验和安装计划。
- `Xke.Storage`：新增 `.updates/` 本地临时目录，只保存更新包、任务状态和安装计划。
- `Xke.WebHost`：提供检查、下载、状态查询端点。
- `Xke.DesktopHost`：触发 updater 并退出当前进程。
- `Xke.Updater`：独立进程，等待主程序退出后替换文件并重启。
