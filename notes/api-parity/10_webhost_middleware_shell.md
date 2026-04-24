# WebHost 基础中间件进展

## 对照来源
- `Z:/XKE/api/app.py:48`
- `Z:/XKE/api/app.py:64`
- `Z:/XKE/api/app.py:74`
- `Z:/XKE/api/app.py:96`
- `Z:/XKE/api/app.py:107`

## 当前已落地
### WebHost
- `Xke.WebHost/Program.cs`
- `Xke.WebHost/WebHostRuntime.cs`

## 当前已对齐的行为
- loopback-only CORS
- `X-XKE-Session -> session_id` 中间件
- beta/access policy middleware 占位
- no-cache 响应头中间件
- `/api/config`、`/api/health` 特例接口
- `assets/` 静态资源挂载壳
- SPA fallback 壳

## 当前仍缺
- 真正的 beta 状态来源与 access policy 接入
- session sweeper
- 路由模块化与强类型 config 输出
- 与现有 `web/` 目录的更稳健路径解析
