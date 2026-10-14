# Xke.Contracts

登录、课表、设置、校园网和桌面宿主的请求/响应模型。JSON 使用 snake_case；接口成功采用 `{ ok, msg, data }`，业务错误采用 `{ ok:false, msg, data:null, detail }` 并保留 HTTP 状态。

本项目只声明数据形态，不访问文件、网络或操作系统；包含当前 GitHub 应用更新状态；不包含旧 Updater、云策略、公告、遥测、反馈或选课模型。
