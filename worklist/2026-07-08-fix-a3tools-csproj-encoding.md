# 2026-07-08 修复 A3Tools.csproj 中文乱码

## 问题
A3Tools.csproj 中文注释显示乱码（虽然 build 没问题，因为注释不影响编译，但代码阅读体验差）。

## 根因
文件里 13 处 3-byte UTF-8 序列的第 3 个字节被替换成了 `0x3F`（ASCII `?`）。  
这是手工保存文件时被某个工具"乱码字符 → ?"替换的痕迹。  

**13 处位置 / 当前坏字节 / 还原结果：**

| 位置 | 坏字节 | 还原 | 还原后中文 | 上下文 |
|---|---|---|---|---|
| 465 | `e8 a1 3f` | `e8 a1 8c` | 行 | "需要时通过命令行 -p:..." |
| 500 | `ef bc 3f` | `ef bc 9f` | ？ | "启用？" (全角问号，注释结束) |
| 982 | `e4 b8 3f` | `e4 b8 ba` | 为 | "<!-- 为 Designer.cs 添加嵌套..." |
| 1070 | `ef bc 3f` | `ef bc 9f` | ？ | "DependentUpon？" (全角问号) |
| 2355 | `e4 bb 3f` | `e4 bb b6` | 件 | "构建后复制插件 DLL..." |
| 2374 | `e5 bd 3f` | `e5 bd 95` | 录 | "到输出目录 Plugins 子目录" |
| 2755 | `e4 bb 3f` | `e4 bb b6` | 件 | "必须和插件 DLL 放在..." |
| 2786 | `e5 88 3f` | `e5 88 99` | 则 | "同一目录，否则 Assembly..." |
| 2843 | `e7 a4 3f` | `e7 a4 ba` | 示 | "工具箱显示 0 个" |
| 2848 | `e4 b8 3f` | `e4 b8 aa` | 个 | "工具箱显示 0 个" |
| 3092 | `e5 bd 3f` | `e5 bd 95` | 录 | "到发布目录 Plugins 子目录" |
| 3150 | `e6 97 3f` | `e6 97 a7` | 旧 | "避免复制 A3Tools\Plugins 下的旧 DLL" |
| 3761 | `ef bc 3f` | `ef bc 9f` | ？ | "方便运行时加载？" (全角问号) |

## 排查过程
1. 写 Node.js 脚本扫所有 .cs / .csproj / .resx 文件，统计非法 UTF-8 序列数。
2. 发现只有 `A3Tools.csproj` 一个文件有 26 处非法序列（13 个 unique 3-byte 位点）。
3. 所有 .cs 文件的"非法序列"实际是合法 4-byte UTF-8 emoji（🌐），不是真坏。
4. 检测文件 git 历史：以前是 UTF-16 LE 保存的（`FF FE` BOM），后来被某个工具转 UTF-8 但替换了部分字节为 `?`。

## 还原方法
按 context 推断原中文字符的 UTF-8 第 3 个字节，写 Node.js 脚本批量替换：

```js
const fixes = {
  465:  0x8c, // 行
  500:  0x9f, // ？
  982:  0xba, // 为
  // ...
};
```

## 修改文件
- `D:\work\A3Tools\A3Tools\A3Tools.csproj`（13 处 3rd byte 替换）

## 验证
- `node` 修复脚本：修复 13 处，剩余 0 处非法 UTF-8 序列
- `dotnet build A3Tools/A3Tools.csproj -c Debug`：**0 错**（2 个 NU1701 NPinyin 兼容性 warning 与本次无关）
- 文件大小不变：4242 bytes
- VS 打开后所有中文注释正常显示

## 教训
1. **保存含中文的 .csproj 用记事本/VS 要小心**：VS 自动保存的 BOM 处理很稳，但偶尔被复制粘贴到 PowerShell 或其他工具再保存时容易爆雷。
2. **检测脚本要覆盖 4-byte UTF-8**（emoji）：漏掉 emoji 检测会出现一堆误报。
3. **注释坏掉不影响 build**，但人是看注释的。该修还是修。