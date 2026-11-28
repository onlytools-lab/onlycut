# OnlyCut 候选构建与发布边界

版本唯一来源为 `Directory.Build.props`。Version 为三段数字，AssemblyVersion/FileVersion 派生为四段，MSI 使用三段。当前均保持 1.0.0，不因工程治理升级产品版本。SDK 由 global.json 固定 feature band，WiX 固定 5.0.2。

PR 不使用路径过滤：ODS、版本、README、CHANGELOG 及 Windows Release/xUnit/应用自检均须执行。首次运行会恢复依赖，测试工程明确为 tests/CutPic.Tests/CutPic.Tests.csproj。

手动运行 Windows candidate 工作流时选择已审查的工作分支。工作流只读仓库，无 Release 写入权限。默认分支首次合并前，GitHub 可能尚不能调度新增 workflow_dispatch 工作流；本地候选证据不能代替 Actions 运行记录。

installer/build-installer.ps1 会清理明确的 artifacts/candidate 生成目录，运行验证、生成自包含 x64 MSI，再从 MSI 数据库检查 ProductVersion、ProductName、UpgradeCode、ProductCode、架构、组件和 Upgrade 表。输出保留 CutPic-Setup-x64.msi 名称，避免破坏历史约定；版本和提交号写入 candidate-manifest.json，另提供 SHA256SUMS.txt 和 msi-metadata.json。脏工作区构建明确标 dirty，只有干净、已提交来源才可作为最终候选。

## 安装兼容性

- EXE/程序集、Program Files/CutPic、LocalAppData/CutPic、UpgradeCode 保持不变。
- ProductCode 仍由 WiX 自动生成，每次构建可能不同，不固定为历史 ProductCode。
- Files 自动组件及快捷方式 Guid=* 保持既有生成方式；应对照最终 MSI 的组件 GUID、KeyPath 和 Upgrade 表，而非仅比较 UpgradeCode。
- 保留 MajorUpgrade 默认检测及拒绝降级行为，不启用 AllowSameVersionUpgrades。
- 当前候选与历史同为 1.0.0，同版本重打包不能证明未来跨版本升级成功，也不应直接替换正式包。未来正式升级必须选择经授权的更高三段 ProductVersion，在隔离系统实际验证。
- 只有候选通过审查并获得明确授权后，才可合并、创建正式 Tag、公开 Release。历史 v1.0.0 附件不得覆盖。

## 验收记录

结果见 windows-validation.md。构建、单元测试、服务层功能测试、交互验收和安装器验收分别记录；未执行即标未测试。

## 同版本与未来升级判定

不同 ProductCode、相同 ProductVersion 并不自动触发 MajorUpgrade。现有 Upgrade 表排除相等版本，存在两个产品注册同时占用同一路径、共享组件或文件意外替换的风险；不能仅凭 MSI 返回 0 或 UpgradeCode 相同判定兼容。当前候选不得覆盖公开的 v1.0.0，真实行为以隔离测试的注册状态、文件摘要和 msiexec 日志为准。

同版本测试只在返回 1638（拒绝）、历史产品仍注册、候选未注册且程序摘要未变化时判为安全拒绝 PASS。返回成功且双产品注册记 COEXISTENCE/FAIL；候选注册而历史消失记 REPLACEMENT/FAIL；其他错误、缺失文件或异常同样 FAIL。安全拒绝不是升级通过。

未来更高三段版本的升级必须在独立快照中完成：历史 v1.0.0 安装启动成功、备份全部配置和项目并记录摘要；安装更高版成功后仅新版 ProductCode 注册、旧码消失、程序路径不变、程序实际版本等于目标版本；启动后用户设置值和项目内容保留；卸载后产品注册和主程序消失但用户数据保留；重新安装历史低版应被拒绝且新版不受损。每一步记录退出码、实际状态和 PASS/FAIL，任何异常不得整体通过。没有实际执行的跨版本测试标记未测试；当前工程保持 1.0.0，不为测试伪造正式版本。

## 候选可信边界

SHA-256 证明文件与清单一致，不证明来源受信。msi-metadata.json 仅是便于审查的旁车报告，不是独立可信来源。Windows 验证必须调用 scripts/verify-msi-candidate.ps1：重新打开实际 MSI 数据库，对照版本、ProductCode、UpgradeCode、架构、组件/文件/升级/目录/启动条件表和清单，并在读前读后核对 MSI 摘要。Python validate_candidate.py 只检查文件/JSON 一致性，不能独立证明 MSI 元数据真实。scripts/test-msi-verifier.ps1 使用实际 MSI 验证有效候选并协同伪造两个 JSON，要求数据库核对拒绝伪造。
