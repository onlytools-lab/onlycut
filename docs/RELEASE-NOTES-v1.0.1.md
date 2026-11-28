# OnlyCut v1.0.1

**公开状态：未公开。** 本说明用于受控 Draft Release；公开发布需要单独授权。

## Highlights

OnlyCut（原 CutPic）1.0.1 主要修复 Windows MSI 安装兼容性：

- 避免相同版本、不同安装产品代码（ProductCode）的安装包意外并存。
- 同一个原始安装包仍可维护和修复已安装产品。
- 已验证历史正式 1.0.0 升级至本安装包 1.0.1。
- 已安装新版时，旧版安装包会被拒绝，避免覆盖新版。

保留 `CutPic.exe`、`Program Files\CutPic` 安装位置、既有用户数据目录和升级标识。本次不包含未经验证的新功能承诺。

## Compatibility

- 平台：Windows x64；MSI 自包含 .NET 10 运行时，无需另装 Desktop Runtime。
- 本安装包已在独立 Windows Sandbox 验证；这不代表所有 Windows 版本、权限或安全软件环境均已通过。
- 安装需要管理员权限。升级前关闭应用，并备份 `%LocalAppData%\CutPic`、项目文件及其引用的原始图片。

### 从正式 1.0.0 升级

核对下方文件大小与 SHA-256 后运行 1.0.1 安装包；正常升级无需先卸载 1.0.0。已验证升级后仅保留新版产品注册、安装路径不变，测试项目和设置保留。

### 已安装本次同一个 1.0.1 MSI

重复运行同一原包可维护原产品。需要修复程序文件时，可在管理员终端运行下列命令，并将路径替换为校验过的原始安装包：

```powershell
msiexec /fa "C:\Downloads\CutPic-Setup-x64.msi"
```

现有安装器隐藏 Windows 卸载列表中的“修复”按钮，因此不要依赖该按钮。维护和修复仅针对相同 ProductCode 的产品。

### 遇到其他 1.0.1 测试包

相同版本但 ProductCode 不同的安装包会被安全拒绝，不视为正常升级。即使文件名和版本号相同，也可能不是同一个安装包。请对照 SHA-256 并联系维护者，不要手动修改安装身份、覆盖程序目录或删除用户配置来绕过检查。

### 数据保留与降级

安装器不主动删除 `%LocalAppData%\CutPic` 或用户保存的项目、原始图片。已验证的测试数据在升级和卸载后保留，但复杂真实用户数据迁移仍需额外验证；请保留备份。项目引用的原图应留在原位置。

新版仍安装时会拒绝旧版安装包。这不是“一键回滚”方案；卸载后重装旧版的数据兼容性不在已验证范围内。

## Downloads

安装包名称为 `CutPic-Setup-x64.msi`，校验文件为 `SHA256SUMS.txt`。以下身份对应已验证的原始文件，不表示附件已公开提供：

- 版本：1.0.1。
- 文件大小：52,610,127 字节。
- SHA-256：`5aab9b685b8c65c703eb61b80c219c20b977c8104fd8dedcd9be1c36d653886a`。
- ProductCode：`{2CE48ACB-DC32-41E7-B5F5-D482C2AD3B04}`。
- UpgradeCode：`{6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A}`。

可在 PowerShell 中核对下载文件：

```powershell
Get-FileHash -LiteralPath .\CutPic-Setup-x64.msi -Algorithm SHA256
(Get-Item -LiteralPath .\CutPic-Setup-x64.msi).Length
```

此 MSI 来自 [原始 Actions 构建](https://github.com/onlytools-lab/onlycut/actions/runs/46139159100)，Artifact ID 为 `15260753305`，实际构建源提交为 `8715a40208114f0b444245e2010fc4cea47db963`。采用原始字节晋升，不从发布 Tag 重新构建。`v1.0.1` Tag 需指向明确批准的最终 master 提交，**与 MSI 实际构建源提交不同**；Tag 的精确 SHA 以获准创建后的远端引用及审批记录为准，不能将两者描述为同一个提交。

## Verification

- 自动化：合并后的 Windows CI 构建、发布目录验证成功，xUnit 10/10、应用自检 13/13、功能管线 6/6；规范和 Python 回归检查通过。
- 真实 MSI：已验证历史 1.0.0→1.0.1 升级、唯一新版注册、同码维护/修复、异码同版拒绝、降级拒绝、卸载和测试数据保留。
- 真实 GUI：AI 在独立 Sandbox 操作并目视核对普通启动、真实图片导入、九宫格识别、实际导出、三种文件冲突策略、源图保护、项目恢复和中文设置。这不是独立人类测试员签字。
- Phase 5 再核验：重新下载原始 Artifact，直接计算 MSI 哈希、核对大小并重读实际 MSI 数据库，结果与已安装候选一致；没有重新打包。
- NuGet：启用全部依赖审计的联网恢复成功，当前源未报告应用和测试项目的已知漏洞。该结果不代表完整软件安全认证，也不替代捆绑原生运行时或操作系统安全审查。

## Known Issues

- 125%、150%、混合 DPI，以及复杂真实用户数据迁移、断电/失败回滚和不同 Windows/安全软件环境仍未验证。
- 原 MSI **未签名**（未进行 Authenticode 签名），Windows 可能提示未知发布者；不要关闭系统安全保护来安装。如果发布政策要求签名，需要针对签名后新字节重新验收，不能继续使用这里的旧摘要。
- 完整证据、风险与审批门禁见 [发布准备报告](release-readiness-1.0.1.md)。
