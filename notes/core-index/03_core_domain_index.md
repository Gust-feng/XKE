# 核心业务与安全索引

> 2026-10-07 路径说明：本文件中的旧 Python `Z:/XKE/...` 路径仅用于历史来源标识。对应目录已归档到当前仓库 ignored 的 `开发数据/历史工程/旧Python工程.zip`，不代表当前活动工程下仍有这些文件，也不参与运行。


## 目标
标记 legacy Python 版本的核心业务巨石、数据存储、安全边界、公告与遥测实现，为当前 `Xke.Core` / `Xke.Storage` / `Xke.Security` 收敛提供参考。

## 当前关键路径
- `Z:/XKE/core/jw_services.py`
- `Z:/XKE/core/campus_network.py`
- `Z:/XKE/core/feedback_client.py`
- `Z:/XKE/core/telemetry.py`
- `Z:/XKE/core/runtime_env.py`
- `Z:/XKE/core/remote_sensing/*`
- `Z:/XKE/core/notice/*`
- `Z:/XKE/重构计划/09_附录/01_现有文件到新模块映射.md`

## 当前核心事实

### 1. 巨石业务文件
- `core/jw_services.py`
  - 同时承载：
    - 学生本地数据存储
    - 登录/SSO
    - 课表抓取
    - Cookie 课表回退
    - 选课预览与抢课
  - 当前至少可识别的核心边界：
    - `StudentDataStore`
    - `JwSsoClient`
    - `UnifiedScheduleService`
    - `CourseService`

### 2. 校园网
- `core/campus_network.py`
  - 当前是相对独立的子域
  - 适合拆为 `NetworkKeepAlive`

### 3. 公告与策略
- `core/notice/*`
  - 公告发布链路使用验签、哈希校验、DPAPI 保护缓存
  - 是版本策略、访问控制、功能开关的重要来源

### 4. 遥测与远程感知
- `core/telemetry.py`
- `core/remote_sensing/*`
  - 当前涉及本地统计、快照、spool、上传链路
  - 与安全边界强相关

### 5. 环境与安全配置
- `core/runtime_env.py`
  - 环境变量、运行时上下文、密钥/配置来源的重要入口

## 推荐模块拆分
- `Auth`
- `Schedule`
- `Course`
- `NetworkKeepAlive`
- `NoticePolicy`
- `Feedback`
- `Telemetry`
- `Preferences`
- `Storage`
- `Security`

## 当前迁移原则
- 保留行为语义
- 不在 C# 中复制新的巨石文件
- 优先先拆 `Storage` 与 `Security`
- 再按 Auth → Schedule → Course → Network → Notice/Feedback/Telemetry 迁移

## 当前已在 .NET 中固定的 Storage 边界
- `LegacyDataRoot`
- `LegacyTelemetryRoot`
- `LegacyNoticeCacheRoot`
- `LegacyStudentRoot`
- `LegacyDataPaths`

这些类型当前只负责固定 legacy `data/` 路径模型，还未开始真实迁移读写逻辑。
