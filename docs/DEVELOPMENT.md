# 开发与验证

- 构建使用 PowerShell 7、.NET 8/10 和 BepInEx 6.0.0-be.785；`scripts/build-release.ps1` 输出到 artifacts/release。
- 源游戏、Emiria 和 ThemeLoader 参考工程只读，测试宿主位于 artifacts。
- 状态谓词读取已加载记录或后台数据库副本索引，禁止在显示列表写锁内扫描曲库；2 万首未加载歌曲的原生压力测试保持零次 ScanFiles。
- 房主通过当前游戏的官方 QueryDownloadInfo/DownloadAndImport/SendChangeChart 接口查询、下载和选谱，不改网络协议。
- 房员只接收当前等待房间、当前房主选中哈希对应的 HTTPS 分享，校验包内准确谱面后事务替换；换房、换谱、哈希错误或重载失败会取消或回滚。
- 支持当前 6.7 发行版，混淆字段集中在 RoomGameApi 并校验类型；不支持的接口只停用联机增强。
- 游戏启动及每 30 分钟由隐藏助手检查更新，以原子写入的类型化进度文件驱动原生 PanelDialog：下载字节/百分比（未知总量时为不定进度）、校验、就绪和失败。独立安装器校验 PID/路径、请求正常退出、安装并重新启动；游玩、编辑或不安全房间期间延后，失败有冷却。原生弹窗创建失败会清理半成品，就绪通知实际可见后才开始 3 秒倒计时。
- 1.2.0 的本地成绩详情“重演并上传”仅接受已登录的同 UID .mrv、哈希一致的本地谱面及受支持的原始设置。ReplayGameApi/ReplayRunController 从原回放生成仅含操作的新流，以原始随机种子和正常 Player/Normal 场景交给原生输入、判定、计分、结算与提交；原回放不变，不注入记录的分数或连击。仅当前重演会话隔离物理游玩输入，不改成绩资格、Guardian、认证或服务器校验。
- FEATURES.md 每项一句话，未修改的安装副本随版本更新，用户手改副本保留；第四项现为手动回放重演，没有持久化自动上传重试队列。

验证入口：`scripts/verify-interop.ps1`、`tests/BetterMalodyV.Tests.csproj`、`scripts/test-native-runtime.ps1`（`-RoomVisual`、`-UpdateVisual`、`-ReplayVisual`；含 ReplayRuntimeTests 原生回放验证）、`scripts/test-installer.ps1`、`scripts/test-runtime-reuse.ps1`、`scripts/test-package.ps1`、`scripts/test-game-update.ps1`。

原生证据在 .impeccable/review：真实 SearchAdv/SceneRoom/PanelDialog 的隔离捕获，官方请求/结果、房员下载及更新版本/进度使用固定响应模拟。1.1.1 两种分辨率各 122 项通过属于历史记录。1.2.0 最终原生验证在 1280×720、1920×1080 各通过 150 项；日志为 artifacts/native-replay-1280-final.log、native-replay-1920-final.log，合并房间/更新/回放捕获并使用当前游戏元数据副本。覆盖真实登录读取、原生 protobuf 解析、拦截真实 SceneLoader 并确认选择 Play、正常 Player 设置且无回放成绩数据、仅操作流/原始种子、原生输入投递与物理输入隔离/恢复、原生计分/TrueScore、未修补受保护方法，以及末次输入后原生 reset/seek 无额外事件或异常。测试宿主保留已知搜索 Canvas，仅用于合并捕获。

托管验证通过 21 项索引、12 项归档、39 项更新/协议检查。artifacts/build-release-1.2.0.log 的最终构建为零警告/错误、171 个游戏/Unity 引用；包内代码/说明/版本哈希一致，包含 ReplayRunController，不含验证类型及 TestLogin/TestChart/TestControl/TestArm/SetLocalPath 测试接口。artifacts/installer-test-1.2.0.log 通过内嵌安装/修复/验证/卸载、原始说明升级、用户编辑保留与兼容运行时复用；artifacts/game-update-test-1.2.0.log 通过正常退出、1.2.0 安装、重复交接拒绝及恰好一次重启。已安装测试副本的插件/共享库/管理器/FEATURES 哈希与最终载荷完全一致。

replay-action 两张真实预制体捕获中的 123456 / 98.5% 为合成详情数据。完整会话的暂停/继续仅经源码审查，reset/seek 断言不代表该流程通过；未完成整谱重演、真实排行榜写入或服务端接受测试，也没有真实双账号联机或物理多屏 DPI 验证。

## 手动发布补丁版

仅修改自己的管理面板说明时，编辑游戏目录下 `BetterMalodyV/docs/FEATURES.md` 并按 F5 即可，无需打包。向其他用户发布则编辑仓库 `docs/FEATURES.md`；升级仍会保留他们手动修改过的安装副本。

以下以从 1.2.0 发布下一个补丁版 1.2.1 为例，在仓库根目录执行；先将 `Directory.Build.props` 的 Version、`src/BetterMalodyV.Plugin/Plugin.cs` 的 BepInPlugin 版本都改为 `1.2.1`，并把 README.md 的安装器文件名改为 `BetterMalodyV-Setup-1.2.1.exe`。

```powershell
pwsh -NoProfile -File scripts/build-release.ps1
pwsh -NoProfile -File scripts/test-package.ps1
git diff -- docs/FEATURES.md Directory.Build.props src/BetterMalodyV.Plugin/Plugin.cs README.md
git add -- docs/FEATURES.md Directory.Build.props src/BetterMalodyV.Plugin/Plugin.cs README.md
git commit -m "Release 1.2.1"
git push origin main
pwsh -NoProfile -File scripts/publish-github-release.ps1 -SkipBuild
```

构建和包测试成功后再提交；涉及代码时，先通过上列相关验证。提交前确认暂存区仅含本次发布文件；发布脚本要求本地 HEAD 与 GitHub main 一致。它自动使用 FEATURES.md 作为 Release 正文，上传 artifacts/release 中的安装器、SHA256SUMS.txt 和 README.txt。生成的绑定、BepInEx、安装包不提交 Git。
