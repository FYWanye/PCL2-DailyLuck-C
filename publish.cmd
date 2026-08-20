@echo off
REM ============================================================================
REM  RpCalculator 一键发布脚本
REM
REM  产出：
REM    artifacts\publish-singlefile\RpCalculator.App.exe   单文件 EXE
REM    artifacts\RpCalculator-Setup.msi                     MSI 安装包
REM
REM  注意：
REM  1. 自包含 .NET 8 WPF 的最小体积约 65MB（WPF 不支持裁剪），单文件无法做到 20MB。
REM     按规格约定 ≥50MB 时跳过单文件强制要求，改为 MSI 安装包。
REM  2. 需要 WiX 4.0.4：
REM       dotnet tool install wix --version 4.0.4
REM       dotnet wix extension add WixToolset.UI.wixext/4.0.4
REM  3. 项目路径不能含 '#'，否则 WiX 在解析 URI 时会失败。脚本会自动
REM     复制到 %TEMP%\rpbuild_clean 构建，最后把产物拷回原 artifacts。
REM ============================================================================

setlocal enabledelayedexpansion

set "ROOT=%~dp0"
pushd "%ROOT%"

REM ---- 0) 项目根不能含 '#'，否则 WiX 会把路径当 URI（# 是 fragment）。
echo [0/4] 检查项目路径...
echo "%ROOT%" | findstr "#" >nul
if not errorlevel 1 (
    echo   警告：项目路径含 '#'。自动复制到临时目录构建。
    set "BUILDDIR=%TEMP%\rpbuild_clean"
    if exist "!BUILDDIR!" rmdir /S /Q "!BUILDDIR!"
    robocopy "%ROOT%" "!BUILDDIR!" /E /NFL /NDL /NJH /NJS ^
        /XD .git .workbuddy bin obj artifacts .vs >nul
    if errorlevel 8 goto :err
    pushd "!BUILDDIR!"
    set "PROOT=!BUILDDIR!"
) else (
    set "PROOT=%ROOT%"
)

REM ---- 1) 生成图标（PNG -> ICO）
echo [1/4] 生成图标...
python "%PROOT%\.workbuddy\scripts\convert_icon.py"

REM ---- 2) 发布单文件 EXE
echo [2/4] 发布单文件 EXE（self-contained + 压缩）...
if exist "%PROOT%\artifacts\publish-singlefile" rmdir /S /Q "%PROOT%\artifacts\publish-singlefile"
dotnet publish "%PROOT%\src\RpCalculator.App\RpCalculator.App.csproj" ^
    -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=embedded ^
    -o "%PROOT%\artifacts\publish-singlefile"
if errorlevel 1 goto :err

REM ---- 3) 发布自包含多文件目录（供 MSI 打包用）
echo [3/4] 发布自包含目录（MSI 内容源）...
if exist "%PROOT%\artifacts\publish-folder" rmdir /S /Q "%PROOT%\artifacts\publish-folder"
dotnet publish "%PROOT%\src\RpCalculator.App\RpCalculator.App.csproj" ^
    -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=false ^
    -p:DebugType=embedded ^
    -o "%PROOT%\artifacts\publish-folder"
if errorlevel 1 goto :err

REM ---- 4) 生成 WiX 源并构建 MSI
echo [4/4] 构建 MSI 安装包...
python "%PROOT%\.workbuddy\scripts\generate_wix.py"
if errorlevel 1 goto :err

dotnet wix build ^
    -b "%PROOT%\artifacts\publish-folder" ^
    -b "%PROOT%" ^
    -arch x64 ^
    -ext WixToolset.UI.wixext ^
    -o "%PROOT%\artifacts\RpCalculator-Setup.msi" ^
    "%PROOT%\installer\installer.wxs" ^
    -loc "%PROOT%\installer\installer.wxl"
if errorlevel 1 goto :err

REM ---- 5) 清理临时构建目录
if defined BUILDDIR (
    popd
    rmdir /S /Q "!BUILDDIR!"
)
popd

echo.
echo ========== 发布成功 ==========
dir "%ROOT%\artifacts\RpCalculator-Setup.msi"
dir "%ROOT%\artifacts\publish-singlefile\RpCalculator.App.exe"
exit /b 0

:err
echo.
echo *** 发布失败，错误码 %errorlevel% ***
exit /b %errorlevel%
