# OmniMix VB.NET 兼容前端

这是一个面向现有 OmniMix 后端的 Windows 桌面前端。项目目标是用 VB.NET/WPF 提供接近 PCL 风格的桌面体验，同时保持对原作者后端 API、模块 UI、曲库、播放队列和游戏集成流程的兼容。

本仓库现在定位为“兼容前端”，不是后端分支。遇到兼容问题时，优先在前端做适配，不要求修改上游后端实现。

## 当前状态

版本：`1.0.0`

主要产物：

```text
OmniMixPlayer.Gui.Vbnet.exe
```

当前发布目标是 Windows 自包含单文件 exe，可以直接放进已有 OmniMix 后端目录中运行。

## 主要功能

- 启动或发现现有 OmniMix 后端。
- 在标题区域显示后端连接状态。
- 提供播放控制、队列/历史管理、封面加载、循环/随机模式、可拖动进度条。
- 读取后端曲库，并把曲库歌曲直接加入播放队列。
- 在 VB.NET 界面中承载后端模块 UI。
- 从现有资源包部署支持的游戏集成桥。
- 自动同步游戏集成实例 ID 和端口文件。
- 清理明显错绑的旧游戏集成实例。
- 提供后端路径、关闭前端时是否关闭后端、个性化、服务控制、均衡器等设置。

## 兼容说明

正常使用时，VB.NET 前端不要求修改后端 API。前端会维护它能控制的兼容文件，例如：

- `.omnimix_instance_id`
- `omnimix_port.txt`
- 游戏集成桥 DLL

对于 FH6 这类根目录桥接集成，前端现在会把 `version.dll` 和 `OmniPcmShared.dll` 作为实体文件复制到游戏目录，不再使用符号链接，减少游戏侧 DLL 加载的不确定性。

启动顺序：

- 推荐先启动前端。前端会启动或发现后端，并在游戏启动前刷新端口文件和实例文件。
- 如果先启动游戏，桥可能会自己尝试发现或启动后端；这种情况下旧端口文件、旧后端进程或旧实例可能导致错绑。
- 当前前端在每次连接后端时都会自动修复已安装游戏集成的绑定文件，并清理明显无效的旧实例。

## 构建

安装项目所需 .NET SDK 后运行：

```powershell
dotnet build "OmniMixPlayer/OmniMixPlayer.sln" -c Debug -v minimal
```

发布单文件 exe：

```powershell
dotnet publish "OmniMixPlayer/gui_vbnet/Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj" `
  -c Debug `
  -o "OmniMixPlayer/bin/GuiVbnetSingle" `
  /p:PublishSingleFile=true `
  /p:SelfContained=true `
  /p:RuntimeIdentifier=win-x64 `
  /p:EnableCompressionInSingleFile=true `
  /p:PublishReadyToRun=false `
  -v minimal
```

输出文件：

```text
OmniMixPlayer/bin/GuiVbnetSingle/OmniMixPlayer.Gui.Vbnet.exe
```

## 部署

把单文件 exe 放入已有 OmniMix 分发目录，例如：

```text
E:\FH6\ChillPatcher\OmniMixPlayer.Gui.Vbnet.exe
```

该目录仍需要保留原有后端 exe、模块、资源和配置文件。

## 开源协议

本项目按 GNU General Public License v3.0 开源，详见 `LICENSE`。

第三方组件保留其各自目录中的原始协议。
