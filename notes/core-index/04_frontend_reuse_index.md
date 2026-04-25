# 前端复用索引

## 目标
明确当前 Web 前端中哪些部分应直接保留复用，哪些部分只保留契约语义，哪些部分后续要拆分。

## 当前关键路径
- `Z:/XKE/web/index.html`
- `Z:/XKE/web/assets/app.js`
- `Z:/XKE/web/assets/style.css`
- `Z:/XKE/web/assets/modules/http.js`
- `Z:/XKE/web/assets/modules/storage.js`
- `Z:/XKE/web/assets/modules/desktop.js`
- `Z:/XKE/web/assets/modules/profile.js`
- `Z:/XKE/web/assets/modules/theme.js`
- `Z:/XKE/web/assets/modules/updates.js`
- `Z:/XKE/web/assets/modules/format.js`
- `Z:/XKE/web/assets/modules/dom.js`

## 保留复用建议

### 1. 直接保留
- `web/index.html`
- `web/assets/style.css`
- 绝大多数 `web/assets/modules/*.js`

原因：
- 这些文件承载当前界面和交互体验
- 用户明确希望保留现有前端界面

### 2. 强兼容复用
- `web/assets/modules/http.js`
  - 固定 API 封装与 `X-XKE-Session` 头
- `web/assets/modules/storage.js`
  - 当前 localStorage 键、恢复逻辑、清理策略

### 3. 后续拆分重点
- `web/assets/app.js`
  - 当前仍是前端 orchestrator 巨石
  - 后续应按领域拆成 controller / coordinator

## 当前前端状态边界
必须先冻结的状态类型：
- session id
- remember login
- avatar cache
- theme fallback / by student
- notice state
- dev tools state

## .NET 宿主策略
- 初期保持当前前端不变
- 由 `WPF + WebView2` 承载现有页面
- 后续仅逐步把宿主相关能力原生化
- 如果未来启动完整界面层重构，优先评估 `WinUI 3 + C#` 原生 UI，现有 Web 前端作为视觉、交互和契约对照基线
