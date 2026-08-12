# CutPic Windows Installer

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

Output:

```text
artifacts\installer\CutPic-Setup-x64.msi
```

The script creates a self-contained .NET 10 `win-x64` publish and packages it with WiX Toolset 5.
Before packaging, it runs the unified Release build, xUnit tests, application self-tests, and publish validation.
The MSI installs CutPic for all users under `Program Files\CutPic`, creates Start Menu and desktop shortcuts,
and registers a normal Windows uninstall entry. Upgrades preserve user data stored under `%LocalAppData%\CutPic`.

Release checklist:

1. Update `Version`, `AssemblyVersion`, and `FileVersion` in `CutPic.csproj`.
2. Keep the `UpgradeCode` in `Package.wxs` unchanged.
3. Update the MSI `Version` to match the project version.
4. Run the build script and verify the MSI ProductVersion.
5. Sign `CutPic.exe` and the MSI when a code-signing certificate is available.
