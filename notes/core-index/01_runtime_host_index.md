# 运行时与桌面宿主索引

## 目标
整理 legacy Python 版本的启动链路、桌面宿主职责、单实例机制与当前 .NET DesktopHost 的迁移参考。

## 当前关键路径
- `Z:/XKE/start_xke.py`
- `Z:/XKE/core/__main__.py`
- `Z:/XKE/core/desktop_runtime.py`
- `Z:/XKE/core/desktop_settings.py`
- `Z:/XKE/core/desktop_shell.py`
- `Z:/XKE/api/routes/desktop.py`

## 当前职责分布

### 1. 启动入口
- `Z:/XKE/start_xke.py`
  - 真实桌面启动入口
  - 负责端口选择、单实例复用、本地 API 启动、桌面壳或浏览器打开、退出清理

### 2. 备用模块入口
- `Z:/XKE/core/__main__.py`
  - 支持 `python -m core [api|cli|gui]`
  - 是代码层面的多入口调试入口

### 3. 单实例与运行时登记
- `Z:/XKE/core/desktop_runtime.py`
  - 保存当前实例信息
  - 检测并复用已运行实例

### 4. 桌面设置
- `Z:/XKE/core/desktop_settings.py`
  - 管理静默启动、开机自启等桌面设置

### 5. 桌面壳
- `Z:/XKE/core/desktop_shell.py`
  - 当前 pywebview 桌面壳
  - 负责窗口、托盘、桌面桥接、通知等

### 6. 桌面 API
- `Z:/XKE/api/routes/desktop.py`
  - 提供桌面设置与激活接口

## 迁移目标
未来映射到：
- `Xke.DesktopHost`

建议承接职责：
- 单实例
- 窗口激活
- 托盘
- 自启
- 静默启动
- 通知
- WebView2 宿主
- 本地 WebHost 生命周期协调

## 保留与替换原则
- 保留：当前启动顺序、失败回退、单实例思想
- 不保留：Python/uvicorn/pywebview 的具体实现细节

## 迁移优先级
- 当前阶段只做事实整理
- 最终迁移顺序靠后，位于 Auth / Schedule / Course / Network 之后
