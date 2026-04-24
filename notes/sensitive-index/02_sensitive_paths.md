# 敏感路径清单

本清单只记录路径、类型与处理规则，不保存真实内容。

## 高优先级敏感路径

### 1. 真实运行数据
- `Z:/XKE/data/`
  - 类型：用户数据根目录
  - 风险：包含账号、缓存、课表、遥测、本地配置
  - 规则：禁止复制到 `XKE.DotNet8/`，只能做脱敏映射

### 2. 受限资料
- `Z:/XKE/重构计划/90_受限资料/`
  - 类型：受限资料归档
  - 风险：可能包含真实密钥、真实环境变量、真实请求头、真实 data 镜像
  - 规则：禁止复制原文；仅允许在索引中记录路径与用途

### 3. 开发环境与开发资料
- `Z:/XKE/开发环境/`
- `Z:/XKE/开发数据/`
  - 类型：开发时环境与资料
  - 风险：可能含云函数配置、测试材料、公私钥、草稿样本
  - 规则：只允许路径索引，不允许整体复制进新目录

### 4. 环境变量文件
- `Z:/XKE/.env`
- `Z:/XKE/data/<student_id>/.env`
  - 类型：环境变量 / 账号凭据 / trusted token / 网络认证凭据
  - 风险：最高
  - 规则：禁止复制原文；若需要测试，只能制作假样本

### 5. 会话与缓存
- `Z:/XKE/data/<student_id>/cache/`
  - 类型：token / cookie / header / 课表相关缓存
  - 风险：高
  - 规则：禁止复制原文缓存；只能基于脱敏样本做兼容验证

### 6. 录包与原始响应
- `Z:/XKE/data/<student_id>/prelogin_capture.xml`
- `Z:/XKE/data/<student_id>/schedules/**/*_raw.json`
- `Z:/XKE/重构计划/90_受限资料/原始资料/http/`
  - 类型：录包 / 原始请求 / 原始响应
  - 风险：可能带 cookie、header、账号标识
  - 规则：禁止复制原文；需要时制作结构保真、内容脱敏的样本

### 7. 遥测与远程感知
- `Z:/XKE/data/.telemetry/`
- `Z:/XKE/data/<student_id>/info/remote_sensing_snapshot.json`
- `Z:/XKE/data/<student_id>/info/app_runtime.json`
- `Z:/XKE/data/<student_id>/info/network_auth_history.json`
  - 类型：安装标识、加密密钥、运行统计、快照
  - 风险：高
  - 规则：禁止复制原文，兼容验证时优先用脱敏向量

## 安全配置资产（当前阶段只做索引）
- `Z:/XKE/core/notice/notice_signing_public.pem`
- `Z:/XKE/core/remote_sensing/public_key.pem`
  - 类型：客户端公钥资产
  - 规则：先作为单一事实源引用，不在 `XKE.DotNet8/` 中复制多份副本

## 后续动作建议
- 为 `fixtures/` 单独建立脱敏样本生成流程
- 为 `tools/` 建立发布前敏感文件扫描器
- 为 `tests/` 建立仅使用脱敏样本的兼容验证约束
