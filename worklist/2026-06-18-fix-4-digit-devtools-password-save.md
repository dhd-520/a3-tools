# 2026-06-18 修复开发工具 4 位密码无法保存

## 问题
陛下反馈：开发工具密码为 4 位时无法保存；查看发现保存成了源密码，导致再次打开设置时为空。

## 根因
`DataService.IsEncrypted` 判断逻辑过于宽松：

- 只判断字符串是否是合法 Base64
- 且是否为 ASCII

例如 `1234` 这类 4 位密码本身也是合法 Base64，会被误判为“已加密”。

结果：
1. 保存设置时，`DevToolsPassword=1234` 被误判为已加密，不再调用 AES 加密，直接明文写入 `settings.json`。
2. 下次加载设置时，程序把 `1234` 当 AES 密文解密。
3. 解密失败返回空字符串，所以设置界面显示为空。

## 修改
文件：`A3Tools/Services/DataService.cs`

### 1. 修正 IsEncrypted
改为同时满足：

- 可 Base64 解码
- 解码后字节长度至少 16
- 字节长度是 AES 块大小 16 的整数倍
- 使用当前机器密钥可成功解密且结果非空

这样 `1234`、`abcd` 等 Base64-like 明文不会再被误判为已加密。

### 2. 新增 DecryptIfEncrypted
仅当字段确认为 AES 密文时才解密，否则按明文兼容处理。

用途：
- 兼容旧版本误保存的开发工具明文密码
- 同时修复账套 DB/远程密码中类似 4 位 Base64-like 明文的潜在问题

### 3. 替换调用
- `LoadAndDecryptAccounts`：改用 `DecryptIfEncrypted`
- `DecryptAccount`：改用 `DecryptIfEncrypted`
- `LoadSettings`：`DevToolsPassword` 改用 `DecryptIfEncrypted`

## 效果
- 新保存的 4 位开发工具密码会正常 AES 加密。
- 已经误保存成明文的 4 位密码，下次打开不会变空，会按明文显示；再次点击保存后会被重新加密。

## 验证
- 执行 `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`
- 结果：0 错误，149 个历史 warning。

## 打包
已重新发布 StandaloneSF：

- 输出目录：`D:\work\A3Tools\publish\StandaloneSF`
- `A3Tools.exe`：77,675,942 bytes
- 总大小：约 76.05 MB
- 已清理旧 `cdp.log` / `win32-login.log` 和 `~$*` 临时文件。
