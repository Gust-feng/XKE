# 传统教务 Cookie 课表开发文档

本目录是 `development-docs/traditional-jw-cookie-schedule/`，按“开发文档 / 开发项目 / 具体内容”组织。

本目录只维护传统教务 Cookie 课表项目的当前开发过程；长期路线图与敏感资料索引分别由 `roadmap/` 和 `sensitive-index/` 维护。

长期协议资料和敏感边界不在本目录展开；需要时跳转到 [../../sensitive-index/教务系统/01_接入资料索引.md](../../sensitive-index/教务系统/01_接入资料索引.md)。

## 当前主线

当前推进主线：

```text
JW 表单登录 -> 人工验证码 challenge -> JW 运行期会话 -> 课表 HTML -> jw_schedule 缓存
```

生产登录与课表刷新统一使用 JW 表单协议；历史移动实现只保留为测试兼容 seam，不参与生产宿主。课程查询和抢课继续后置。

## 阅读顺序

1. [00_current_focus.md](./00_current_focus.md)
   - 当前切片、边界、完成定义
2. [01_implementation_steps.md](./01_implementation_steps.md)
   - 具体开发步骤和验收顺序
3. [02_task_board.md](./02_task_board.md)
   - 当前任务板
4. [../../sensitive-index/教务系统/01_接入资料索引.md](../../sensitive-index/教务系统/01_接入资料索引.md)
   - 传统教务资料索引和预登录流程边界
5. [../../sensitive-index/移动教务/01_认证流程索引.md](../../sensitive-index/移动教务/01_认证流程索引.md)
   - 移动教务回归边界

## 使用规则

- 每次开始一个开发切片，先更新 `00_current_focus.md`。
- 每次拆解或完成任务，更新 `02_task_board.md`。
- 每次触及真实认证资料，先检查 `../../sensitive-index/`。
- 不在本目录保存真实账号、密码、token、cookie、验证码图片、原始 HTML 或真实响应。

## 桌面解析器复用边界

已有桌面 HTML parser 的数据流：

```text
fixtures/schedule/sample_cookie_schedule.html
-> 传统教务 HTML parser
-> JwScheduleFetchResult
-> source_key=jw_schedule
```

该解析器可用脱敏 fixture 离线验证。Linux 脚本只复用协议信息并输出 HTML，不复制桌面缓存、统一 rows 或可信账号流程。


## 本地辅助工具

独立 Python 协议验证工具及其测试按当前提交范围保留在本地，不作为 C# 产品运行依赖，也不纳入本次提交。工具操作说明和交接材料见 ignored 的 `开发数据/本地协议工具说明/`。
