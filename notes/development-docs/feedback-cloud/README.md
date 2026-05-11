# 反馈云上传

## 项目定位

本项目把已完成的本地反馈入口接入私有云函数。公开仓库只保留通用 transport、运行时配置读取和测试，不保存真实云函数地址、COS bucket、密钥或 token。

## 阅读顺序

1. [00_current_focus.md](./00_current_focus.md)
2. [01_implementation_steps.md](./01_implementation_steps.md)
3. [02_task_board.md](./02_task_board.md)

## 真实资料位置

- 私有云函数工程：`开发数据/反馈云函数/`
- 原始 ZIP：`开发数据/helloworld-1774171556.zip`
- 本机私有端点配置：`开发数据/dotnet8-cloud.local.json`

上述路径在仓库 ignored 的 `开发数据/` 下，不能复制真实地址或云配置到公开代码与 notes 正文。

## 目标边界

- `/api/feedback/report` 继续保持现有前端契约。
- 客户端本地限制同一学号当天最多提交 7 条反馈。
- 默认 transport 尝试 `POST /f` 上传到反馈云函数。
- 上传明文学号，云函数保存 `student_id` 并重新计算 `student_id_hash`。
- 云端上传失败、响应异常或不可用时，反馈写入本地加密 outbox。
- 打包时通过 `xke.runtime.json` 的 `remote.f` 注入真实反馈端点。

## 非目标

- 不在客户端内置云函数写权限、COS 密钥或私有 token。
- 不在云函数内提供公开查询接口。
- 不做后台常驻补传队列；本轮只做提交时即时上传和失败本地兜底。
- 不在云函数内做次数限制；每日 7 次限制是客户端体验约束，不是安全边界。
