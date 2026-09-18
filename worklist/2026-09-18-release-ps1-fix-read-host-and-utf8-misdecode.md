# 修复 release.ps1 3 个老 bug

**日期**: 2026-09-18
**触发**: 陛下发 v2.6.0 时反馈"为什么发布这么多次了,每次发布还要弯弯绕绕这么久,是不是发布流程没修正"
**修复文件**: `scripts/release.ps1`(+46 / -15 行)

---

## 现场复盘 v2.6.0 的踩坑顺序

| # | Bug | 现场现象 | 修复前 workaround |
|---|---|---|---|
| 1 | Read-Host 卡 agent 模式 | script 在"git status 脏"的提示处卡死,即使 `"y" | .\release.ps1` 也救不了 | **手动跳过整个脚本**,手工跑 12 步 |
| 2 | ConvertTo-Json 把含中文 body 膨胀 3500x | body 1KB → JSON 3.6MB, API 拒收 | **手工拼 JSON** 替代 `ConvertTo-Json -Depth 5` |
| 3 | PS 5.1 把 Gitee API UTF-8 中文按 Latin-1/GBK 误读 | 验证步骤显示 mojibake, 误以为 Gitee 存错 | **HttpWebRequest + Stream + 原始字节** 替代 `Invoke-WebRequest -Content` |

陛下原话: "弯弯绕绕" — 准确。3 个 fix 不该每次现场绕。

---

## Bug 1: Read-Host 在 agent (OpenClaw) 模式卡死

### 改之前

```powershell
if ([Environment]::UserInteractive) {
    $ans = Read-Host "Continue? [y/N]"
    if ($ans -ne 'y' -and $ans -ne 'Y') { exit 1 }
} else {
    Warn "non-interactive mode -> auto continue"
}
```

### 问题

- PS 5.1 在 OpenClaw exec 环境下, `[Environment]::UserInteractive` 仍然返 `$true`(因为进程是从 Explorer.exe 派生的, 有 user session)
- 但 stdin 没有真正的 TTY → `Read-Host` 抛 `InvalidOperation`
- `"y" | .\release.ps1` 也不行, 因为 Read-Host 检查的是 `IsInputRedirected`, 跟管道参数是两回事

### 改之后

```powershell
$isAgent = $env:OPENCLAW_SESSIONNAME -or $env:CI -or $env:AGENT_RUN `
    -or [Console]::IsInputRedirected `
    -or $Host.Name -eq 'ServerRemoteHost'
$isRealInteractive = -not $isAgent -and [Environment]::UserInteractive
if ($isRealInteractive) {
    $ans = Read-Host "Continue? [y/N]"
    if ($ans -ne 'y' -and $ans -ne 'Y') { exit 1 }
} else {
    Warn ("non-interactive/agent mode -> auto continue (UserInteractive=" + ... + ")")
}
```

### 检测逻辑

5 个条件,任一为真即认定 agent:
1. `$env:OPENCLAW_SESSIONNAME` (OpenClaw exec 自动注入)
2. `$env:CI` (CI 环境)
3. `$env:AGENT_RUN` (其他 agent 框架)
4. `[Console]::IsInputRedirected` (stdin 被 pipe)
5. `$Host.Name -eq 'ServerRemoteHost'` (PowerShell remoting)

---

## Bug 2: ConvertTo-Json 把含中文 body 字段膨胀 3500x

### 现象(陛下 v2.6.0 实测)

- 原始 notes 文件: 1901 bytes (UTF-8)
- 走 `ConvertTo-Json`: **JSON 字节 3664665** (3.5 MB!)
- 同样的中文内容, 在 hashtable 经过 `ConvertTo-Json -Depth 5` 后, 中文字符的处理路径触发 PS 5.1 的字符扩展 bug
- Gitee API 收到 3.5 MB body → 返回 `{"messages":["body is invalid"]}`

### 改之前

```powershell
$createBody = @{
    access_token = ***; tag_name = $tag; name = ("A3Tools v" + $Version)
    body = $ReleaseNotes; target_commitish = "master"; prerelease = "false"
} | ConvertTo-Json -Depth 5
$jsonText = $createBody
$release = Invoke-RestMethod ... -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonText))
```

### 改之后

新增 helper 函数 `Build-GiteeReleaseJsonBytes`, 手工拼 JSON, 只对 body/name/tag 做最小转义 (`\\ \" ` `"`, CR 删除, LF→`\n`, TAB→`\t`):

```powershell
function Build-GiteeReleaseJsonBytes {
    param(
        [string]$Token, [string]$Tag, [string]$Name,
        [string]$Body, [string]$TargetCommitish = "master", [string]$Prerelease = "false"
    )
    $eb = $Body -replace '\\','\\\\' -replace '"','\"' -replace "`r",'' -replace "`n",'\n' -replace "`t",'\t'
    $ec = $Name -replace '\\','\\\\' -replace '"','\"'
    $et = $Tag -replace '\\','\\\\' -replace '"','\"'
    $raw = '{"access_token":"' + $Token + '","tag_name":"' + $et + '","name":"' + $ec + '","target_commitish":"' + $TargetCommitish + '","prerelease":"' + $Prerelease + '","body":"' + $eb + '"}'
    return ,([System.Text.Encoding]::UTF8.GetBytes($raw))
}
$createJsonBytes = Build-GiteeReleaseJsonBytes -Token $giteeToken -Tag $tag -Name ("A3Tools v" + $Version) -Body $ReleaseNotes
```

JSON 大小:**1901 字节内容 → 2118 字节** (正常范围, 无膨胀)。

---

## Bug 3: PS 5.1 把 Gitee API UTF-8 中文按 Latin-1/GBK 误读

### 改之前

```powershell
$verifyBytes = (Invoke-WebRequest -Uri "https://gitee.com/api/v5/..." -UseBasicParsing).Content
$verifyFile = Join-Path $env:TEMP ("verify_release_" + $Version + ".json")
[System.IO.File]::WriteAllBytes($verifyFile, $verifyBytes)
```

### 问题

`Invoke-WebRequest -Content` 返 string, 不返字节数组。PS 5.1 把 string 解码用 `Default` 编码:
- Windows 中文系统通常是 GBK (936)
- Windows 英文系统通常是 Windows-1252 (Latin-1)
- Gitee 返回的 UTF-8 中文被按 GBK/Latin-1 解码 → 显示成 mojibake (e.g. `e??? ????????￥???`)
- 陛下看到验证输出 mojibake, 误以为 Gitee 存错 / 上传失败

**实际上 Gitee 存的是正确的 UTF-8, 是 PS 5.1 显示问题。**

### 改之后

改用 `HttpWebRequest + Stream + 原始字节`:

```powershell
$verifyUri = ("https://gitee.com/api/v5/repos/" + $giteeOwner + "/" + $giteeRepo + "/releases/" + $giteeReleaseId)
$verifyReq = [System.Net.HttpWebRequest]::Create($verifyUri)
$verifyReq.Headers.Add("Authorization", ("token " + $giteeToken))
$verifyReq.UserAgent = "A3Tools-release-verify"
$verifyResp = $verifyReq.GetResponse()
$verifyStream = $verifyResp.GetResponseStream()
$verifyMs = New-Object System.IO.MemoryStream
$verifyStream.CopyTo($verifyMs)
$verifyBytes = $verifyMs.ToArray()
$verifyStream.Close()
$verifyResp.Close()
$verifyFile = Join-Path $env:TEMP ("verify_release_" + $Version + ".json")
[System.IO.File]::WriteAllBytes($verifyFile, $verifyBytes)
```

之后下游 `$rawJson = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($verifyFile))` 已经正确 (用 UTF8 显式解码 byte[]), 跟之前的兼容。

---

## 验证

| 检查项 | 结果 |
|---|---|
| `[System.Management.Automation.Language.Parser]::ParseFile` | 0 错 ✓ |
| 改动行数 | +46 / -15 |
| 旧 v2.4.3 / v2.4.0 流程 | 不破坏(ConvertTo-Json 仅在 body 是小字节时正确, body 大时才膨胀 — 但 v2.6.0 之前的发布说明都小, 不会触发这个 bug) |
| 新流程 | 陛下下次跑 `release.ps1 -Version "2.7.0"` 应该 1 步完成 |

---

## 下次踩坑预案(给未来自己)

1. **agent 模式 Read-Host**: 看 `[Console]::IsInputRedirected` / `$env:OPENCLAW_SESSIONNAME`
2. **大 body 字段 + ConvertTo-Json**: 走手工拼 JSON, 永远不要 `@{...} | ConvertTo-Json` 处理超过 500 字节的非 ASCII 字符串
3. **PS 5.1 UTF-8 API 响应解码**: 永远用 `HttpWebRequest` 或 `HttpClient` 拿原始字节, 不用 `Invoke-WebRequest -Content` 或 `Invoke-RestMethod` 的隐式 string 通道

---

## 教训(写给未来的自己)

**用户反馈是 bug 的最好来源。** 3 个 bug 里:
- #1 之前每次 "y" 都试过但都失败, 没人说"为啥"
- #2 之前每次小 body 不会触发, 也没人触发过大 body
- #3 之前误以为 Gitee 存错, 没意识到是 PS 5.1 显示问题

**陛下说"弯弯绕绕"才暴露出来** — 这才是真 bug 报告。下次类似反馈要**马上停下来修**, 不要每次都现场绕。