# 本地工程结构整理

## 范围

将当前 C# 仓库从旧 Python 工程中独立，活动工程根为 `Z:\XKE`。本轮处理物理目录、入口路径和历史资料归档，不删除业务模块、不重做前端、不重新打包。

## 保留关系

- 保留当前仓库的完整 `.git`、提交历史、`main` 分支、远端及 upstream 配置。
- 保留原有未提交修改、未跟踪源码和当前私有开发资料。
- 保留 solution 与 csproj 名称，目录迁移不等于项目重命名。
- 用户数据仍由 `XkeDataRootFactory` 决定；默认是 `%LOCALAPPDATA%/.XKE/data`。

## 阅读顺序

1. [00_current_focus.md](./00_current_focus.md)
2. [01_implementation_steps.md](./01_implementation_steps.md)
3. [02_task_board.md](./02_task_board.md)

## 历史资料位置

- `开发数据/历史工程/旧Python工程.zip`：旧源码、旧 Git、文档、私有配置和旧运行数据；仅归档，不导入当前运行根。
- `开发数据/历史工程/早期CSharp工程.zip`：早期 C# 骨架与其 Git 元数据。
- `开发数据/历史页面样本/`：原来散落仓库根的原始页面与配套资源。

归档内容不进入 notes、fixtures 或公开 Git。虚拟环境、node_modules、bin/obj 和可再生成的构建缓存不归档。

## 完成状态

2026-10-07 核查：旧工程目录、旧嵌套目录、空文档目录、过时发布目录均已删除；`nul` 残留文件与临时清理 GUI 脚本也已移除。当前仓库和两份私有归档保留完整，Git 分支、提交和远端关系不变。
