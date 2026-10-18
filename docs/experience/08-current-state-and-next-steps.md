# 当前状态与下一步

状态截面为 2026-10-19，Asia/Shanghai。用户最新选择是继续完整原图瀑布流；本轮只归档经验，不改客户端、不升级数据、不发新版。

## 远端与本机是不同状态

| 位置 | 本次核实的状态 |
|---|---|
| `main` | `c65a20f4a342179fae490a237223c010364604ed`；正式清单 v0.1.7 |
| `v2-dev` | `cf1a3074e26394bd4d4d83254f22a040aed7f1bf`；已合入 Windows 宿主和批准的合成界面 |
| PR #34 | OPEN、Draft，UI专业化；头提交 `a65b8ca` |
| PR #36 | OPEN、Draft，真实只读库 Alpha；头提交 `531e626` |
| PR #37 | OPEN，封面/搜图/本机替换/拖拽/缩放；头提交 `25757fc`，不包含后来全部多目录和布局修正 |
| 最新本机试用来源 | `codex/v2-fluid-natural-masonry` 的 `fe430a888f5d33d5493d0146231df98d5d2bc87e`；已推远端，尚未整体合入 `v2-dev` |
| C4D Issue #15 | OPEN；有用户普通打开和 Shift 合并确认，完整重复/依赖验证门禁尚未签收 |

PR 和分支今后会变化，继续开发前重新 fetch/查询，不把此截面当永久现状。

## 试用成果的证据

| 能力 | 已有结果 | 仍需区分的边界 |
|---|---|---|
| Windows EXE | 自包含发布、实际启动并接桌面入口 | 不代表公开签名 Stable |
| 真实库 | 多根追加、重复扫描不重复收录、重启保留 | 当前新扫描以 C4D/FBX/OBJ 为主，不是所有旧版格式已对齐 |
| 封面 | Ctrl+V/按钮持久保存，重扫保留 | 不改原图；不是每个模型都有专属渲染 |
| 图像搜索 | dHash 基础相似度与同图测试 | 非语义/几何识别 |
| 定位 | 原生 ID 到文件定位链路已测试 | 某些自动测试只验证桥接，非目视 Explorer |
| C4D 拖拽 | 用户确认可打开及 Shift 合并 | 未按合同完成各3次和贴图完整验证 |
| 瀑布流 | 自然比例、密集列均分、稀疏不放大、Ctrl滚轮 | 用户最新确认方向，未宣布最终视觉满意 |
| 本机替换 | 新程序目录、校验、快捷方式备份和旧版保留 | 不是 v0.1.7 按钮到 V2 的完整 OTA |
| V2 更新器 | 隔离实验 A→B成功、B→C失败恢复B | 连续两次升级/回滚/正式通道未完成 |
| 数据库与分类 | 本机 Alpha 仍用隔离 JSON 与封面映射 | SQLite 规范存在不等于运行时已迁移；真实虚拟分类编辑未齐 |
| 系统缩略图 | 完成概念和另一个工具的只读实现分析 | 尚未接入或实测当前模型成功率 |

最新布局修复测试：27项界面、51项.NET通过；发布EXE的大库和UI检查退出码0，实际截图尺寸已检查。库规模为1,034模型记录；11模型封面、929项目参考记录、94无预览。原索引/配置/缓存7个非浏览器文件的SHA256不变；这不等于所有大模型全文重新哈希。

## 关键功能提交

- [封面与搜图 c53d3c5](https://github.com/lvbihiu-ctrl/3d-asset-library/commit/c53d3c5ed33a237bd0c8e965446b66909f499baa)
- [真实模型 FileDrop d793760](https://github.com/lvbihiu-ctrl/3d-asset-library/commit/d7937600097e92d9c30c1f4673cb4f803a419377)
- [Ctrl滚轮与紧凑卡片 25757fc](https://github.com/lvbihiu-ctrl/3d-asset-library/commit/25757fcc49cc7d01ad5b8109977fa5ee1131e483)
- [多文件夹追加 972573c](https://github.com/lvbihiu-ctrl/3d-asset-library/commit/972573c62563aca86140781c7686158703afde9c)
- [大库分页和项目参考 4a53aae](https://github.com/lvbihiu-ctrl/3d-asset-library/commit/4a53aae828b8e639f55c4086ff16ae5d93784b42)
- [自然比例与铺满列宽 fe430a8](https://github.com/lvbihiu-ctrl/3d-asset-library/commit/fe430a888f5d33d5493d0146231df98d5d2bc87e)

完整 Windows 证据集中在[最新试用提交的 evidence 目录](https://github.com/lvbihiu-ctrl/3d-asset-library/tree/fe430a888f5d33d5493d0146231df98d5d2bc87e/docs/v2/evidence/clipboard-core)。历史报告的“未完成”可能由后续报告补充，按日期和提交对照，不删历史失败。

## 下一步顺序

1. 用户体验当前瀑布流，确认是否仍有具体排版阻碍；不再整体换风格。
2. 后续代码任务先整理已有 PR/提交依赖，审查并集成已完成成果，避免从旧 `v2-dev` 重做。此次文档 PR 不顺带合并它们。
3. 若用户选择补预览，先少量只读验证 Windows 系统缩略图，不能直接开大批模型渲染。
4. 单独补齐 C4D 门禁、实际分类与更新等工作；正式发布前验证从真实旧版到 V2 的安装、连续升级、数据恢复与回滚。

这不是在本轮自动领取上述开发任务。正式 v0.1.7 更新清单、其他工作区和另一个软件保持原状。
