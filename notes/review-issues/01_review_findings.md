# XKE.DotNet8 重构审查合并报告

> 更新时间：2026-04-24  
> 范围：`Z:/XKE/XKE.DotNet8/`  
> 来源：合并 `18_重构问题.md`（归档于 `Z:/XKE/reference-archive/notes/legacy-notes/legacy-18-review-issue-memo.md`）与历史修订后的当前执行版。  
> 使用规则：本文件是后续修复排序的唯一主报告；`18_重构问题.md` 与 `19_重构审查收敛报告.md`（分别归档于 `Z:/XKE/reference-archive/notes/legacy-notes/legacy-18-review-issue-memo.md`、`Z:/XKE/reference-archive/notes/legacy-notes/legacy-19-convergence-memo.md`）只作为历史材料，不再作为独立决策依据。  

## 0. 名词边界（统一）

- 移动教务（Mobile JW）：作为当前唯一主链路数据源，覆盖身份、课表、首登初始化、定期维护。
- 教务系统（Legacy JW）：指传统教务站点与验证码/OCR/Cookie 场景，当前仅保留未来边界，**不进入主登录和本次恢复链路**。
- 传统教务功能若出现术语歧义，以“教务系统”为准；涉及课表/身份同步的实际优先级以“移动教务”为准。

## 1. 总体判断

当前重构方向总体正确，已经形成 `Contracts / Core / Storage / Security / WebHost / DesktopHost` 的基本边界，并且保持了按垂直切片推进的路线。当前推进重心已经从隐私落盘增强切到后端长期结构收敛。主要风险不是“能不能构建运行”，而是活跃链路边界和长期维护结构还没有完全固化：

- 首登初始化原子性还有异常缺口。
- 后端测试和 WebHost 支持层还需要继续去重。
- Legacy 兼容层必须保持 reference-only，不能重新接回 Python runtime。
- 部分业务边界条件还依赖隐含约定，后续容易漂移。
- `WebHost` 仍有继续膨胀成新中心模块的风险；前端暂不拆分，只保留兼容。

## 2. P1 问题

### P1-1：首登回滚不完整

- 位置：`src/Xke.Core/Auth/AuthService.cs:404-438`
- 影响：首登初始化已经写入 profile、preferences、credentials、session，并把 session 标记为 connected；但后续只对 `AuthException` 和 `ScheduleException` 做回滚。
- 风险：如果课表初始化期间发生 `OperationCanceledException`、`IOException`、`JsonException` 或其它未归一化异常，可能留下半初始化本地状态，破坏“首登必须完整落库才算成功”的主线。
- 建议：把远端初始化后的所有失败都纳入统一回滚边界。可以保留 cancellation 的语义，但仍应清理本次 session、connected token、临时凭据和本次学生目录变更。
- 测试建议：补一个 fake schedule/client 抛普通异常或取消异常的 WebHost 测试，断言 credentials、sessions、student directory 都没有残留。

### P1-2：认证历史仍落明文账号

- 位置：`src/Xke.Core/Network/NetworkService.cs:486-491`
- 当前裁决：暂缓。
- 原因：本轮默认用户电脑处于相对可信环境，隐私落盘不作为核心优先级。该问题保留为后续隐私增强项，不阻塞当前后端结构收敛。
- 后续建议：如恢复隐私增强切片，再统一清洗 attempts 或重做 network auth history schema。

## 3. P2 问题

### P2-1：反馈云上传补传策略仍未完成

- 位置：`src/Xke.Core/Feedback/HttpFeedbackTransport.cs`
- 当前状态：反馈 outbox 已使用本地加密文件保存；默认 transport 会优先上传云函数，失败后写入本地加密 outbox。
- 剩余风险：本轮没有后台常驻补传器；如果云端短时不可用，反馈会保留在本地，需要后续切片决定是否扫描 outbox 并补传。
- 后续建议：如需要“最终一定到云端”，再设计 outbox 枚举、重试节流、成功删除和用户隐私保留周期。

### P2-2：当前周使用 UTC 日期

- 位置：`src/Xke.Storage/XkeScheduleStore.cs:103-111`
- 影响：`FindScheduleWeekLabelForDate` 默认使用 `DateTime.UtcNow` 推断当前日期。
- 风险：桌面应用面向本地用户，香港/中国时区凌晨会比 UTC 早一天，周切换边界可能选到上一周课表。
- 建议：使用本地日期，或引入可注入时钟，让测试能覆盖本地时区边界。
- 测试建议：补周日/周一凌晨边界测试，明确当前周推断基于本地日期。

### P2-3：LegacyCompat 编译边界需要保持明确

- 位置：`XKE.DotNet8.sln:6-27`
- 当前裁决：保留目录，不加入活跃 solution 门禁。
- 原因：`Xke.LegacyCompat` 是后续前端重构和迁移参考层，不应重新扩大到 active runtime。
- 测试建议：保留 active `.NET` 数据根不读取 `XKE_DATA_DIR` / Python legacy `data/` 的隔离测试。

### P2-4：运营商凭据清空语义不清

- 位置：`src/Xke.Core/Network/NetworkService.cs:540-568`
- 影响：空 account 会保留旧 account，空 password 或任何包含 `*` 的 password 都会触发保留逻辑。
- 风险：用户无法可靠清空某个运营商凭据，也无法设置包含星号的真实密码。前端展示用 masked password 与后端保存语义耦合过紧。
- 建议：把请求语义改成显式字段，例如 `password_unchanged`、`clear_provider`，或每个 provider 使用独立 patch 模型。不要用 `*` 推断是否保留密码。
- 测试建议：补三类测试：保留旧密码、清空 provider、设置包含 `*` 的新密码。

## 4. 模块化和质量观察

- `XkeWebHostApplication` 已经用 partial endpoint 文件拆开，但服务注册、通用错误处理、session 解析、blocked policy 仍集中在同一个静态入口里。
- `Xke.Contracts` 同时承载 HTTP DTO 和本地持久化 document。短期减少映射成本，但长期会让 API 契约和存储 schema 互相牵制。
- `NetworkService`、`AuthService`、`NoticeCompatibility`、`NativeJwScheduleClient` 文件偏大，后续新增功能前应先拆稳定边界，避免继续形成新巨石。
- Telemetry/Feedback 已进入云上传链路；后台补传、管理侧查询权限和更细隐私保留周期后续单独推进。
- 前端本轮不做结构优化，只要求后端保留现有 envelope 和字段兼容，方便后续前端整体重构。

## 5. 对 18 号报告的合并裁决

18 号报告覆盖面较广，但部分优先级偏重。合并后的执行口径如下：

- `XkeDpapi / NoticeDpapi` 的 P/Invoke 重复属于长期维护债，不是当前 P0。两个保护域使用不同 entropy，后续可抽公共底层 helper，但不能破坏现有已加密数据兼容。
- `AuthEndpoints` 重复 try/catch、`XkeWebHostApplication` 偏大、缺少 `Xke.Core.Tests` 都成立，但优先级低于首登原子性、敏感数据落盘和真实数据链路闭环。
- `global.json` 使用 SDK 10 与 `net8.0/net8.0-windows` 目标并存，是当前工具链兼容策略，不作为缺陷处理；如后续需要团队/CI 固化，再单独调整。
- `StoredCredentialDocument.Password` 不是普通明文落盘，因为凭据文件经 DPAPI 保护；内存中明文生命周期可以后续优化，但不应误判为当前最高风险。
- 移动教务 AES-ECB 是外部协议约束，不能自行替换算法；应保留边界注释，说明这是提交加密字段的兼容实现。
- `XkeDataRoot` 每次创建轻量 store 实例暂不处理，当前收益低于复杂度。
- `Dictionary<string, object?>` 应逐步限制在 Notice、Legacy、真实移动教务格式兼容等动态边界；新业务 DTO 优先强类型，但不做机械大替换。
- `XkeStudentStore.WriteJson` 非原子写入是有效长期风险，可在完成 P1/P2 行为边界后统一处理。

## 6. 已验证门禁

本次审查后已验证：

```text
dotnet build XKE.DotNet8.sln --nologo
结果：通过，0 warning / 0 error

dotnet test XKE.DotNet8.sln --no-build
结果：通过，71 tests

tools/frontend-smoke.ps1
结果：通过，26 tests

tools/schedule-smoke.ps1
结果：通过，15 tests

dotnet build src/Xke.LegacyCompat/Xke.LegacyCompat.csproj --nologo
结果：通过，0 warning / 0 error
```

## 7. 建议处理顺序

1. 修 `AuthService` 首登回滚边界，并补异常回滚测试。
2. 修当前周本地日期推断。
3. 固化 active `.NET` 数据根与 Legacy reference-only 的隔离测试和注释。
4. 扩展后端测试支持层，减少 WebHost 测试启动样板重复。
5. 启动 WebHost 注册、错误处理、session/context 加载、blocked policy 的长期结构拆分。
6. 建立 `Xke.Core.Tests`，把 normalize/parser/decision 纯逻辑逐步迁出 WebHost 集成测试。
7. Network 认证历史脱敏、Feedback 后台补传、运营商凭据清空语义作为后续隐私/设置增强切片处理。

## 8. 收束方向图（用于后续开发排队）

### 8.1 当前主线（不改变现有 API）
- 首先保证数据主链路稳定：Auth 首登/本地恢复、Schedule 初始化与恢复、Notice 兼容、Desktop settings、Network/Telemetry/Feedback 本地与云端兜底闭环。
- 优化后端结构：集中排查活跃链路边界、测试重复、错误面和 session/blocked 边界，不新增前端字段。
- 将文档、测试、注释全部按照“移动教务主线 + 教务系统后置”统一标注。

### 8.2 下一步开发优先级
- P0：`AuthService` 首登异常回滚测试与实现、`NetworkService` 运营商清空语义与文档澄清。
- P1：后端测试支持层统一、`Xke.Core.Tests` 建立、`XkeWebHostApplication` 结构性拆分。
- P2：`Course` 维持 deferred 入口，补齐课程框架说明与占位文档；不回填验证码/抢课逻辑。
- P3：`Mobile JW` 与前端数据契约对齐：若真实格式差异继续出现，先补解析器与 fixtures，再看需要。
- P4：在确认稳定后，再启用任何云端或隐私落盘加固切片。

### 8.3 术语与边界约束（跨文档一致）
- 所有文档和注释默认先写“移动教务”，只有明确与验证码/Cookie/传统课程抢课相关时才写“教务系统”。
- 任何实现计划若要提到“登录恢复”，必须区分：
  - 移动教务：可信本地状态恢复 + 可选重连；
  - 教务系统：人工 challenge / OCR / 抢课链路，属于后置切片。
- 本文件更新时保持 `../roadmap/01_phase_summary.md`、`./00_current_issues.md`、`../future-plans/00_traditional_jw_manual_login.md` 的指向一致，不产生并行口径。


## 2026-10-07 提交前核对

- 当前生产 Auth/Schedule 使用 JW 表单协议，旧移动路径只作为兼容测试入口。当前公共测试宿主仍显式使用旧移动路径，默认协议入口新增了无远程调用的参数/challenge 错误验收；首登成功和回滚的完整 API 验证仍需后续补齐，不能由兼容测试通过代替。
- 本轮修正 `LocalOnly` 只禁公告/更新却仍装配云上传 transport 的缺口；明确本地模式采用本地 telemetry 与 feedback transport，并增加装配回归测试。
- 登录 challenge 过期提交时释放其协议客户端，避免从待处理字典移除后遗留资源。
- 前端由用户明确选择本地保留，不纳入当前提交。后端仓库可构建，桌面资源需本地提供；该交付边界写入 README。
- 校园网后台调度仍依赖前端；生产默认 JW 首登失败/取消与重复初始化的全路径验收属于下一阶段，当前提交不宣称这些已完成。
