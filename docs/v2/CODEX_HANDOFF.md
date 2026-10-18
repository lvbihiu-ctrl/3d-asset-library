# 给 Windows Codex 的第一份交接

**任务:** [Issue #14](https://github.com/lvbihiu-ctrl/3d-asset-library/issues/14)
**目标分支:** `v2-dev`，个人功能分支 `v2/14-webview-host`。
**起始点:** 当前已合并 v0.1.7（不得更改）、现有 Skill（.agents/skills/）和 `docs/v2/PRODUCT_CONTRACT.md`。

## 你负责完成
- Windows x64 .NET 10 + WebView2 的最小 WinForms 或 WPF 客户端；构建可运行的 EXE，应用中加载受信任的本地 HTML。
- 页面动作发送 `{id,action,payload}` → C# 严格动作白名单验证；请求失败返回明确错误，禁止页面消息任意执行 OS 命令、任意写磁盘。
- 建立 Windows GitHub Actions 构建、单元测试和无私有资产的产物；不要把漂亮 Demo 当成已完成真实扫描或拖拽。
- 明确测试环境/命令/.NET SDK/WebView2 Runtime 处理方案，给出 1016×689 与 1920×1080 两个窗口截图（合成资源）。
- **不要**在本 Issue 中修改 v0.1.7、`latest.json`、用户 `.local-data`；不要提前把 PoC 发成正式更新。

## 交付动作
- Draft PR to `v2-dev` + 对应 Issue 链接 + Windows 构建日志 + 可安装/可运行工件路径 + 任何已知风险。
- Windows C4D 的 FileDrop/Shift 测试是 **Issue #15**，此任务不能用网页事件来宣称 C4D 完成。
- 当前 ChatGPT 可以读 PR 并审查，但**不能直接启动你本机 Codex**；如果未运行/未领取 Issue 就不会自动推进。
