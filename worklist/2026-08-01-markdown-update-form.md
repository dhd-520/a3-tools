# 2026-08-01 更新窗体支持 Markdown 渲染

## 背景

陛下 11:18 反馈:
> "把更新窗体，展示更新内容的地方能支持上 MARKDOWN 格式么，现在看更新内容一坨"

陛下选 **方案 D**:Markdig + WebBrowser (11:18 确认)

## 改造前

- 控件:`System.Windows.Forms.TextBox` (Multiline + ReadOnly + ScrollBars.Vertical)
- 内容处理:`body.Replace("**", "").Replace("`", "")` — 只去粗体和反引号,其余纯文本
- 效果:陛下看到的"一坨" — 标题/列表/代码块/表格都没格式

## 改造后

- 控件:`System.Windows.Forms.WebBrowser` (用 DocumentText 加载 HTML 字符串)
- 渲染:`Markdig 0.34.0` (NuGet, ~456 KB) 解析 markdown → HTML,套 GitHub 风格 CSS
- 支持:标题(1-6 级)、粗体、斜体、代码、代码块、列表、任务列表、表格、引用、链接、分割线

## 改动文件

| 文件 | 改动 |
|------|------|
| `A3Tools/A3Tools.csproj` | 加 `<PackageReference Include="Markdig" Version="0.34.0" />` |
| `A3Tools/Forms/UpdateForm.Designer.cs` | `txtBody` 控件类型 TextBox → WebBrowser;调整 Anchor/BorderStyle 等属性 |
| `A3Tools/Forms/UpdateForm.cs` | 加 `using Markdig;`;新增 `RenderMarkdownAsHtml(string markdown)` 静态方法;`UpdateForm_Load` 里调它 |

## 关键代码

```csharp
// UpdateForm.cs
using Markdig;

private static string RenderMarkdownAsHtml(string markdown)
{
    var pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()  // GFM 表格、任务列表、自动链接等
        .Build();
    string bodyHtml = Markdown.ToHtml(markdown ?? "", pipeline);
    
    string html = @"<!DOCTYPE html>
<html><head><meta charset=""utf-8""><style>
/* GitHub 风格 CSS:Microsoft YaHei UI + Consolas */
body { font-family: 'Microsoft YaHei UI', ...; line-height: 1.6; ... }
h1, h2 { border-bottom: 1px solid #d0d7de; }
code { font-family: 'Consolas'; background: rgba(175,184,193,0.2); ... }
pre { background: #f6f8fa; ... }
table { border-collapse: collapse; }
blockquote { border-left: 4px solid #d0d7de; background: #f6f8fa; }
/* 等等 */
</style></head><body>" + bodyHtml + @"</body></html>";
    return html;
}

// UpdateForm_Load
txtBody.DocumentText = RenderMarkdownAsHtml(_update.Body ?? "");
```

## Markdig 版本

**Markdig 0.34.0** (2024 年稳定版)
- 支持 GFM (GitHub Flavored Markdown)
- AdvancedExtensions 启用:表格、任务列表、自动链接、脚注、删除线、HTML 块等
- single-file publish 把 Markdig.dll 内嵌到 A3Tools.exe,不需要额外文件

## 编译

- **0 错 2 警告**(NU1701 NPinyin 兼容性提示 + 1 个新 warning,均为项目历史警告)
- A3Tools.dll: 345,600 bytes(v2.4.6.1 = 341,504 → +4 KB)
- Markdig.dll: 465,920 bytes(单文件发布时内嵌到 A3Tools.exe)
- 单文件 zip: 71.25 MB(v2.4.6.1 = 71.08 MB → +0.17 MB,Markdig 压缩后很小)

## 设计决定

1. **Markdig 而不是 Markdig.Signed**:Markdig 0.34 默认是 strong-name signed,不需要额外包
2. **WebBrowser 而不是 WebView2**:WebView2 需要运行时(~100MB),WebBrowser 是 WinForms 自带 IE 内核,Win10/11 都能用
3. **CSS 模仿 GitHub**:浅色主题、`#24292f` 主文字色、`#d0d7de` 边框、`#f6f8fa` 引用块底色、`Consolas` 代码字体
4. **不开启脚本**:WebBrowser 默认禁用脚本,纯渲染,避免安全问题

## 测试用例

1. 启动 A3Tools v2.4.6.2 → 帮助 → 检查更新
2. Gitee release body(中文 markdown)被 WebBrowser 渲染:
   - `# ## ###` 标题有大字号 + 边框
   - `**粗体**` 加粗
   - `> 引用` 灰底左蓝边
   - `` `code` `` 灰底等宽字体
   - `1. 2. 列表` 有序列表
   - `---` 分割线
   - 表格边框 + 表头灰底
   - 链接蓝色可点
3. 滚动:WebBrowser 自带滚动条
4. 大小:内容多了窗体能滚,不会撑爆

## 已知问题

- 🟡 WebBrowser 是 IE 内核,Win11 显示效果跟 Win10 略有差异(都是 IE 11)
- 🟡 大字体 + 中文环境下 Markdig 输出可能有 CJK 间距问题(待陛下实测)

## 经验沉淀

**WinForms 渲染 markdown 选 Markdig + WebBrowser 是性价比最高的方案**:
- Markdig 单一依赖,纯 .NET,无 native 依赖
- WebBrowser 自带,无外部运行时
- CSS 可以完全控制,模仿 GitHub 风格

**WebBrowser 控件的坑**:
- 默认 `ScriptErrorsSuppressed = false` 会弹脚本错误对话框
- `ScrollBarsEnabled` 设为 false 让 HTML body 自己控制滚动
- 必须 `DocumentText` 写完整 HTML(含 `<!DOCTYPE>` 和 `<meta charset>`),不能只写 body

**Markdig 高级扩展一览**(`UseAdvancedExtensions` 等价于):
- UseAbbreviations, UseAutoIdentifiers, UseCitations, UseCustomContainers
- UseDefinitionLists, UseEmphasisExtras, UseFigures, UseFooters, UseFootnotes
- UseGridTables, UseMathematics, UseMediaLinks, UsePipeTables, UseListExtras
- UseAutoLinks, UseTaskLists, UseDiagrams, UseGenericAttributes, UseHtmlBlocks
- ...(共 30+ 个)

---

**未提交、未发版。** 等陛下决定是否并入 v2.4.6.2 Beta。