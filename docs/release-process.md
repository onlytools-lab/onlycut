# OnlyCut 候选构建与发布边界

版本唯一来源为 `Directory.Build.props`。Version 为三段数字，AssemblyVersion/FileVersion 派生为四段，MSI 使用三段。正式历史版本仍为 1.0.0；本开发分支经授权使用 1.0.1 测试升级，不代表批准发布。SDK 由 global.json 固定 feature band，WiX 固定 5.0.2。

PR 不使用路径过滤：ODS、版本、README、CHANGELOG 及 Windows Release/xUnit/应用自检均须执行。首次运行会恢复依赖，测试工程明确为 tests/CutPic.Tests/CutPic.Tests.csproj。

手动运行 Windows candidate 工作流时选择已审查的工作分支。工作流只读仓库，无 Release 写入权限。默认分支首次合并前，GitHub 可能尚不能调度新增 workflow_dispatch 工作流；本地候选证据不能代替 Actions 运行记录。

installer/build-installer.ps1 会清理明确的 artifacts/candidate 生成目录，运行验证、生成自包含 x64 MSI，再从 MSI 数据库检查 ProductVersion、ProductName、UpgradeCode、ProductCode、架构、组件和 Upgrade 表。输出保留 CutPic-Setup-x64.msi 名称，避免破坏历史约定；版本和提交号写入 candidate-manifest.json，另提供 SHA256SUMS.txt 和 msi-metadata.json。脏工作区构建明确标 dirty，只有干净、已提交来源才可作为最终候选。

## 安装兼容性

- EXE/程序集、Program Files/CutPic、LocalAppData/CutPic、UpgradeCode 保持不变。
- ProductCode 仍由 WiX 自动生成，每次构建可能不同，不固定为历史 ProductCode。
- Files 自动组件及快捷方式 Guid=* 保持既有生成方式；应对照最终 MSI 的组件 GUID、KeyPath 和 Upgrade 表，而非仅比较 UpgradeCode。
- 保留 MajorUpgrade 默认检测及拒绝降级行为，不启用 AllowSameVersionUpgrades。
- 历史 1.0.0 重打包发生过并存；本分支增加同版本 detect-only 守卫并使用 1.0.1 测试跨版本升级。正式发布仍须单独授权，不能替换历史包。
- 只有候选通过审查并获得明确授权后，才可合并、创建正式 Tag、公开 Release。历史 v1.0.0 附件不得覆盖。

## 验收记录

结果见 windows-validation.md。构建、单元测试、服务层功能测试、交互验收和安装器验收分别记录；未执行即标未测试。

## 同版本与未来升级判定

不同 ProductCode、相同 ProductVersion 并不自动触发 MajorUpgrade。历史 Upgrade 表排除相等版本，曾造成两个产品注册同时占用同一路径；本分支另加 detect-only 相等版本守卫。不能仅凭 MSI 返回 0 或 UpgradeCode 相同判定兼容，真实行为以隔离测试的注册状态、文件摘要和 msiexec 日志为准。测试候选不替换公开历史资产。

历史无守卫测试只接受安全拒绝；新守卫由 LaunchCondition 返回 1603，必须同时验证 ONLYCUT_SAME_VERSION_BLOCKED 日志标记、被检测产品代码、注册状态不变及 EXE/DLL 摘要不变，不能将任意 1603 当作通过。返回成功且双产品注册记 COEXISTENCE/FAIL；候选注册而历史消失记 REPLACEMENT/FAIL。安全拒绝不是升级通过。

更高三段版本的升级必须在独立快照中完成：历史 v1.0.0 安装启动成功、备份配置和项目并记录摘要；安装更高版后仅新版 ProductCode 注册、旧码消失、路径不变、实际版本正确；启动后设置值和项目内容保留；在新版仍安装时尝试历史低版并验证拒绝降级、原程序不受损；最后卸载，验证注册和主程序消失但用户数据保留。每一步记录退出码、实际状态和 PASS/FAIL。当前仅在开发分支使用经授权的 1.0.1 测试候选，正式历史版本仍为 1.0.0。实际测试及未测试项见 msi-upgrade-1.0.1-test.md。

## 候选可信边界

SHA-256 证明文件与清单一致，不证明来源受信。msi-metadata.json 仅是便于审查的旁车报告，不是独立可信来源。Windows 验证必须调用 scripts/verify-msi-candidate.ps1：重新打开实际 MSI 数据库，对照版本、ProductCode、UpgradeCode、架构、组件/文件/升级/目录/启动条件表和清单，并在读前读后核对 MSI 摘要。Python validate_candidate.py 只检查文件/JSON 一致性，不能独立证明 MSI 元数据真实。scripts/test-msi-verifier.ps1 使用实际 MSI 验证有效候选并协同伪造两个 JSON，要求数据库核对拒绝伪造。
