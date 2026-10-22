# 验证记录

## 1.0.1 搜索修复

1.0.0 的人工搭建搜索控件样例不能验证真实预制体布局，用户报告只显示原有三个筛选器。本次改用当前游戏 FrontLayerManager.prefabs 中实际 SearchAdv，原生控制器自动发现面板并挂载，测试刻意不调用 Show；原 Show 在实际 interop 标注 CallerCount(0)，不能作为唯一挂载入口。

在隔离 IL2CPP 宿主的 1280×720、1920×1080 两种分辨率下，各 55 项检查通过，包括实际面板发现、输入框内边界、完整单行标签、至少 200 单位文字输入宽度、重复挂载、鼠标事件勾选、键盘提交切换、长关键词输入及原有全部状态过滤检查。截图等待面板入场动画完成，并断言所有控件处于捕获视野内。证据为 `.impeccable/review/search-native-<宽>x<高>.png/.json`。

两种分辨率均使用 CanvasScaler 做游戏 UI 合成适配，不代表物理显示器 DPI 测试。谱面数据仍为人工记录；联机入口与大厅共用这个预制体，但未进行真实账号联机对局。没有修改或安装到用户的正式游戏目录。

## 1.0.0

验证环境：Windows x64，Unity 2022.3.62f2，BepInEx 6.0.0-be.785，.NET 8/10 SDK，PowerShell 7。目标游戏 GameAssembly.dll SHA-256：`f554af9b371f7cd3629ee1e257b76fba41b3899fa6f2c33f2c6e121b17b8021f`。

| 检查 | 结果与范围 |
| --- | --- |
| Release 编译 | 五个工程通过，0 警告、0 错误 |
| 当前 interop | 直接游戏/Unity 成员及 Song、ChartRecord、IsContain、两个选曲入口与 Refresh 状态机的唯一结构验证通过 |
| IL2CPP 原生运行 | 独立复制的游戏宿主成功加载真实插件；35 项人工数据检查通过，含 0–7 多选状态、显示列表恢复、同谱面模式匹配、缓存更新、未知状态和真实 Toggle/全部按钮 |
| 安装与修复 | 假游戏目录中新装、修复缺失 DLL、缺失文档、更新未编辑文档、保留手动文档、生成/移除桌面快捷方式通过 |
| 环境共存 | 复用兼容 BepInEx 不接管其文件；保留其他插件和自定义配置；自有运行环境被其他插件使用时卸载保留 |
| 失败与回滚 | 锁定管理器文件导致安装失败，恢复到原文件树；拒绝未知 winhttp.dll、禁用 Doorstop、错误 IL2CPP 入口、缺失 Harmony 的环境 |
| 自动更新 | 19 项状态/更新断言通过，使用模拟 HTTP 与真实下载/哈希代码；相同/旧版本、限流、缺校验、错误哈希、正确下载和等待当前 PID 的参数验证。测试不执行下载的样例安装器 |
| Windows 界面 | 16 张管理器和 8 张安装器矩阵捕获，64+32 次完整按钮文字测量通过；明暗、普通/最小窗口、125/150/200% 合成缩放 |
| 原生窗口裁切 | 系统截图确认最小安装器的内容正确裁切；DrawToBitmap 单独存在原生子控件滚动条的打印伪影，不能用其伪影判断实际窗口溢出 |

图像与测量日志在 [.impeccable/review](../.impeccable/review)。真实窗口截图为 [installer-minimum-native.png](../.impeccable/review/installer-minimum-native.png)，Unity 人工样例为 [search-filter-fixture.png](../.impeccable/review/search-filter-fixture.png)。设计复核初次仅发现安装器裁切疑点；补充系统截图后，复核将该项判定 resolved、ship，范围仅限该问题。

原生样例不等于完整大厅/联机房间画面或真实账号对局实测。合成缩放不代表多显示器物理 DPI 验证。没有替换或安装插件到真实游戏；首次独立启动曾由 Steam 转发到正式游戏，随后使用标准 Steam 开发标识避免转发。由游戏自身运行产生的数据变化不视为源代码/安装器写入，也没有回滚玩家数据。

复现入口：scripts/test-native-runtime.ps1、test-installer.ps1、test-runtime-reuse.ps1、verify-interop.ps1、capture-installer-window.ps1，及 `dotnet run --project tests/BetterMalodyV.Tests.csproj -c Release`。原生测试代码仅在 RuntimeVerification=true 的测试构建中编译，发行插件中不包含测试执行器。
