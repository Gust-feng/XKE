# 迁移计划提炼

本文件从旧迁移计划中保留仍适用于 `XKE.DotNet8` 的阶段顺序和验收口径。

## 当前阶段定位

`XKE.DotNet8` 已从迁移验证进入后端长期收敛阶段。当前不是复制旧 Python 能力，而是以已完成切片为基础，持续收敛结构、边界和测试。

## 已完成主线

- DesktopHost 开发壳。
- Auth 移动教务首次初始化、本地凭据校验、trusted-login、session restore、connect。
- Schedule 移动教务刷新、本地缓存和真实格式兼容。
- Notice、Desktop settings、Network、Telemetry、Feedback 本地闭环。
- Course 新框架占位，传统教务真实连接和抢课后置。

## 后续顺序

1. 修复仍影响正确性的后端边界：首登原子性、Schedule 本地日期、Legacy reference-only 隔离。
2. 收敛后端测试结构：统一 WebHost 测试支持层，建立 Core 纯逻辑测试。
3. 拆分 WebHost 长期结构：服务注册、session/context、blocked policy、错误 envelope。
4. 进入传统教务 v1：先做 challenge 契约、fake client、Cookie 课表骨架，再联调真实验证码。
5. 最后处理云上传、OCR、真实抢课、打包发布等后置切片。

## 验收口径

- 每个切片必须保持 `dotnet build` 和 `dotnet test` 通过。
- 真实移动教务 smoke 只作为手动验收，不写入真实账号或响应。
- 传统教务真实验证码不作为当前自动化门禁。
- 前端保持 API 兼容，不在后端收敛阶段主动拆分。

## 不再沿用的旧迁移策略

- 不将 Python legacy runtime 作为新宿主的运行依赖。
- 不把旧 data 镜像直接迁入 `.NET` 数据根。
- 不将旧文档中的真实请求常量和凭据复制进 notes。
