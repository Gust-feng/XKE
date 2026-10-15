# Linux systemd 部署文件

这里提供两个同主机 systemd unit 模板和占位环境文件。它们使用共同的非 root 用户/组 `xke`。仓库验证仅生成 Linux 编译产物，不执行安装、部署或启动服务。

服务器目录约定：

| 路径 | 内容 |
| --- | --- |
| `/opt/xke/protocol/` | `dotnet publish` 生成的协议服务文件 |
| `/opt/xke/ocr-venv/` | 使用 `tools/ocr/requirements.txt` 安装依赖的 Python venv |
| `/opt/xke/tools/ocr/` | 项目的 `server.py`，使用其固定的 loopback 监听 |
| `/etc/xke/protocol.env` | 从 `protocol.env.example` 复制的运行配置，部署者替换 API key |
| `/var/lib/xke-protocol/` | 业务 JSON/CSV；由协议 unit 的 `StateDirectory=xke-protocol` 创建并交给 `xke` |

部署者先创建 `xke` 系统用户/组，安装 .NET 8 ASP.NET Core runtime，确认 `/usr/bin/dotnet`，准备上述文件及 OCR venv。环境文件保留 `root:root`、权限 `600` 即可，systemd 以服务管理器权限读取并传入；不要放学校密码或 Cookie。`/opt/xke` 中程序、脚本和模型必须允许 `xke` 读取/执行。

准备完成后，将两份 `.service` 文件复制到 `/etc/systemd/system/`，运行 `systemctl daemon-reload` 和 `systemctl enable --now xke-protocol.service`。这些操作在服务器上由部署者执行，本次开发未执行。协议 unit 使用 `Wants/After` 启动 OCR；OCR 单独失败/重启时保留协议进程及其内存学校会话，已有 Cookie 的资料/课表 GET 仍可使用。

`After` 只保证进程启动顺序，不保证模型已经载入。以 `http://127.0.0.1:8866/health` 的 `status=ready` 确认 OCR 就绪；随后通过携带 `X-XKE-Api-Key` 的 login 请求连接学校。协议 `/health` 只检查宿主。OCR 未就绪或不可用时 login 返回 502/504，由调用方重试，不自动循环学校登录。

两个服务均默认监听 loopback。对外访问由 HTTPS 反向代理转发协议端口，OCR 8866 不对外发布。API、存储和生命周期见 [协议宿主说明](../../src/Xke.ProtocolHost/README.md)；OCR 使用项目的 [现有 sidecar](../ocr/README.md)，此目录不实现识别脚本。

仓库中的独立发布命令：

```sh
dotnet publish src/Xke.ProtocolHost/Xke.ProtocolHost.csproj -c Release --no-self-contained -r linux-x64 -p:UseAppHost=false -o .runtime/protocol-linux
```

`.runtime/protocol-linux` 只是编译产物，不是已部署的服务器目录。没有旧数据迁移、Docker 编排或自动部署脚本。
