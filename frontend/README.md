# XKE 前端 · 产品第一版

## 2026-10-15：最终demo已适配runtime

用户确认XKE-frontend-fixed-20261015.zip为最终设计。正式前端已适配其三栏设置、左侧身份/账户卡片、独立连接配置和课时点阵动效。设置展示最新学期，不跟随历史课表浏览；计数使用完整真实课表的节次与周规则，不用demo估算。API/凭据/认证/后台任务未替换。当前实现及验证见docs/runtime-integration.md，下方早期设计与验收记录保留历史语境。

2026-10-14：课程详情收敛为课程名/地点/具体时间/教师，取消复制与周规则列表；窗口按钮独立顶栏，页面导航下移分层。校园网左栏已接入独立summary接口的账号状态、套餐、计费方式和周期；连接设置就地展开，技术字段收在高级设置。当前实现和验收见runtime-integration及任务板。

2026-10-13交互修正：恢复校园网连接中的中档等待动效，日期收为月份/星期一行与大号日期。课程详情按真实卡片四边展开、宽侧栏卡保持左右贴合，只展示上课所需信息；原始数据不删。桌面只保留公共软件窗口按钮，认证时也可用；移除系统标题栏，保留原生拖动/缩放与既有托盘行为。此轮只构建代码，没有重启正在运行的窗口，桌面行为仍待实机验收。

2026-10-13：已同步用户返回的 `XKE-frontend-fixed-20261013.zip` 源码优化。当前活动设计在唯一one版继续迭代，导航/课块/课程详情和校园网点阵采用返包实现；实际API、认证、存储边界不变。详见运行时接手文档最新同步记录。

第七版「合成」是唯一正式设计基线，采用 React + TypeScript + Vite。沿用布局骨架、竖向周次尺和从第四版继承的交互动效，按当前学生与所选周聚焦真实数据；其他版本和设计研究素材已移出项目。

2026-10-13：现第七版「合成」锚定为产品第一版，已实现当前 C# HTTP runtime 对接。登录、独立验证码、单周/学期课表、账号/机器设置、校园网检测/保活/配置和桌面桥接均使用实际接口。密码与 session 仅驻内存；普通业务数据持久化由后端负责。

mock 仅在显式 preview 使用，所有演示值来自 src/api/demo-data.json；runtime 不因服务不可用切回 mock。
## 2026-10-13：同步 schedule (2).zip v22 优化

本次只同步压缩包中已确认的产品优化，不覆盖当前 runtime/API/认证实现：

- 课表显示层使用 `src/lib/course-blocks.ts` 在快照合并之后处理连堂与换教室；`parts`、`moveFrom` 只属于前端显示模型，原始 rows、JSON/CSV 和协议响应不改写。
- 详情改为课程块就地展开，配置类 Sheet 仍保持原有边界；不再用 `title`/tooltip 重复展示已经可见的信息。
- 校园网页面维持未知、连接中、在线、离线四种同一布局。在线矩阵改为通用当前时间 `HH:MM` 点阵，不显示虚假的流量/余额/在线时长；取消鼠标鼓起交互，状态轮询仍由 runtime 只读接口驱动。
- 学期标题、刷新和换学期入口继续遵循当前服务器数据规则；时间轴不恢复硬编码作息。

本轮同步未导入压缩包内其他 edition、mock 账号或旧 API。runtime 默认仍使用 C# HTTP，preview 才使用合成数据。

2026-10-11：首次使用需要认证；启动自动恢复已有本地账号，成功后直接进入合成主界面。重新连接从设置打开，取消保留已有工作区；注销返回认证页。界面以按钮表达操作，保留必要标签、状态和错误，去掉说明性引导。无账号不发送受保护请求。

学期标题、学期切换和刷新收进左栏学期尺弹层；回看历史时左栏仍显示当前安排，T回本学期、R刷新。学期目录按入学日期筛选，时间轴优先尊重服务器作息表，缺少作息表时使用已加载课程的真实范围，不展示重复悬浮提示。校园网连接前后保持同一布局，可见期间自动更新状态，无需单独检测。

连堂课按查看周聚合：同课程/教师/教室且课间不超过30分钟显示为一块，原课时保留在详情。换教室保持分块，左栏提前显示时间与目的地，课间倒计时显示距续课。普通课块浅底、今天墨色、下一节/正在上红色；只改变显示，不改后端课表文件。

## 启动和验证

在仓库根执行：

```sh
npm --prefix frontend ci
npm --prefix frontend run dev
```

runtime 开发地址为 http://127.0.0.1:5182/，使用同源或环境变量 VITE_XKE_API_BASE_URL 指定 C# 服务。桌面生产模式使用宿主同源地址，不写死 API 端口。

设计预览运行 npm --prefix frontend run dev:preview。只有 preview 接受 ?t=2026-10-08T13%3A30 的时钟覆盖；视图参数 v 可为 week、net、settings。

```sh
npm --prefix frontend run check
npm --prefix frontend run lint
npm --prefix frontend run test
npm --prefix frontend run build
npm --prefix frontend run build:demo
npm --prefix frontend run build:preview
```

build 写入 dist/，标记 mode=runtime 且不包含 demo chunk。build:demo 写入 dist-preview/，build:preview 复用同一 Vite 配置，将多文件静态预览写入 preview/（index.html + assets）；两者标记 mode=preview。显式 VITE_XKE_PREVIEW=true 构建也只输出 dist-preview/，打包门禁仍拒绝 preview。

预览导出不再使用单文件插件，也不再支持通过 file:// 直接双击 HTML；使用现有本地服务打开：

```sh
npm --prefix frontend run preview -- --outDir preview
```

lint 采用 oxlint --deny-warnings，警告和错误都会阻止检查通过。StoreProvider 位于 src/lib/store.tsx，消费者从 src/lib/store-context.ts 导入 useStore 与状态类型，避免组件与 Hook 混合导出。

## 结构和交互

- src/editions/one/：课表、校园网、设置、布局和动效。
- src/components/：详情与提示；src/lib/：状态和时间；src/styles/：公共样式。
- src/api/：HTTP 客户端、DTO、视图模型适配、runtime 和显式 mock。
- 快捷键：1–3 切页，左右方向键切周，T 回本周，Esc 关闭详情。

[运行时接手说明](./docs/runtime-integration.md)、[设计基线](./docs/design-baseline.md)、[后端交接](./docs/backend-handoff.md)和[实际 API 契约](../notes/development-docs/personal-runtime/03_api_contract.md)记录当前边界。定向自动化与真实隔离 HTTP 流程已通过；真实学校、浏览器 UI、Windows 和发布验收仍待进行。
