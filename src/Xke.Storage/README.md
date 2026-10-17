# Xke.Storage

## 2026-10-16 提醒去重回执

XkeLessonReminderStore写students/<student_id>/info/lesson_reminders.json：仅日期与提醒指纹的普通业务JSON，原子写入、裁剪旧日期，防进程重启重复通知；不含课程正文、账号密码、Cookie或session。XkeScheduleStore.LoadLatestTermSourceSchedule按最新学期而非最近浏览时间选择提醒基线。

只持久化当前个人软件需要的数据：账号、session、JW Cookie、学生资料、设置、课表缓存、校园网凭据及桌面运行态。

根由 `XkeDataRootFactory` 确定，默认 `%LOCALAPPDATA%/.XKE/data`，测试使用显式临时根；不探测旧 Python data。

`JsonValueConverter` 是通用 JSON 值转换，不再依赖公告命名或云端模型。项目未投入使用，不提供旧业务文件兼容/迁移；退出非核心存储入口不代表删除旧目录。

独立 `Xke.ProtocolHost` 使用显式独立数据根，只调用普通资料和课表写入，不调用凭据/会话/桌面存储或DPAPI。
接手见 [后端技术手册](../../notes/development-docs/personal-runtime/05_backend_technical_manual.md)。


## 2026-10-08 当前业务文件

个人资料 profile.json、账号偏好 preferences.json 写普通JSON，旧加密业务数据不兼容，不自动迁移或删除。课表JSON/新CSV来自同一标准化结果，以同目录临时文件替换；学期快照放 schedules/<term>/semester/semester_schedule.json/.csv，周缓存仍在同学期weeks目录。CSV数组为分号分隔，字段定义见 notes/development-docs/personal-runtime/03_api_contract.md。密码、Cookie与运行会话的既有保护未改。

2026-10-11：业务JSON无法解析时视为不可用，读取不修改文件、不尝试旧格式解密。没有完整资料/课表的本地账号不能自动恢复，需经学校验证码认证重新建立工作区；成功生成新JSON，失败由已有初始化快照回滚。
