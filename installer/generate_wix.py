"""根据已发布的 self-contained 目录生成 WiX 4 MSI 安装包源文件。

这是仓库内可复用的通用版本，供本地 publish.cmd 和 GitHub Actions 使用。
用法示例：

    python installer/generate_wix.py ^
        --publish-dir artifacts/publish-folder ^
        --out-wxs installer/installer.g.wxs ^
        --out-wxl installer/installer.g.wxl ^
        --version 1.0.0.0

说明：
- 递归 harvest 发布目录下的所有文件和子目录（culture 子目录也会包含）。
- 生成确定性 GUID，重复生成不会导致安装包“每次都不一样”。
- 只针对 Windows x64/arm64 自包含发布目录使用。
"""
import argparse
import uuid
from pathlib import Path

APP_NAME = "今日人品间隔分析器"
APP_EXE = "RpCalculator.App.exe"
APP_DIR_NAME = "RpCalculator"
MANUFACTURER = "RpCalculator"
UPGRADE_CODE = "{5E5B9565-48C5-4D94-B133-BAF67CAFBA77}"
STARTMENU_GUID = "{C74A9D98-E1A6-4DA7-844A-3160D74E6D38}"
DESKTOP_GUID = "{0AE36D6D-0D9F-42F4-BE65-D434D52B9D84}"

INSTALL_DIR_ID = "INSTALLFOLDER"
APP_SUBDIR_ID = "AppSubFolder"
STARTMENU_DIR_ID = "StartMenuAppFolder"

# 组件 GUID 命名空间：用 uuid5 基于路径生成稳定 GUID。
GUID_NS = uuid.UUID("6d46b292-3cfb-4f37-a863-f61ed0686d05")


def stable_guid(*parts: str) -> str:
    return "{" + str(uuid.uuid5(GUID_NS, "/".join(parts))).upper() + "}"


def xml_escape(s: str) -> str:
    return (s.replace("&", "&amp;")
             .replace("<", "&lt;")
             .replace(">", "&gt;")
             .replace('"', "&quot;"))


def dir_id(rel: str) -> str:
    if rel == ".":
        return APP_SUBDIR_ID
    return "Sub_" + rel.replace("-", "_").replace("/", "_").replace(".", "_")


def render_dir_tree(subdirs) -> str:
    lines = [f'    <StandardDirectory Id="ProgramFiles64Folder">',
             f'      <Directory Id="{INSTALL_DIR_ID}" Name="{APP_DIR_NAME}">',
             f'        <Directory Id="{APP_SUBDIR_ID}" Name="{APP_NAME}">']
    for rel in sorted(subdirs):
        lines.append(f'          <Directory Id="{dir_id(rel)}" Name="{xml_escape(rel)}" />')
    lines.append('        </Directory>')
    lines.append('      </Directory>')
    lines.append('    </StandardDirectory>')
    lines.append('    <StandardDirectory Id="ProgramMenuFolder">')
    lines.append(f'      <Directory Id="{STARTMENU_DIR_ID}" Name="{APP_NAME}" />')
    lines.append('    </StandardDirectory>')
    return "\n".join(lines)


def render_component_groups(root_files, subdirs) -> str:
    parts: list[str] = []
    root_others = [f for f in root_files if f != APP_EXE]
    lines = [f'  <Fragment>',
             f'    <ComponentGroup Id="AppFiles" Directory="{APP_SUBDIR_ID}">',
             f'      <Component Id="AppAllFiles" Guid="{stable_guid("root")}">']
    for f in sorted(root_others):
        lines.append(f'        <File Source="{xml_escape(f)}" />')
    lines.append(f'        <File Id="AppExeKeyPath" Source="{APP_EXE}" KeyPath="yes" Checksum="yes" />')
    lines.append('      </Component>')
    lines.append('    </ComponentGroup>')
    lines.append('  </Fragment>')
    parts.append("\n".join(lines))

    for rel in sorted(subdirs):
        files = sorted(subdirs[rel])
        cg_id = "CG_" + dir_id(rel)
        comp_id = "Cmp_" + dir_id(rel)
        first = files[0]
        rest = files[1:]
        flines = [f'  <Fragment>',
                  f'    <ComponentGroup Id="{cg_id}" Directory="{dir_id(rel)}">',
                  f'      <Component Id="{comp_id}" Guid="{stable_guid(rel)}">']
        flines.append(f'        <File Id="{comp_id}_kp" Source="{xml_escape(rel)}/{xml_escape(first)}" KeyPath="yes" />')
        for f in rest:
            flines.append(f'        <File Source="{xml_escape(rel)}/{xml_escape(f)}" />')
        flines.append('      </Component>')
        flines.append('    </ComponentGroup>')
        flines.append('  </Fragment>')
        parts.append("\n".join(flines))

    return "\n\n".join(parts)


def render_feature_refs(subdirs) -> str:
    refs = ['      <ComponentGroupRef Id="AppFiles" />']
    for rel in sorted(subdirs):
        refs.append(f'      <ComponentGroupRef Id="CG_{dir_id(rel)}" />')
    refs.append('      <ComponentGroupRef Id="Shortcuts" />')
    return "\n".join(refs)


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate WiX 4 source from a publish folder")
    parser.add_argument("--publish-dir", required=True, help="Path to self-contained publish folder")
    parser.add_argument("--out-wxs", required=True, help="Output .wxs path")
    parser.add_argument("--out-wxl", required=True, help="Output .wxl path")
    parser.add_argument("--version", default="1.0.0.0", help="Installer version, e.g. 1.0.0.0")
    args = parser.parse_args()

    publish_dir = Path(args.publish_dir)
    out_wxs = Path(args.out_wxs)
    out_wxl = Path(args.out_wxl)
    version = args.version

    dirs: dict[str, list[str]] = {}
    for p in sorted(publish_dir.rglob("*")):
        if p.is_file():
            rel = p.relative_to(publish_dir).as_posix()
            parent = rel.rsplit("/", 1)[0] if "/" in rel else "."
            dirs.setdefault(parent, []).append(p.name)

    root_files = dirs.get(".", [])
    subdirs = {k: v for k, v in dirs.items() if k != "."}
    print(f"harvested: {len(root_files)} root files, {len(subdirs)} subdirs, "
          f"total {sum(len(v) for v in dirs.values())} files")

    wxs = f'''<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">

  <!--
    {APP_NAME} 安装包（WiX 4，由 generate_wix.py 自动生成）
    - 自包含发布：所有 .NET / WinForms 运行时已嵌入，安装时无需目标机器预装 .NET
    - 递归打包所有 culture 子目录
    - 安装目录：%ProgramFiles%\\{APP_DIR_NAME}\\{APP_NAME}
    - 桌面 + 开始菜单快捷方式，使用 app.ico
  -->
  <Package Name="{APP_NAME}"
           Version="{version}"
           Manufacturer="{MANUFACTURER}"
           UpgradeCode="{UPGRADE_CODE}">

    <MajorUpgrade AllowSameVersionUpgrades="yes"
                  DowngradeErrorMessage="!(loc.DowngradeErrorMessage)" />

    <MediaTemplate EmbedCab="yes" CompressionLevel="high" />

    <ui:WixUI Id="WixUI_Minimal" />

    <Feature Id="MainFeature" Title="!(loc.FeatureMainTitle)" Level="1">
{render_feature_refs(subdirs)}
    </Feature>
  </Package>

  <!-- ============== 目录结构 ============== -->
  <Fragment>
{render_dir_tree(subdirs)}
  </Fragment>

{render_component_groups(root_files, subdirs)}

  <!-- ============== 快捷方式：开始菜单 ============== -->
  <Fragment>
    <DirectoryRef Id="{STARTMENU_DIR_ID}">
      <Component Id="StartMenuShortcut" Guid="{STARTMENU_GUID}">
        <Shortcut Id="StartMenuShortcutLink"
                  Name="{APP_NAME}"
                  Target="[{APP_SUBDIR_ID}]{APP_EXE}"
                  Icon="AppIcon"
                  WorkingDirectory="{APP_SUBDIR_ID}" />
        <RemoveFolder Id="RemoveStartMenuFolder" Directory="{STARTMENU_DIR_ID}" On="uninstall" />
        <RegistryValue Root="HKCU"
                       Key="Software\\{APP_DIR_NAME}\\{APP_NAME}"
                       Name="installed"
                       Type="integer"
                       Value="1"
                       KeyPath="yes" />
      </Component>
    </DirectoryRef>
  </Fragment>

  <!-- ============== 快捷方式：桌面 ============== -->
  <Fragment>
    <StandardDirectory Id="DesktopFolder">
      <Component Id="DesktopShortcut" Guid="{DESKTOP_GUID}">
        <Shortcut Id="DesktopShortcutLink"
                  Name="{APP_NAME}"
                  Target="[{APP_SUBDIR_ID}]{APP_EXE}"
                  Icon="AppIcon"
                  WorkingDirectory="{APP_SUBDIR_ID}" />
        <RegistryValue Root="HKCU"
                       Key="Software\\{APP_DIR_NAME}\\{APP_NAME}"
                       Name="desktop"
                       Type="integer"
                       Value="1"
                       KeyPath="yes" />
      </Component>
    </StandardDirectory>
  </Fragment>

  <!-- ============== 快捷方式 ComponentGroup ============== -->
  <Fragment>
    <ComponentGroup Id="Shortcuts">
      <ComponentRef Id="StartMenuShortcut" />
      <ComponentRef Id="DesktopShortcut" />
    </ComponentGroup>
  </Fragment>

  <!-- ============== 图标：多分辨率 ICO（路径相对 -b 绑定根） ============== -->
  <Fragment>
    <Icon Id="AppIcon" SourceFile="src\\RpCalculator.App\\Assets\\app.ico" />
  </Fragment>

</Wix>
'''
    out_wxs.parent.mkdir(parents=True, exist_ok=True)
    out_wxs.write_text(wxs, encoding="utf-8")
    print(f"wrote {out_wxs} ({len(wxs)} bytes)")

    wxl = f'''<?xml version="1.0" encoding="utf-8"?>
<WixLocalization Culture="zh-CN" xmlns="http://wixtoolset.org/schemas/v4/wxl">
  <String Value="已安装更高版本，无法降级。" Id="DowngradeErrorMessage" />
  <String Value="{APP_NAME} 主程序" Id="FeatureMainTitle" />
</WixLocalization>
'''
    out_wxl.parent.mkdir(parents=True, exist_ok=True)
    out_wxl.write_text(wxl, encoding="utf-8")
    print(f"wrote {out_wxl} ({len(wxl)} bytes)")


if __name__ == "__main__":
    main()
