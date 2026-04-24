# Xke.Storage

当前阶段目标：
- 固定 `.NET` runtime data root：`XKE.DotNet8/.runtime/data`
- 提供学生、认证、课表、notice、desktop、network、telemetry、feedback、course 的持久化入口
- 用 DPAPI 保护本机敏感数据，同时保留必要的 legacy 只读兼容辅助层

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
