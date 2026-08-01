# 2026-08-01 External 场景弹框改 toast 提示 (v2.4.7)

## 背景

陛下 20:32 反馈:
- 启账套时,launcher 弹"发现更新"框,文案"检测到更新,同时发现其他 A3 客户端或开发工具正在运行:「君则A3」、「君则A3集成开发工具」"
- 但**没有任何 A3 更新**(A3 启动时自己检测没发现新版本)
- launcher 框误导陛下以为有更新 → 实际 launcher 自己也没要升级

陛下 20:35 明确:
- A3 客户端启动时检测更新 → A3 自己的事,launcher 只负责自动点 A3 自己弹的"升级文件检测"框
- launcher 自己弹"检测到更新"框**误导陛下**

## 修改

`A3Tools/Forms/MainForm.cs` 的 `PrepareUpdateScenarioForLaunch` External 分支:

| 项 | 之前 | 现在 |
|---|---|---|
| 行为 | 弹"发现更新" MessageBox,问"是否继续 launcher 升级?" | 只 toast 提示"其他 A3 进程正在运行:..." |
| 误导 | 文案"检测到更新 / launcher 升级" 完全错误 | 准确说明"其他 A3 在跑,launcher 没动" |
| 阻塞 | 阻塞启账套(陛下必须选 是/否) | 不阻塞,直接 return true |

## 为什么

A3 升级完全由 A3 自己负责(``A3_UPDATE_DIALOG_TITLES` / `TryAutoConfirmUpdateDialog`):
- A3 启动时自己检测更新文件
- 有新版本 → A3 自己弹"升级文件检测"框
- launcher 看到 A3 弹框 → 自动按【是】(走 `TryAutoConfirmUpdateDialog`)
- A3 下载 → 升级完成 → A3 弹"系统提示"
- launcher 看到 → 自动按【确定】

launcher 自己的 `MessageBox.Show` "发现更新" 框完全是冗余 + 误导。

## 改动文件

- `A3Tools/Forms/MainForm.cs` (line ~499-526): `PrepareUpdateScenarioForLaunch` External 分支

## 编译

- 0 错 22 警告(项目历史警告,不变)

## 没动

- ✅ A3 升级处理逻辑(`TryAutoConfirmUpdateDialog` / `A3_UPDATE_DIALOG_TITLES` 等)
- ✅ launcher 自更新 UI(`UpdateForm` Markdown 渲染)
- ✅ `_userUpdateChoice` 字段逻辑
- ✅ `JointSpawn` / `Solo` 场景处理

## 验证

启账套时:
- 不弹"发现更新"框
- 只看到右下角 toast "其他 A3 进程正在运行:..."
- A3 自己启动 → A3 检测到新版本 → A3 弹"升级文件检测" → launcher 自动按【是】 → A3 下载 → A3 弹"升级完成!" → launcher 自动按【确定】

## 版本号

v2.4.6 → v2.4.7