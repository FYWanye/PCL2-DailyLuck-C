# 今日人品间隔分析器

一个基于 **.NET 8 + WinForms + AntdUI** 的 Windows 桌面工具，用于对大量识别码计算“今日人品”的 100 分日期分布，并找出最大间隔 / 距今最久的 Top-K 识别码。

界面采用 Ant Design 现代浅色 / 深色风格，包含实时进度、性能监控、原始算法独立验算与性能基准测试。

## 功能特性

### 主扫描计算
- 支持**随机生成**与**从文件导入**两种识别码来源。
- 支持超大规模数量输入，如 `10000000000`、`1e10`。
- 两种算法模式：
  - **最大间隔模式**：找出所有 100 分日期中“相邻最大间隔”最大的 Top-K。
  - **距今最久模式**：找出第一个 100 分日期出现最晚的 Top-K，命中首个 100 分即早停。
- 实时进度、已处理数量、当前全局最佳、速度 / ETA / 运行时间。
- 可随时取消，取消后保留已找到的最佳结果。
- 可手动选择 CPU 占用档位：25% / 50% / 75% / 100%（默认 50%，100% 表示使用全部逻辑核心）。
- 性能基准测试固定使用 100% CPU，不受该档位影响。

### 结果分析
- 候选识别码下拉，支持筛选、按指标 / 次数 / 发现顺序 / 识别码排序。
- 展示关键指标、100 分次数、首个 100 分日期、完整 100 分日期列表。
- 支持自动查找“最近一次满分日期”。
- 一键复制结果到剪贴板。

### 原始计算验算
- 使用独立、未优化的逐日算法重新计算单个识别码，用于核对主扫描器结果。
- 可从当前最佳结果一键填入原始计算输入。
- 展示最大间隔、首个 100 分天数、100 分日期数量及完整日期列表。

### 性能基准测试
- 固定 1,000,000 个随机识别码 × 1780 天完整计算。
- 展示总耗时、平均处理速度、等效性能倍数与性能评级。
- 测试完成后可查看当前测试中的最佳识别码。

### 界面与动效
- AntdUI 无边框圆角窗口，左侧导航、主内容区、右侧快速状态栏三段式布局。
- 浅色 / 深色主题一键切换。
- 页面切换 / 启动欢迎使用轻量滑入动画。
- 按钮加载动画、进度条平滑过渡。
- 完成提示显示在**应用窗口内部顶部**。
- 主计算、原始计算、性能基准完成后发送 **Windows 原生通知**。

## 技术栈

| 层 | 技术 |
|----|------|
| UI | WinForms + AntdUI 2.4.6 |
| 语言 | C# / .NET 8 (`net8.0-windows`) |
| 算法核心 | `RpCalculator.Core` 纯 .NET 类库，无 UI 依赖 |
| 测试 | xUnit |
| 视觉规范 | DESIGN.md + `@google/design.md` CLI |
| 发布 | 自包含单文件 EXE / WiX 4 MSI / GitHub Actions |

## 项目结构

```text
RpCalculator.sln
├── DESIGN.md                        # 视觉规范（@google/design.md 格式）
├── package.json                     # design.md CLI 工具链
├── publish.cmd                      # 一键发布 EXE + MSI
├── installer/                       # WiX 安装包源码与自动生成脚本
│   ├── installer.wxs                # 本地发布生成的 WiX 源
│   ├── installer.wxl                # WiX 中文本地化
│   └── generate_wix.py              # 根据发布目录自动生成 WiX 源（本地/CI 共用）
├── .github/workflows/build-release.yml
├── src/
│   ├── RpCalculator.Core/           # 纯算法类库，无 UI 依赖
│   │   ├── StableHash.cs            # 稳定哈希（64 位无符号 ulong）
│   │   ├── IdFormat.cs              # 识别码固定格式（16 位十六进制 4-4-4-4）
│   │   ├── DateRangeInfo.cs         # 窗口日期预计算与按年分组
│   │   ├── RpScanner.cs             # 单识别码扫描（标量 / 早停 / 完整收集）
│   │   ├── ParallelRpProcessor.cs   # 并行批处理、进度、取消、Top-K 合并
│   │   ├── TopKResultStore.cs       # 线程安全 Top-K 容器
│   │   ├── RandomIdGenerator.cs     # 惰性随机识别码生成
│   │   ├── FileIdSource.cs          # 流式读取文件识别码
│   │   ├── CountParser.cs           # 数量解析（支持 1e10）
│   │   ├── RawVerifier.cs           # 原始算法独立验算
│   │   └── Models.cs                # 扫描/进度/结果模型
│   └── RpCalculator.App/            # WinForms + AntdUI 界面
│       ├── Program.cs               # 入口
│       ├── MainForm.cs              # 主窗口与交互逻辑
│       ├── DesignTokens.cs          # DESIGN.md token 的 C# 镜像
│       ├── AppleTheme.cs            # AntdUI 全局字体 / 主题配置
│       ├── WindowChrome.cs          # 无边框窗口原生拖拽/调整大小辅助
│       └── Assets/                  # 应用图标
└── tests/
    └── RpCalculator.Core.Tests/     # xUnit 核心算法测试
```

## 视觉规范（DESIGN.md）

UI 的视觉身份由根目录 `DESIGN.md` 定义。颜色、字体、圆角、间距与组件状态都有精确 token，`src/RpCalculator.App/DesignTokens.cs` 是这些 token 的 C# 镜像；代码统一引用 token 常量，避免散落魔法值。

```bash
npm install                 # 安装 @google/design.md@0.1.1
npm run design:lint         # 校验 DESIGN.md（应 0 errors / 0 warnings）
npm run design:spec         # 输出规范
npm run design:export:dtcg  # 导出 W3C DTCG tokens.json
```

## 构建与运行

需要 .NET 8 SDK 或更高版本，目标框架为 `net8.0-windows`。

```bash
# 构建
dotnet build RpCalculator.sln -c Release

# 运行 WinForms 应用
dotnet run --project src/RpCalculator.App/RpCalculator.App.csproj -c Release

# 运行测试
dotnet test RpCalculator.sln -c Release
```

## 发布

> 当前应用是 WinForms + AntdUI，**仅支持 Windows**，不会产出 macOS / Linux 安装包。
> CI 会同时构建 Windows x64 与 Windows arm64。

### GitHub Actions 自动发布

推送**任意 tag**（例如 `1.0.5` 或 `v1.0.5`）会触发 `.github/workflows/build-release.yml`，自动完成：

1. 还原依赖
2. Release 构建
3. 单元测试
4. 发布 Windows x64 / arm64 自包含程序
5. 生成单文件 EXE + MSI 安装包 + 绿色版 zip
6. 创建 GitHub Release 并附加产物

Release 标题和附件名会统一显示为 `v<版本>`：

```bash
# 不带 v 也可以，例如 1.0.5
git tag 1.0.5
git push origin 1.0.5

# 带 v 也可以
git tag v1.0.6
git push origin v1.0.6
```

### 手动触发 Release

在 GitHub 的 **Actions → Build & Release → Run workflow** 中填写版本号：

```text
version: 1.0.5
```

运行成功后会自动创建/更新 `v1.0.5` Release。

### 本机一键发布（仅 Windows x64）

```cmd
publish.cmd
```

脚本会自动：发布单文件 EXE → 发布多文件目录 → 生成 WiX 源 → 构建 MSI。

| 本机产物 | 说明 |
|------|------|
| `artifacts\publish-singlefile\RpCalculator.App.exe` | 自包含单文件 EXE |
| `artifacts\RpCalculator-Setup.msi` | WiX 4 MSI 安装包（x64） |

> 注意：项目路径如果包含 `#`，WiX 会把路径当 URI 解析失败；`publish.cmd` 会自动复制到临时目录构建。

## 识别码格式约定

- 固定格式：16 位大写十六进制（`0-9` / `A-F`），按 `4-4-4-4` 分组，形如 `7123-4567-890A-BCDE`。
- 去掉横线后以 `0` 开头的识别码会被另一个应用规范化改写，因此视为无效输入。
- 随机生成时首字符来自 `1-9` / `A-F`（排除 `0`）；文件导入时逐行验证并统计无效行。

## 算法实现说明

1. **稳定哈希**：`hash = 5381`，对每个 UTF-16 `char c`：`hash = (hash << 5) ^ hash ^ c`，最终异或 `0xA98F501BC684032F`，全程使用 `ulong`。
2. **每日种子**：
   - `seed1 = "asdfgbn" + dayOfYear + "12#3$45" + year + "IUY"`
   - `seed2 = "QWERTY" + id + "0*8&6" + day + "kjhg"`
3. **100 分判断**：对两个种子哈希做与 Python 参考一致的浮点计算，`rounded >= 970` 即为 100 分。
4. **性能优化**：哈希状态复用、日期按年分组、并行批处理、局部 Top-K 合并、候选二次扫描、早停、自适应进度节流。

## 测试覆盖

- 稳定哈希已知向量
- 100 分判断已知真 / 假用例
- 识别码格式规范化与无效排除
- 随机生成器首字符排除 `0`、格式正确、种子确定性
- 最大间隔 / 距今最久两种模式计算正确性
- 距今最久模式早停验证
- Top-K 容器维护策略
- 并行结果与单线程 Top-K 一致性
- 无效识别码过滤、全无效输入
- 取消、空输入、大批量分批稳定性

当前测试数量：**84 个 xUnit 测试全部通过**。

## 致谢

- 项目灵感：[0x1A5 的视频](https://www.bilibili.com/video/BV1PLgK6XEyy/)
- 算法来源：[Zyx-2012 / daily-luck](https://github.com/Zyx-2012/daily-luck)
- 项目重构前的 Rust 版：[FYWanye / daily-luck-rust](https://github.com/FYWanye/daily-luck-rust)
