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

等待本轮实际执行后填写；未执行前均为未测试。

人工 GUI、高 DPI、混合 DPI、复杂真实用户数据迁移、断电/失败回滚为未测试。此分支和测试版本不授权合并、Tag、Release 或历史资产替换。
