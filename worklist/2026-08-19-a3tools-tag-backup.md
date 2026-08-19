# A3Tools 版本 tag → commit 备份

**创建日期：** 2026-08-19
**原因：** 清理源仓库（origin / github）上的版本 tag，tag 应该只在 A3ToolsRelease 发布仓库里。
**说明：** 本文件备份所有 tag → commit 映射，删除 tag 后仍可凭此找回历史版本。

## 备份清单（28 个本地 tag）

| Tag | Commit (short) | 提交说明 |
|-----|----------------|----------|
| v1.2.0 | 918a19b | 托盘右键菜单优化 + SettingsDialog设计器兼容 |
| v2.3.0 | edec6e5 | feat(update): 支持 zip 完整包更新（含 Plugins/） |
| v2.3.1 | c9d1164 | feat(update): 双源自动更新（Gitee 主 + GitHub 兜底）+ 一键发布脚本 |
| v2.3.2 | 2adb777 | chore: 版本号 2.3.1 → 2.3.2（修复右上角版本号写死） |
| v2.3.3 | 2adb777 | chore: 版本号 2.3.1 → 2.3.2（同上） |
| v2.3.4 | 76d9132 | chore: bump version 2.3.3 → 2.3.4 |
| v2.3.5 | ed55c16 | fix(update): wait for bat to start before Environment.Exit |
| v2.3.6 | ed55c16 | fix(update): wait for bat to start before Environment.Exit |
| v2.3.7 | ed55c16 | fix(update): wait for bat to start before Environment.Exit |
| v2.3.8 | 39516a3 | fix(updater): bat detailed log + Plan B cd /d %~dp0 + UTF-8 BOM |
| v2.3.9 | e98a9bc | chore: bump version 2.3.8 → 2.3.9 |
| v2.3.10 | e98a9bc | chore: bump version 2.3.8 → 2.3.9 |
| v2.3.11 | 39516a3 | fix(updater): bat detailed log + Plan B cd /d %~dp0 + UTF-8 BOM |
| v2.3.12 | 39516a3 | fix(updater): bat detailed log + Plan B cd /d %~dp0 + UTF-8 BOM |
| v2.3.13 | 39516a3 | fix(updater): bat detailed log + Plan B cd /d %~dp0 + UTF-8 BOM |
| v2.3.14 | 0150d89 | fix(scripts): Gitee zip 上传改用 .NET HttpClient（PS 5.1 不支持 -Form） |
| v2.3.14-test | 14c2bfb | test |
| v2.4.0 | b104532 | chore(release): csproj 版本号 2.3.14 → 2.4.0 |
| v2.4.1 | 4212697 | v2.4.1: 修复手动关掉开发工具后无法重新启动 |
| v2.4.2 | b758bfc | v2.4.2: 回滚更新流程到 cmd 窗口显示模式 |
| v2.4.3 | 8377e18 | docs(worklist): v2.4.3 修复自动更新 cmd 不弹出 + 脚本日志不清空 |
| v2.4.4 | 8e9feac | A3Tools v2.4.4 - SQL IntelliSense and editor fixes |
| v2.4.5 | 90e69a7 | A3Tools v2.4.5 |
| v2.4.6 | 6db4c3c | v2.4.6 (Beta) |
| v2.4.6.1 | 068486e | v2.4.6.1 (Beta) - 修复 11:07 5min 卡死 |
| v2.4.6.2 | 501a81c | v2.4.6.2 (Beta) - 更新窗体 Markdown 渲染 |
| v2.4.7 | 6026113 | v2.4.7 - External 场景改 toast 提示 |
| v2.5.0 | b776306 | chore(release): csproj 版本号 bump → 2.5.0 |

## 找回方式

如果需要找回某个历史版本的代码（commit 还在仓库里，tag 只是被删了），用：

```bash
git checkout <commit_short_hash>      # 切到那个版本
git checkout -b hotfix/from-<tag>     # 或者建个分支
```

## 远端仓库 tag 差异（已记录）

- `origin/a3-tools` (Gitee)：30 个 tag（本地 28 + 历史 2）
- `github/a3-tools`：4 个 tag（origin 有但 github 没的 tag 也都被删了）
- `A3ToolsRelease`（发布仓库）：33 个 tag —— **保留不动**（这是正确的位置）