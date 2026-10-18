# Windows 与 C4D 落地经验

## EXE 能启动只是第一层

真正桌面交付要使用 Release、自包含、Windows x64 的发布输出，双击 `AssetLibrary.V2.exe` 能出现完整界面。不要求用户安装 SDK、Node 或 Python。WebView2 Runtime 的环境要求仍须如实说明；自包含 .NET 不等于系统所有组件都已打包。

页面负责界面，C# 宿主负责白名单原生操作。页面传素材 ID，宿主解析可信索引中的路径；不要给网页任意执行命令、任意读写磁盘的能力。受信页面来源、消息字段、动作、ID 和授权根目录需要一起约束。

## 原生拖拽不是拖网页图片

用户曾把卡片拖入 C4D 后看到“不能打开文件”。页面图片/URL 的浏览器拖拽，与 Windows 原生模型文件拖拽不是一回事。

修正方式是禁用图片默认拖拽，左键真实拖动越过阈值后，页面只发送 `beginAssetDrag` 和素材 ID；宿主验证模型路径，用 Windows FileDrop/OLE 交给目标程序。进入 OLE 前退出 WebView2 消息回调，保留物理按键检查。

取消拖动不等于 C4D 导入成功。Shift 状态由 Windows 和目标 C4D 处理，不靠网页动画假装合并。打开文件、合并到当前场景、贴图依赖保持正确是三个不同验证点。

用户已在实机反馈普通拖动能打开模型，并确认 Shift 拖动可合并。它是用户实际确认，不是已经完成“各连续三次＋贴图完整性”的正式门禁；Issue #15 在本次同步时仍未关闭。

## 剪贴板与窗口时序

Windows 剪贴板是共享资源。测试并发访问可能失败，应顺序运行、有限重试、明确错误，必要时恢复测试前内容。一次重跑成功不能证明并发风险已经根治。

后台或被遮挡的 WebView2 可能节流绘制。界面测试应等真实尺寸、状态和图片解码，不用固定 100ms 假设已经画完。测试中的事件注入不能写成“自动化已经操作了物理鼠标”。

## 本机安装的安全做法

发布到新版本目录，逐文件校验，备份桌面快捷方式，保留旧 EXE 和数据恢复点，再从用户实际使用的快捷方式启动。不要覆盖正在运行的 EXE，也不要不明范围地杀掉 Electron、Python 或 C4D 进程。

必须确认新窗口的可执行路径和可见窗口，而不是只看启动命令返回成功。本机快捷方式替换成功不代表软件内 OTA 已经完成。

## 异常诊断先看证据

窗口空白、材质崩溃、拖不进 C4D、快捷方式失效可能分属不同层。先确认实际 EXE、依赖版本、进程和日志，再做最小修复；不把一次 DLL/安装包替换当成通用答案，不关闭安全软件作为默认安装步骤。MCP 凭据、个人认证文件不写进仓库或错误截图。

依据：[Windows 最小宿主](../v2/evidence/issue-14/WINDOWS_RESULTS.md)、[FileDrop 及用户后续确认](https://github.com/lvbihiu-ctrl/3d-asset-library/blob/fe430a888f5d33d5493d0146231df98d5d2bc87e/docs/v2/evidence/clipboard-core/DRAG_MASONRY_RESULTS.md)、[封面剪贴板证据](https://github.com/lvbihiu-ctrl/3d-asset-library/blob/fe430a888f5d33d5493d0146231df98d5d2bc87e/docs/v2/evidence/clipboard-core/WINDOWS_RESULTS.md)。
