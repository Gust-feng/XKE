# 独立协议服务与云部署

更新：2026-10-10。协议服务是新增开发版本，未实际部署到云服务器。
登录全链路和共享解析规则见 [后端技术手册](05_backend_technical_manual.md)。

## 运行边界

`src/Xke.ProtocolHost/` 是 .NET 8 ASP.NET Core 无界面宿主，复用 `DirectJwLoginProtocol`、
`TraditionalJwClient`、AngleSharp 解析器及普通业务文件存储。
不引用 WebHost/DesktopHost，不启动浏览器、WebView2、WPF、Windows 校园网或桌面调度。
Core 的依赖图包含 Security 程序集，但本服务不调用 DPAPI；资料和课表文件跨平台。

当前React连接本机WebHost；ProtocolHost供独立HTTP调用，鉴权与路由按下表使用，不是桌面API的直接替代。
桌面通过 `ProtocolScheduleApiClient` 读取已连接服务的周/学期接口；由本机WebHost继续提供前端API。配置 `provider=protocol` 才启用，不需要修改 `VITE_XKE_API_BASE_URL`。

个人服务每进程只保留一个学校连接，所有学校请求串行，避免 Cookie 上下文互相覆盖。
不同账户可以显式重新登录并替换连接；没有多租户或管理员体系。
登录失败保留原连接和已保存业务快照。重启、注销或发现学校 401 后需重新调用登录。
账号密码/学校 Cookie 只在活跃客户端内存，未开放外部 Cookie 上传接口，也不返回学校 Cookie。
不保存云端凭据、不读取 Windows 本地密码目录；云端长期自动重登录与定时采集属于后续切片。

## 配置

| 环境变量 | 含义 |
| --- | --- |
| `XKE_PROTOCOL_API_KEY` | 必填，客户端每次请求使用 `X-XKE-Api-Key`，启动时不允许为空 |
| `XKE_PROTOCOL_DATA_DIR` | 必填独立绝对目录，例如 `/var/lib/xke-protocol` |
| `XKE_DDDDOCR_URL` | 必填识别地址，`http://127.0.0.1:8866/recognize` |
| `XKE_PROTOCOL_URLS` | 可选，默认 `http://127.0.0.1:38444`，多个地址以分号分隔 |

默认只监听 loopback。远程使用时由 HTTPS 反向代理转发此端口，OCR 保持本机端口。
API key 是个人服务的最小访问边界；不让学校密码/Cookie裸露在公网 API 中。
密钥与真实部署地址放宿主私有环境文件，不写入仓库。

## API

`/health` 无需 API key，其余接口均需 `X-XKE-Api-Key`。
普通接口沿用 `{ok,msg,data}` / `{ok:false,msg,data:null,detail}`；不使用桌面 session_id。

| 方法与路径 | 参数与行为 |
| --- | --- |
| GET `/health` | 仅服务存活信息，不证明 OCR 或学校在线 |
| POST `/api/auth/login` | JSON `{user,password}`；取图片→ddddocr→学校表单POST→资料确认→保存profile；返回 `{connected:true,student:...}` |
| GET `/api/auth/session` | 读取进程内连接状态；没有连接时 401，不重新验证学校 |
| POST `/api/auth/logout` | 释放学校客户端，业务文件保留 |
| GET `/api/auth/profile` | 重新 GET 学籍卡片，更新普通 profile.json |
| GET `/api/schedule/week` | 可选 `week=<INTEGER>`、`academic_term=<TERM>`；未指定week查询当前周 |
| GET `/api/schedule/semester` | 可选 `academic_term=<TERM>`；未指定为服务器当前学期 |

历史学期单周必须指定 week。参数只转换为已确认学校 GET 参数，不做任意URL代理。
成功课表按学生/学期落盘 JSON+CSV，与桌面业务格式一致；不落原始 HTML 或验证码。
正常空周可写入，学校登录 HTML/失效 JSON 会返回 401并断开内存连接；失败不覆盖上次快照。
协议版没有个人快照服务优先/回退，因为它自身就是直接学校协议的数据提供端。

错误边界：请求缺字段或非法周次 400；缺 API key/学校连接/登录拒绝 401；不可查询学期周次 404；
学校/OCR响应错误 502；HTTP/OCR超时 504；业务目录写失败 500。
验证码自动识别仅尝试本次图片，学校拒绝后由调用方重新登录取新图片，不无限循环提交。

## 桌面消费配置

本机私有 `xke.schedule-api.json`（或 `XKE_SCHEDULE_API_CONFIG` 指定文件）示例：

```json
{
  "provider": "protocol",
  "endpoint": "https://protocol.example.test/",
  "protected_token_file": "protocol-key.json",
  "timeout_seconds": 8,
  "max_age_hours": 36
}
```

endpoint是协议服务基地址，可含反向代理路径前缀；使用HTTPS。`protocol-key.json`为本机现有DPAPI存储写入的API key文件，相对配置目录解析，不能直接写普通JSON字符串冒充。真实配置继续放ignored私有位置。
默认provider为snapshot，继续使用已存在的个人快照API。protocol采用 `X-XKE-Api-Key` 与 `{ok,msg,data}`，由桌面后端读取，不将地址、密钥传给React。桌面只发课表GET，服务必须已由调用方登录；401/超时/错误回退直接JW，不自动上传桌面学校凭据。周和学期获取时间保留，不能用本机下载时间重标云数据。

## 本地启动

Python 推荐3.11/3.12；模型依赖固定为 `ddddocr==1.6.1`，CPU推理。

```sh
python3 -m venv .runtime/ocr-venv
.runtime/ocr-venv/bin/pip install -r tools/ocr/requirements.txt
.runtime/ocr-venv/bin/python tools/ocr/server.py
```

另一个终端配置个人私有 key 与独立目录，再启动协议服务：

```sh
export XKE_PROTOCOL_API_KEY='<PRIVATE_API_KEY>'
export XKE_PROTOCOL_DATA_DIR='/absolute/path/to/protocol-data'
export XKE_DDDDOCR_URL='http://127.0.0.1:8866/recognize'
dotnet run --project src/Xke.ProtocolHost
```

Windows 可用 `.runtime/ocr-venv/Scripts/python.exe`，通过 `$env:<NAME>` 设置相同环境变量。
生成请求时从私有环境读取学校账号密码，不把真实值写在命令示例、shell history或日志中。

## Linux部署步骤

1. 安装 .NET8 ASP.NET Core Runtime、Python3及venv支持，建立普通服务用户 `xke`。
2. 构建协议服务到独立目录：

   ```sh
   dotnet publish src/Xke.ProtocolHost -c Release -r linux-x64 --no-self-contained -o artifacts/protocol-linux
   ```

3. 将产物放 `/opt/xke/protocol`；复制 `tools/ocr` 到 `/opt/xke/tools/ocr`。
4. 在 `/opt/xke/ocr-venv` 建立Python虚拟环境，安装 OCR requirements。先启动验证 `/health`，模型只加载一次。
5. 建立 `/var/lib/xke-protocol` 并赋予xke写权限；从
   [env模板](../../../tools/protocol-host/protocol.env.example) 生成宿主私有 `/etc/xke/protocol.env`。
6. 安装 [OCR服务](../../../tools/protocol-host/xke-ocr.service) 和
   [协议服务](../../../tools/protocol-host/xke-protocol.service)，确认部署路径，然后启用两项服务。
7. HTTPS反向代理只转发协议端口，客户端带API key。不要把OCR端口直接对外发布。

此部署形态无数据库、队列、Redis和容器编排需求。服务重启后由调用方登录，文件仍可供后续任务使用。
真实云服务地址与代理域名由部署时配置，不硬编码到公开代码。

## 验证记录

- ddddocr 1.6.1 在Windows Python3.12隔离环境实际加载，合成图片“1234”识别正确。
- C# OCR契约11项、Python HTTP服务5项通过，包含错误图片/错误响应/取消。
- 协议服务实际Kestrel定向测试25/25通过，0 skipped；Linux x64目标发布构建成功，产物在ignored的 `.runtime/protocol-linux`。这不等于Linux运行或云端部署。
- 用户确认既有表单登录协议此前已实测可用，本轮复用。新增OCR组合未使用学校真实密码重新实登，未测真实验证码准确率，也未在Linux服务器运行或部署。
  假学校handler验证流程和持久化，不替代这些验收。
