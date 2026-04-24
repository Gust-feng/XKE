# 开发文档入口

本目录按以下结构组织：

```text
development-docs/
  <开发项目>/
    README.md
    00_current_focus.md
    01_implementation_steps.md
    02_task_board.md
```

## 当前开发项目

| 项目 | 入口 | 当前阶段 |
| --- | --- | --- |
| 传统教务 Cookie 课表 | [traditional-jw-cookie-schedule/README.md](./traditional-jw-cookie-schedule/README.md) | 阶段 1：离线解析 |

## 文件职责

- `README.md`：项目入口、阅读顺序、项目边界
- `00_current_focus.md`：当前切片、目标、完成定义
- `01_implementation_steps.md`：实施步骤、接口行为、验收顺序
- `02_task_board.md`：任务状态、下一步、阻塞项

当前项目文件：

- [traditional-jw-cookie-schedule/README.md](./traditional-jw-cookie-schedule/README.md)
- [traditional-jw-cookie-schedule/00_current_focus.md](./traditional-jw-cookie-schedule/00_current_focus.md)
- [traditional-jw-cookie-schedule/01_implementation_steps.md](./traditional-jw-cookie-schedule/01_implementation_steps.md)
- [traditional-jw-cookie-schedule/02_task_board.md](./traditional-jw-cookie-schedule/02_task_board.md)

## 新增项目规则

新增开发项目时：

1. 在 `development-docs/<project>/` 下创建项目目录
2. 至少包含 `README.md`、`00_current_focus.md`、`01_implementation_steps.md`、`02_task_board.md`
3. 更新本文件的“当前开发项目”表
4. 更新 `../README.md`
5. 更新 `../doc-entry/00_overview.md`
6. 更新 `../doc-entry/01_notes_maintenance.md`

## 边界

- 本目录保存“可执行开发文档”，不是敏感资料正文。
- 真实账号、密码、token、cookie、验证码图片、原始 HTML、原始响应不得进入本目录。
- 需要认证资料时，只引用 `../sensitive-index/` 中的路径索引和规则。
