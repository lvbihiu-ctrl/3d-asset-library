# 项目 Skill 安装说明

项目 Codex Skills 使用 **`.agents/skills/<skill>/SKILL.md`** 目录。通过 GitHub 同步进本地仓库后，Codex 才能直接在项目中读取这些说明和配套文件。**这不是 Windows 全局安装，也不会修改 3D 素材管理器 EXE。**

## 已有的 Skill

- `frontend-design` — 来自 Anthropic，已提前安装，保持原样。
- `ui-ux-pro-max` — 上游 [NextLevelBuilder/ui-ux-pro-max-skill](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill)，固定上游 Commit `56ea9cf174ed86786187daa9de0e6247cb0747e4`，MIT，见 `.agents/skills/ui-ux-pro-max/LICENSE`。
- `webapp-testing` — 上游 [anthropics/skills](https://github.com/anthropics/skills/tree/main/skills/webapp-testing)，固定上游 Commit `dbd4588f9e1033efb41dad4bef2f7947c8993d44`，Apache-2.0，见 `.agents/skills/webapp-testing/LICENSE.txt`。

## ui-ux-pro-max

从项目根目录运行：

```powershell
python .agents/skills/ui-ux-pro-max/scripts/search.py "3D asset manager desktop" --design-system -p "3D Asset Library"
python .agents/skills/ui-ux-pro-max/scripts/search.py "native desktop navigation" --stack wpf
python .agents/skills/ui-ux-pro-max/scripts/search.py "asset library card" --domain ux
```

已包含 `scripts/search.py` 与 `core.py`、`design_system.py`、`reasoning_contract.py`，以及所有运行时 CSV（包括 22 套技术栈）、设计规则和 MIT 许可证。上游 `SKILL.md` 的命令路径仅调整为 Codex 项目目录 `.agents/skills/`，搜索算法保持上游原样。

**未复制**上游原始仓库中仅供开发/更新数据库使用的图标与字体上游原始快照、大型字体许可证元数据，以及回归测试测试夹具；它们不是 `scripts/search.py` 的运行依赖。此 Skill 没有下载或安装字体文件。

## webapp-testing

保留官方 `SKILL.md`、Apache-2.0 许可证、`scripts/with_server.py` 和 3 个示例。在项目根目录运行：

```powershell
python .agents/skills/webapp-testing/scripts/with_server.py --help
```

依赖：**Python 3**；真正自动操作网页还需要 `playwright` Python 库及浏览器运行时（**没有**随 GitHub Skill 自动安装）。该工具只能验证 HTML/WebView 的网页层；C4D 原生文件拖拽、Shift 合并及 EXE 自动更新仍需要 Windows 原生实机验证。

## 安全说明

只新增 Skill 文件、本文和自检工作流。禁止改动正式版 `desktop-updates/native/latest.json`、`app-v0.1.7.ps1`、启动器、客户原始模型、素材索引及封面。未来使用这些 Skill 做 UI 时，保持原生 FileDrop 拖拽能力。
