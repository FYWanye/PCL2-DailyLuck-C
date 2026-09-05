# 更新日志

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)。

## [v1.0.6] - 2026-09-05

### 重构

- UI 从 WPF 迁移到 **WinForms + AntdUI**，采用 Ant Design 现代浅色 / 深色风格。
- 新增 `DESIGN.md` 视觉规范，并将设计 token 镜像到 `DesignTokens.cs`。
- 重写 `README.md`，贴合当前 WinForms + AntdUI 项目状态。

### 新增

- 页面切换 / 启动欢迎动画：轻量滑入，降低卡顿与掉帧。
- 按钮加载动画、进度条平滑过渡。
- 计算完成提示改为显示在应用窗口内部顶部。
- 主计算 / 原始计算 / 性能基准完成后发送 Windows 原生通知。
- CI 支持：
  - 推送任意 tag（例如 `1.0.0` 或 `v1.0.0`）自动构建并发布；
  - 手动运行 Workflow 并填写版本号即可发布；
  - 自动产出 Windows x64 / arm64 的 **MSI 安装包**和绿色版 zip；
  - Release 标题与附件统一使用 `v` 前缀版本号。
- 新增 `installer/generate_wix.py`，自动根据发布目录生成 WiX 安装包源文件。

### 修复

- 优化并行任务进度上报：改为自适应节流，小任务进度反馈更及时，大任务不会长时间无反馈。
- 原始计算补充“首个 100 分日期”信息，界面展示更完整。
- 清理旧 WPF 时代遗留的 UI / 项目结构描述与 CI 文案。
