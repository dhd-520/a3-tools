# 清理源仓库 tag + 发布仓库源码污染

**日期：** 2026-08-19
**负责人：** 陛下 → 哈士奇执行
**触发问题：** 别人拉 master 拉不到 v2.5.0 最新代码 + 陛下发现 git 仓库不该有那么多 tag + 发布仓库里居然有源码

## 问题全景

| 仓库 | 角色 | 错误状态 | 期望状态 |
|------|------|----------|----------|
| `origin/a3-tools` (Gitee) | 源 | 30 个版本 tag + master 落后 1 commit | 0 tag + master 最新 |
| `github/a3-tools` | 源镜像 | 4 个版本 tag + master 落后 1 commit | 0 tag + master 最新 |
| `A3ToolsRelease` (Gitee) | 发布 | 92 个文件含源码（.sln/.cs/.csproj/worklist 等） + 33 个 tag | 0 源码 + 保留 tag |

## 根本原因

之前 `release.ps1` 推 tag 的逻辑是：

```powershell
foreach ($remote in (& git remote)) {
    $url = (& git remote get-url $remote)
    if ($url -notmatch 'gitee\.com') { continue }
    & git push $remote $tag --force
}
```

这段代码遍历**所有 gitee.com remote**，结果把 `origin`（源）和 `A3ToolsRelease`（发布）都打了 tag —— 源仓库被 tag 污染。

另外 `release.ps1` 从来没 push 过 `master` commit —— 只 push tag。所以别人拉源仓库 master 永远拿不到最新代码，得手动 `git push origin master`。

发布仓库的源码是早期整库镜像过去的，后来陆续删过几个目录（A3Tools/、A3Tools.Plugins.Default/、A3Tools.Common/、scripts/、tools/、TestContext、DATA、A3ToolsHub）但删了一半卡住了。

## 执行清单

### ✅ 1. 备份所有 tag → commit 映射

写入 `worklist/2026-08-19-a3tools-tag-backup.md`，28 个本地 tag 全部记录，万一以后要找回历史版本直接查 commit。

### ✅ 2. 删除 tag

- 本地：28 个 `v*` tag 全删
- `origin/a3-tools`：30 个 tag 全删（远端比本地多 2 个历史遗留）
- `github/a3-tools`：4 个 tag 全删
- 验证：`git ls-remote --tags {origin,github}` 各 0 条

### ✅ 3. 重建 A3ToolsRelease master

用 worktree + `--force` 强推新 commit：

- 删了 92 个文件（.sln / .cs / .csproj / MEMORY.md / WORKLOG.md / worklist/ / *.docx 等全部源码和文档）
- 新增 `README.md`（说明本仓库只放产物 + 源仓库链接）
- 新增 `releases/.gitkeep`（目录占位）
- 新 commit：`60e1267 chore: 清空 master 源码,只保留 README + releases/ 目录`
- `git push A3ToolsRelease HEAD:master --force` 成功（`1c371c8..60e1267`）

⚠️ **注意：** master 历史 commit（包括之前的"删除 A3Tools/"等）已被新 commit 顶掉，但所有 `v*` tag 仍指向原 commit —— 历史完整可查。新 commit 是 standalone orphan 风格的，**和源仓库 master 内容无关**。

### ✅ 4. 改 `release.ps1`（scripts/release.ps1 479 → 504 行）

新增 **Step 7.7**：推 master commit 到源仓库（origin + github），跳过 A3ToolsRelease：

```powershell
foreach ($remote in (& git remote)) {
    $url = (& git remote get-url $remote)
    if ($remote -eq 'A3ToolsRelease' -or $url -match 'A3ToolsRelease') {
        Info ("  skip " + $remote + " (release repo, only receives tag+zip)")
        continue
    }
    & git push $remote master
}
```

重写 **Step 8**：tag 只推 A3ToolsRelease，源仓库不再打 tag。

### ✅ 5. 推 master 落后 commit 到源仓库

```
git push origin master   → 7a2138c..b776306  master -> master
git push github master   → 4bfc0cc..b776306  master -> master
```

源仓库 master 现在指向 `b776306` (v2.5.0)，别人 `git pull` 能拉到完整 v2.5.0 代码了。

## 最终验证

| 项 | 期望 | 实测 |
|------|------|------|
| origin tag 数 | 0 | 0 ✅ |
| github tag 数 | 0 | 0 ✅ |
| A3ToolsRelease tag 数 | 33（保留） | 33 ✅ |
| A3ToolsRelease master 文件 | README.md + releases/.gitkeep | README.md + releases/.gitkeep ✅ |
| origin master HEAD | b776306 (v2.5.0) | b776306 ✅ |
| github master HEAD | b776306 (v2.5.0) | b776306 ✅ |
| 本地 tag 数 | 0 | 0 ✅ |

## 后续规则（避免再犯）

1. **源仓库（origin / github）只接 master push，不接 tag**
2. **发布仓库（A3ToolsRelease）只接 tag + zip，不接源码**
3. **下次发版时**：跑 `release.ps1`，脚本会自动按新规则推 master 到源 / 推 tag 到发布仓库
4. **发布仓库 master 永远是干净的**：只有 `README.md` + `releases/` 目录（zip 由 Gitee Release 页面承载，不进 master）

## 关联文件

- `worklist/2026-08-19-a3tools-tag-backup.md`：删除的 28 个 tag → commit 映射备份
- `scripts/release.ps1`：新增 Step 7.7 推 master，重写 Step 8 只推 tag 到 A3ToolsRelease
- `A3ToolsRelease/README.md`：发布仓库说明（指向源仓库）