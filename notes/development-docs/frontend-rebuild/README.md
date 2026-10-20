# 产品第一版：现第七版合成真实对接（2026-10-10）

## 本轮自动验证码验收入口

桌面已接入可信账号本地OCR（首次人工、后续可关闭、验证码错误最多三次）、动态设置状态及模型/CRT随包方案。当前结果与未验收边界以personal-runtime的最新任务板/API/技术手册为准；旧“桌面仅人工”“尚未接入”是历史阶段，不代表当前候选。2026-10-20用户已明确授权V1.0.2发布；必须先验证，再按必要范围提交并由Actions校验后公开。

用户确定当前设计完毕，锚定为产品第一版。现第七版是设计来源，保持布局与交互动效；授权子智能体在frontend/接真实C# API。

当前任务状态见 [任务板](02_task_board.md)；后端入口为 [技术手册](../personal-runtime/05_backend_technical_manual.md) 与 [API契约](../personal-runtime/03_api_contract.md)。下方mock阶段与旧设计过程保留作历史。

---

# 当前前端：合成第七版（2026-10-09）

用户已确定第七版为唯一正式设计基线。活动 frontend/ 仅保留合成布局，尊重第七版周次尺及从第四版继承的交互动效。全部页面读取 src/api/demo-data.json 的统一假数据；其他版本与设计研究素材已独立移出项目，不作为工程依赖。

当前仍为 React + TypeScript + Vite 的 mock 预览，产物 mode=preview；「正式」指设计选择已确定，真实 C# 接入待完成。

阅读顺序：[当前焦点](./00_current_focus.md) → [实施步骤](./01_implementation_steps.md) → [任务板](./02_task_board.md) → [活动前端](../../../frontend/README.md) → [设计基线](../../../frontend/docs/design-baseline.md) → [后端交接](../../../frontend/docs/backend-handoff.md)。

---

## 历史过程（旧路径、版本状态与待办不作为当前指令）

# 2026-10-09：设计融合研究中

用户从 `schedule (3).zip` 选中聚焦、瑞士、仪表、杂志、原生及当前优化经典，要求进一步整理、优化并结合优点。已在 ignored 的 `开发数据/设计预览/schedule-v3/` 建立设计取舍页和独立融合交互草案；执行状态见当前焦点/实施步骤/任务板。

活动 `frontend/` 仍为压缩包第二版优化基线，未替换。此次是设计推敲，不宣称最终视觉已确认或 React/C# 接入已完成。

---

# 当前前端：压缩包优化设计标准

2026-10-08 用户明确采用压缩包第二版及其优化设计作为唯一标准，活动入口为 frontend/，技术栈为 React + TypeScript + Vite。旧版 TS 迁移、原 web、新 Vue 与独立 demo 均进入 ignored 备份，不再参与编译。

当前仅整理工程与版本关系，不重做视觉或默认接回旧设计。当前原型只使用 mock API；构建输出 dist-preview/、preview/，mode=preview，真实 C# 集成待完成。

## 阅读顺序

1. [当前焦点](./00_current_focus.md)
2. [实施步骤](./01_implementation_steps.md)
3. [任务板](./02_task_board.md)
4. [活动前端说明](../../../frontend/README.md)
5. [设计基线与备份关系](../../../frontend/docs/design-baseline.md)
6. [后端交接](../../../frontend/docs/backend-handoff.md)
7. [实际 API 契约](../personal-runtime/03_api_contract.md)

---

## 历史阶段记录（旧设计已归档，不作为当前指令）

## 2026-10-08：独立设计 demo 待视觉验收

用户在完成旧版 TS 迁移后，授权重新打磨一套 demo 先看效果。预览入口为 `frontend/demo/`，采用 vanilla TypeScript + Vite；与 `frontend/src/legacy/` 及桌面正式入口分离。演示课程、账号、网络状态与时钟均为合成数据，不调用真实服务、不保存输入、不修改系统启动项。

课表为主界面，包含今日安排、课程详情、切周/日程、浅深主题、显示偏好；另有登录、校园网和设置预览。启动命令与范围见 [demo 说明](../../../frontend/demo/README.md)。旧版 TS 运行入口继续保留；新设计尚待用户验收，不作为已批准的业务替换。

# 旧前端一比一 TypeScript 迁移

用户已否定新 Vue 设计，活动前端以只读的旧 web/ 为唯一视觉基线。控制器和20个辅助模块迁为严格TypeScript，HTML/CSS与图标保留，构建为vanilla TS+Vite。新设计已在私有历史目录可恢复归档，不参与活动构建。

## 阅读顺序

1. [00_current_focus.md](./00_current_focus.md)
2. [01_implementation_steps.md](./01_implementation_steps.md)
3. [02_task_board.md](./02_task_board.md)
4. [迁移验证](../../../frontend/docs/ui-validation.md)
5. [后端当前接口](../personal-runtime/03_api_contract.md)

本次子智能体由用户明确授权，主负责人整合配置/启动/行为验收。完整旧web保持本地私有参考，不强制提交。
