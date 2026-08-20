# 今日人品间隔分析器

一个基于 .NET 8 + WPF + MVVM 的桌面应用，用于：

- 对大量识别码逐个计算指定时间窗口内每天的"今日人品"；
- **最大间隔模式**：找出每个识别码所有 100 分日期中"相邻最大间隔"，并在全部识别码中找出间隔最大的 Top-K；
- **距今最久模式**：找出第一个 100 分日期出现最晚的识别码（Top-K），每个识别码命中首个 100 分即早停，计算量极小。

## 项目结构

```
RpCalculator.sln
├── src/
│   ├── RpCalculator.Core/          # 纯算法类库，无 UI 依赖
│   │   ├── StableHash.cs           # 稳定哈希（UTF-16 char 流，哈希状态复用）
│   │   ├── IdFormat.cs             # 识别码固定格式（16 位十六进制 4-4-4-4）规范化与验证
│   │   ├── DateRangeInfo.cs        # 窗口日期预计算与按年分组（避免重复字符串分配）
│   │   ├── RpScanner.cs            # 单识别码扫描（标量 / 早停 / 完整收集三种路径）
│   │   ├── ParallelRpProcessor.cs  # 并行批处理、进度、取消、Top-K 合并
│   │   ├── TopKResultStore.cs      # 线程安全 Top-K 容器（淘汰第 K 名、同指标保留先发现）
│   │   ├── RandomIdGenerator.cs    # 惰性随机识别码生成（Xoshiro256**，首字符排除 0）
│   │   ├── FileIdSource.cs         # 流式读取文件识别码
│   │   ├── CountParser.cs          # 数量解析（支持 1e10 科学计数法）
│   │   └── Models.cs               # 扫描/进度/结果模型
│   └── RpCalculator.App/           # WPF UI（MVVM、卡片式布局、深/浅主题、自定义标题栏）
│       ├── MainWindow.xaml
│       ├── MainViewModel.cs
│       ├── Mvvm/                   # ObservableObject / RelayCommand / AsyncRelayCommand
│       └── Themes/                 # Light.xaml / Dark.xaml
└── tests/
    └── RpCalculator.Core.Tests/    # xUnit 核心算法测试
```

## 构建与运行

需要安装 .NET 8 SDK（或更高版本，目标框架为 net8.0）。

```bash
# 构建
dotnet build RpCalculator.sln -c Release

# 运行 WPF 应用
dotnet run --project src/RpCalculator.App/RpCalculator.App.csproj -c Release

# 运行测试
dotnet test RpCalculator.sln -c Release
```

## 发布（单文件 EXE / MSI 安装包）

> 由于 WPF 不支持裁剪（`PublishTrimmed=true` 在 .NET 8 WPF 项目里直接报错），
> 自包含 .NET 8 桌面应用最小体积约 65MB。**单文件无法做到 20MB 以内**，
> 因此按约定 `≥50MB` 跳过单文件强制要求，改为生成 MSI 安装包。
> 我们仍同时发布单文件 EXE 作为副产物。

### 产物

| 文件 | 大小 | 说明 |
|------|------|------|
| `artifacts\publish-singlefile\RpCalculator.App.exe` | ~68 MB | 自包含单文件 EXE，运行时自动解压到临时目录。适合拷贝即用。 |
| `artifacts\RpCalculator-Setup.msi` | ~52 MB | WiX 4.0.4 MSI 安装包，桌面 + 开始菜单快捷方式，图标使用 `app.ico`。 |

### 一键发布

```cmd
publish.cmd
```

脚本自动完成：

1. 把 `C:\Users\Lenovo\Desktop\6.png` 转成 16/32/48/64/128/256 多分辨率 ICO（`Assets\app.ico`）；
2. 发布单文件 EXE（`PublishSingleFile=true` + `EnableCompressionInSingleFile=true` + `IncludeNativeLibrariesForSelfExtract=true`）；
3. 发布自包含多文件目录（`PublishSingleFile=false`），作为 MSI 内容源；
4. 生成 WiX 4 源（`installer\installer.wxs` + `.wxl`）并调用 `wix build` 输出 MSI。

> **注意**：项目路径如果包含 `#`，WiX 会把路径当 URI 解析失败。
> `publish.cmd` 会自动检测并把项目复制到 `%TEMP%\rpbuild_clean` 再构建，
> 最后把产物拷回原 `artifacts/`。

### 前置依赖

```bash
# 安装 WiX 4 工具链（一次性）
dotnet tool install wix --version 4.0.4
dotnet wix extension add WixToolset.UI.wixext/4.0.4

# 图标转换需要 Python + Pillow（项目里用 venv 装）
python -m venv C:\Users\Lenovo\.workbuddy\binaries\python\envs\default
C:\Users\Lenovo\.workbuddy\binaries\python\envs\default\Scripts\pip install Pillow
```

### 手动发布（与 `publish.cmd` 内部步骤一致）

```bash
# 1) 转换图标（仅当 PNG 更新时）
python .workbuddy\scripts\convert_icon.py

# 2) 单文件 EXE
dotnet publish src\RpCalculator.App\RpCalculator.App.csproj -c Release -r win-x64 ^
    --self-contained true -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=embedded -p:InvariantGlobalization=true ^
    -o artifacts\publish-singlefile

# 3) 自包含多文件目录
dotnet publish src\RpCalculator.App\RpCalculator.App.csproj -c Release -r win-x64 ^
    --self-contained true -p:PublishSingleFile=false ^
    -p:DebugType=embedded -p:InvariantGlobalization=true ^
    -o artifacts\publish-folder

# 4) 生成 WiX 源 + 构建 MSI
python .workbuddy\scripts\generate_wix.py
dotnet wix build -b artifacts\publish-folder -b . -arch x64 ^
    -ext WixToolset.UI.wixext -o artifacts\RpCalculator-Setup.msi ^
    installer\installer.wxs -loc installer\installer.wxl
```

## 应用图标

- 源图：`C:\Users\Lenovo\Desktop\6.png`（96×96 RGBA）。
- 自动转换：`Assets\app.ico`（16/32/48/64/128/256 多分辨率）。
- 设置位置：
  - `RpCalculator.App.csproj` 的 `<ApplicationIcon>` → 嵌入为 EXE 图标资源（资源管理器、任务栏、Alt-Tab）；
  - `MainWindow.xaml` 的 `Icon="Assets\app.ico"` → 运行时窗口图标；
  - 自定义标题栏左侧 `<Image Source="Assets\app.ico">` → 标题栏小图标。
- MSI：快捷方式使用同一个 `Assets\app.ico`。

如果 AI 环境无法读取 PNG 源图，请用以下命令手动生成：
```bash
python .workbuddy\scripts\convert_icon.py
```

## 原始计算（独立验算）

UI 新增“原始计算”卡片，按 Python 参考算法逐日重算，结果用于核对主扫描器。
算法与 `src\RpCalculator.Core\RawVerifier.cs` 严格等价：

- 字符串直接拼接 `rid + year + doy` 与 `rid + year + doy + day`；
- 不复用哈希状态，每次对完整字符串跑 `stable_hash`；
- 64 位有符号 long，移位/异或/加法全部 `unchecked`，不抛 `OverflowException`；
- `abs(h1/3 + h2/3) % 527527 >= 510927` 整数判断 100 分；
- `long.MinValue` 绝对值用 ulong 承载，避免 `Math.Abs` 异常；
- 计算放在 `Task.Run` 后台线程，UI 不卡；
- 日期格式严格 `yyyy-MM-dd`；
- 点击“填入最佳”自动从主扫描器当前选中结果复制识别码与窗口参数，便于对比。

测试覆盖：`tests\RpCalculator.Core.Tests\RawVerifierTests.cs` 同时调用
`RawVerifier.CheckId` 与 `RpScanner.ScanWithDates`，断言最大间隔、100 分日期
数量与列表完全一致（多个识别码与窗口）。

## 持续集成（GitHub Actions）

仓库内置 `.github/workflows/build-release.yml` 自动构建工作流：

- **推送以 `v` 或 `r` 开头的 tag**（如 `v1.0.0`、`r2.1`）→ 自动还原、构建、跑全部测试，发布 win-x64 自包含可执行程序，并自动创建 GitHub Release 附加 zip 产物：
  ```bash
  git tag v1.0.0
  git push origin v1.0.0
  ```
- **手动触发**（Actions 页面 → Build & Release → Run workflow）→ 同样执行构建与测试，产物上传为 Artifacts，不创建 Release。

## 识别码格式约定

- **固定格式**：16 位大写十六进制字符（`0-9` / `A-F`），按 `4-4-4-4` 分组、段间用 `-` 连接，形如 `7123-4567-890A-BCDE`。
- **排除左侧零**：另一个应用会对识别码做"去横线 → 删前导 0 → 左侧补 7 → 重新分组"的规范化。因此任何去掉横线后以 `0` 开头的识别码都会被改写，对当前计算无效。
  - 随机生成时保证首字符来自 `1-9` / `A-F`（排除 `0`）；
  - 文件导入时逐行验证（去空格、去横线、转大写、16 位十六进制、首字符非 0），无效行跳过并统计，UI 显示"共跳过无效识别码：N 个"。

## 算法实现说明

1. **稳定哈希**
   - `hash = 5381`，对每个 UTF-16 `char c`：`hash = (hash << 5) ^ hash ^ c`；
   - 最终 `hash ^= 0xA98F501BC684032F`，全部使用 `unchecked` 64 位有符号运算。
2. **每日种子**
   - `seed1 = id + year + dayOfYear`，`seed2 = seed1 + day`，分别做完整哈希得到 `h1`、`h2`。
3. **100 分整数判断**
   - `Q = abs(h1 / 3 + h2 / 3)`，若 `Q % 527527 >= 510927` 则该日为 100 分。
   - 完全等价于原始浮点算法，但避免了浮点/舍入开销。
4. **哈希状态复用**
   - `stateId = ContinueHash(5381, id)`，每年 `stateYear = ContinueHash(stateId, year)`，每天从 `stateYear` 继续，避免重复计算识别码与年份前缀。

## 算法模式

- **最大间隔**：扫描全部日期，计算相邻 100 分日期间隔的最大值（不足 2 个 100 分日期的识别码无效）。
- **距今最久**：从窗口起始日向后扫描，找到第一个 100 分日期立即返回（早停），比较其距离起始日的天数，取最大者。每个识别码命中后不再扫描后续日期，计算量远小于完整扫描。

两种模式均支持 Top-K（K 默认 10，范围 1~1000）与并行处理。

## 性能设计

- **不保存每日结果**：`ScanCore` 只保留标量（最大间隔、100 分次数、首个 100 分索引）。
- **只为候选保存日期**：只有可能进入 Top-K 的候选才在"最大间隔"模式下做第二次完整扫描收集 100 分日期。
- **流式 + 分批**：`IEnumerable<string>` 惰性生成/读取，每次只物化 100,000 条，支持 100 亿规模输入而不一次性载入内存。
- **并行合并**：`Parallel.ForEach` 每个 worker 维护局部 Top-K（最多 K 个标量候选），批处理结束后合并，减少锁竞争；`TopKResultStore` 内部用 `lock` 保护。
- **进度节流**：每个批次通过 `IProgress<T>` 更新一次 UI，避免频繁跨线程调用。
- **取消**：每批开始与 `ParallelOptions.CancellationToken` 双重检查；取消后返回当前已找到的最佳结果。

## UI 功能

- 随机生成 / 文件导入两种识别码来源；
- 识别码数量支持 `10000000000`、`1e10` 等格式；
- K 值（Top-K）输入与算法模式下拉切换；
- 实时进度条、已处理数量、当前全局最佳、跳过无效识别码统计；
- 结果区：候选识别码下拉（按发现时间排序），选中后显示识别码、关键指标、100 分次数、首个 100 分日期与 100 分日期列表；
- "复制结果"将选中识别码的详细信息写入剪贴板；
- 自定义标题栏（拖拽移动、最小化/最大化/关闭），浅色 / 深色主题一键切换（所有控件含下拉、日历、滚动条、标题栏均跟随主题）。

## 测试覆盖

- 稳定哈希已知向量；
- 100 分判断已知真/假用例；
- 识别码格式规范化与无效排除（含前导零示例）；
- 随机生成器首字符排除 `0`、格式正确、种子确定性；
- 最大间隔 / 距今最久两种模式的计算正确性；
- 距今最久模式早停验证（命中后不再扫描后续日期）；
- Top-K 容器维护（K=3 保留最大 3 个、同指标保留先发现、发现时间排序）；
- 并行结果与单线程 Top-K 一致性（两种模式）；
- 无效识别码过滤统计、全无效输入；
- 立即取消、空输入、大批量分批稳定性与内存上界。

> 说明：随机生成不强制唯一；在 100 亿规模下用 `HashSet` 去重会内存爆掉，因此默认允许极小概率重复。若业务上必须严格唯一，建议改用布隆过滤器或数据库/外部存储去重。
