# Xke.ProtocolHost

独立的 .NET 8 ASP.NET Core 协议服务，用于个人服务器部署。复用 `DirectJwLoginProtocol`、`TraditionalJwClient`、现有资料/课表解析器及业务模型；不加载桌面宿主、网页、浏览器或 JavaScript。ddddocr 由独立 sidecar 提供。

## 配置与启动

必须显式配置以下环境变量；不读取桌面运行根或已有凭据。

| 环境变量 | 含义 |
| --- | --- |
| `XKE_PROTOCOL_DATA_DIR` | 独立、可写的绝对业务数据目录，须与桌面目录分开 |
| `XKE_PROTOCOL_API_KEY` | 非空 API key，所有业务请求通过 `X-XKE-Api-Key` 请求头传递 |
| `XKE_DDDDOCR_URL` | 完整识别地址，如 `http://127.0.0.1:8866/recognize`；HTTP 超时 10 秒 |
| `XKE_PROTOCOL_URLS` | 可选，分号分隔的监听地址；默认 `http://127.0.0.1:38444` |

Linux 示例（先启动项目提供的 ddddocr sidecar）：

```sh
export XKE_PROTOCOL_DATA_DIR=/var/lib/xke-protocol
export XKE_PROTOCOL_API_KEY='<YOUR_API_KEY>'
export XKE_DDDDOCR_URL=http://127.0.0.1:8866/recognize
dotnet run --project src/Xke.ProtocolHost/Xke.ProtocolHost.csproj
```

单独发布 Linux 服务，不需要构建桌面 solution：

```sh
dotnet publish src/Xke.ProtocolHost/Xke.ProtocolHost.csproj -c Release -r linux-x64 --self-contained false -p:UseAppHost=false -o /tmp/xke-protocol-publish
dotnet /tmp/xke-protocol-publish/Xke.ProtocolHost.dll
```

云服务器由 HTTPS 反向代理转发至默认 loopback 地址，并传递 API key 请求头。若需直接监听其它地址，显式设置 `XKE_PROTOCOL_URLS`。服务不包含多租户、选课、校园网管理、桌面设置或旧云业务。

服务器进程管理、固定 `/opt/xke` 路径及环境文件见 [systemd 模板](../../tools/protocol-host/README.md)。两个服务同机运行，OCR 固定 loopback；数据目录使用 `/var/lib/xke-protocol`。

## API

每进程只有一个已连接的学校账号。API key 是服务访问凭据；不生成 `session_id`，也不返回学校 Cookie。成功业务响应为 `{ok:true,msg,data}`，业务错误为 `{ok:false,msg,data:null,detail}`。

| 请求 | 输入与返回 |
| --- | --- |
| `GET /health` | 无需 key；返回 `{status:"ok",service:"xke-protocol"}`。只表示宿主可用，不表示学校或 OCR 可用 |
| `POST /api/auth/login` | JSON `{user,password}`；返回 `data:{connected:true,student:StudentProfileDocument}` |
| `GET /api/auth/session` | 只读取内存，不请求学校；已连接时返回相同连接/资料结构，否则 401 |
| `POST /api/auth/logout` | 释放内存客户端；返回 `data:{connected:false}`，保留业务文件 |
| `GET /api/auth/profile` | GET 学校资料并更新普通 `profile.json`；data 为完整 `StudentProfileDocument` |
| `GET /api/schedule/week` | 可选 `week` 正整数、`academic_term`；省略 week 查询学校当前周，历史学期需提供 week |
| `GET /api/schedule/semester` | 可选 `academic_term`；省略时查询学校当前学期；data 为 `SemesterScheduleData` |

单周 data 沿用 `ScheduleData` 字段，补充 `notes`、`supplementary_courses`；包含 student、term、rows、block_times、available_weeks、week_dates、current/viewed_week_number、updated_at、saved_files 等。周标签规范为 `第N周`，周日期仅从返回的对应周目录生成，历史日期无来源时为空。节次、周次、多个时间段及学时原文直接复用 Core 解析结果。

登录顺序为：获取登录页/验证码 → sidecar 识别 → 获取 seed/编码登录表单 → 学校登录提交 → GET 校验并读取本人资料。一次请求只提交登录一次，OCR/验证码错误不自动循环重试；failed login 保留之前的连接及业务文件。

登录包含必要的学校 POST；资料和课表仅发送学校 GET。登录、读取、刷新和注销共用一个信号量，避免 Cookie 会话并发变化。学校返回 HTTP 401 或失效 HTML/JSON 时，复用 Core 的失效分类，清理内存会话并返回 401；调用方需重新 POST login，不隐式 OCR/重登录。临时网络失败为 502、超时为 504；未查询到学期/周为 404，不回退其它周。无效查询参数/JSON 返回统一的 400 错误 envelope。

## 存储与生命周期

只调用 `XkeStudentStore.SaveProfile`、`XkeScheduleStore.SaveSourceSchedule` / `SaveSemesterSchedule`，使用同一结果生成普通 JSON 和 CSV：

```text
<XKE_PROTOCOL_DATA_DIR>/students/<server_student_id>/
  profile.json
  schedules/<academic_term>/weeks/<week>/jw_schedule.json
  schedules/<academic_term>/weeks/<week>/jw_schedule.csv
  schedules/<academic_term>/semester/semester_schedule.json
  schedules/<academic_term>/semester/semester_schedule.csv
```

不持久化密码、Cookie、运行 session、验证码图片或学校原始 HTML，也不读取桌面的 Credentials/Sessions/JwSessions/Settings store。业务存储不调用 Windows DPAPI；Core/Storage 的现有 Security 程序集引用不代表服务器启用了其 Windows 存储路径。服务重启后须重新登录，已有业务文件继续保留，没有旧文件兼容/迁移。

CSV 列沿用现有定义，数组以分号连接；JSON 是完整模型，CSV 是导出。无课程的正常周也写 JSON 和仅有表头的 CSV。解析/网络失败不会覆盖已有快照；文件按既有 storage 的每文件原子替换保存，JSON+CSV不是跨文件事务。

## 验证

```sh
dotnet test tests/Xke.ProtocolHost.Tests/Xke.ProtocolHost.Tests.csproj
```

测试启动实际 Kestrel，学校/OCR使用 fake handler 和临时数据根；覆盖 key、登录/失败、内存会话、资料、当前/指定周、学期、空周、失效、缓存保留及请求串行。此验证不代表真实账号密码登录、真实 OCR 准确率或云端部署已验收。
