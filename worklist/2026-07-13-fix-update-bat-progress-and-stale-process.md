## 2026-07-13 修复 _update.bat 进度反馈 + 防重入

### 问题
v2.4.0 (71MB zip, 含 75MB 单文件 A3Tools.exe) 升级时，bat 卡在解压步骤 1 分钟没反应。
陛下手动重试，导致 2 个 powershell 同时解压同一 zip，行为未知。

### 根因
1. **bat 内嵌 `powershell -Command "..."` + ExtractToDirectory 静默不输出**
   - 解压期间没有任何进度反馈
   - 75MB A3Tools.exe + Windows Defender 首次扫描 → 30-60 秒
   - 陛下 1 分钟看不到进度以为卡了
2. **手动重试导致两个 PS 争抢同一 zip**
   - 第二个 PS 启动后 ExtractToDirectory 不知道怎么处理文件锁
3. **历史"成功"是错觉**
   - v2.3.14 zip 80MB 但解压只 1 秒（数据全在小文件，没触发 Defender 长扫描）
   - 陛下没等过 1 分钟就看到 STEP 2 完成
   - 实际机制一样脆，只是没遇到 Defender 慢扫

### 修复
1. **解压逻辑抽到独立 `_unzip.ps1` 文件（UTF-8 BOM）**
   - bat 调 `powershell -File _unzip.ps1 zipPath tempExtract`
   - .ps1 走 BOM 路径，不依赖命令行传中文
   - 避免 PS 5.1 GBK 源文件解析 bug
2. **加进度 echo**
   - 用 ZipArchive 手动遍历解压
   - 每 5 entries 输出 `[unzip] progress: i / total`
   - 让陛下看到真在跑（而不是死锁）
3. **防重入：bat 启动时检测残留 powershell 进程**
   - 杀掉 10 分钟内启动的 powershell
   - 避免手动重试后两个 PS 争抢

### 文件改动
- `A3Tools/Services/UpdateService.cs` 第 460-530 行
  - 在 bat 之前先生成 `_unzip.ps1`（UTF-8 BOM）
  - bat 改用 `powershell -File _unzip.ps1`
  - bat 启动时加 stale powershell 检测

### 测试
- Build: 0 错 334 警告（warnings 都是已有 nullable 警告，无新增）
- Publish: StandaloneSF-test 编译成功
- 手动 unzip test: 71MB zip → 0.5 秒解压 10 entries
- 进度 echo 工作: `[unzip] total entries: 10` / `[unzip] progress: 5 / 10` / `[unzip] progress: 10 / 10` / `[unzip] OK`
- 验证 _unzip.ps1 内容（用 utf8.bom 写入）

### 已知问题（未修）
- Windows Defender 扫描 75MB A3Tools.exe 仍可能慢 30-60 秒
- 建议把 A3ToolsAutoUpdate 加到 Defender 排除路径（用户手动操作）

### 下次发布
v2.4.1 发版时带上这个修复
