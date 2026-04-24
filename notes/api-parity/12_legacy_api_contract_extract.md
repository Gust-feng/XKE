# Legacy API 契约提炼

本文件记录旧 XKE 文档中仍需要被 `XKE.DotNet8` 兼容或明确后置的 API 结论，不复制旧契约全文。

## 保持稳定的契约

- 所有 WebHost API 继续使用 `{ ok, msg, data }` envelope。
- session 型接口继续支持 `X-XKE-Session` header 与 `session_id` query。
- blocked 学生继续返回现有 403 detail，不在业务 endpoint 中绕过 Notice access policy。
- `AuthPayload.connected` 表示当前进程内移动教务最近验证成功，不是本地工作区恢复前置条件。

## 当前主线 API

- `/api/auth/login`：未初始化账号执行移动教务首次初始化；已有本地凭据只做本地校验。
- `/api/auth/trusted-login`：基于本地 trusted token 恢复工作区。
- `/api/auth/session`：恢复本地 session，不访问远端。
- `/api/auth/connect`：重新验证移动教务数据源状态。
- `/api/schedule/preferred`：移动教务课表刷新入口。
- `/api/schedule/local`：本地课表缓存读取入口。

## 后置 API

- `/api/schedule/cookie`：传统教务 Cookie 课表入口，当前 deferred。
- `/api/course/connect`、`/api/course/rounds`、`/api/course/preview`：Course 框架存在，真实传统教务连接后置。
- `/api/course/grab/start`、`/api/course/grab/stop`：真实抢课后置，当前不得启动线程或访问 JW mutation。

## 提炼规则

- 旧契约中的字段只有被当前前端或 WebHost 测试消费时才进入 .NET 主契约。
- 涉及真实 Cookie、token、账号、请求头的旧契约内容只记录语义，不记录值。
- 如果旧契约与当前移动教务主线冲突，以当前 `roadmap` 和 `sensitive-index/移动教务` 为准。
