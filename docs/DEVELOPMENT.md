# 开发与验证

- 构建使用 PowerShell 7、.NET 8/10 和 BepInEx 6.0.0-be.785；`scripts/build-release.ps1` 输出到 artifacts/release。
- 源游戏、Emiria 和 ThemeLoader 参考工程只读，测试宿主位于 artifacts。
- 状态谓词读取已加载记录或后台数据库副本索引，禁止在显示列表写锁内扫描曲库；2 万首未加载歌曲的原生压力测试保持零次 ScanFiles。
- 房主通过当前游戏的官方 QueryDownloadInfo/DownloadAndImport/SendChangeChart 接口查询、下载和选谱，不改网络协议。
- 房员只接收当前等待房间、当前房主选中哈希对应的 HTTPS 分享，校验包内准确谱面后事务替换；换房、换谱、哈希错误或重载失败会取消或回滚。
- 支持当前 6.7 发行版，混淆字段集中在 RoomGameApi 并校验类型；不支持的接口只停用联机增强。
- 游戏更新由隐藏助手下载并校验，独立安装器校验 PID/路径、请求正常退出、安装并重新启动；游玩或编辑期间延后，失败有冷却。
- FEATURES.md 每项一句话，未修改的安装副本随版本更新，用户手改副本保留。

验证入口：`tests/BetterMalodyV.Tests.csproj`、`scripts/test-native-runtime.ps1 -RoomVisual`、`scripts/test-package.ps1`、`scripts/test-game-update.ps1`。

原生证据在 .impeccable/review：真实 SearchAdv/SceneRoom 的隔离捕获，官方请求/结果与房员下载使用固定响应模拟；没有真实双账号联机，也未作物理多屏 DPI 验证。