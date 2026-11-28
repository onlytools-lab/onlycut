# PR #3 合并前最终验收（2026-11-28）

状态：本地验收证据已补充；等待独立审查、远端推送及更新后 CI 确认。不是正式发布批准。

## 代码、候选与未来资产不是同一个概念

| 对象 | 精确身份 | 状态 |
| --- | --- | --- |
| A：实际安装的 Actions MSI | 源提交 `8715a40208114f0b444245e2010fc4cea47db963`，dirty=false；[构建 46139159100](https://github.com/onlytools-lab/onlycut/actions/runs/46139159100)，Artifact ID `15260753305` | 之前完整安装矩阵及本轮真实 GUI 均针对这一包 |
| B：本轮起始 PR Head | `1eca178d4235fd49e330ab0a3ac28c7526718708`；分支 `codex/onlycut-msi-upgrade-validation` | PR #3 起始状态为 OPEN、Draft、未合并；本轮仅新增证据和文档 |
| master | `bf3bb260d88c09839b3fc235a7a287538f03df2d` | 未修改；最终远端状态仍需成功联网复核 |
| C：未来正式提供的 MSI | **尚不存在已授权的正式发布资产** | 建议经审批后逐字节复用 A，不重新运行 WiX；不是已经执行的发布 |

A 为 `CutPic-Setup-x64.msi`，52,610,127 字节，SHA-256：

```text
5aab9b685b8c65c703eb61b80c219c20b977c8104fd8dedcd9be1c36d653886a
```

实际 MSI 数据库：ProductVersion `1.0.1`，ProductCode `{2CE48ACB-DC32-41E7-B5F5-D482C2AD3B04}`，UpgradeCode `{6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A}`，x64、ALLUSERS=1。本轮再次运行 `scripts/verify-msi-candidate.ps1`，重新打开实际 MSI，校验属性、组件、文件、升级、目录、条件与序列表，以及清单、大小、读前读后 SHA-256，PASS。旁车 JSON 只是报告，不是独立可信来源。实际数据库摘要另见 [observations.json](evidence/gui-final/observations.json)。

## 完整差异复核

实际检查 `git diff 8715a402..1eca178`，不依据提交说明猜测。全部六个文件：

| 文件 | 实际改动和影响 |
| --- | --- |
| docs/evidence/msi-policy-1.0.1.json | 新增 MSI 策略检查证据；不参与安装包载荷 |
| docs/evidence/upgrade-1.0.1-sandbox.json | 新增沙箱各阶段判定证据；不参与载荷 |
| docs/msi-upgrade-1.0.1-test.md | 把待测占位改为真实安装结果、精确资产身份及未测范围 |
| docs/release-process.md | 更新同版本守卫说明和升级测试执行顺序、引用结果；不是构建脚本 |
| docs/windows-validation.md | 新增第四阶段报告入口；保留旧的 1.0.0 失败记录 |
| scripts/compare-msi-identity.ps1 | **不是文档**：新增按安装目录标识/长文件名交叉比对公共组件身份，防止生成 ID 改名掩盖 GUID/KeyPath 变化。只被升级测试入口调用，不被 candidate.yml、build-installer.ps1 或 verify-release.ps1 调用 |

同时核对了 `.csproj` 默认项、安装构建和候选工作流：上述文档/测试脚本不被复制进 publish；没有应用源代码、XAML、资源、EXE、版本、依赖、SDK/WiX 选择、安装器策略、构建脚本或候选生成/发布门禁变化。本轮提交也仅包含本报告、报告入口、发布策略说明与截图/观察记录，不修改生产构建输入。

结论：A 不是从最终 B 构建，但两者的产品/安装器构建输入相同。没有理由仅因补充证据而重新打包；该结论**不等于**任意重打包 MSI 的字节相同或已安装通过。最终独立审查还应复核本轮证据提交到实际最终 Head 的完整差异。

## 真实 GUI 验收

方法：在独立新 Windows Sandbox 中安装 A（退出码 0），通过真实窗口点击、输入、文件选择器和设置界面操作，逐步目视截图；正常启动安装路径中的 EXE，**没有使用 --smoke-main、自检或功能管线替代 GUI**。这是 AI 操作并视觉核对的 GUI 验收，不冒充另一个人类测试员的签字。此前自己的已完成沙箱会话仅在核对归属后结束；没有关闭或修改 OnlyShell 测试会话。

隔离：宿主 MSI 未安装；源素材复制到沙箱专用可写映射目录；`CUTPIC_DATA_DIR` 指向专用测试数据目录，不触碰宿主用户配置。映射和设置见本地 `artifacts/gui-final/`。网络关闭，测试桌面 96 DPI（100%）。

| 场景 | 结果 | 可核对证据 |
| --- | --- | --- |
| 普通启动主窗口 | PASS | [01](evidence/gui-final/01-startup.png)；有正常主界面，无白屏/崩溃；两次正常退出均为 0 |
| 真实图片导入、任务缩略图、预览 | PASS | [02](evidence/gui-final/02-import-recognized.png)、[03](evidence/gui-final/03-nine-slices.png)；女装 1200×1200 图，1 个任务、9 个选区、初始警告 0 |
| 实际切图与导出 | PASS | [04](evidence/gui-final/04-export-overview.png)、[05](evidence/gui-final/05-export-result.png)；成功 9/跳过 0/失败 0；实际 9 张 512×512 PNG，目视抽查导出的首/末张主体完整，与总览对应 |
| 同名自动改名 | PASS | [08](evidence/gui-final/08-renamed-files.png)、[09](evidence/gui-final/09-rename-result.png)；新增 9 张 `(1)` 文件，合计 18；原件保留 |
| 跳过已有文件 | PASS | [10](evidence/gui-final/10-skip-preflight.png)～[12](evidence/gui-final/12-skip-result.png)；成功 0/跳过 9/失败 0；总数仍 18，原始及改名文件修改时间保持不变 |
| 覆盖已有导出图 | PASS | [13](evidence/gui-final/13-overwrite-preflight.png)、[14](evidence/gui-final/14-overwrite-result.png)；预检明确提示覆盖；成功 9/跳过 0/失败 0；原始 9 张修改时间从 11:49:24～25 UTC 变为 11:53:17 UTC，改名副本仍为 11:50:36 UTC；总数仍 18 |
| 跨任务源图保护 | PASS | [15](evidence/gui-final/15-source-protection.png)；导入第二个任务 `clothing_01.png`，让任务一首张输出与其碰撞；覆盖策略下向源图目录导出被预检阻止；没有输出写入，两个源图 SHA-256 均保持原值 |
| 保存、退出、重启、打开项目 | PASS | [16](evidence/gui-final/16-project-saved.png)、[17](evidence/gui-final/17-project-restored.png)；关闭退出码 0；重启为空任务状态（自动恢复关闭），经打开项目对话框恢复 2 个任务，各 9 个选区、缩略图/预览及任务状态 |
| 中文设置与主要交互、持久化 | PASS | [06](evidence/gui-final/06-settings.png)、[18](evidence/gui-final/18-settings-restored.png)～[20](evidence/gui-final/20-shortcuts.png)；实际切换并保存三种冲突策略，重启仍是覆盖；外观/语言与快捷键页面能打开，文本可读、保存/取消可点击 |
| 100% 布局 | PASS（已走查范围） | 主窗口、设置、总览的本轮所操作控件无明显文本截断，含最大化 [21](evidence/gui-final/21-maximized-100dpi.png)。注意重启窗口被 Windows 级联放置后底部落在任务栏后，最大化后完整；没有声称覆盖所有窗口尺寸/控件 |
| 125%、150%、混合 DPI | NOT TESTED | 当前专用沙箱只执行了固定 96 DPI；没有修改宿主缩放或注销沙箱以改变 DPI，不从 100% 推断通过 |
| 人类测试员独立 GUI 签字 | NOT TESTED | 如审查要求真正人类验收，使用下方最小清单；本轮不是人类签字 |

目录 [gui-final](evidence/gui-final/) 有 21 张原始截图，观察记录包括截图 SHA-256、18 张输出的大小/像素/摘要/时间、两份源图摘要、项目摘要、设置和退出码。未把截图中的状态当作文件落盘证明，另从真实输出目录读取并检查了全部 PNG 尺寸。源图摘要均为 `cf7c609a9f08ce4947979a2418540a94980ac379a039ffe9ae6b59495def58ab`。原图/导出文件和项目保存在本地 `artifacts/gui-final/results/`，未上传用户原始素材；截图中出现的是本次用户提供的测试素材。

范围说明：本轮 GUI 使用有边界女装卡片；无边界/透明导出、长图及更多编码格式没有重新做 GUI 走查；既有服务层自动测试不能填成这些场景的 GUI PASS。

## 安装验收与自动化证据分开

- A 的完整真实 MSI 安装矩阵仍引用 [原始安装报告](msi-upgrade-1.0.1-test.md) 和 [13 阶段结果](evidence/upgrade-1.0.1-sandbox.json)：历史 1.0.0→A 升级、唯一新版注册、同码维护/修复、异码同版拒绝、降级拒绝、启动、卸载及数据保留。只适用于上面的精确 SHA-256。
- 本轮新沙箱补做 A 独立安装与普通 GUI，不能把这个新沙箱的导入/导出称为“升级过程中的 GUI”；新沙箱没有再次运行升级/修复/卸载矩阵。其余矩阵是同一字节资产的此前真实证据，不是本轮重跑。
- 起始 Head 的 [CI 46140515062](https://github.com/onlytools-lab/onlycut/actions/runs/46140515062) 已成功：ODS 与 Windows 两个作业。证据更新后的新 CI 必须另行确认，不沿用旧结果冒充新 Head。
- 本轮本地重新运行 `scripts/verify-release.ps1`：Release 构建与 publish 成功，xUnit **10/10**、应用自检 **13/13**、功能管线 **6/6**。构建有 **2 条 NU1900 网络审计警告、0 错误**；NuGet 漏洞数据服务无法访问，不能声称漏洞审计通过或本轮零警告。CLI 测试不计入上面的 GUI PASS。
- Python 策略/候选回归 **3/3** 通过。初次受限进程运行有一个用例因临时目录写入权限 ERROR；更换工作区临时目录仍受限。经授权在非受限测试进程重跑原样三个用例全部通过，没有修改测试、绕过断言或将 ERROR 隐藏。ODS 配置/版本/README/CHANGELOG/历史 Release Notes 检查与 `git diff --check` 通过。摘要见 [automated-recheck.json](evidence/gui-final/automated-recheck.json)。

## 最终 MSI 策略和门禁

建议 A → C 的**精确资产晋升**，而非重建：审批明确接受“产品源提交 8715a402 + 后续证据/验证脚本提交”的来源关系；发布说明须同时记录源提交、最终审查/合并提交、Actions run、Artifact ID、文件名、版本、大小、哈希和实际安装结果。不得改写清单中的源提交为后续 Head/Tag，不宣称 A 构建自未来 Tag。

发布前必须重新下载/取出保留的原始 Artifact，核对来源与清单、直接重读 MSI 数据库，并要求 MSI SHA-256 **仍精确等于本报告的值**。清单和四个原始 Artifact 文件一起保留；Actions Artifact 有保留期限，本地已留有副本，但未成功联网时不能声称重新确认了当前到期时间。未获正式授权前不得创建 Tag、Release 或上传正式附件。

若未来要求“MSI 必须从合并后 Head/Tag 构建”，或任何构建输入/依赖/安装策略改变，或原包丢失/哈希不同：必须受控 Actions 重建测试 Artifact，记录精确 SDK/WiX/版本/源提交/产品身份并直接重读 MSI，然后对**新 SHA-256**完整重做独立安装、历史 1.0.0 升级、异码同版拒绝、降级拒绝、维护修复、卸载、数据保留和必要 GUI。不允许套用 A 的测试结论。即使同为 1.0.1，也可能产生不同 ProductCode，已装 A 的测试者不能直接覆盖另一个异码 1.0.1。

本轮没有重建 MSI，没有改动 EXE、安装路径、用户数据目录、UpgradeCode、组件 GUID/KeyPath，也没有修改版本或历史发布记录。

## 剩余风险与最小人工补验模板

未测试：125%/150%/混合 DPI、真实用户复杂配置/项目迁移、断电/磁盘满/人为中断升级回滚、不同 Windows/权限/防病毒环境、所有素材模式的独立 GUI 回归。还需独立审批是否接受精确 A 晋升方案。高 DPI 若作为合并前硬门禁，则未满足。

如需人类补验，在独立 Windows 环境使用相同 SHA-256 的 MSI，不碰 OnlyShell 会话，逐项填写：

| 操作 | 预期/截图要求 | 实测 PASS/FAIL/NOT TESTED、环境、测试人、日期、证据 |
| --- | --- | --- |
| 校验 MSI 后安装并普通启动 | 版本 1.0.1，正常主窗口；记录安装日志/身份 | 等待人工验证 |
| 导入真实图并导出 | 任务/缩略图/预览正确；输出主体完整；截图总览与结果，核查实际文件 | 等待人工验证 |
| 同目录重复导出三种策略 | 改名副本、跳过不改文件、覆盖有明确提示；源图哈希不变 | 等待人工验证 |
| 保存项目、关闭重启再打开 | 任务/选区/设置一致；截图恢复后窗口 | 等待人工验证 |
| 中文设置；100/125/150% | 分别截图主窗口、总览、设置；记录实际 DPI，检查按钮和截断 | 等待人工验证 |

## 远端交付状态

本轮后半段 GitHub API 返回 EOF、Git fetch 返回 schannel TLS 握手失败；连接器与网页备用查询也失败。本地证据完成不表示已推送。没有把失败查询标记为成功，也没有擅自把 PR 转为 Ready for Review。只有证据提交成功推送且对应新 CI 全绿、独立审查接受上述资产策略及未测风险后，才建议申请合并；目前继续保持 Draft，等待远端交付与审查。
