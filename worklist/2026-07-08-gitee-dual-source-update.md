# 2026-07-08 Gitee 双源自动更新

## 需求
陛下反馈：GitHub 国内不稳定，想再开一个 Gitee 公开仓库做更新分发。臣设计了**双源自动更新**：Gitee 主 + GitHub 兜底。

## 实现

### 1. UpdateService 重构（双源探测）

**位置**：`A3Tools/Services/UpdateService.cs`

**架构**：
- 引入 `UpdateSource` 枚举（GitHub / Gitee）
- 拆分 `CheckForUpdateAsync` 为：
  - `CheckGiteeAsync()` — 两步探测（先 `/releases/latest` 拿 id → 再 `/releases/{id}/attach_files` 拿附件）
  - `CheckGitHubAsync()` — 一步探测（`/releases/latest` 自带 assets）
- 公共入口并发跑两个源，任一失败不致命，取**版本号高**的那个
- `UpdateInfo` 加 `Source` 字段
- `UpdateForm` 显示「来源：Gitee/GitHub」+ "查看完整说明"按钮按 Source 跳对应仓库

**实测结果**：
- Gitee 公开仓库**完全免 token** ✅
- 下载直链：`https://gitee.com/api/v5/repos/{owner}/{repo}/releases/{id}/attach_files/{aid}/download` ✅
- 单附件限制 100MB（社区版）— A3Tools 74-75MB 够用 ✅

### 2. 关于窗体改版
- 版本号改成动态读 `UpdateService.CurrentVersion`（之前写死 v2.2.0）
- 同时显示 GitHub + Gitee 仓库链接

### 3. release.ps1 一键发布脚本

**位置**：`D:\work\A3Tools\scripts\release.ps1`

**用法**：
```powershell
$env:GITEE_TOKEN = "xxx"   # Gitee 私人令牌
$env:GITHUB_TOKEN = "xxx"  # GitHub PAT（可选：装了 gh CLI 可省）
.\scripts\release.ps1 -Version "2.3.2"
```

**流程**：
1. 校验版本号 + git 状态 + tokens
2. 自动更新 3 个 csproj 的 `<Version>`
3. `dotnet publish StandaloneSF`（单文件自包含，74MB）
4. 打 zip → `publish/A3Tools_v{Version}.zip`
5. 推送 git tag（origin + gitee）
6. **Gitee**：API 创建 release → 上传 zip
7. **GitHub**：gh CLI 或 REST API 创建 release → 上传 zip
8. 输出两个 release 链接

**降级**：Gitee 失败不影响 GitHub（独立 try-catch）

## 待陛下手动完成

1. **建 Gitee 公开仓库**
   - 路径：https://gitee.com/new
   - 仓库名建议：`a3-tools`（与 GitHub 同名好记）
   - 如果用户名是 `wangq80368036`，最终路径 `gitee.com/wangq80368036/a3-tools`
2. **拿 Gitee Token**
   - 路径：https://gitee.com/profile/personal_access_tokens
   - 权限：`projects` / `releases` 全勾
3. **GitHub Token**（可选，gh CLI 已装可省）
   - 路径：https://github.com/settings/tokens
   - 权限：`repo`
4. **首次测试**：先 v2.3.2 用脚本发布到 Gitee + GitHub
5. **验证自动更新**：
   - 装 v2.3.1 启动 → 不弹窗
   - 装 v2.3.1 + 远端 v2.3.2 → 启动应弹 UpdateForm，标题「来源：Gitee」

## 配置常量位置

`UpdateService.cs`：
```csharp
public const string GitHubOwner = "dhd-520";        // ✓ 已对
public const string GitHubRepo  = "a3-tools";       // ✓ 已对
public const string GiteeOwner  = "wangq80368036";  // 待陛下确认
public const string GiteeRepo   = "a3-tools";        // 待陛下确认
```

`release.ps1` 也有同样默认值（`$env:GITEE_OWNER` / `GITEE_REPO` 覆盖）

## 构建验证
- `dotnet build A3Tools.sln -c Debug`：0 错误

## 已知细节
- Gitee release 详情接口的 `created_at` 字段名跟 GitHub `published_at` 不同，代码里做了区分
- Gitee 下载链接用 `/api/v5/.../download` API 路径，**不是** 网页抓的 `/releases/download/...` 路径（更稳定）
- 旧 `UpdateService.cs` 用 `accept: application/vnd.github+json`，新代码已改为 `application/json`（Gitee 不接受 vnd.github+json，会 406）
