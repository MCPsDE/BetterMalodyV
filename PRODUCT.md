# BetterMalodyV

<!-- impeccable:product-schema 1 -->

## Platform

Windows x64 desktop, native WinForms; Malody V Unity IL2CPP.

## Users and purpose

Malody V 玩家照常从 Steam 启动游戏，使用本地谱面筛选、房主谱面更新、房员分享版本同步和本人本地回放重演。

## Capabilities and constraints

用户指定沿用 MalodyVThemeLoader 的安装器、BepInEx 安装与复用逻辑、管理器视觉风格、赞助入口和 GitHub Release 自动更新流程。管理面板以阅读功能说明为中心，内容来自可手动编辑的 Markdown。安装时创建独立桌面快捷方式。图标来自用户提供的 BetterMalodyV.jpg，仅去除外围黑色背景。

三个状态控件等宽、对齐、淡灰半透明，以勾号表示选中；功能说明每项一句话。1.1.1 在游戏启动及每 30 分钟检查更新，原生游戏弹窗显示下载进度、校验、就绪倒计时和失败；仅在非游玩、非编辑且房间安全时显示并正常退出、安装、重启，无需打开管理面板。就绪通知实际可见后才开始 3 秒倒计时。

1.2.0 在本地成绩详情提供“重演并上传”：当前登录账号的 .mrv 回放须匹配本地谱面哈希，按原始设置和随机种子在正常游玩中重演操作，由游戏重新判定、计分和结算提交。原回放保持完整，不注入记录的分数或连击；正常成绩资格、Guardian、认证和服务器校验保持原样。此功能由玩家手动发起，不含自动重试队列；发布说明沿用 FEATURES.md。

1.3.0 在本地成绩详情提供“回放分析”全屏页，显示记录成绩、逐轨正负偏移与分布、按压时长和间隔、原始判定，以及使用 MalodySite CVT 默认档位估算的准确率；仅 Key、Step、DJ 提供轨道式 CVT 换算，Flip 会镜像轨道，Luck 随机轨道会隐藏不可靠的偏移与估分。解析时只操作 CloneWithMeta 创建的临时谱面副本，分析不提交或改写成绩。

## Stack

沿用参考工程：C#，.NET 8 WinForms 管理面板及离线安装器，.NET 6 BepInEx 6 IL2CPP 插件。中文界面，默认暗色管理器、固定亮色安装器。开发时仅只读访问源游戏和参考仓库。

## Accessibility

命令完整单行显示，按真实字体和 DPI 测量按钮；容器不足时重排。支持键盘、焦点提示和浅深色。缩放测试明确区分合成缩放和物理显示器。
