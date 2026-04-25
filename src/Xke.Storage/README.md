# Xke.Storage

当前阶段目标：
- 固定 `.NET` runtime data root：默认 `%LOCALAPPDATA%/.XKE/data`，测试和开发可用 `XKE_DOTNET_DATA_DIR` 覆盖
- 提供学生、认证、课表、notice、desktop、network、telemetry、feedback、course 的持久化入口
- 用 DPAPI 保护凭据/session，用客户端内置密钥加密主要运行数据；头像和课表缓存保持普通文件，同时保留必要的 legacy 只读兼容辅助层

当前已落地：
- `XkeDataRoot`
- `XkeStudentStore`
- `XkeCredentialStore`
- `XkeSessionStore`
- `XkeScheduleStore`
- `XkeCourseStore`
- `XkeDesktopSettingsStore`
- `XkeDesktopRuntimeStore`
- `XkeNetworkCredentialStore`
- `XkeTelemetryStore`
- `XkeFeedbackOutboxStore`
- `LegacyNotice*` 与 `LegacyData*` 兼容辅助类型

当前限制：
- 不读写 Python legacy runtime `data/`、真实 `.env`、真实 token 或生产镜像
- 传统教务验证码 / Cookie 课表 / Course 真实连接仍后置，不在当前 store 中落真实运行态
- Telemetry / Feedback 云上传仍后置，当前只落本地闭环文件
