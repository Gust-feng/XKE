# Xke.Contracts

本模块承载当前 Python API 的共享契约层。

## 职责
- 固定 Request / Response DTO
- 固定 API envelope 与会话常量
- 固定枚举、选项、字段命名
- 作为 WebHost / Core / LegacyCompat 的共同事实层

## 当前阶段
当前重点仍是：
- 补齐 snake_case JSON 映射
- 补齐缺失请求模型
- 逐步加入验证层

## 设计约束
- 只表达契约，不承载业务逻辑
- 优先对齐 Python 现有字段与语义，而不是追求 C# 风格重命名
