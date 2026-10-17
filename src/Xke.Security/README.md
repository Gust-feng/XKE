# Xke.Security

Windows当前用户 DPAPI 存储基础，供本地密码、Cookie、运行会话、校园网凭据与桌面运行态使用。
普通个人资料、账号偏好和课表使用JSON/CSV，不调用此层。独立ProtocolHost的学校登录材料只在内存。

- `XkeDpapi`：Windows 当前用户保护。
- `ProtectedJsonFileStore`：受保护 JSON 文件。
- `LocalEncryptedFileStore`：当前 DPAPI envelope 及原子文件写入。

不再提供公告签名、云端遥测加密或旧固定 AES envelope。安全存储只保护本机数据，不承担认证协议或网络请求。
