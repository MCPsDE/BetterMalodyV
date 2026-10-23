# 开发与验证

- 构建使用 PowerShell 7、.NET 8/10 和 BepInEx 6.0.0-be.785；`scripts/build-release.ps1` 输出到 artifacts/release。
- 源游戏、Emiria 和 ThemeLoader 参考工程只读，测试宿主位于 artifacts。
- 状态谓词读取已加载记录或后台数据库副本索引，禁止在显示列表写锁内扫描曲库；2 万首未加载歌曲的原生压力测试保持零次 ScanFiles。
- 房主通过当前游戏的官方 QueryDownloadInfo/DownloadAndImport/SendChangeChart 接口查询、下载和选谱，不改网络协议。
- 房员只接收当前等待房间、当前房主选中哈希对应的 HTTPS 分享，校验包内准确谱面后事务替换；换房、换谱、哈希错误或重载失败会取消或回滚。
- 支持当前 6.7 发行版，混淆字段集中在 RoomGameApi 并校验类型；不支持的接口只停用联机增强。
- 游戏启动及每 30 分钟由隐藏助手检查更新，以原子写入的类型化进度文件驱动原生 PanelDialog：下载字节/百分比（未知总量时为不定进度）、校验、就绪和失败。独立安装器校验 PID/路径、请求正常退出、安装并重新启动；游玩、编辑或不安全房间期间延后，失败有冷却。原生弹窗创建失败会清理半成品，就绪通知实际可见后才开始 3 秒倒计时。
- FEATURES.md 每项一句话，未修改的安装副本随版本更新，用户手改副本保留；当前成绩上传失败后自动重试条目仅是未来计划。

验证入口：`scripts/verify-interop.ps1`、`tests/BetterMalodyV.Tests.csproj`、`scripts/test-native-runtime.ps1 -RoomVisual -UpdateVisual`、`scripts/test-installer.ps1`、`scripts/test-runtime-reuse.ps1`、`scripts/test-package.ps1`、`scripts/test-game-update.ps1`。

原生证据在 .impeccable/review：真实 SearchAdv/SceneRoom/PanelDialog 的隔离捕获，官方请求/结果、房员下载及更新版本/进度使用固定响应模拟；两种分辨率均通过 122 项检查，包含真实游玩/编辑场景与弹窗失败/可见性断言。没有真实双账号联机，也未作物理多屏 DPI 验证。

## 手动发布补丁版

仅修改自己的管理面板说明时，编辑游戏目录下 `BetterMalodyV/docs/FEATURES.md` 并按 F5 即可，无需打包。向其他用户发布则编辑仓库 `docs/FEATURES.md`；升级仍会保留他们手动修改过的安装副本。

以下以从 1.1.1 发布下一个补丁版 1.1.2 为例，在仓库根目录执行；先将 `Directory.Build.props` 的 Version、`src/BetterMalodyV.Plugin/Plugin.cs` 的 BepInPlugin 版本都改为 `1.1.2`，并把 README.md 的安装器文件名改为 `BetterMalodyV-Setup-1.1.2.exe`。

```powershell
pwsh -NoProfile -File scripts/build-release.ps1
pwsh -NoProfile -File scripts/test-package.ps1
git diff -- docs/FEATURES.md Directory.Build.props src/BetterMalodyV.Plugin/Plugin.cs README.md
git add -- docs/FEATURES.md Directory.Build.props src/BetterMalodyV.Plugin/Plugin.cs README.md
git commit -m "Release 1.1.2"
git push origin main
pwsh -NoProfile -File scripts/publish-github-release.ps1 -SkipBuild
```

构建和包测试成功后再提交；涉及代码时，先通过上列相关验证。提交前确认暂存区仅含本次发布文件；发布脚本要求本地 HEAD 与 GitHub main 一致。它自动使用 FEATURES.md 作为 Release 正文，上传 artifacts/release 中的安装器、SHA256SUMS.txt 和 README.txt。生成的绑定、BepInEx、安装包不提交 Git。
