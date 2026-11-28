# Windows 验收记录

第四阶段 1.0.1 开发候选的跨版本升级、维护修复及拒绝降级结果见 [独立报告](msi-upgrade-1.0.1-test.md)。下文保留之前 1.0.0 治理候选的历史证据，不用新结果覆盖旧失败记录。

本文件区分自动测试、真实 MSI 测试、人工 GUI 和跨版本升级。历史 v1.0.0 公开包是安装兼容参考，不替代候选验收。

## 隔离要求

应用自动测试使用 CUTPIC_DATA_DIR 临时目录，不访问真实用户会话。MSI 为 perMachine，会修改 Windows Installer 注册、Program Files 和快捷方式；必须在 Windows Sandbox/虚拟机快照内验证。仅改变 INSTALLFOLDER 或数据目录不是安装器隔离。

执行安装测试前，备份测试用户 LocalAppData/CutPic，并记录全部文件的 SHA-256。依次测试历史包安装、启动、同版本候选处理、未来更高版本升级、启动和卸载；每步比较配置和项目文件，保留 msiexec 日志。当前版本不提升，因此不能把同版本重装当作跨版本升级。

## 本轮状态

- 本轮环境：Windows x64，.NET SDK 10.0.402，运行时 10.0.13，WiX 5.0.2。
- Release：通过，0 警告、0 错误。现有 xUnit：10/10；现有应用自检：13/13。
- 自动功能管线验收现为 6/6。新增组实际加载 task-a/task-b 两任务，通过生产导出函数执行指向 task-b 源图的改名、跳过及覆盖尝试：改名输出新文件；跳过不写入；覆盖抛出 IOException；两个源图 SHA-256 不变。普通输出文件另验证改名、跳过不改原文件，覆盖确实写出 64px 新图。
- 真实历史样图回归：女装识别 9 个并导出 9 张，家电识别 6 个并导出 6 张。测试使用原样图，仅输出到 test-results。
- 候选校验器：1 个 unittest 方法，包含成功例及版本错误、提交错误、脏来源、额外文件、篡改文件、缺失文件等负例，通过。
- 服务层自动验收不等于完整鼠标交互或批量导出取消/失败重试验收；这些交互为未测试。
- 自动恢复逻辑和原子保存有自检；完整进程崩溃后恢复用户会话仍为未测试。
- MSI 安装、启动、卸载和有限数据保留已在专用沙箱实测，见下表；同版本并存检查 FAIL，整体安装验收 FAIL。跨版本升级仍未测试。
- 高 DPI、混合 DPI、Windows 各版本：未测试。

## 安装器数据库对照

历史 v1.0.0 本地副本 SHA-256 与 GitHub 相同：e6ae6ebd6050c25257fa27c9229ef358c793dc315c54388dcb29c753a1432862。

历史 ProductCode 为 {2B276364-52BF-4615-8729-45B70CA63F2B}；候选 ProductCode 继续由 WiX 自动生成，并非复用历史产品代码。历史与初始候选均为 405 个组件，其中 404 个公共组件 ID、GUID、Directory 和 KeyPath 完全一致。余下一个是 .NET 运行时带版本文件名的 mscordaccore DLL，从 10.0.225.61305 变成 10.0.1326.50504，属于依赖更新；并非安装目录/主程序组件重命名。

Upgrade 表保持原范围：小于 1.0.0 的版本进入 WIX_UPGRADE_DETECTED；大于 1.0.0 的版本进入 WIX_DOWNGRADE_DETECTED，LaunchCondition 保留 NOT WIX_DOWNGRADE_DETECTED。同版本不在正常 MajorUpgrade 范围。以上是数据库证据，不能证明实际升级成功。

## 隔离安装尝试

第一轮因 OnlyShell 占用沙箱而未执行。收尾时再次检查未发现活动沙箱客户端，随后创建专用 OnlyCut 沙箱，未终止或复用 OnlyShell 会话，未在宿主安装 MSI。

实际结果保存在 [原始阶段判定](evidence/sandbox-2026-11-28.json)。本地完整 msiexec 日志和测试数据备份位于 artifacts/sandbox-20261128-130356-results/。安装状态 5 表示已安装，-1 表示未注册。

| 阶段 | 退出码 | 实际状态及判定 |
| --- | --- | --- |
| 历史包安装 | 0 | 旧码=5、新码=-1、EXE存在；PASS |
| 历史包启动 | 0 | 安装后的 EXE 执行 --smoke-main；PASS（非人工 GUI） |
| 同版本候选安装 | 0 | 旧码=5、新码=5，COEXISTENCE；FAIL |
| 历史包卸载 | 0 | 旧码=-1、新码=5，共享 EXE 仍存在；PASS（仅旧产品注销） |
| 清理并存候选后独立新装 | 0 | 旧码=-1、新码=5、候选 EXE存在；PASS |
| 候选启动 | 0 | 安装后的 EXE 执行 --smoke-main；PASS（非人工 GUI） |
| 候选卸载 | 0 | 双码=-1、EXE消失；PASS |
| 数据保留 | 不适用 | 项目/标记文件摘要及 NamingSuffix 设置值保留；PASS（有限夹具范围） |

**整体结果 FAIL**：同版本并存风险不会被其余成功项抵消，外层运行脚本返回非零。不能认为 1.0.0 候选可以安全覆盖安装，也不能把此项归为升级通过。没有变更历史包、UpgradeCode 或启用同版本升级。

本次实测候选 SHA-256 为 dddb593bb9dedd6ab0107ce124fbdd15f68772dd3f45bfb4a0df951e036c6b05，大小 52,597,613 字节，ProductCode {96EAE0AA-8259-4174-B380-D6595B813A62}。它来自本次提交前的开发工作区（基线 23cc92d、dirty=true），不是最终可发布来源；后续干净提交重新生成的候选需另行核对摘要，不能自动继承本次安装证据。

人工 GUI 验收、实际业务用户完整数据迁移、崩溃恢复和更高版本升级均为「未测试」。未来高版本验收规则见 release-process.md；安装更高版后须先验证拒绝降级，再验证卸载与数据保留。

## 直接读取 MSI 与旁车伪造回归

verify-msi-candidate.ps1 在 Python 一致性检查外，重新从实际 MSI 提取版本、产品代码、升级代码、架构和表数据。有效实际包通过；同时伪造 candidate-manifest.json 和 msi-metadata.json 的 ProductCode 后，Python 一致性仍能通过，但 Windows 数据库对照以 MSI/JSON property mismatch: ProductCode 拒绝。实际执行的 2 个用例均满足预期。JSON 不是独立可信来源，SHA-256 也不是签名或来源认证。
