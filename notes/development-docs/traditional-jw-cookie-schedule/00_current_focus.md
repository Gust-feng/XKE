# 当前开发焦点

## 切片名称

`jw-cookie-schedule-v1`

## 目标

把传统 Web 教务系统接入推进到可用的 Cookie 课表闭环：

```text
预登录
-> SSO 登录
-> JW bootstrap
-> 拉取传统教务课表 HTML
-> 解析为统一课表 rows
-> 保存 jw_schedule
-> 本地读取
```

## 第一阶段策略

先离线、再接口、最后真实登录。

第一阶段不直接接真实教务登录，先使用 `fixtures/schedule/sample_cookie_schedule.html` 固定解析结果、返回结构和缓存行为。

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
- 不改移动教务主链路
- 不写入真实账号、密码、Cookie、token、验证码图片或原始响应

## 完成定义

本切片完成时应满足：

- 离线 fixture 能解析出有效课表行
- 解析结果能映射到 `JwScheduleFetchResult`
- `/api/schedule/cookie` 的成功路径能在测试中使用假客户端或 fixture 跑通
- 成功后可写入 `jw_schedule` 缓存
- `/api/schedule/local?source=cookie` 能读取该缓存
- 移动教务 `schedule/preferred` 和 `schedule/local` 回归不变

## 依赖文档

- `../sensitive-index/教务系统/00_声明.md`
- `../sensitive-index/教务系统/01_接入资料索引.md`
- `../sensitive-index/移动教务/01_认证流程索引.md`
- `../api-parity/01_contract_freeze.md`
- `../api-parity/02_legacy_data_model.md`
