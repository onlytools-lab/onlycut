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
