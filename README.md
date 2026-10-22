# BetterMalodyV

Windows x64 / IL2CPP 版 Malody V 优化插件。首个功能：大厅和联机房间高级搜索新增谱面状态多选，歌曲列表和歌曲内谱面列表同时按状态筛选。

[下载安装器](https://github.com/MCPsDE/BetterMalodyV/releases/latest) · [全部功能说明](docs/FEATURES.md)

1. 退出游戏，运行 `BetterMalodyV-Setup-1.0.1.exe`，确认游戏目录并安装。
2. 安装器复用兼容的 BepInEx 6 IL2CPP；没有环境时离线安装随附的 `6.0.0-be.785`。
3. 照常从 Steam 启动游戏，打开右上角搜索，在输入框内部右侧勾选 Stable、Beta、Alpha（含 Promotion）；All 恢复全部。
4. 桌面的「BetterMalodyV 管理面板」用于查看功能说明、赞助和检查更新。

可以与 [MalodyVThemeLoader](https://github.com/MCPsDE/MalodyVThemeLoader) 共存，使用相同的运行环境。卸载时优先卸载各插件，最后再移除共享运行环境；主题加载器的旧安装器不识别其他插件的依赖关系。

管理面板的内容直接来自 `docs/FEATURES.md`。修改仓库文档后重新打包即可分发；已安装用户可以直接编辑游戏目录下的 `BetterMalodyV/docs/FEATURES.md`，保存后面板自动刷新。修复和更新会保留手动修改过的说明文件，未修改的说明会随插件更新；想恢复新版随附说明时，先备份并移走该文件，再修复。

状态来自游戏本地数据库的 `ChartRecord.RankStatus`，不是实时联网查询。Alpha=0、Beta=1、Stable=2；Promotion 是推荐属性，没有独立的本地状态值。Unknown=10、Update=11 仅在「全部」时显示。首次启用状态筛选会按游戏原有方式延迟加载本地谱面元数据，大曲库可能需要等待。

管理面板打开时自动检查 GitHub 正式 Release，下载对应版本安装器并校验 SHA-256，等待面板关闭后安装并重新打开。请先退出游戏，插件在下次启动游戏时更新。安装器也支持 `--check`、`--install`、`--verify`、`--uninstall <游戏目录>`。

构建需 .NET 8/10 SDK、PowerShell 7 和当前游戏生成的 interop 缓存：

```powershell
pwsh -NoProfile -File scripts/prepare-dependencies.ps1
dotnet build BetterMalodyV.slnx -c Release
pwsh -NoProfile -File scripts/build-release.ps1
pwsh -NoProfile -File scripts/test-installer.ps1 -InstallerExe artifacts/release/BetterMalodyV-Setup-1.0.1.exe
pwsh -NoProfile -File scripts/test-runtime-reuse.ps1
```

产物统一位于 `artifacts/release`。版本在 `Directory.Build.props`，插件元数据版本同步写于 `Plugin.cs`。开发接口、数据边界和验证范围见 [开发说明](docs/DEVELOPMENT.md)。

图标由用户提供图片去除外围黑底。Windows 组件、安装和更新代码沿用 MCPsDE 的 MalodyVThemeLoader 并按本插件用途修改。

by 猫猫虫小窝(451586654) · [赞助支持](https://ifdian.net/a/mcpsde)
