# 2026-07-07 自动更新 v2.3.0

## 需求
陛下要求利用 GitHub Releases + 版本号实现自动更新。
用户在 Git 仓库发布新版本后，打开工具就会提示更新。

## 实现方案

### 1. 数据源
- **GitHub Releases API**（公开仓库，无需 Token）
- 端点：`https://api.github.com/repos/{owner}/{repo}/releases/latest`
- 关键字段：`tag_name` (v2.3.0)、`body` (发布说明)、`assets[].browser_download_url` (exe 下载链接)
- 限流：未认证 60次/小时/IP，足够个人/小团队使用

### 2. 新增文件
- `A3Tools/Services/UpdateService.cs` - 检查/下载/更新核心逻辑
  - CheckForUpdateAsync(): 异步查 GitHub API
  - DownloadUpdateAsync(): 带进度回调的流式下载
  - PerformUpdate(): 备份 + bat覆盖 + 重启
  - CompareVersion(): 语义化版本号比较
- `A3Tools/Forms/UpdateForm.cs` + `.Designer.cs` - 更新提示窗
  - 显示新版本号、发布时间、当前版本、文件大小
  - 滚动展示发布说明（去除markdown符号）
  - 进度条 + 实时速度
  - 三个按钮：立即更新 / 稍后 / 查看完整说明

### 3. MainForm 集成
- **菜单** 帮助 → 【检查更新】（手动触发）
- **启动时** 后台 2 秒延迟后静默检查，有新版本自动弹窗
- **关于窗体** 修正版本号 v1.2.0 → v2.2.0 + 加 GitHub 仓库链接

### 4. 版本号
- 3 个 csproj 全部 2.2.0 → 2.3.0

## 发布流程（陛下侧操作）

1. 陛下开发完成新功能
2. `git push github master`
3. `git tag v2.x.0 && git push github v2.x.0`
4. 打开 https://github.com/dhd-520/a3-tools/releases → Draft new release
5. 选择 tag、填说明、拖拽 exe 到 Attach binaries
6. 点 Publish release
7. 所有 v2.x.0 及以下版本用户启动 A3Tools → 自动弹窗 → 一键更新

## 配置位置
`UpdateService.cs` 第 25-26 行：
```csharp
public const string GitHubOwner = "dhd-520";
public const string GitHubRepo = "a3-tools";
```

## 测试状态
- dotnet build: 0错
- GitHub API连通性 ✓
- Tag v2.3.0 已推送

## 待陛下手动完成
- [ ] GitHub 网页上传 A3Tools.exe (76MB) 到 v2.3.0 Release
- [ ] 测试完整流程：装 v2.2.0 启动 → 应该不弹窗；v2.2.0 启动后上传 v2.3.0 → 应该弹窗
