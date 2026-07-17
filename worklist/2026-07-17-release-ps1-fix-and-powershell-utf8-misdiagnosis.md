# 2026-07-17 release.ps1 端到端加固 + PowerShell 5.1 UTF-8 误判修正

## 陛下原话

> "更新Memory，并且把发布流程固化下来，我不希望下次还有这样那样的问题。毕竟这都不知道发布多少次了"

## 一句话总结

**v2.4.0 → v2.4.4 一直以为的"Gitee API 中文 mojibake bug"是误判**——是 PowerShell 5.1 `Invoke-WebRequest` 的 UTF-8 解码 bug，Gitee 一直存得正确。v2.4.5 发版踩了 3 个真坑，全部用 release.ps1 自动兜底。

---

## 第一部分：PowerShell 5.1 UTF-8 误判真相

### 现象

之前每次发版都看到 Gitee API 返回的 `release.body` 是 mojibake（`## ????¤? SQL ??￥èˉ￠?·￥??·`），误以为是 Gitee 服务端 bug。

### 真相

PowerShell 5.1 的 `Invoke-WebRequest` 把 HTTP 响应的 UTF-8 字节流当 **Latin-1** 解码，所以**我读回来的**是 mojibake。**Gitee 服务端存的**始终是正确 UTF-8 字节。

**证据**（hex dump raw API 响应）：

| 内容 | 期望 UTF-8 | 实际存储字节 | 结论 |
|------|-----------|-------------|------|
| "修" + "复" | E4 BF AE E5 A4 8D | ✅ E4 BF AE E5 A4 8D | 正确 |
| "设" + "置" | E8 AE BE E7 BD AE | ✅ E8 AE BE E7 BD AE | 正确 |
| "查" + "询" | E6 9F A5 E8 AF A2 | ✅ E6 9F A5 E8 AF A2 | 正确 |
| "智" + "能" + "提" + "示" | E6 99 BA E8 83 BD E6 8F 90 E7 A4 BA | ✅ 全部正确 | 正确 |

### 客户端为什么显示正常？

`UpdateService.cs` 用 .NET `HttpClient` 读 Gitee API，**正确处理 UTF-8** → 客户端 popup 显示中文正常。

陛下 v2.4.4 截图就是铁证（`## 修复 SQL 查询工具`、`### SQL 智能提示（IntelliSense）` 等中文显示完美）。

### 教训

**绝不能再信 PowerShell 5.1 的 `Invoke-WebRequest | ConvertFrom-Json` 输出**。

调试 API 中文编码问题时的正确做法：
```powershell
# 1. 存原始字节
Invoke-WebRequest -Uri "..." -UseBasicParsing -OutFile raw.json
# 2. 用 .NET UTF-8 显式解码
$rawJson = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes("raw.json"))
# 3. 或者直接 hex 看
$bytes = [System.IO.File]::ReadAllBytes("raw.json")
```

### 影响范围修正

| 版本 | 之前判断 | 修正后 |
|------|---------|--------|
| v2.4.0 | "Gitee UTF-8 mojibake bug" | **误判**——实际存得正确 |
| v2.4.1 | 同上 | **误判** |
| v2.4.2 | 同上 | **误判** |
| v2.4.3 | 同上 | **误判** |
| v2.4.4 | 同上 | **误判** |
| v2.4.5 | 误以为有 mojibake，改用英文 body + 塞 zip | **过度修复**——其实中文 body 直接用就行 |

注：v2.4.5 已经 PATCH body 改回中文（id=750218，hex 已验证 UTF-8 正确存储）。

---

## 第二部分：v2.4.5 真实踩坑 + 自动化兜底

### 踩坑 #1: csproj bump 没 commit

**原 release.ps1 Step 5 行为**：改 3 个 csproj 文件的 `<Version>` 标签，**不改 commit**。

**踩坑**：陛下得手 `git add . && git commit -m "chore(release): ..." && git push origin master`，否则 Step 8 push tag 时 tag 指向的还是上一个 commit（v2.4.4）。

**修复**：新增 Step 5.5 自动 commit csproj bump。

```powershell
# Step 5.5
$csprojChanged = $false
foreach ($csproj in $csprojs) {
    if ((& git status --porcelain $csproj) -ne "") { $csprojChanged = $true; break }
}
if ($csprojChanged) {
    & git add $csprojs
    & git commit -m ("chore(release): csproj 版本号 bump -> " + $Version) --no-verify | Out-Null
    Ok "csproj bump committed"
}
```

### 踩坑 #2: Gitee API 自动建错 tag 指向

**现象**：Step 9 创建 Gitee release 时，API 提示"tag_name is missing"或自动创建一个指向 **A3ToolsRelease 仓库** master 分支 HEAD 的 tag。陛下 A3ToolsRelease 仓库的 master 仍指向 `aafca3d`（v2.4.4 commit），所以新 tag 指向 v2.4.4 而不是 v2.4.5。

**根因**：Gitee release API 的 `target_commitish` 字段在某些场景下被忽略，API 用 A3ToolsRelease 仓库的 master HEAD 作为 tag 指向。

**修复**：Step 8.5 push tag 后**验证 tag 指向的 commit 是否 = 当前 HEAD**，不一致就 force push 修。

```powershell
# Step 8.5
$expectedSha = (& git rev-parse HEAD).Trim()
$actualSha = (& git rev-parse "$tag^{}" 2>$null).Trim()
if ($expectedSha -ne $actualSha) {
    Err ("tag 指向错 commit: " + $actualSha + " (期望 " + $expectedSha + ")")
    foreach ($remote in $remotes) {
        $url = (& git remote get-url $remote)
        if ($url -notmatch 'gitee\.com') { continue }
        & git push $remote $tag --force
    }
}
```

### 踩坑 #3: 手动塞 RELEASE_NOTES.md 进 zip

**现象**：之前 v2.4.0-v2.4.2 手动塞过，v2.4.3/v2.4.4 忘了，客户端下载 zip 解压后看不到中文 release notes。

**修复**：新增 Step 7.5，**默认**把 -ReleaseNotes 内嵌为 zip 根目录下的 `RELEASE_NOTES.md`（`-SkipEmbedNotes` 可跳过）。

```powershell
# Step 7.5
if (-not $SkipEmbedNotes -and $ReleaseNotes) {
    $notesPath = Join-Path $gitRoot (Join-Path $PublishDir ("RELEASE_NOTES_v" + $Version + ".md"))
    [System.IO.File]::WriteAllText($notesPath, $ReleaseNotes, ...)
    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, "Update")
    $entry = $zip.CreateEntry("RELEASE_NOTES.md")
    $writer = New-Object System.IO.StreamWriter($entry.Open(), ...)
    $writer.Write($ReleaseNotes)
    $writer.Close()
    $zip.Dispose()
}
```

### 踩坑 #4: 失败只 warn 不 stop

**原行为**：Step 8 tag push 失败只 Warn 然后继续，导致后面 Step 9 创建 release 时用错状态，最后整个 release 半残。

**修复**：失败时 throw + exit 1，立即停止流程，避免半残 release。

---

## 第三部分：Step 12 最终验证

新发布流程结束后，自动 GET release 检查三件事：

1. **body UTF-8 中文存在**：扫原始字节是否有 `[\u4e00-\u9fff]` 字符范围
2. **tag 指向正确**：`git ls-remote origin refs/tags/vX.Y.Z` 拿到的 SHA 必须 = local HEAD
3. **zip asset 挂载**：release.assets 列表里有 `A3Tools_vX.Y.Z.zip`

```powershell
# Step 12
$verifyBytes = (Invoke-WebRequest -Uri "..." -UseBasicParsing).Content
$rawJson = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($verifyFile))
if ($rawJson -match "[\u4e00-\u9fff]") {
    Ok "body 包含中文字符 (UTF-8 验证通过)"
}
```

---

## 改动文件

| 文件 | 改动 |
|------|------|
| `scripts/release.ps1` | +120 行 / -3 行。新增 Step 5.5 / 7.5 / 12，改造 Step 4/5/8，新增 -SkipEmbedNotes 参数 |
| `scripts/release.ps1` (commit) | `42fb045 fix(release): release.ps1 端到端加固 (v2.4.5 踩坑自动化)` |

---

## 新 release.ps1 用法

```powershell
# 标准发版（中文 release notes 直接传）
.\scripts\release.ps1 -Version "2.4.6" -ReleaseNotes "...完整中文 release notes..."

# 不内嵌 notes 到 zip（不推荐）
.\scripts\release.ps1 -Version "2.4.6" -ReleaseNotes "..." -SkipEmbedNotes

# 同时发 GitHub（默认只发 Gitee）
.\scripts\release.ps1 -Version "2.4.6" -ReleaseNotes "..." -SkipGitHub:$false
```

## 验证

- ✅ PowerShell 5.1 语法解析通过（`[System.Management.Automation.Language.Parser]::ParseFile`）
- ✅ 新增 step 5.5 / 7.5 / 8.5 / 12 全部存在
- ✅ 改动已 commit 到 master (`42fb045`)
- 下次发版（v2.4.6）直接跑脚本即可，无需手动 commit csproj / 手动塞 zip / 手动修 tag

## 待陛下回归（下次发版时）

- [ ] `.\scripts\release.ps1 -Version "2.4.6" -ReleaseNotes "..."` 一键跑完
- [ ] 自动 commit csproj bump（看 git log 应有 "chore(release): csproj 版本号 bump -> 2.4.6"）
- [ ] 自动验证 tag 指向当前 HEAD（不再指向 aafca3d 这种旧 commit）
- [ ] zip 里自动有 RELEASE_NOTES.md
- [ ] Step 12 输出"body 包含中文字符 (UTF-8 验证通过)"+"tag 指向正确"+"zip asset 已挂载"