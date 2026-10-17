# 3D 素材管理器 OTA 更新协议（从 v0.1.7 起）

## 目的

永远不要再因为 GitHub Raw CDN 缓存旧 `latest.json`，就误报“当前已是最新版”。

### 客户端
1. 主检查在 `raw.githubusercontent.com/.../refs/heads/main/.../latest.json` 后添加每次随机的 `?cacheBust=<nonce>`，附带禁缓存头。
2. 如果 Raw 没发现高于当前的版本，或 Raw 无法访问：**必须**向 `api.github.com/repos/lvbihiu-ctrl/3d-asset-library/contents/desktop-updates/native/latest.json?ref=main&cacheBust=<nonce>` 独立二次确认，并解码 Contents API 的 Base64 `content` 字段。
3. 只有有效的第二次查询确认版本号未提高，才能提示“已是最新版”；如果 API 也无法确认，提示检查失败，不能误报。
4. 正式升级脚本只允许从**本项目完整 40 位 Commit SHA** 固定的 GitHub Raw HTTPS URL 下载，清单 SHA256 必须准确匹配实际字节。旧版脚本和 EXE 不直接覆盖，使用 `.runtime/override.ps1` 与 `override.sha256`。
5. 如果 Raw 已经发现更高版本，跳过第二次 API 查询以减少匿名 API 限额占用。
6. 检查失败不等于“没有更新”；GitHub 或网络完全中断时仍可能暂时无法更新。

### 发版
1. 不修改旧版发布脚本，不改已发布 Commit；新功能或修复写入新的 `app-vX.Y.Z.ps1`。
2. 运行 Windows PowerShell 的语法、原生控件启动、旧素材索引/封面，以及模拟更新器缓存故障测试。
3. 合并新脚本到 main，引用**那个合并 Commit**作为 `scriptUrl`，依据文件最终字节计算 SHA256。
4. 最后才更新 `latest.json`，确保 version、URL、sha256 一致。
5. GitHub Actions `Verify native OTA release integrity` 会核验：固定 Commit URL、本地版本脚本、远端下载字节、SHA256 和内置版本号，不匹配就失败。
6. 仍必须在 Windows 真机验收原生 C4D 拖拽与 Shift 合并；CI 无法替代用户软件环境。

### 老版本的过渡
- v0.1.5/v0.1.6 已经含有旧更新检查代码。**单纯修改 GitHub 无法远程替换它们**。
- 如果旧按钮拿到新清单，它可以直接升级 v0.1.7；若旧 CDN 长时间返回旧清单，需**仅一次**使用经过 SHA256 校验的修复引导包来安装 v0.1.7 脚本。
- 成功升级 v0.1.7 后，正常使用软件内的“检查更新”；无需再次使用一次性引导包。

**安全边界：** 不将任何客户模型、贴图、封面或索引提交 GitHub；不移动原始模型、不自动改动本机用户数据。
