# v1.0.1 文档门禁修复与双磁盘备份核验

记录日期：2026-11-28（Asia/Shanghai）。本轮只做文档 PR 与备份，不执行发布。

## 基线与权限

- 重新 fetch 后的 origin/master：`97879dd00ac1a6277175b9bc6e6fc0176f028c51`。
- 独立分支：`codex/onlycut-release-docs-1.0.1`，从该基线创建；不直接修改 master。
- 前次受控执行在最终文档门禁失败时停止，没有创建 Tag、Release 或附件。本轮只修复门禁，原来对旧 SHA 的批准不能自动用于本 PR 合并后的新 SHA。
- 本轮不得创建 Tag、任何 Release、上传附件、运行 WiX、重建/签名 MSI、合并 PR 或修改历史 v1.0.0。

## 日期与链接的规则核对及用户确认

本轮实际读取本仓库 `scripts/ods.py`：完整 CHANGELOG 检查要求 `## [1.0.1] - YYYY-MM-DD`、真实日历日期、至少一个有条目的分类、`[1.0.1]` 精确指向 Release 页面及 `[Unreleased]` 比较链接。

同时只读核对 [ONLYTOOLS CHANGELOG 模板](https://github.com/onlytools-lab/onlytools-standard/blob/09258ccf389e5dfe0054251fbfe1fa81693739fc/templates/CHANGELOG.template.md) 与 [发布授权清单](https://github.com/onlytools-lab/onlytools-standard/blob/09258ccf389e5dfe0054251fbfe1fa81693739fc/docs/RELEASE-CHECKLIST.md)，读取时标准仓库 main 为 `09258ccf389e5dfe0054251fbfe1fa81693739fc`。模板要求日历日期，公开发布另需人工授权；现有校验器不支持“待定”日期，也不能从语法检查得出已经公开。

经本轮用户分别明确确认：

1. 保留规范要求的 `[1.0.1]` Release 链接，另增明确的版本对比链接。没有设置两个相同标签、隐藏匹配文本或修改校验器。
2. 章节使用 `2026-11-28`，并紧邻标明“**变更归档日期，非公开发布日期**”；当前仅准备 Draft Release，实际公开日期另行确认。这是本项目本轮明确批准的日期语义，不声称统一规范普遍允许伪造发布日期。

CHANGELOG 的 1.0.1 章节由已验证修复与既有 Unreleased 项归档，`[Unreleased]` 指向 `v1.0.1...HEAD`，`[1.0.1]` 指向 `releases/tag/v1.0.1`，`[1.0.1-compare]` 指向 `v1.0.0...v1.0.1`。后两个目标尚未创建，文档明确说明链接要等未来获准创建 Tag 后才可用，不把预备链接写成现存 Release。

历史 1.0.0 章节正文与其链接定义保持不变；仅调整全局 Unreleased 链接并增加新版本定义。没有触碰历史 GitHub Release 或附件。

## 文档修复范围

| 文件 | 内容 |
| --- | --- |
| CHANGELOG.md | 增加带明确归档日期语义的 1.0.1 章节，准确列出安装器/源图保护修复及既有治理变化；保留 1.0.0 |
| docs/RELEASE-NOTES-v1.0.1.md | 删除 Phase 5 占位性准备措辞，形成可用于 Draft Release 的正文；清楚保留未公开状态、精确原包身份、未签名、升级/维护/拒绝策略及未测范围 |
| docs/release-checklist-1.0.1.md | 区分 Phase 5 历史禁令、已有受控授权、本轮仅文档/备份、新 SHA 再批准和单独公开授权；保留全部原资产及异常停止门禁 |
| docs/evidence/release-docs-backup-1.0.1.json | 两份备份的实际位置、磁盘映射、五文件逐项大小/摘要、ZIP 条目重读及来源检查结果 |
| 本文件 | 日期/链接决定、备份故障域、校验范围、剩余门禁 |

没有改版本、依赖、应用、安装器、生产 workflow 或校验器。只提交文档及证据 JSON，不提交 ZIP、MSI、用户原图或备份脚本。

## 原始资产与来源

来源为 [Actions run 46139159100 / Artifact 15260753305](https://github.com/onlytools-lab/onlycut/actions/runs/46139159100/artifacts/15260753305)，构建源 `8715a40208114f0b444245e2010fc4cea47db963`。本轮再次查询 Artifact API，未过期，run/source SHA 和 ZIP digest 与已验证原下载一致。使用前次受控执行重新下载的原 ZIP/四文件，先逐字节哈希核对，再复制；没有重新构建或重新安装测试。

| 原始文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| original-15260753305.zip | 52,389,307 | `52eb5280ed6e3c1d07335cb26b4b4fef86d491aafe3911d68c4dd0c283fcf6f1` |
| CutPic-Setup-x64.msi | 52,610,127 | `5aab9b685b8c65c703eb61b80c219c20b977c8104fd8dedcd9be1c36d653886a` |
| candidate-manifest.json | 432 | `5ca5db7bcc382f7a48a261d4beb9da5534485f8d9b4a359a2073ee23290a069c` |
| msi-metadata.json | 146,671 | `7a4f8c73837c34a55604a7ca1589097d5cc61b9bd14e7246011e23bbd5d70304` |
| SHA256SUMS.txt | 88 | `fba17957c3fff62145020c7c717888d8d0e7b1c38b4bbdd67c35df4503ef2fe6` |

原 MSI 数值版本 1.0.1，ProductCode `{2CE48ACB-DC32-41E7-B5F5-D482C2AD3B04}`，UpgradeCode `{6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A}`。这些身份已有实际数据库检查证据，本轮不把复制/哈希检查冒充一次新的安装验收。Release Notes 保留真实构建源，不用未来 Tag/master SHA 替换它。

## 两份仓库外备份的实际结果

| 备份 | 位置 | 实际物理设备 | 结果 |
| --- | --- | --- | --- |
| A | `C:\OnlyCut-Release-Backups\v1.0.1\artifact-15260753305` | Disk 2，Samsung SSD 870 EVO 1TB，SATA | PASS |
| B | `E:\OnlyCut-Release-Backups\v1.0.1\artifact-15260753305` | Disk 1，TOSHIBA HDWD120，SATA | PASS |

仓库 D 盘位于另一个 Disk 0。用 `Get-Partition` / `Get-Disk` 实际核对了映射，不按盘符猜测物理独立性；未记录硬盘序列号。两个备份根路径均在仓库外，创建前不存在，没有覆盖已有备份。

每份备份都完成：

- 重新打开并计算五个目标文件的 SHA-256、读取精确大小，逐项与上表匹配，不能仅靠 Copy-Item 成功判定。
- 重新打开目标 ZIP，检查恰好四个预期根目录条目；逐条读取解压流计算 SHA-256，与旁边四个原始文件逐一匹配。
- 重新读取备份 manifest，核对 version=1.0.1、dirty=false、真实源 SHA 和 MSI 摘要；再次对照本轮读取的 GitHub Artifact 来源和 ZIP digest。
- 两份各五文件均 PASS；[机器证据](evidence/release-docs-backup-1.0.1.json) 记录每份的实际结果，不把“预期摘要”伪装成已读取值。

**故障独立性边界**：两个备份确在不同物理磁盘，可覆盖单块磁盘失效；但均为同一主机的在线本地磁盘，仍共享主机/供电/部分控制器/操作系统暴露，不能抵御主机级勒索软件、盗窃、火灾或其他站点级灾难。不是异地或离线容灾备份，不把“两个独立文件副本”夸大为“所有故障域独立”。未来使用前仍必须重新核验。

## 本轮校验与剩余门禁

- PASS：`python scripts/ods.py validate-changelog --version 1.0.1`，不修改校验器。
- PASS：CHANGELOG structure-only、现有版本、产品配置、README、1.0.1 Notes 和历史 1.0.0 Notes 结构检查。
- PASS：历史 1.0.0 章节与其定义不变、关键原包身份/风险保留、无重复 Markdown 链接定义、新文档本地链接和 git diff 检查。
- 补充文字检查初次未匹配 Notes 中明确的“未签名”字样（当时写作“未进行 Authenticode 签名”）；已把正文改为明确“未签名（未进行 Authenticode 签名）”后重新检查，没有放宽或修改 ODS 校验器。
- PASS：仓库外 C/E 两份原 ZIP + 四文件逐项校验及实际不同物理磁盘核对；共享主机风险如上。
- 本轮 Draft PR 的实际 Head 和 CI 结果以该 PR 检查及交付讨论为准。CI 编译/.NET publish 是已有非发布性验证，不运行 WiX、不产生新的 MSI。
- NOT EXECUTED：Tag、任何 Release、正式附件上传/下载、合并、公开发布；本轮没有重新安装或重新签名测试。

剩余顺序：独立审查本 PR → 授权方合并 → 合并后 CI 成功 → 用户明确批准新的最终 master SHA → 另次受控执行时重查对象冲突、原资产/备份和全部门禁 → 如获授权只创建 Tag/Draft Release 并下载复核 → 停止待审。公开发布继续需要单独授权，不因本轮文档全绿而自动放行。
