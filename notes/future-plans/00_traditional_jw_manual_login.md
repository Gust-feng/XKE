# Course 传统教务验证码与抢课 Future Plan

> 状态：future-only  
> 用途：只保存 Course 连接、验证码交互、OCR 和真实抢课的后置边界。  
> 当前传统教务 Cookie 课表开发状态见 [traditional-jw-cookie-schedule](../development-docs/traditional-jw-cookie-schedule/README.md)。  
> 传统教务长期资料见 [教务系统资料索引](../sensitive-index/教务系统/01_接入资料索引.md)。

## 当前裁决

- 当前开发中的传统教务 Cookie 课表切片不在本文件维护。
- 本文件只管 Course 相关 future-only 能力：验证码连接、课程轮次、课程预览、抢课执行。
- 移动教务继续作为身份和课表主链路；传统教务验证码不得回到主登录卡片。

## Future-only 范围

- Course 连接 challenge 建模。
- 验证码图片获取、刷新和人工提交。
- OCR 自动识别。
- Course `rounds / preview` 真实传统教务连接。
- `course/grab/start`、`course/grab/stop` 的真实抢课轮询、限流、冷却和提交。

## UI 约束

- 验证码入口只能放在 Course 面板内的独立区域或弹层。
- 不在主登录卡片增加验证码输入区。
- 不让验证码流程干扰移动教务 trusted/session 主线。

## 技术约束

- Course 连接状态必须与移动教务 `connected` 分离。
- 抢课真实执行不得在当前 Cookie 课表切片中顺手实现。
- 真实验证码和 OCR 启动前，应先确认当前传统教务 Cookie 课表切片已经完成并通过回归。

## 重启条件

重新开启本 future plan 前，至少满足：

- [traditional-jw-cookie-schedule](../development-docs/traditional-jw-cookie-schedule/README.md) 已完成 Cookie 课表闭环。
- [教务系统资料索引](../sensitive-index/教务系统/01_接入资料索引.md) 已补齐 challenge/captcha 运行资料。
- Course 框架的 status/connect/rounds/preview/grab deferred 语义仍保持可测。
