# Changelog

## [Unreleased]

### Changed

- 接入 OnlyCut 工程治理、统一版本来源、Windows PR 验证及手动候选包门禁；第一阶段当时保持 1.0.0，当前源码为尚未正式发布的 1.0.1 候选。
- 保留 CutPic 程序集、安装目录、数据目录和 MSI UpgradeCode。
- 开发分支允许制作 1.0.1 升级测试候选；这不是正式版本发布记录。

### Fixed

- MSI 新装时拒绝不同 ProductCode 的相同版本；保留原产品维护、修复与卸载。
- 导出写入前检查整个任务队列的源图片路径，阻止覆盖其他任务的源图。

## [1.0.0] - 2026-08-12

### Added

- 首次发布 CutPic：商品卡片、无边界素材、透明背景和长图切分，多任务与导出总览。
- 项目保存恢复、预设、快捷键与中英文界面。

历史 Release 记录可核实发布事实；不把旧会话中的测试报告作为本轮候选包的验收证明。

[Unreleased]: https://github.com/onlytools-lab/onlycut/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/onlytools-lab/onlycut/releases/tag/v1.0.0
