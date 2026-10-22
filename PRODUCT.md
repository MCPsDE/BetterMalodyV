# BetterMalodyV

<!-- impeccable:product-schema 1 -->

## Platform

Windows x64 desktop, native WinForms; Malody V Unity IL2CPP.

## Users and purpose

Malody V 玩家安装优化插件后照常从 Steam 启动游戏。首个功能是在大厅及联机房间选曲时按本地谱面状态筛选歌曲和谱面。

## Capabilities and constraints

用户指定沿用 MalodyVThemeLoader 的安装器、BepInEx 安装与复用逻辑、管理器视觉风格、赞助入口和 GitHub Release 自动更新流程。管理面板以阅读功能说明为中心，内容来自可手动编辑的 Markdown。安装时创建独立桌面快捷方式。图标来自用户提供的 BetterMalodyV.jpg，仅去除外围黑色背景。

## Stack

沿用参考工程：C#，.NET 8 WinForms 管理面板及离线安装器，.NET 6 BepInEx 6 IL2CPP 插件。中文界面，默认暗色管理器、固定亮色安装器。开发时仅只读访问源游戏和参考仓库。

## Accessibility

命令完整单行显示，按真实字体和 DPI 测量按钮；容器不足时重排。支持键盘、焦点提示和浅深色。缩放测试明确区分合成缩放和物理显示器。
