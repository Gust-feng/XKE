局部的最优解往往不是全局的最优解，你编写代码和制定计划必须统领全局，切勿顾此失彼，留下技术债！

# 项目记忆

## 协作方式

- 用户说“先看看”“分析下当前项目”时，先做只读结构分析，不要直接改代码。
- 用户要求“代码审查”时，先检查真实工作区和暂存区 diff，发现必须绑定到当前变更；结论优先讲 bug、风险、回归和缺测试，不做泛泛风格点评。
- 用户在一次审查后说“继续”时，默认是在同一批 diff 上做更深一轮审查，而不是切换方向。
- 用户问数据存在哪里时，先给具体路径、存储类和读写入口，再讲流程。
- 用户问“这些是缓存数据吗”时，必须区分本地持久化缓存/物化副本、浏览器缓存、临时内存态。
- 用户要求把问题写入文件时，优先写入 repo 内 notes/review-issues 或现有 notes 体系，而不是只在聊天中总结。
- 如果用户说“子 git”“子仓库”，审查和修改必须针对 `Z:\XKE\XKE.DotNet8` 这个子仓库本身。

## 必读入口

- 任何非平凡开发、审查、规划或打包任务，先读本文件，再按 `notes/README.md` 和 `notes/doc-entry/00_overview.md` 的当前阅读顺序定位资料。
- 开始当前开发项前，必须读对应 `notes/development-docs/<project>/README.md`、`00_current_focus.md`、`01_implementation_steps.md`、`02_task_board.md`；没有项目目录时，先判断是否需要创建并同步 notes 索引。
- 做全局路线或优先级判断时，必须读 `notes/roadmap/00_development_direction.md` 和 `notes/review-issues/00_current_issues.md`。
- 做代码审查时，必须同时看 `notes/review-issues/00_current_issues.md` 与 `notes/review-issues/01_review_findings.md`，把新发现归并到既有问题体系，避免重复或冲突结论。
- 涉及移动教务、传统教务、真实资料、云端点、账号、token、cookie、运行数据时，必须先读 `notes/sensitive-index/00_overview.md` 及对应子目录声明。
- 新增、移动、归档 notes 文件时，必须同步更新 `notes/README.md`、`notes/doc-entry/00_overview.md` 和 `notes/doc-entry/01_notes_maintenance.md`；新增开发项目还要同步 `notes/development-docs/README.md`。
- 若 notes 与当前代码不一致，先指出不一致并用当前代码验证，再决定是更新 notes 还是修代码；不要静默忽略 notes，也不要把过期 notes 当成事实。

## 全局边界

- 以 README 和当前代码为准确认主线/后置边界。不要凭历史记忆假设功能仍处于旧状态。
- 当前工程边界主要是 `Xke.Contracts`、`Xke.Core`、`Xke.Storage`、`Xke.Security`、`Xke.WebHost`、`Xke.DesktopHost`、`Xke.Updater`。
- `Xke.WebHost` 是 API/runtime hub，`Xke.DesktopHost` 是 WPF + WebView2 shell。
- 保持 `Xke.Core` 作为一个项目内的 namespace 级模块化，除非边界已经稳定到值得拆 csproj。
- Auth、Schedule、Course、Notice、Telemetry、Feedback、Desktop、Update、CloudSync 的改动要看全局数据流，不要只修局部调用点。
- 传统 JW 验证码、Cookie 课表刷新、选课真实执行等曾多次作为后置/谨慎边界处理；如果要推进，先确认当前代码和测试是否已经改变这个事实。

## 数据与存储

- 默认运行数据根以当前 `XkeDataRootFactory` 和 README 为准；可由 `XKE_DOTNET_DATA_DIR` 显式覆盖。
- 测试和开发脚本可以显式使用 `.runtime/data`，但不要把旧 Python runtime `data/` 当作 .NET 主运行根。
- 不读取、不写入 Python legacy `data/`、真实 `.env`、真实 token/cookie 缓存、生产镜像，除非用户明确要求且动作符合敏感数据规则。
- 课表抓取后的本地文件是持久化缓存/物化副本，不是浏览器缓存。常见形态是 `students/<student_id>/schedules/<term>/weeks/<week_label>/<source>.json`，可能带 `<source>_raw.json` 和 CSV。
- 头像和课表缓存可以是普通文件；凭据、设置、反馈、运行实例、遥感/云同步等本地运行态默认应走加密或明确的受控存储。
- DPAPI 是本地敏感数据保护主线；不要重新引入仓库可见的固定 AES key。

## 云端、遥感与私有打包

- 云函数、COS、遥感上报、公告拉取等真实云端点不能硬编码进 GitHub 公开仓库，也不能为了脱敏而删除本地开发能力。
- 公开代码只保留中性读取逻辑、占位默认值和可替换接口；真实端点放在被忽略的 `开发数据/` 私有配置中。
- 打包时通过工具自动注入私有端点到发布目录运行时配置，例如 `xke.runtime.json`；公开仓库只提交注入工具和模板，不提交真实配置。
- GitHub 公开基线里不能出现明显真实 COS bucket、腾讯云函数 URL、密钥、token、cookie、云资源地址或发布桶路径。
- 端点不是密钥：即便打包产物里能看到，也只能作为公开入口；写权限、鉴权、密钥必须留在服务端/云函数环境变量或云端权限系统。
- 不要把“`.gitignore` 不上传”误当成隐藏逻辑的完整方案；如果公开仓库不能暴露某个能力，要做 build-time/private-overlay 或私有注入。

## 文档边界

- `development-docs` 是当前开发过程状态和执行文档，不要随意删除或压缩。
- `sensitive-index/教务系统` 是长期资料索引/参考库，不是可以删减的临时文档。
- `future-plans` 只放未来计划，避免让未实现功能看起来像当前实现。
- `roadmap` 放阶段方向，`review-issues` 放审查问题和风险，`doc-entry`/`README` 负责导航。
- notes 是开发文档，可以提交，但必须脱敏；真实账号、密码、token、cookie、验证码图片、原始响应、真实云资源值只允许留在被忽略的 `开发数据/` 或用户明确指定的受限位置。

## 审查重点

- 用户更关心全局架构、数据存储规范、模块边界、长期迁移形态和真实逻辑风险，而不是单点风格问题。
- 审查时优先看：是否误读/误写 legacy 数据根、是否泄漏敏感数据、是否把本地-only 变成默认外发、是否把后置能力伪装成已完成、是否破坏文档角色。
- 对远程控制面、自动更新、遥感上传、云同步策略要按真实威胁模型审查；公开入口即使脱敏，也要区分“端点可公开”和“控制内容可信”。
- 历史审查曾重点关注 first-login rollback、network auth history 明文、feedback outbox 明文、schedule week 日期、Xke.LegacyCompat solution gate、operator credential clearing 等问题；这些是否仍存在必须现查，不要直接复述为当前事实。

## 验证与环境

- 优先跑与改动同 slice 的 xUnit 项目，再考虑整套 `dotnet test XKE.DotNet8.sln`。
- 在这个 Windows/Codex 环境里，PowerShell 可能出现 `8009001d` 初始化失败；遇到时不要误判为仓库问题，可改用 Node/cmd 或可用的本地工具。
- `dotnet` 验证可能需要 repo-local `DOTNET_CLI_HOME`、`DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`、`DOTNET_CLI_TELEMETRY_OPTOUT=1`。
- 历史上 `UpdateWebHostTests.PrepareInstall_OnlySucceedsAfterVerifiedPackage` 曾作为无关残留失败出现；每次报告测试结果时要说明当前实际结果，不要假设它仍失败或已修复。
- passing build/test/smoke 是基线信号，不替代语义边界审查，尤其是验证码 challenge、cookie schedule、云端策略和隐私上传路径。
