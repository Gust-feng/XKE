# 反馈云上传实施步骤

## 1. 私有云函数整理

- 解包 `开发数据/helloworld-1774171556.zip` 到 `开发数据/反馈云函数/`。
- 保留 `index.main_handler` 作为 SCF 入口。
- 补充 `README.md`、`docs/contracts.md`、`docs/deploy.md`、fixtures 与本地测试。

## 2. 客户端 transport

- 新增 `HttpFeedbackTransport`。
- 端点读取顺序：
  - 显式构造参数
  - `XKE_FEEDBACK_CLOUD_ENDPOINT`
  - `XKE_CLOUD_FEEDBACK_BASE_URL`
  - `XKE_CLOUD_FUNCTION_FEEDBACK_BASE_URL`
  - 私有配置 `remote.f` / `feedback_cloud_endpoint`
  - 脱敏占位默认值
- 配置值可以是云函数 base URL；客户端自动补 `/f`。
- 上传 payload 保留 `student_id`，云函数重新计算 `student_id_hash`。
- 提交前写入本地加密 usage 计数，同一学号本地当天最多 7 条。
- HTTP 失败、云端拒绝或响应不可解析时，写入本地加密 outbox。

## 3. 打包注入

- `tools/inject-private-cloud-config.ps1` 读取反馈端点字段。
- 发布目录 `xke.runtime.json` 写入：

```json
{
  "remote": {
    "f": "https://<feedback-function-host>"
  }
}
```

## 4. 验收

- 私有云函数：`npm run check`、`npm test`。
- .NET：`dotnet build .\XKE.DotNet8.sln --nologo`。
- WebHost：反馈与 CloudEndpointResolvers 相关测试通过。
