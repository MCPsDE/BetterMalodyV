# BetterMalodyV

<!-- impeccable:product-schema 1 -->

## Platform

Windows x64 desktop, native WinForms; Malody V Unity IL2CPP.

## Users and purpose

Malody V 玩家照常从 Steam 启动游戏，使用本地谱面筛选、房主谱面更新和房员分享版本同步。

## Capabilities and constraints

用户指定沿用 MalodyVThemeLoader 的安装器、BepInEx 安装与复用逻辑、管理器视觉风格、赞助入口和 GitHub Release 自动更新流程。管理面板以阅读功能说明为中心，内容来自可手动编辑的 Markdown。安装时创建独立桌面快捷方式。图标来自用户提供的 BetterMalodyV.jpg，仅去除外围黑色背景。

三个状态控件等宽、对齐、淡灰半透明，以勾号表示选中；功能说明每项一句话。1.1.1 在游戏启动及每 30 分钟检查更新，原生游戏弹窗显示下载进度、校验、就绪倒计时和失败；仅在非游玩、非编辑且房间安全时显示并正常退出、安装、重启，无需打开管理面板。就绪通知实际可见后才开始 3 秒倒计时。

当前 FEATURES.md 的成绩上传失败后自动重试条目是未来计划，尚未实现；发布说明沿用该文档，不把计划表述为现有功能。

## Stack

沿用参考工程：C#，.NET 8 WinForms 管理面板及离线安装器，.NET 6 BepInEx 6 IL2CPP 插件。中文界面，默认暗色管理器、固定亮色安装器。开发时仅只读访问源游戏和参考仓库。

## Accessibility

命令完整单行显示，按真实字体和 DPI 测量按钮；容器不足时重排。支持键盘、焦点提示和浅深色。缩放测试明确区分合成缩放和物理显示器。
