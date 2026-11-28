#!/usr/bin/env python3
"""Cross-product version check. Supports JSON and MSBuild XML without third-party packages."""
import argparse
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
import re
from check_installer_policy import check_source

def check(version, files):
    if not files:
        raise ValueError("No version files configured")
    for name in files:
        p = Path(name)
        if p.is_absolute() or ".." in p.parts or not p.is_file():
            raise ValueError(f"Invalid or missing version file: {name}")
        if p.suffix == ".json":
            obj = json.loads(p.read_text(encoding="utf-8"))
            actual = obj.get("version") or obj.get("package", {}).get("version")
        elif p.suffix in (".csproj", ".props"):
            root = ET.parse(p).getroot()
            actual = root.findtext(".//Version") or root.findtext(".//VersionPrefix")
        else:
            raise ValueError(f"Unsupported version source: {name}")
        if actual != version:
            raise ValueError(f"{name}: expected {version}, got {actual}")
    return True

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--version")
    p.add_argument("--config", default="onlytools.config.json")
    args = p.parse_args()
    try:
        cfg = json.loads(Path(args.config).read_text(encoding="utf-8"))
        source = ET.parse("Directory.Build.props").getroot()
        canonical = source.findtext(".//Version")
        if not canonical or not re.fullmatch(r"\d+\.\d+\.\d+", canonical):
            raise ValueError("OnlyCut MSI requires a numeric major.minor.build version")
        major, minor, build = map(int, canonical.split('.'))
        if major > 255 or minor > 255 or build > 65535:
            raise ValueError("Version exceeds Windows Installer ProductVersion limits")
        args.version = args.version or canonical
        if args.version != canonical:
            raise ValueError("Requested version differs from canonical MSBuild version")
        for field in ("AssemblyVersion", "FileVersion"):
            if source.findtext(f".//{field}") != "$(Version).0":
                raise ValueError(f"{field} must map to $(Version).0")
        project = ET.parse("CutPic.csproj").getroot()
        if any(project.find(f".//{field}") is not None for field in ("Version", "AssemblyVersion", "FileVersion")):
            raise ValueError("CutPic.csproj must inherit version fields")
        wix = ET.parse("installer/Package.wxs").getroot()
        package = wix.find("{http://wixtoolset.org/schemas/v4/wxs}Package")
        if package.get("Version") != "$(var.ProductVersion)" or package.get("UpgradeCode") != "6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A":
            raise ValueError("MSI version mapping or UpgradeCode changed")
        files = cfg.get("build", {}).get("versionFiles")
        if files is None:
            files = ["package.json", "src-tauri/tauri.conf.json"]
        check(args.version, files)
        check_source()
    except (OSError, ValueError, ET.ParseError) as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return 1
    print(f"Version sources agree: {args.version}")
    return 0

if __name__ == "__main__":
    sys.exit(main())
