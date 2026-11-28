# 1.0.1 安装兼容性测试候选（尚未批准正式发布）

## 根因与最小修复

历史 Package 未固定 ProductCode，WiX 每次构建生成新产品代码；UpgradeCode 标识产品家族，ProductVersion 的前三段参与比较。默认 MajorUpgrade 检测低版本用于替换、检测高版本用于拒绝降级，但排除相同版本，因此不同 ProductCode 的 1.0.0 重打包曾实际发生双注册并存。

本分支保留原 MajorUpgrade，不启用 AllowSameVersionUpgrades。新增相等上下界、OnlyDetect=yes 的 UpgradeVersion，并以 Installed OR NOT ONLYCUT_SAME_VERSION_FOUND 阻止同版本新装。Installed 放行同产品维护/修复/卸载。实际 MSI 必须验证 detector 属性 Secure、Upgrade 表属性位 770，以及 UI/Execute 两条序列都在 LaunchConditions 前执行 FindRelatedProducts。

EXE/程序集、安装目录、用户数据目录、UpgradeCode、稳定组件 GUID、KeyPath 均不要求改变；若比较脚本发现变化立即失败并停止安装验收。历史运行时带版本名称的不同 DLL 为依赖文件变化，不视为公共组件身份重写。

依据：[Microsoft FindRelatedProducts](https://learn.microsoft.com/en-us/windows/win32/msi/findrelatedproducts-action)、[WiX UpgradeVersion](https://docs.firegiant.com/wix/schema/wxs/upgradeversion/)。维护/卸载不执行 FindRelatedProducts；新增 Launch 条件也显式放行 Installed。

## 测试方法

1. 构建 1.0.1 主候选，直接读取 MSI 并核对清单；历史 MSI 必须匹配公开 SHA-256。
2. scripts/build-msi-test-twin.ps1 用同源编译输出和同一 WiX 源再次构建真实 1.0.1 MSI（不同 ProductCode），而非篡改 MSI 表。
3. scripts/compare-msi-identity.ps1 核对公共组件 ID/GUID/Directory/KeyPath、安装目录与 UpgradeCode。
4. scripts/run-upgrade-sandbox.ps1 在全新沙箱执行 13 个阶段，拒绝覆盖已有沙箱。每阶段记录退出码、所有关联产品注册、文件版本、EXE/DLL 摘要和用户数据摘要；任何失败或缺失阶段使整体失败。
5. 修复用例先删除沙箱中的已安装 CutPic.dll，再 /fa 修复并验证原摘要恢复。主要功能冒烟使用安装后的 EXE 执行 13 组 self-test 与 6 组 acceptance。

测试前备份 settings.json、项目和标记文件。应用启动会正常保存设置，因此设置比较保留的值，项目/标记比较完整 SHA-256。验证范围不能代表所有真实用户数据迁移。

## 验收结果

本轮在全新 Windows Sandbox 中实际执行，13/13 阶段 PASS。原始结果：[upgrade-1.0.1-sandbox.json](evidence/upgrade-1.0.1-sandbox.json)。完整日志、数据备份及身份对照保存在本地 artifacts/upgrade-20261128-192115/results/；代码仓库保留脱敏判定和下列关键证据。

测试使用 GitHub Actions 产物，而非重建后冒充同一安装包：

- 源提交：8715a40208114f0b444245e2010fc4cea47db963，dirty=false。
- 候选构建：[46139159100](https://github.com/onlytools-lab/onlycut/actions/runs/46139159100)，成功。
- 分支 CI：[46139162619](https://github.com/onlytools-lab/onlycut/actions/runs/46139162619)，ODS 与 Windows 均成功。
- 下载后实际 MSI 重读、版本、架构、ProductCode、UpgradeCode、文件大小和 SHA-256 均一致。
- 1.0.1 候选：52,610,127 字节；SHA-256 5aab9b685b8c65c703eb61b80c219c20b977c8104fd8dedcd9be1c36d653886a；ProductCode {2CE48ACB-DC32-41E7-B5F5-D482C2AD3B04}。
- 同源 WiX 双胞包：SHA-256 59168945e2aad789b162bdd56f3d6843e8dcf5c836a4a6eda994ced309525f4a；ProductCode {C7E1FC9F-E087-4FE8-A03B-F7640C7A322A}，同版本且不同产品代码。
- 与历史 MSI 对照：两个包各 405 个组件，404 个公共组件完全一致，0 个公共组件身份改变；余下为运行时版本命名 DLL。本地主候选与本地双胞包的 405 个组件全部一致；Actions 候选与本地双胞包的 404 个公共组件一致，余下分别是 mscordaccore_amd64_amd64_10.0.1526.53008.dll 与 10.0.1326.50504.dll，来自 CI/本地运行时补丁差异，而不是公共组件身份更改。检测相等版本时仍正确拒绝该不同 ProductCode 的真实双胞包。

| 阶段 | 退出码 | 实际判定依据 |
| --- | --- | --- |
| 历史包安装/启动 | 各 0 | 历史产品注册，DLL 为 1.0.0.0；PASS |
| 1.0.0 → 1.0.1 升级 | 0 | 历史产品注销，仅新产品注册，DLL 为 1.0.1.0；PASS |
| 升级后启动 | 0 | 已安装 EXE 启动，唯一新注册不变；PASS |
| 升级后数据 | 不适用 | 项目/标记完整摘要与设置值保留；PASS |
| 已安装程序功能冒烟 | 两次 0 | 13 组 self-test 和 6 组 acceptance；PASS |
| 同 ProductCode 维护 | 0 | /i 相同 MSI，注册和 DLL 摘要不变；PASS |
| 实际修复 | 0 | 先删除 DLL，/fa 后原摘要恢复，版本不变；PASS |
| 同版本不同 ProductCode | 1603 | ONLYCUT_SAME_VERSION_BLOCKED 原因与产品代码入日志，双胞包未注册，EXE/DLL 摘要不变；PASS（预期拒绝） |
| 历史旧包覆盖新包 | 1603 | 日志实际添加 WIX_DOWNGRADE_DETECTED，新产品唯一注册和文件摘要不变；PASS（预期拒绝） |
| 拒绝后启动 | 0 | 新程序仍可启动；PASS |
| 卸载 | 0 | 关联产品为空，所有测试 ProductCode 注销，EXE/DLL 消失；PASS |
| 卸载后数据 | 不适用 | 项目/标记 SHA-256 和两项设置值保留；PASS |

自动化：Release 0 警告/错误，现有 xUnit 10/10、自检 13/13、功能管线 6/6；Python 策略/候选测试 3/3。安装器策略测试覆盖相等范围、detect-only、维护条件、安全属性以及 UI/Execute 检测顺序的正反例。仅靠退出码 1603 不能通过拒绝测试。

额外文档/证据提交不改变上述已测试包；所有安装证据严格绑定这里列出的 SHA-256。未声称之后其他 SHA-256 的重打包资产已经安装通过。

以上为原安装矩阵当时的验收范围。本轮真实 GUI、100% 布局及精确 MSI 晋升策略的补充结果见 [最终 PR #3 验收](final-pr3-acceptance.md)；125%/150%/混合 DPI、复杂真实用户数据迁移、断电/失败回滚仍未测试。此分支和测试版本不授权合并、Tag、Release 或历史资产替换。
