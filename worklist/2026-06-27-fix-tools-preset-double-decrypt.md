# 2026-06-27 修复工具箱预设代入密码「双重解密」连接失败

## 问题
昨天（6-26）加的工具箱预设源/目标账套代入功能（`f479006` + `48c6ac0`）：
- 主窗体工具箱 Tab 新增「选择源账套…」/「选择目标账套…」按钮
- 启动跨库工具时自动带入源/目标连接信息（服务器、库名、用户、密码）
- **代入后工具点查询/复制报"连接失败"**

陛下怀疑是密码加密问题。

## 根因（不是密码是密文，是明文被"双重解密"清空）

调用链：
1. 主窗体 `SelectToolAccount` → `_dataService.LoadAndDecryptAccounts()` → `account.DbPassword` 已是**明文**
2. `LoadPresetAccounts` 把明文塞进 `txtSourcePassword.Text`
3. 工具 BuildConnString / TestConnectionAsync 调 `EncryptionService.Decrypt(password)` ← **这里再解一次！**

明文（如 `Xfbl@KingRule.2023070617`）走 `Convert.FromBase64String` 抛异常 → `Decrypt` 返回 `""` → 连接串变成 `Password=;` → **连不上**。

**为啥以前能连上**：原来「工具内手动选账套」走 `_context.GetAllAccounts()` = `LoadAccounts()`（不解密，密文进文本框）→ `Decrypt` 一次正好解成明文 → 能连。预设代入是新的代码路径，把这条兼容链破坏了。

## 修复（最小改动：只改 MainForm.cs 一行）

**`A3Tools/Forms/MainForm.cs`** — `SelectToolAccount`：

```csharp
// 改前：
var accounts = _dataService.LoadAndDecryptAccounts();

// 改后：
// 取密文账套：工具内 BuildConnString/TestConnectionAsync 等会调 EncryptionService.Decrypt(password) 解密，
// 这里如果预先解密成明文就会导致"明文当密文解"返回空串，连不上。保持密文状态代入即可，工具无需任何改动。
var accounts = _dataService.LoadAccounts();
```

**11 个工具文件零改动**：`EncryptionService.Decrypt(password)` 不动，继续按密文解密。

## 设计要点 / 教训

- **「密码使用时才解密」是正确的工具层逻辑**，不要为了「兼容多种代入来源」去重写它
- **代入源头要保持一致**（带密文 OR 带明文，二选一）。这里所有原工具都是按密文设计的，那代入就应该给密文
- **改源头比改工具省事得多**：这次只改 1 行（MainForm），回滚 11 个工具文件即可；之前错误地改了 11 个文件 × 37 处，复杂度爆炸还引入新 helper 类
- **不要发明「兼容多种来源」的 helper**：`SafeDecrypt` 看似稳健，实则是工具设计契约不一致的临时补丁——上游对齐了下游自然就不需要兼容

## 验证

- **`dotnet build A3Tools.sln -c Debug`**：0 错（只有原本就有的 nullability warning）
- 端到端：`SelectToolAccount` 改 `LoadAccounts()` 后，`_toolSourceAccount.DbPassword` 是密文（如 `Q742zH+kkLkQ...`），代入到 `txtSourcePassword.Text`，工具 `BuildConnString` 调 `Decrypt(密文)` → 正确解出明文（如 `Xfbl@KingRule.2023070617`）→ 连接成功

## 待陛下测试

1. 启动 A3Tools → 工具箱 Tab → 点【选择源账套…】选一个账套
2. 启动【跨库复制表结构】工具，源连接信息应自动带入
3. 点【查询】→ 应能正常连接源库（不再报"连接失败"）
4. 同样试一下【目标账套】代入 + 跨库复制
5. 也试一下【对比表结构】功能（CompareTablesForm）