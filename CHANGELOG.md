# Changelog

## [Unreleased]

尚无后续版本的已归档条目。

## [1.0.1] - 2026-11-28

> 日期说明：2026-11-28 是经用户确认的变更归档日期，**不是公开发布日期**。当前仅准备受控 Draft Release，尚未公开；实际公开日期须在获得单独授权后确认。本章节不表示 Tag、Release 或附件已经创建。

版本差异：[v1.0.0...v1.0.1][1.0.1-compare]。版本链接和比较链接的目标在后续获准创建 Tag 后才可用。

### Changed

- 接入 OnlyCut 工程治理、统一版本来源、Windows PR 验证及手动候选包门禁；源码与已验证 MSI 的数值版本为 1.0.1，尚未公开发布。
- 保留 CutPic 程序集、安装目录、数据目录和 MSI UpgradeCode。
- 已验证历史 1.0.0 升级至精确 1.0.1 原始 MSI；正常维护、文件修复、卸载和测试数据保留分别有 Windows Sandbox 证据。

### Fixed

- MSI 新装时拒绝不同 ProductCode 的相同版本；保留原产品维护、修复与卸载。
- 新版仍安装时拒绝旧版覆盖；同版本安全拒绝不计为升级通过。
- 导出写入前检查整个任务队列的源图片路径，阻止覆盖其他任务的源图。

## [1.0.0] - 2026-08-12

### Added

- 首次发布 CutPic：商品卡片、无边界素材、透明背景和长图切分，多任务与导出总览。
- 项目保存恢复、预设、快捷键与中英文界面。

历史 Release 记录可核实发布事实；不把旧会话中的测试报告作为本轮候选包的验收证明。

[Unreleased]: https://github.com/onlytools-lab/onlycut/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/onlytools-lab/onlycut/releases/tag/v1.0.1
[1.0.1-compare]: https://github.com/onlytools-lab/onlycut/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/onlytools-lab/onlycut/releases/tag/v1.0.0
