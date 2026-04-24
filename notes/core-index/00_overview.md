# 核心索引总览

本目录用于整理 legacy Python 版本的事实索引，以及这些事实在 `XKE.DotNet8` 中的迁移参考。不复制真实敏感内容，只记录：
- 关键路径
- 模块职责
- 迁移目标
- 复用/兼容建议

## 索引文件
- [01_runtime_host_index.md](./01_runtime_host_index.md)：启动、桌面宿主、运行时相关
- [02_api_contract_index.md](./02_api_contract_index.md)：API 路由、模型、会话与契约
- [03_core_domain_index.md](./03_core_domain_index.md)：核心业务、存储、安全、公告、遥测
- [04_frontend_reuse_index.md](./04_frontend_reuse_index.md)：前端页面、模块、兼容层与复用面
- [05_doc_entry_index.md](./05_doc_entry_index.md)：核心文档与阅读入口

## 使用规则
- 这里是 legacy 事实和迁移参考入口，不是当前实现状态总表
- 真实密钥、真实请求、真实 data 镜像一律不复制到这里
- 需要样本时，只能在 `../fixtures/` 中使用脱敏版本
