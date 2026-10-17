# V1.0.0 发布与自动更新（2026-10-17）

## 当前边界

用户最新要求：版本定为 **1.0.0**，支持检查、自动下载与安装；个人本地软件不另建更新服务器。公开仓库已核对为 https://github.com/Gust-feng/XKE ，核对时没有Release。当前公开main仍是旧源码，工作区大量变更尚未提交；本轮没有stage/commit/push、打tag或公开Release，不能把旧main当本次版本源码。

本轮新增的是GitHub Releases + Velopack **1.2.161**，不是恢复已退役的云控制/遥测/旧Updater。用户账号、密码、Cookie、课表与校园网缓存都不发给GitHub，也不打包。只读取公开Release元数据与更新资产；SDK GitBase/GithubSource不向GitHub传本地staging ID。无需GitHub登录或PAT。

## 使用行为

- 系统标题旁的小版本入口打开应用更新面板，可查看项目主页/发布记录、检查、下载、立即更新并重启以及自动更新开关。
- 自动模式首次约30秒检查，此后约6小时检查一次；手动检查发现版本后，自动模式可在下一轮后台循环下载，不等待6小时。网络故障不阻断登录、课表或校园网。
- 下载由C#持有，不绑定面板/HTTP连接存活时间；关闭面板继续下载，真正退出会取消并排空在途工作。前端仅在面板可见时串行读取状态。
- 下载完成不强退正在使用的软件；可立即重启安装，或在真正退出/下次启动应用。关闭到托盘不安装。关闭自动模式会取消其后台工作，不自动应用已经下载的包；显式“立即更新并重启”仍可安装。
- VelopackApp.Run在自定义Main首行、WPF/数据访问/互斥之前处理安装器hook。关闭SDK无条件startup auto-apply，由本机偏好决定；正常退出先排空宿主，再启动官方独立更新器等待进程退出。
- 安装/便携版必须包含Velopack启动器、Update.exe、current及sq.version。直接运行bin/Debug不是受管理安装，不允许自我覆盖，只显示版本/开发状态；首次使用正式版前退出开发版，避免单实例保护唤回旧窗口。
- 只接受正式稳定前向更新；固定PackageId=XKE、channel=win、公开项目来源，无降级、预发布或任意更新URL。要求包路径为普通文件名、大小有效且SHA256完整，复用SDK校验下载；SDK原本跳过已有完整缓存，包装层会复验并移除损坏的下载缓存后重下。校验失败不标ready，不替换当前程序。
- SHA256是完整性校验，不是独立代码签名；当前包未配置Authenticode签名。不要声称完成了真实GitHub跨版本发布验收或应用健康检查回滚。

## API 与职责

- GET `/api/app/updates`：current_version、repository_url、release_url、latest_version、status、progress、error、checked_at。
- POST `/api/app/updates/check`、`/download`、`/apply`：同一状态模型。download只排队启动，HTTP不等待整个大包；apply排队安全退出，不在API线程直接杀进程。
- 桌面模式所有上述入口要求原生X-XKE-Desktop证明，不依赖学校session。自动更新开关为机器设置`auto_update_enabled`，DPAPI本地保存；默认启用，用户可关闭。
- Core/Releases只编排状态/节流/取消；DesktopHost/Updates接Velopack，WebHost只提供受控入口及独立worker；不引入新的更新服务器或通用远程执行入口。
- 主产品版本唯一来源Directory.Build.props Version=1.0.0。前端版本读API，不拿npm版本猜最新版本。health新增app_version/update_supported，方便验证实际运行包。

## 构建候选包

```powershell
npm --prefix frontend run build
pwsh -NoProfile -File tools/package-desktop.ps1
```

默认输出`artifacts/releases/1.0.0`，必须不存在或为空。固定三套.NET runtime pack **8.0.32** 自包含，local vpk与SDK固定1.2.161；WebView2由官方`--framework webview2` bootstrap处理，便携版仍需要可用的WebView2 Runtime。必要校园网自助地址可通过CampusSelfServiceConfigPath显式最小字段生成，但不是连接校园网的前提；默认不注入私有课表服务或DPAPI文件。

公开发布前须维护THIRD-PARTY-NOTICES.txt，依赖版本升级时更新其中许可/通知。此文件不替项目自身选择开源许可证，打包预检与归档检查要求其存在。

脚本仅接收严格runtime前端资源，独立staging生成，校验包/元数据/归档内容与hash后同卷移动；无.env、账号缓存、日志、token、协议抓包/研究资料。发布资源JSON与sq.version是程序元数据，不是个人缓存。

当前标准产物：

- `XKE-win-Setup.exe`：安装版。
- `XKE-win-Portable.zip`：便携版，解压后运行根目录XKE.exe，不单拷贝current里的exe。
- `XKE-1.0.0-full.nupkg`：自动更新完整包。
- `releases.win.json`：更新feed，不能遗漏。
- `assets.win.json`、`RELEASES`：官方打包元数据。

## 发布到GitHub（尚未执行）

1. 先获得当前改动提交/推送的明确授权；按文件审查敏感边界，不全仓库盲目暂存，不把旧main标成新版本。
2. 让版本1.0.0对应正确源码commit，创建正式tag `v1.0.0`，标题`V1.0.0`，不标prerelease。
3. 上传上述全部6个资产；只上传ZIP/Setup而漏掉full.nupkg和releases.win.json，客户端不能自动更新。
4. 后续同一PackageId/channel以递增版本发布，比如1.0.1；不覆盖已有同版本资产。无需另搭更新接口。
5. 本地已验证的候选包不是已经公开的GitHub Release。源码许可证由用户确定，不擅自替项目选择。

## 验证边界

- 专用Windows测试覆盖真实Velopack SDK坏包校验、坏完整缓存重下、包身份/路径/稳定版本与未安装环境。
- Core/HTTP测试覆盖非阻塞下载、取消、6小时节流、显式安装与关闭自动模式、本机证明和不依赖学校登录。
- 合成独立PackId微型便携程序已真实完成1.0.0→1.0.1的校验下载、退出替换与自动重启，外部合成数据保持；不是实际XKE账号或GitHub发布端到端验收。
- 候选包已在仓库之外、临时数据根启动，确认版本1.0.0、update_supported=true、无账号会话并正常关闭，不借用开发目录；没有改真实注册表/账号。当前开发版已重启但不自我覆盖。
- 最终证据：Windows SDK 13项、后端83项、前端214项、发布门禁79项通过；汇总见任务板顶部与ignored artifacts/v1.0.0-verification.json。
- GitHub线上发布/真实XKE跨版本更新、缺WebView2新机安装及代码签名未验收；不将本地合成升级等同全部验收。

官方参考：https://docs.velopack.io/integrating/overview 、https://docs.velopack.io/packaging/bootstrapping 。

## 正式界面查看（2026-10-17后续）

用户已切到artifacts/desktop-release-preview/1.0.0的正式便携版，根启动器XKE.exe；开发/正式JS和CSS完全一致，能力状态须真实。原数据根不搬移，具体进程及核对见任务板顶部；不代表已公开发布。
