# Phase 5 — v1.0.1 正式发布准备总报告

核验日期：2026-11-28（Asia/Shanghai）。结论：**READY FOR INDEPENDENT REVIEW**。

这表示材料可提交独立审查，**不是正式发布许可**。本阶段没有创建 Tag、Draft Release、公开 Release、上传正式附件、改写安装包或修改历史发布。所有未来实际发布动作均未执行。

## 1. GitHub 当前状态与基线

本轮重新执行 fetch、GitHub REST 和 Actions 查询，而不是仅引用任务提供的信息：

- master：`2913936438828ddeecc0b0d2f124cbcf84707659`；[PR #3](https://github.com/onlytools-lab/onlycut/pull/3) 已 Squash Merge，实际 merge_commit_sha 一致。
- [合并后 CI 46144513678](https://github.com/onlytools-lab/onlycut/actions/runs/46144513678)：`validate`、`windows` 均 completed/success，对应同一 master SHA。
- 从日志核对：xUnit 10/10、应用自检 13 组、功能管线 6 组，Release 编译与 publish 验证通过；Python 回归 3/3。未把这些 CLI 结果称为 GUI 验收。
- 初始工作区干净；从最新 origin/master 创建 `codex/onlycut-release-readiness-1.0.1`。本分支只准备文档、草稿、证据和两处未发布状态措辞修正；不改生产工作流、应用代码、版本或安装身份。
- 初始查询无其他打开的 PR；本轮提交的 Draft PR 是发布准备材料，不是发布工作流。
- 重新查询无 `v1.0.1` Tag 或任何状态的同版本 Release；已有公开 Release 只有历史 `CutPic v1.0.0`。
- master 当前没有分支保护，rulesets 查询为空。因此“观察到两个 CI 成功”不等于“服务器已配置必需检查或强制审查”。本轮不更改仓库规则。

具体查询快照、逐文件差异、实际 MSI 信息见 [机器证据](evidence/release-readiness-1.0.1.json)。Phase 5 Head/新 CI 以包含该证据文件的提交和 Draft PR 为准，不能把它误写为原包构建提交。

历史 v1.0.0 本轮只读核对：发布时间 `2026-08-12T14:37:07Z`，附件 ID `511688981`，文件名 `CutPic-Setup-x64.msi`，大小 52,671,338 字节，GitHub digest 为 `e6ae6ebd6050c25257fa27c9229ef358c793dc315c54388dcb29c753a1432862`。没有下载重传、覆盖或删除历史附件；本报告中的新包摘要不用于旧版。

## 2. 精确资产身份及可信来源

| 字段 | 本轮重新核验结果 |
| --- | --- |
| 构建 | [Actions 46139159100](https://github.com/onlytools-lab/onlycut/actions/runs/46139159100)，success |
| Artifact | [15260753305](https://github.com/onlytools-lab/onlycut/actions/runs/46139159100/artifacts/15260753305)，未过期；到期 `2027-02-26T11:17:17Z` |
| 构建源提交 | `8715a40208114f0b444245e2010fc4cea47db963`；原清单 dirty=false；与 Actions run 和 Artifact workflow_run 一致 |
| 构建工具 | 原运行日志实际选择 SDK 10.0.402、WiX 5.0.2；不是由当前本地工具版本反推 |
| 原 ZIP | 52,389,307 字节；SHA-256 `52eb5280ed6e3c1d07335cb26b4b4fef86d491aafe3911d68c4dd0c283fcf6f1`，与 GitHub Artifact API digest 一致 |
| MSI | `CutPic-Setup-x64.msi`；**52,610,127 字节** |
| MSI SHA-256 | `5aab9b685b8c65c703eb61b80c219c20b977c8104fd8dedcd9be1c36d653886a` |
| 实际 ProductVersion | `1.0.1` |
| 实际 ProductCode | `{2CE48ACB-DC32-41E7-B5F5-D482C2AD3B04}` |
| 实际 UpgradeCode | `{6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A}` |
| 实际架构/范围 | SummaryTemplate `x64;2052`，ALLUSERS=`1`，per-machine；ProductName `CutPic` |
| 文件版本 | 实际 File 表的 CutPic.dll 为 `1.0.1.0`；与规范映射一致 |
| 数字签名 | 重新检查为 `NotSigned`，不是有签名的受信发行者包 |

重新下载 ZIP 后先逐个检查四个根目录文件，没有多余文件、嵌套目录或路径穿越，再解压到新的本地测试目录 `artifacts/phase5-20261128/original/`：

| ZIP 内路径 | 解压后字节数 |
| --- | ---: |
| CutPic-Setup-x64.msi | 52,610,127 |
| msi-metadata.json | 146,671 |
| candidate-manifest.json | 432 |
| SHA256SUMS.txt | 88 |

本轮直接对 MSI 计算摘要和大小，执行现有 `verify-msi-candidate.ps1` 重开实际 MSI 数据库，逐项对照 Properties、组件/文件/升级/目录/启动条件、UI/Execute 序列和清单，读前读后 SHA-256 不变，PASS。没有以 ZIP 摘要替代 MSI 摘要，也没有仅信任旁车 JSON。原 MSI 与此前安装和 GUI 验收绑定的哈希完全一致，本轮不需要本地保留副本兜底来证明来源。

可信边界：GitHub 来源、传输和摘要建立了可核对的来源链；不是 Authenticode 信任或完整软件安全证明。未创建新的 GitHub Artifact 来延长旧包期限。原 ZIP、四文件和本地下载记录保存在忽略目录，没有提交 MSI 或原始用户图片到 Git。

## 3. 原构建源提交 → 当前 master 的完整差异

真实执行 `git diff --name-status/--numstat` 及文本 diff，范围为 `8715a402...` 的文件树与精确 master `2913936...`，**不是**只看提交说明，也不是拿 PR 的 merge base 替代源文件树。共 **31 个文件**，其中 21 个截图、9 个文档/观察记录、1 个升级验收脚本。

下表逐文件列出；“否/否”分别表示不参与应用 publish 输入、不参与 MSI 载荷或构建定义。所有条目均不改变数值版本、依赖、ProductCode 生成方式、UpgradeCode、组件 GUID 或 KeyPath。

| 文件 | 应用/MSI 输入 | 实际内容与验证行为 |
| --- | --- | --- |
| docs/evidence/gui-final/01-startup.png | 否/否 | 启动截图 |
| docs/evidence/gui-final/02-import-recognized.png | 否/否 | 导入截图 |
| docs/evidence/gui-final/03-nine-slices.png | 否/否 | 九选区截图 |
| docs/evidence/gui-final/04-export-overview.png | 否/否 | 总览截图 |
| docs/evidence/gui-final/05-export-result.png | 否/否 | 导出结果截图 |
| docs/evidence/gui-final/06-settings.png | 否/否 | 中文设置截图 |
| docs/evidence/gui-final/07-rename-overview.png | 否/否 | 同名预检截图 |
| docs/evidence/gui-final/08-renamed-files.png | 否/否 | 改名列表截图 |
| docs/evidence/gui-final/09-rename-result.png | 否/否 | 改名结果截图 |
| docs/evidence/gui-final/10-skip-preflight.png | 否/否 | 跳过预检截图 |
| docs/evidence/gui-final/11-skip-overview.png | 否/否 | 跳过总览截图 |
| docs/evidence/gui-final/12-skip-result.png | 否/否 | 跳过结果截图 |
| docs/evidence/gui-final/13-overwrite-preflight.png | 否/否 | 覆盖预检截图 |
| docs/evidence/gui-final/14-overwrite-result.png | 否/否 | 覆盖结果截图 |
| docs/evidence/gui-final/15-source-protection.png | 否/否 | 跨任务源图保护截图 |
| docs/evidence/gui-final/16-project-saved.png | 否/否 | 保存项目截图 |
| docs/evidence/gui-final/17-project-restored.png | 否/否 | 恢复项目截图 |
| docs/evidence/gui-final/18-settings-restored.png | 否/否 | 恢复设置截图 |
| docs/evidence/gui-final/19-appearance-language.png | 否/否 | 外观语言截图 |
| docs/evidence/gui-final/20-shortcuts.png | 否/否 | 快捷键截图 |
| docs/evidence/gui-final/21-maximized-100dpi.png | 否/否 | 100% 最大化截图 |
| docs/evidence/gui-final/automated-recheck.json | 否/否 | 当时自动复验计数、NU1900 和权限重试记录 |
| docs/evidence/gui-final/observations.json | 否/否 | 文件落盘、源图/输出/截图摘要和真实 MSI 数据库观察 |
| docs/evidence/gui-final/remote-delivery-status.json | 否/否 | 当时网络阻塞快照；本轮成功联网不改写该历史记录 |
| docs/evidence/msi-policy-1.0.1.json | 否/否 | MSI 策略检查证据 |
| docs/evidence/upgrade-1.0.1-sandbox.json | 否/否 | 真实安装矩阵状态及摘要 |
| docs/final-pr3-acceptance.md | 否/否 | GUI 范围、来源差异、旧网络状态与风险报告 |
| docs/msi-upgrade-1.0.1-test.md | 否/否 | 真实结果替换待测占位，引用后续 GUI 范围 |
| docs/release-process.md | 否/否 | 解释守卫、升级检查顺序、精确资产晋升与可信边界 |
| docs/windows-validation.md | 否/否 | 新增阶段入口，保留旧 1.0.0 失败记录 |
| scripts/compare-msi-identity.ps1 | 否/否 | **不是文档**：增加按安装目录标识/长文件名匹配的共享组件身份检查，防生成 ID 改名掩盖 GUID/KeyPath 改变；加强升级验收门禁 |

额外逐类核对：C#、XAML、资源、csproj、Directory.Build.props、global.json、NuGet 引用/锁定文件、WiX 源、安装包构建脚本、两个 GitHub workflow、候选校验脚本和 publish 包含规则均无差异。项目未使用 NuGet 锁定文件，这不构成本次差异，但重建时传递依赖解析仍是额外变因。

`compare-msi-identity.ps1` 只被 `run-upgrade-sandbox.ps1` 调用，不被候选 workflow、build-installer 或 verify-release 调用；它改变**升级验收检查行为**，不改变安装包。检查 `.csproj` 默认包含规则及 WiX `Files Include=$(PublishDir)\**` 后，上述 docs/脚本没有 CopyToOutputDirectory/CopyToPublishDirectory，不会随 publish 进入 MSI。机器证据对全部 31 项也分别记录这些分类。

结论：当前 master 没有引入原包所缺失的产品功能或安装器行为。**这不等于重建会字节相同**：SDK 的 `InitializeSourceControlInformation` 实测返回当前提交，`IncludeSourceRevisionInInformationalVersion=true`；Git 信息可能进入程序集版本元数据。WiX 自动 ProductCode、PackageCode、时间和 SDK 补丁/传递依赖解析也可能造成差异。因此本报告支持原包适用性，不支持把任意新包冒充原包。

Phase 5 自身仅文档/证据和 README/CHANGELOG 未发布措辞变化，仍不是产品构建输入；若后续 master 再变，必须对实际最终提交重新比对，不能无限沿用本报告。

## 4. 发布流程静态审查

远端与仓库均只有以下两个 active workflow，没有隐藏的第三个 release workflow；只读查询未发现仓库 ruleset。未通过真实发布事件试验。

| 流程 | 事件/权限 | 是否构建或发布 |
| --- | --- | --- |
| ods-validation.yml | pull_request；master push；workflow_dispatch；contents: read | 规范检查、Windows 编译/测试/publish、Actions Artifact；不运行 WiX，不发布 Release |
| candidate.yml | 仅 workflow_dispatch；contents: read | 运行 build-installer、WiX 5.0.2，生成并验证新的 MSI 后上传 Actions Artifact；不创建 Tag/Release |

- 当前 Tag push、Release created/published 都不匹配这两个 workflow 事件。创建 Tag 不会由仓库现有 workflow 自动创建 Release；创建 Release 也不会由其重建 MSI。这是代码/远端工作流静态结论，不是实际 Tag/Release 试运行结果；外部账号自动化不在仓库静态检查的可证明范围。
- build-installer 会清理 `artifacts/candidate`，重新 publish 自包含载荷并运行 WiX。Package 未固定 ProductCode，**重跑有换码/换字节风险**；同版本拒绝机制不能被关闭来迁就重打包。
- 当前只有 upload-artifact，没有 `gh release create/upload/delete`、附件覆盖或删除、Git Tag 写入、自动改版本/CHANGELOG 的工作流步骤；未见影响历史 v1.0.0 的自动动作。
- `ods.py release-notes` 是未被当前 workflow 调用的手动草稿生成命令，会写草稿文件，不发布；不能用其结构检查成功证明已获授权。
- 当前 candidate 的 `if-no-files-found:error` 能避免完全缺失输入，构建脚本还检查固定文件集合与真实 MSI；但仓库**没有精确晋升和正式附件完整性工作流**。不能以 Actions 上传成功推断未来 Release 四文件完整。
- 未来操作者至少需要 Actions read 下载候选、Contents write 创建 Tag/Release/附件，组织或私有仓库可能有额外限制。保持本轮 workflow 只读权限；没有扩大 Token 权限。
- 最小安全建议：不改造生产 workflow，在独立发布授权后由指定操作者从原 Artifact 取四个原文件，重核 MSI，再上传原始字节并下载复核。具体见 [发布操作清单](release-checklist-1.0.1.md)。**本轮只提出方案，没有实现晋升脚本或发布 workflow。**
- `installer/README.md` 的通用“构建、签名”说明不是本次精确晋升步骤；本次不得照此重新打包或给旧 MSI 事后签名。签名会改变 SHA-256，必须另走新资产验证。

## 5. 版本、文档与 ODS 一致性

| 项目 | 当前含义/检查 |
| --- | --- |
| Directory.Build.props | 唯一数值来源 1.0.1；程序集/文件版本映射 1.0.1.0，注释明确非已批准正式版 |
| 实际 MSI | ProductVersion=1.0.1，CutPic.dll FileVersion=1.0.1.0；名称继续 CutPic，不改升级兼容身份 |
| README | 公开已发布版本仍为 1.0.0；改正“本开发分支”措辞为当前源码待审批候选，并链接本报告，没有宣布发布 |
| CHANGELOG | 仍为 Unreleased；澄清“保持 1.0.0”是第一阶段历史语境，当前为未发布 1.0.1；不创建带正式日期的 1.0.1 已发布章节 |
| onlytools.config.json | product.status=development；版本来源指向 props；资产名称保留 CutPic；无另一个独立 releaseVersion 可误配 |
| 版本检查脚本 | 检查数值一致性、MSI 格式范围与安装器守卫，不创建/推送版本对象，不负责授予正式状态 |
| 新 Release Notes | [1.0.1 草稿](RELEASE-NOTES-v1.0.1.md)，DRAFT、尚未发布；新包大小/摘要来自本轮原包核验，不引用 1.0.0 摘要 |
| 历史记录 | `docs/RELEASE-NOTES-v1.0.0.md` 未改；CHANGELOG 从 `[1.0.0]` 开始的历史区段及已有链接不改；GitHub 历史 Release/附件只读 |

本轮 ODS 配置、README、CHANGELOG 结构、版本和两份 Release Notes 的结构分别验证，均通过。当前 CI 仍只显式校验历史 1.0.0 Notes，因此本轮**手动新增执行** 1.0.1 草稿结构检查，不擅自改生产 workflow。另对原 MSI 运行真实正例及协同伪造两个 JSON 的反例，2/2 通过，伪造被实际数据库核对拒绝。结构检查可以接受完整 DRAFT 内容，**不是授权/正式状态门禁**；发布时还需人工授权与精确资产检查。

完整 `validate-changelog --version 1.0.1` 在准备阶段预期拒绝：尚无正式日期、版本章节和发布链接；不为让它变绿而伪造发布。本轮只要求 Unreleased 结构检查成功；正式发布文档更新列于操作清单，必须在获授权的后续操作中完成。

## 6. 安全审计、已有验收与未测事项

Phase 5 在固定 SDK 10.0.402 下执行联网 `dotnet restore ... --force --no-cache -p:NuGetAudit=true -p:NuGetAuditMode=all`，应用和测试项目恢复成功、未观察到 NU1900；分别运行两个项目的直接/传递包漏洞查询，当前 NuGet 源均报告无已知漏洞，没有关闭审计或修改依赖来清除警告。应用依赖版本与实际 MSI File 表对应：MaterialDesignThemes/Colors 5.3.2、Microsoft.Xaml.Behaviors.Wpf 1.1.77。

这关闭了“本轮未能联网完成 NuGet 查询”的问题，不抹掉第四阶段的 NU1900 历史记录；结果仅针对查询时源中已知的 NuGet 漏洞，不覆盖原生运行时、OS、所有供应链攻击或未来漏洞。正式发布当天还应再审计；高危结果必须阻断。

已有真实安装矩阵见 [原安装报告](msi-upgrade-1.0.1-test.md) 与 [13 阶段证据](evidence/upgrade-1.0.1-sandbox.json)；已有真实 GUI 见 [GUI 范围](final-pr3-acceptance.md) 及其截图/观察记录。两者的 MSI SHA-256 与新下载原包一致，故来源链没有换成另一个安装包。**Phase 5 没有重新执行安装、升级或 GUI，也没有把已有结果填成新一次验收。**

## 7. 风险与发布门禁

| 分类 | 风险/门禁及具体理由 | 处置 |
| --- | --- | --- |
| BLOCKER（执行门禁） | 尚无正式发布授权；本轮明确禁止 Tag/Release/附件 | 等用户与独立审查方单独批准，不由本报告替代 |
| BLOCKER（执行门禁） | 发布日精确 Tag 目标、最终文档/日期、无冲突对象、最新 CI/哈希及附件完整性尚未执行 | 按操作清单逐项完成；出现产品输入变化、来源丢失、哈希不同或高危漏洞则停止发布 |
| ACCEPTANCE REQUIRED | Tag/最终 master 与 MSI 构建源不同 | 已逐文件证明产品/安装器输入无差异；审批明确接受并如实公开两个 SHA，不改原 manifest |
| ACCEPTANCE REQUIRED | MSI 未签名 | 可能有未知发布者提示；若必须签名，精确晋升方案失效，需要新 SHA-256 与完整重新验收 |
| ACCEPTANCE REQUIRED | 125%/150%/混合 DPI 未验证 | 100% GUI 不证明高 DPI 按钮可用及文本不截断；如定为硬门禁则先补验 |
| ACCEPTANCE REQUIRED | 复杂真实用户数据迁移未验证 | 现有测试夹具不能代表全部用户数据；发布说明要求完整备份 |
| ACCEPTANCE REQUIRED | 断电、磁盘满、异常升级回滚未验证 | 正常卸载/拒绝降级不证明事务失败恢复或无损降级；不得承诺一键回滚 |
| ACCEPTANCE REQUIRED | 不同 Windows、权限、安全软件环境 | 仅有特定 Sandbox 和开发/CI 环境证据，未覆盖通用兼容矩阵 |
| ACCEPTANCE REQUIRED | 原 Artifact 有期限、还没有独立长期保留策略验收 | 原包可重新下载至 2027-02-26 UTC 截止；本轮本地保留副本不等于冗余离线备份，正式批准前安排至少两份可核验原字节备份 |
| ACCEPTANCE REQUIRED | master 无服务器强制审查/必需检查 | 两项 CI 成功是事实但可被绕过；本轮坚持人工冻结与双重审批，不擅改仓库设置 |
| ACCEPTANCE REQUIRED | 原生运行时/完整软件安全审查范围有限 | NuGet 查询不覆盖捆绑 .NET 原生运行时和全部威胁；发布者需接受范围或要求扩展审查 |
| POST-RELEASE | 缺少产品路径锁定/可重现构建和完整 DPI/UI 测试矩阵 | 原包精确晋升不依赖重建；后续可另立任务改进，不在本轮擅改产品构建输入 |
| POST-RELEASE | 长期自动化晋升/附件完整性流程缺失 | 当前最小人工四文件校验可供审批；如要自动化，另提设计和权限审查，不用当前 candidate 代替 |

NU1900 的本轮联网 NuGet 检查已完成，不列为“仍未执行”；若发布日重新查询失败或审计源不可用，重新归为 ACCEPTANCE REQUIRED；出现高危依赖漏洞则按 BLOCKER 处理。所有风险的接受者是用户/独立审查方，不是本轮执行 AI。

## 8. 最终建议与停止条件

1. **建议直接晋升原始 MSI，但须明确批准。** 原包可获取、GitHub ZIP digest/源提交链完整、实际数据库/大小/哈希一致，现有安装和 GUI 证据绑定同一字节；没有需要靠重建才能获得的新产品变更。重建只会引入未验证的新 ProductCode/字节风险。
2. 清楚区分 A（已验证原 MSI，源 8715a402）、B（当前 master 2913936 及后续待审文档提交）、C（将来经授权上传的 MSI）。只有明确批准 C=A 且哈希一致，才能沿用 A 的安装证据；Tag 指向后续批准的最终 master，不能谎称 A 是由 Tag 构建。
3. 若要求签名、要求从最终 Tag 重建，或实际产品输入/原包字节改变：原包晋升不适用，另做受控新候选并完整重验。不能直接重新运行候选工作流后发布。
4. 当前没有发现未解释的产品构建输入差异、来源断链或冲突版本对象；正常审批/发布日门禁及上表未测风险仍未被自动豁免。
5. 本分支提交 Draft PR 后等待其 CI 和独立审查。没有替用户接受风险，没有合并 PR，没有创建任何正式发布对象。**本阶段结束即暂停。**

配套材料：[面向用户的 Notes 草稿](RELEASE-NOTES-v1.0.1.md)、[未来发布操作清单](release-checklist-1.0.1.md)、[机器可核对证据](evidence/release-readiness-1.0.1.json)。
