# 开发与验证

- 构建使用 PowerShell 7、.NET 8/10 和 BepInEx 6.0.0-be.785；`scripts/build-release.ps1` 输出到 artifacts/release。
- 源游戏、Emiria 和 ThemeLoader 参考工程只读，测试宿主位于 artifacts。
- 状态谓词读取已加载记录或后台数据库副本索引，禁止在显示列表写锁内扫描曲库；2 万首未加载歌曲的原生压力测试保持零次 ScanFiles。
- 房主通过当前游戏的官方 QueryDownloadInfo/DownloadAndImport/SendChangeChart 接口查询、下载和选谱，不改网络协议。
- 房员只接收当前等待房间、当前房主选中哈希对应的 HTTPS 分享，校验包内准确谱面后事务替换；换房、换谱、哈希错误或重载失败会取消或回滚。
- 支持当前 6.7 发行版，混淆字段集中在 RoomGameApi 并校验类型；不支持的接口只停用联机增强。
- 游戏启动及每 30 分钟由隐藏助手检查更新，以原子写入的类型化进度文件驱动原生 PanelDialog：下载字节/百分比（未知总量时为不定进度）、校验、就绪和失败。独立安装器校验 PID/路径、请求正常退出、安装并重新启动；游玩、编辑或不安全房间期间延后，失败有冷却。原生弹窗创建失败会清理半成品，就绪通知实际可见后才开始 3 秒倒计时。
- 1.2.0 的本地成绩详情“重演并上传”仅接受已登录的同 UID .mrv、哈希一致的本地谱面及受支持的原始设置。ReplayGameApi/ReplayRunController 从原回放生成仅含操作的新流，以原始随机种子和正常 Player/Normal 场景交给原生输入、判定、计分、结算与提交；原回放不变，不注入记录的分数或连击。仅当前重演会话隔离物理游玩输入，不改成绩资格、Guardian、认证或服务器校验。
- 1.3.0 的本地成绩详情新增“回放分析”。ReplayAnalysisGameApi 读取 .mrv 原生记录，使用 CloneWithMeta 创建的临时对象及 ChartParser.CreateForExistChart 重解析本地谱面；车道式偏移、逐轨正负统计、按压分布、原生 HitTiming 数量和 MalodySite CVT 默认 profile 由 Shared 分析引擎生成。CVT 仅支持 Key、Step、DJ，Flip 会映射轨道，Luck 随机轨道会隐藏不可靠的偏移与估分；其他模式保留按压与原始判定统计。
- FEATURES.md 每项一句话，未修改的安装副本随版本更新，用户手改副本保留；第五项合并本人回放重演与分析入口，没有持久化自动上传重试队列。

验证入口：`scripts/verify-interop.ps1`、`tests/BetterMalodyV.Tests.csproj`、`scripts/test-native-runtime.ps1`（`-RoomVisual`、`-UpdateVisual`、`-ReplayVisual`；含 ReplayRuntimeTests 原生回放验证）、`scripts/test-installer.ps1`、`scripts/test-runtime-reuse.ps1`、`scripts/test-package.ps1`、`scripts/test-game-update.ps1`。

原生证据在 .impeccable/review：真实 SearchAdv/SceneRoom/PanelDialog 的隔离捕获，官方请求/结果、房员下载及更新版本/进度使用固定响应模拟。1.1.1 两种分辨率各 122 项通过属于历史记录。1.2.0 最终原生验证在 1280×720、1920×1080 各通过 150 项；日志为 artifacts/native-replay-1280-final.log、native-replay-1920-final.log。1.3.0 在两种分辨率各通过 155 项，覆盖临时谱面克隆/解析、原缓存哈希不变、Flip 映射、Luck 估分停用、成绩详情双操作、分析页四个分区和返回；全屏页面、轨道、按压和 profile 页面捕获使用合成回放数据。

1.3.0 发布构建零警告/错误并校验 233 个游戏/Unity 引用；托管验证通过 21 项索引、12 项归档、16 项回放分析、19 项状态/下载检查及 39 项更新/重启协议断言。内嵌包安装/校验/卸载、功能说明升级与手改保留、运行时复用/回滚、正常退出/单次重启均通过。安装器 SHA-256：`9562b8c0044ffe58ab31a42215891df247fa59ed44db27e75517d7bacd7be39f`。

1.3.1 修正回放图表：直方图按数值边界定位并按桶宽绘制密度，CVT 仅显示有样本的时间桶并按谱面时间定位。托管回放分析 22 项通过；隔离游戏在 1280×720 与 1920×1080 各通过 161 项，覆盖非等宽分桶、溢出桶和稀疏时间轴。

1.3.2 按 MalodySite Canvas 点图重画 CVT：每个评分对象一个圆点、零分红点和五条百分比网格；其他分布改为细线与圆点。托管分析 21 项通过；两种原生分辨率各通过 163 项。

1.3.3 将偏移、按压时长和间隔图细分为 5–10 ms 采样并提高图高；本地回放列表在 ACC 左侧显示小写 `baka` 或 Turbo 尾判对应的 `baka-e` 准确率。托管分析 23 项通过；两种原生分辨率各通过 166 项。

1.3.4 修复 LOCAL 成绩列表复用和刷新时遗漏 Baka 标签的问题，并验证蓝色标签实际显示在 ACC 左侧；两种原生分辨率各通过 167 项。

1.3.5 改为从 `ItemScore.AssignObject` 读取 LOCAL 成绩，避免 `Fill` 参数绑定导致 Baka 标签缺失；托管与原生回放验证保持通过。

1.3.6 修复兜底扫描取不到 `AssignObject` 时误隐藏已显示标签的问题，避免 LOCAL Baka 标签闪现后消失。

replay-action 两张真实预制体捕获中的 123456 / 98.5% 为合成详情数据。完整会话的暂停/继续仅经源码审查，reset/seek 断言不代表该流程通过；未完成整谱重演、真实排行榜写入或服务端接受测试，也没有真实双账号联机或物理多屏 DPI 验证。

## 手动发布补丁版

仅修改自己的管理面板说明时，编辑游戏目录下 `BetterMalodyV/docs/FEATURES.md` 并按 F5 即可，无需打包。向其他用户发布则编辑仓库 `docs/FEATURES.md`；升级仍会保留他们手动修改过的安装副本。

以下以从 1.3.6 发布下一个补丁版 1.3.7 为例，在仓库根目录执行；先将 `Directory.Build.props` 的 Version、`src/BetterMalodyV.Plugin/Plugin.cs` 的 BepInPlugin 版本都改为 `1.3.7`，并把 README.md 的安装器文件名改为 `BetterMalodyV-Setup-1.3.7.exe`。

```powershell
pwsh -NoProfile -File scripts/build-release.ps1
pwsh -NoProfile -File scripts/test-package.ps1
git diff -- docs/FEATURES.md Directory.Build.props src/BetterMalodyV.Plugin/Plugin.cs README.md
git add -- docs/FEATURES.md Directory.Build.props src/BetterMalodyV.Plugin/Plugin.cs README.md
git commit -m "Release 1.3.7"
git push origin main
pwsh -NoProfile -File scripts/publish-github-release.ps1 -SkipBuild
```

构建和包测试成功后再提交；涉及代码时，先通过上列相关验证。提交前确认暂存区仅含本次发布文件；发布脚本要求本地 HEAD 与 GitHub main 一致。它自动使用 FEATURES.md 作为 Release 正文，上传 artifacts/release 中的安装器、SHA256SUMS.txt 和 README.txt。生成的绑定、BepInEx、安装包不提交 Git。
