BetterMalodyV 首个版本：在 Malody V 大厅与联机房间的高级搜索中按 Stable、Beta、Alpha（含 Promotion）多选筛选，歌曲与歌曲内的谱面列表同时生效。

- 离线安装器沿用 MalodyVThemeLoader 的 BepInEx 6 IL2CPP 安装与兼容复用逻辑，提供安装、修复、卸载和独立桌面快捷方式。
- 管理面板直接显示可手动修改的 Markdown 功能说明，带浅深色外观和同款炫彩赞助按钮。
- 图标使用提供的 M 图案，去除外围黑底。
- GitHub Release 自动更新：下载对应版本安装器、校验 SHA-256、等待管理面板退出后更新。

运行 `BetterMalodyV-Setup-1.0.0.exe` 前请退出游戏。安装完成后照常从 Steam 启动。状态依据游戏已经缓存的本地记录，未缓存状态不推测为 Stable；Promotion 没有独立本地状态值，归入 Alpha。

验证包括 Release 编译、当前游戏 interop 兼容、35 项隔离 IL2CPP 原生样本检查、更新下载/校验分支、假游戏安装/修复/卸载/回滚/运行环境共存，以及浅深色与合成缩放。原生样本使用人工谱面数据，未以真实账号联机对局验证；合成缩放不代表物理显示器 DPI 测试。
