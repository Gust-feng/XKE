# 迁移路线图

## 总目标
在保留现有 Web 前端可复用资产的前提下，把 XKE 从当前 Python 桌面宿主 + FastAPI + Web SPA，迁移到面向 Windows 的 `.NET` 架构。

## 当前主线
不再按“技术层逐个补骨架”的方式推进，改为：

1. 先让工程可构建、可测试、可运行
2. 先跑通 `DesktopHost + WebHost + 本地 HTTP` 宿主壳
3. 再按垂直切片逐步替换真实业务

## 固定约束
- 目标框架保持 `net8.0` / `net8.0-windows`
- 工具链优先适配当前可用 SDK
- 首轮继续保留本地 `WebHost + HTTP`
- 首轮不纳入 `WebView2`、托盘、自启、单实例、通知
- 前端和本地 API 可以随切片一起调整，不再承诺一次性冻结全部 legacy 契约

## 里程碑

### 里程碑 1：宿主壳可运行
- solution 可直接 `dotnet build`
- 有统一 `build/test/run` 入口
- `DesktopHost` 可启动窗口并托管 `WebHost` 生命周期
- `WebHost` 可提供 `/api/health`、`/api/config`
- 现有前端入口页可通过系统浏览器加载

### 里程碑 2：Auth 切片
- 迁登录
- 迁可信登录
- 迁会话恢复
- 前端主登录链路实际可用

### 里程碑 3：Notice / 控制面
- 迁公告本地读取与远端控制平面
- 迁访问策略、只读/封禁决策
- 收口 fail-closed 行为

### 里程碑 4：Schedule
- 迁课表抓取
- 迁本地缓存与展示
- 迁 Cookie 回退等周边读取链路

### 里程碑 5：Network / Feedback / Telemetry
- 迁校园网
- 迁反馈
- 迁遥测

### 里程碑 6：宿主增强
- 接入 `WebView2`
- 收口宿主桥
- 托盘、自启、单实例、通知

## 验收原则
- 每个切片必须同时落代码、测试和最小验收标准
- `tests/`、`fixtures/`、`tools/` 是强制交付物，不再后置
- 不把真实密钥、真实 token、真实 `.env`、真实 data 镜像复制进 `XKE.DotNet8/`
