# BetterMalodyV

Windows x64 / BepInEx 6 IL2CPP 版 Malody V 优化插件。

- 状态筛选：大厅和联机搜索按 Stable、Beta、Alpha（含 Promotion）筛选歌曲及谱面。
- 房主更新：选中的官方谱面有新版时，在房间内一键下载更新。
- 分享同步：房员接收分享后自动替换本地不一致的版本。
- 自动更新：游戏启动及每 30 分钟检查新版，校验后等待本局结束、安装并自动重启。

[下载安装器](https://github.com/MCPsDE/BetterMalodyV/releases/latest)，退出游戏后运行 `BetterMalodyV-Setup-1.1.1.exe` 安装；后续升级无需打开管理面板。

[功能文档](docs/FEATURES.md) 可直接编辑，管理面板会读取游戏目录下的 `BetterMalodyV/docs/FEATURES.md`；升级保留手动修改过的说明。

开发：PowerShell 7 运行 `scripts/prepare-dependencies.ps1`、`scripts/build-release.ps1`，验证见 [开发记录](docs/DEVELOPMENT.md)。

by 猫猫虫小窝(451586654) · [赞助支持](https://ifdian.net/a/mcpsde)
