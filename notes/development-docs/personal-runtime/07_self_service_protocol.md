# 校园网自助后台协议整理

## 2026-10-15 凭据来源更新

用户要求校园网独立账号登录。当前CampusSelfServiceSummaryService从当前学生NetworkCredentials().Load("campus")取账号/密码，缺失时返回“请先配置校园网账号和密码”，不再读取教务主凭据。缓存身份由学生/校园网账号/密码共同决定，变更后丢弃旧摘要与客户端。下方2026-10-14复用主凭据的实现与验证记录为历史，不覆盖此决定；客户端仍只读白名单路径，不新增实际登录验收结论。

更新：2026-10-14。独立 C# 客户端、白名单解析、内存缓存和本机 summary API 已实现；真实 HTTP 登录、dashboard 五个白名单字段及同客户端会话复用已验证。前端展示与桌面重启由主线程验收，下方历史浏览器观察不替代当前验证范围。

## 1. 边界

自助后台与校园网认证门户是两套独立系统：

| 系统 | 作用 | 当前实现边界 |
| --- | --- | --- |
| 校园网认证门户 | 上网认证、在线状态和外网放行 | 由 `NetworkService` 负责，本文不改动 |
| 自助后台 | 当前仅套餐、状态与服务器计费周期 | 独立 `CampusSelfServiceClient` / `CampusSelfServiceSummaryService`，`GET /api/network/summary`；不并入 `NetworkService` |

自助后台不是 JWT 接口。登录后使用服务端会话 Cookie；产品正式运行时不依赖浏览器，应由独立 `HttpClient` 和 `CookieContainer` 持有会话。

真实地址只放在本机私有配置中。文档和代码使用 `<SELF_HOST>` 占位符，不把个人网络地址写入公共契约或测试样本。

## 2. 登录链路

```text
GET  http://<SELF_HOST>:8080/Self/login
  -> 302 /Self/login/，客户端直接使用该规范路径
  -> 读取表单 action、隐藏字段与 foo/bar/code
GET  http://<SELF_HOST>:8080/Self/login/randomCode
  -> 同 Cookie 会话初始化；即使图片隐藏也保留该请求，响应仅内存丢弃
POST http://<SELF_HOST>:8080/Self/login/verify
  Content-Type: application/x-www-form-urlencoded
  -> 302 /Self/dashboard
GET  http://<SELF_HOST>:8080/Self/dashboard
```

### 登录字段

当前观察到的提交字段：

```text
foo
bar
checkcode
account
password
code
```

`foo`、`bar` 是隐藏/屏外字段，不能省略。正式客户端应先解析登录页的 hidden input，再覆盖 `account`、`password`，不要把隐藏字段值硬编码。

2026-10-14实际客户端补充：保留登录页 `Referer` 和同源 `Origin` 请求头、先初始化 randomCode 后以空 code 提交，成功302到dashboard。首次 Tomcat 表单 action 可能带 `;jsessionid`；客户端只认可同源固定 login/verify 路径，去掉该 URL 会话参数，使用已收到的独立 CookieContainer。禁止自动跟随任意重定向，尤其不能误调 logout/tooffline；仅明确登录路径和 dashboard GET 可达。

当前登录页脚本显示 `md5=false`，本次密码没有前端 MD5。页面虽然请求了 `randomCode`，但当前登录没有可见验证码；客户端应保留空验证码字段的兼容性，不应把“当前无验证码”写成永久协议保证。

### 会话

- Cookie 名称：`JSESSIONID`
- 作用域：自助后台的 `/Self` 路径
- 属性：当前观察为 HttpOnly、非 Secure 的会话 Cookie
- 不转换为 JWT，不跨域复用认证门户 Cookie
- 不向上层返回 Cookie 原值，不写入业务 JSON/CSV

已有会话读 dashboard 遇到登录页时，客户端在本次请求内重登一次；再次失败返回简短状态并释放该客户端。无验证码识别、无新登录页、无无限重试，不读取或复用浏览器 Cookie。

## 3. 已确认的只读接口

以下接口均在有效后台会话下通过 GET 观察。查询参数中的时间戳、分页值和排序值由客户端按接口契约生成，真实值不进入日志。

| 接口 | 查询参数 | 返回形态 | 结论 |
| --- | --- | --- | --- |
| `/Self/dashboard` | 无 | HTML | 只从页面可见卡片提取关键摘要，不保存整页 |
| `/Self/dashboard/getOnlineList` | `t`、`order`、`_` | JSON 对象数组 | 在线会话列表；空数组表示当前没有可展示的在线会话 |
| `/Self/dashboard/getLoginHistory` | `t`、`order`、`_` | `text/plain` 包裹 JSON 数组 | 每行 12 列数组，目前只冻结前 10 列，末两列语义未知 |
| `/Self/bill/getUserOnlineLog` | `pageSize`、`pageNumber`、`sortName`、`sortOrder`、`startTime`、`endTime` | `{summary,total,rows}` | 上网明细和汇总 |
| `/Self/bill/getMonthPay` | `pageSize`、`pageNumber`、`sortName`、`sortOrder`、`year` | `{summary,total,rows}` | 月账单；rows 是 8 列数组，列语义需按页面列定义映射 |
| `/Self/bill/getPayMent` | `pageSize`、`pageNumber`、`sortName`、`sortOrder`、`startTime`、`endTime` | `{summary,total,rows}` | 充值明细 |
| `/Self/bill/getOperatorLog` | `pageSize`、`pageNumber`、`sortName`、`sortOrder`、`startTime`、`endTime` | `{total,rows}` | 业务办理记录；rows 是 5 列数组 |
| `/Self/service/getMacList` | `pageSize`、`pageNumber`、`sortName`、`sortOrder` | 文本/JSON 混合响应 | 绑定设备列表，不等同在线会话 |

页面加载附带的 `/Self/dashboard/refreshaccount` 和 `/Self/dashboard/refreshMauthType` 是状态刷新请求，不作为关键业务数据来源，也不保存原始响应。

## 4. 第一版数据裁剪

根据当前账号的实际返回结果，第一版不应把后台所有字段都包装成“有效数据”。当前账号是免费套餐，金额类字段没有业务意义，部分用量字段只是服务器保留列或累计展示。

### 第一版暂时只保留

- 账号状态
- 套餐名称
- 计费方式
- 当前计费周期的服务器起止时间

这些字段用于判断后台账号是否正常、当前套餐和服务器当前周期，不需要读取个人资料。

### 已观察但暂不纳入模型

- 账户余额：当前返回为零，且与免费套餐场景不匹配，暂按无效/占位字段处理；不进入 JSON/CSV。
- 首页累计时长、累计流量：能读取，但更像历史累计计数，当前没有明确产品用途，默认不保存。
- 在线会话和近期记录：当前在线列表为空；近期记录虽然有时间/IP/MAC，但时长、流量、金额均为零，暂不作为有效业务数据。
- 日上网记录：当前查询结果的时长、流量、费用、上下行流量和 NAS 端口均为零，只有时间和网络标识有值，暂不保存。
- 月账单：账单周期和套餐有值，但基本月租、计费金额均为零，使用时长/流量只有部分记录有值，不进入第一版。
- 充值明细：当前没有记录，不建立空的业务模型。
- 绑定设备：当前没有匹配记录，且它与在线会话不是同一概念，不作为在线状态来源。
- 业务办理记录：可读但不是核心数据，暂不接入。

暂不获取或保存：

- 姓名、手机号、用户资料卡片
- 页面内嵌对象的完整内容
- `userPassword` 等密码相关字段
- 运营商绑定密码
- 未确认语义的数组末列
- 原始 HTML、完整响应、完整请求头和 Cookie

页面响应中可能混有密码相关字段，因此实现必须采用白名单抽取，不能将整个 `window.user` 或原始响应直接序列化。

## 5. 实际账号返回结果判断

本次只统计字段是否为空或为零，不记录真实账号值：

| 页面/接口 | 当前观察 | 判断 |
| --- | --- | --- |
| 首页在线信息 | 0 条 | 不作为在线状态来源 |
| 首页近期记录 | 5 条，时长/流量/金额均为 0 | 记录存在，但当前业务信息无效 |
| 上网记录 | 3 条，时长/流量/费用/上下行流量均为 0 | 不保存 |
| 月账单 | 10 条，金额类字段均为 0；用量列部分为 0 | 不保存 |
| 充值明细 | 0 条 | 不接入 |
| 绑定设备 | 0 条 | 不接入 |
| 业务办理记录 | 2 条 | 非核心，暂不接入 |

因此当前推荐的最小数据模型只有“账号状态 + 套餐 + 计费周期”。以后只有当账号实际出现非零且有明确用途的用量数据时，再扩展用量模型；不为潜在字段预留一整套前端展示。

## 6. 单位和解析规则

- 本版计费周期起止保留服务器显示字符串，不猜时区、不转换为 `DateTimeOffset`；`updated_at` 单独使用本机 UTC 抓取时间。历史记录中的时长/流量换算不属于本版。
- 页面展示的时长单位为分钟、流量单位为 M；在线列表内部时长和流量字段存在换算，正式模型冻结前必须以页面 formatter 和脱敏响应测试确认，不能直接把原始数值当作 MB 或秒。
- 数组响应先按页面列定义映射为命名对象，再交给上层；未知列丢弃但保留诊断字段名，不把位置数组直接暴露给前端。
- 当前在线列表与绑定设备列表分开建模，不能用绑定设备数量代替在线设备数量。
- 同一计费周期可能有多条账单记录，不能按周期简单去重，也不能把累计用量与历史账单相加。

## 7. 实现顺序

1. 新建独立 `CampusSelfServiceClient`，只负责后台登录、会话和 GET 请求；不并入认证门户的 `NetworkService`。
2. 新建白名单解析器，第一版只覆盖 dashboard 的状态、套餐和计费周期；不解析无用途的金额、累计用量和空记录。
3. 增加脱敏 HTML/JSON fixture 和单元测试，测试只验证字段名、类型、单位和登录重定向，不保存真实响应。
4. 冻结最小 `CampusSelfServiceSummary` 契约后，再增加 WebHost 只读接口；前端只消费统一对象，不解析学校原始数据。
5. 最后决定是否物化 JSON/CSV。当前字段很少且变化价值有限，优先保留内存快照，不为了“数据完整”保存无用个人记录。

## 8. 明确禁止的动作

本协议客户端不调用以下接口：

- `/Self/login/logout`
- `/Self/dashboard/tooffline`
- `/Self/dashboard/oprateMauthAction`
- 运营商绑定、解绑、修改密码和资料提交接口

它们分别会注销会话、踢下线设备或修改账户状态，不属于只读协议。

## 9. 已实现的 summary 切片与接入配置（2026-10-14）

- `CampusSelfServiceParser` 只读 dashboard `.row`：首列含 `text-right`、去空白后的标签严格为“状态/套餐/计费方式/计费周期”，值只取相邻列；周期以实际页面“至”分隔。真实标签包括全角空格，解析规则已经核对实际 DOM。无脚本执行，不解析 `window.user`。
- 客户端独立超时和 CookieContainer。请求范围仅 login/、login/randomCode、login/verify、dashboard；不请求账单、个人资料、在线列表或绑定列表。
- 凭据复用 `XkeDataRoot.Credentials().Load(session.User)` 的已有受控校园账号密码，与当前运营商选择无关；没有新增明文凭据配置。API要求现有应用session。
- 独立服务只缓存当前账号快照，成功5分钟、并发成功请求合并；账号或密码变化清空自助会话和快照。到期获取失败保留同账号最后成功值和原 `updated_at`，失败不冷却，下一次显式重试实际请求。缓存不落盘，不接30秒门户status轮询或保活任务。
- JSON契约见03：平铺五个业务字段和 configured/available/cached/updated_at/message，缺失为null。`available` 表示有可展示白名单值，不能当作互联网已连接；`cached=true` 可能是TTL命中或失败后保留，后者有简短message。

配置读取顺序：`XKE_CAMPUS_SELF_SERVICE_CONFIG` 显式文件 → 可执行目录 `xke.campus-self-service.json` → 开发目录 `开发数据/校园网/self-service.local.json`。显式文件缺失/畸形不静默回退；没有配置时不联网。

公开模板（实际host只写ignored私有文件）：

```json
{"endpoint":"http://<SELF_HOST>:<PORT>/Self/","timeout_seconds":8}
```

主线程已在上述ignored开发目录配置endpoint并重启桌面，文件不含密码；本机summary接口返回available=true、五个业务字段有值，再次读取命中成功缓存。发布环境可由启动环境变量指向私有文件，或显式复制到可执行目录。当前打包脚本只自动注入课表配置，没有自动打包该新增自助配置，不要将开发目录发现机制误认为发布已配置。

最终工作区验证：独立HTTP/C#实登及桌面summary接口均取得五字段，重复API读取命中缓存，未保存dashboard/验证码响应。当前6项自助测试覆盖白名单/部分和错误页面、登录字段与初始化顺序、会话复用和过期重登、同账号缓存/失败保留及立即重试、账号/凭据切换、应用session与未配置状态；连同现有校园网与保活共11/11，0跳过，隔离输出 `artifacts/network-summary-validation`。前端100项定向、10项限定合成Edge布局检查与桌面构建通过，桌面已重启；未执行禁用业务接口，也不把这些验证扩大为所有学校/Windows/发布验收。
