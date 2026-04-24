# 敏感边界规则

## 目标
为 `XKE.DotNet8/` 建立统一的安全边界，确保重构过程中不会把真实密钥、真实凭据和真实用户数据带入新目录。

## 禁止复制到 `XKE.DotNet8/` 的内容
- 真实私钥
- 真实 token / cookie / trusted login token
- 真实 `.env`
- 真实录包（xml/http/yml 等）
- 真实 `data/` 镜像
- `重构计划/90_受限资料/` 中的任何原文内容
- 浏览器 profile、Crashpad dump、临时调试缓存中的真实会话材料

## 允许进入 `XKE.DotNet8/` 的内容
- C# 源代码
- 目录/模块/边界索引
- 仅含元数据的敏感清单
- 完整脱敏后的测试样本
- 测试用假密钥与假请求向量

## 处理原则
1. 先记录路径，不记录正文
2. 先做脱敏，再落入 `fixtures/`
3. 真实资料只允许原地只读引用，不允许复制副本
4. 安全相关验证优先使用测试向量与脱敏样本
5. 发布阶段必须检查 `XKE.DotNet8/` 与产物中不存在真实敏感内容

## 特别说明
- `core/notice/notice_signing_public.pem` 与 `core/remote_sensing/public_key.pem` 属于公钥资产，可作为“安全配置资产”索引，但当前阶段也不在 `XKE.DotNet8/` 复制副本。
- 所有真实环境文件统一仍以原路径为唯一来源。
