# 开发与兼容说明

游戏参考源工程：`E:\UnityProjects\Emiria`。当前安装游戏：`E:\Program Files (x86)\Steam\steamapps\common\MalodyV`。开发时均只读。参考安装器：`E:\Desktop\MalodyVThemeLoader`，未修改参考工程。

## 数据

`Protocol.dll` 的 `Malody.DB.ChartRecord.RankStatus` 持久保存本地状态。`MaChart.Keyword` 还会聚合 `type=<状态>` 到歌曲 Searchwords，但单张谱面在线更新状态后这个字符串可能过期，因此本插件直接读取谱面记录。没有独立 Promotion 值，推荐的 Alpha 保持状态 0。

## 绑定与筛选

实际发行版混淆了 Song、ChartManager、meta 和 ShowList 等别名。`GameAdapter` 按保留的 `RebuildShowList`、`RefreshDisplayList`、`IsContain`、唯一泛型显示列表、`ChartRecord` 类型绑定，启动时验证唯一性。遇到歧义，卸下补丁并保留原始游戏行为，日志记录具体错误。

歌曲筛选只在原始 `IsContain` 返回 true 后收窄，保留原关键词、收藏夹等逻辑；选定模式时，同一张谱面须满足模式和状态。按游戏原有 `Song.ScanFiles(false)` 延迟加载本地数据库记录。原始完整图表映射不变，不写 RankStatus，不修改成绩或网络协议。

歌曲内列表只在大厅 `SceneChart.Refresh` 状态机的同步 MoveNext 与联机 `PanelRoomChart.OpenChart` 执行期间暂时收窄 ShowList；完成或异常时立即恢复。这样实际原生列表与选中索引都按过滤后条目生成，且后台完整谱面映射、编辑和通过 hash 访问不受影响。所有临时修改均在 UI 主线程。

搜索 UI 加在双方共用的 PanelSearchSong。原筛选区扩展一行，新增 Unity 原生 Toggle。配置保存为 `BepInEx/config/com.mcpsde.bettermalodyv.cfg` 的 Search/StatusMask，0=空、7=全部（含未知）。

## 安装与更新

BepInEx、Doorstop 的版本、检测、兼容复用、拒绝覆盖无主文件、暂存复制、逐文件回滚及哈希所有权沿用参考实现。独立路径为 `BetterMalodyV/` 和 `BepInEx/plugins/BetterMalodyV/`，独立清单和快捷方式。卸载若检测到其他插件，会保留共享运行环境。安装器修改目标文件前先留本次回滚备份，失败时恢复。

已安装 FEATURES.md 为可编辑文件，通过安装清单中的原始哈希识别手动修改，修复/升级保留手动修改过的文件，未修改或缺失时复制发行版文档。面板缺少外部文档时使用嵌入副本。修改仓库文档会影响后续安装和未手动修改的升级用户。

更新使用 `MCPsDE/BetterMalodyV` 的正式 Release，绑定 `BetterMalodyV-Setup-<版本>.exe` 和 SHA256SUMS.txt。校验成功后，更新器等待管理器 PID 退出再替换，避免原参考实现的文件锁竞争。完成后重新打开面板。没有新版本和 API 限流不影响正常使用。

## 验证

构建、真实 interop 成员和结构验证、一次性假游戏目录中的安装/修复/回滚/卸载、保留已有 BepInEx/其他插件、浅深色/最小与普通尺寸及 125/150/200% 合成缩放。合成缩放不代表物理多显示器 DPI 实测。

开发中的 Unity 原生运行测试与假谱面数据必须在 artifacts 下单独宿主完成；不能启动、改写或结束用户的真实游戏进程。验证结果见 VERIFICATION.md。
