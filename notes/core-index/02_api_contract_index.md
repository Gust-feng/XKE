# API 与契约索引

> 2026-10-07 路径说明：本文件中的旧 Python `Z:/XKE/...` 路径仅用于历史来源标识。对应目录已归档到当前仓库 ignored 的 `开发数据/历史工程/旧Python工程.zip`，不代表当前活动工程下仍有这些文件，也不参与运行。


## 目标
冻结当前 `/api/*` 的路径、入参、出参、状态码、错误语义、会话语义，作为 .NET `Xke.Contracts` 与 `Xke.WebHost` 的事实基线。

## 当前关键路径
- `Z:/XKE/api/app.py`
- `Z:/XKE/api/routes/__init__.py`
- `Z:/XKE/api/deps.py`
- `Z:/XKE/api/models.py`
- `Z:/XKE/api/routes/auth.py`
- `Z:/XKE/api/routes/schedule.py`
- `Z:/XKE/api/routes/course.py`
- `Z:/XKE/api/routes/network.py`
- `Z:/XKE/api/routes/updates.py`
- `Z:/XKE/api/routes/feedback.py`
- `Z:/XKE/api/routes/telemetry.py`
- `Z:/XKE/web/assets/modules/http.js`
- `Z:/XKE/tests/test_api_app.py`

## 当前契约特征

### 1. 应用装配
- `api/app.py`
  - 托管 FastAPI 应用
  - 挂载 `/api/*`
  - 托管 `web/` SPA
  - 对 loopback origin 做 CORS 限制
  - 允许从 `X-XKE-Session` 自动恢复 `session_id`

### 2. 路由聚合
- `api/routes/__init__.py`
  - 当前域包括：
    - `auth`
    - `schedule`
    - `course`
    - `desktop`
    - `feedback`
    - `network`
    - `updates`
    - `telemetry`

### 3. 会话守卫
- `api/deps.py`
  - `SessionStore`
  - `require_session()`
  - TTL 清理
  - 访问控制拦截

### 4. 模型与枚举
- `api/models.py`
  - 定义主题、课表来源、周次模式、网络提供商、预登录模式等枚举
  - 定义登录、偏好、网络、课表、抢课等请求模型

### 5. 前端 API 适配
- `web/assets/modules/http.js`
  - 固定使用 `X-XKE-Session`
  - 统一 GET/POST 封装
  - 统一错误格式化

## 当前必须兼容的语义
- `APIResponse` 包装结构
- `X-XKE-Session` 头语义
- `/api/config`、`/api/health` 等基础接口行为
- loopback-only CORS
- 现有字段名与枚举值

## .NET 迁移目标
映射到：
- `Xke.Contracts`
- `Xke.WebHost`
- `Xke.LegacyCompat`

## 当前验证来源
- `Z:/XKE/tests/test_api_app.py`
  - 配置转发
  - session header 恢复
  - loopback CORS
