> 2026-10-10：用于产品第一版React runtime的隔离HTTP联调，默认托管frontend/dist。新增当前/历史学期fixture及节次/周次/时间数组；不使用学校真实凭据或执行系统修改。preview资源不会因此变为runtime。

# 隔离前后端联调宿主

这是测试辅助程序，不是生产宿主，不加入产品 solution 或发布包。它托管真实 `Xke.WebHost` 路由和 `frontend/dist`，使用全新的临时数据根以及测试替身；不会读取真实账号、私有课表服务配置、Cookie 或旧运行数据。

## 启动

先在 `frontend/` 执行 `npm run build`，再在仓库根执行：

```bash
DOTNET_CLI_HOME=Z:/XKE/.dotnet_cli DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet run --project tests/Xke.FrontendIntegrationHost -- frontend/dist
```

Windows PowerShell 可先分别设置相同环境变量，再执行 `dotnet run`。终端输出 `INTEGRATION_URL`，浏览器打开该地址；端口动态分配，不写入前端配置。也可以通过第一个参数指定其他已构建资源目录。

## 固定测试场景

- 使用合成学号 `20260001`、合成密码 `test-password`，验证码图片内容为 `1234`。这些不是学校真实凭据。
- 首次初始化成功；错误登录验证码走真实后端错误路径，原 challenge 已被消费，需要重新获取。
- 首次课表刷新需要验证码。错误课表验证码可在原 challenge 重试，`1234` 完成刷新。
- 下一次刷新返回 503，应保持原缓存并显示失败；之后刷新成功。
- 提供第 8、9 周选项，初始只有第 8 周缓存；第 9 周缓存读取会返回真实 404。
- 学期接口提供2026-2027-1和2025-2026-2合成目录；未知学期返回404。学期条目带完整节次/周次数组，时间待定条目保留。
- 课表包含不规则 `2-4` 节课程及时间/星期待定课程，验证前端不丢弃未知课程。
- 所有教务 HTTP 都由测试 handler 回答。校园网 HTTP 为离线替身，系统命令不执行，自启动注册只改变内存。后台任务关闭。
- HostMode 为 `desktop-webview`，便于验证桌面可信恢复 API；此程序并不创建 WPF 窗口或注入 WebView2 桥接，不能替代真实桌面验收。

## 停止

前台按 Ctrl+C 正常停止，或向本次动态地址的 `POST /__integration/stop` 发送请求。程序最多运行 30 分钟；正常退出时只删除本程序新建的临时目录。强制终止进程可能留下测试目录，不代表产品数据发生变化。

测试通过仅证明本地 UI/API/状态流，不证明学校服务、真实校园网、自启动、托盘或发布包已经验收。
