# 当前开发焦点

## 切片名称

`jw-protocol-login-v1`

## 目标

把教务系统表单协议接入推进到可用的登录与课表闭环：

```text
JW 登录页
-> 验证码 challenge
-> encoded 表单提交
-> 拉取传统教务课表 HTML
-> 解析为统一课表 rows
-> 保存 jw_schedule
-> 本地读取
```

## 第一阶段策略

先离线、再接口、最后真实登录。

登录、重新初始化与课表刷新统一使用 JW 表单协议。成功会话保存本次 JW/IP 登录域实际下发的完整 Cookie 集合到 DPAPI 保护文件；下次登录先用 GET 校验，失效即删除并重新完成验证码登录。

验证码策略：

- v1 先做人工验证码识别。
- 后端负责预登录上下文、验证码图片、challenge 状态和后续登录提交。
- Web 前端只负责展示验证码图片、收集用户输入、提交验证码。
- 后续 OCR 只作为验证码求解器的替换实现；OCR 失败时必须能回退人工输入。

## 当前第一个任务

实现传统教务 HTML 课表离线解析：

- 输入：`fixtures/schedule/sample_cookie_schedule.html`
- 辅助输入：`fixtures/schedule/sample_available_weeks.json`
- 输出：统一课表 rows
- 来源标记：`source_key=jw_schedule`
- 来源类型：`source_type=jw`
- 来源名称：`source_label=教务系统课表`

## 不做事项

- 不实现真实抢课
- 不实现课程轮次和预览真实连接
- 不把外部 Cookie 当作登录入口
- 不把验证码识别写死在 Web 前端或后端阻塞输入里
- 不恢复移动教务为生产数据源
- 不写入真实账号、密码、Cookie、token、验证码图片或原始响应

## 完成定义

本切片完成时应满足：

- 离线 fixture 能解析出有效课表行
- 解析结果能映射到 `JwScheduleFetchResult`
- `/api/schedule/cookie` 的成功路径能在测试中使用假客户端或 fixture 跑通
- 成功后可写入 `jw_schedule` 缓存
- `/api/schedule/local?source=cookie` 能读取该缓存
- 人工验证码 challenge 设计完成，且保留 OCR fallback 到人工的扩展点
- `schedule/preferred` 统一走 JW 会话；历史 mobile 缓存仅作为本地读取回退

## 依赖文档

- `../sensitive-index/教务系统/00_声明.md`
- `../sensitive-index/教务系统/01_接入资料索引.md`
- `../sensitive-index/移动教务/01_认证流程索引.md`
- `../api-parity/01_contract_freeze.md`
- `../api-parity/02_legacy_data_model.md`


## 本地辅助资料

Python 协议验证及云端交接资料保留在 ignored 的 `开发数据/本地协议工具说明/`，不作为桌面主线完成定义。
