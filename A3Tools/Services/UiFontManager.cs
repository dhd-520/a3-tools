using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// ★ 2026-11-12：全局界面字体管理。
///
/// 现状问题：A3Tools 的 31 个窗体在 Designer 里硬编码了 570 处字体
///   （微软雅黑 9/10/11/14f、Microsoft YaHei UI 8.5~11f、Consolas 9/9.5/12f），
///   只有 5 个窗体设了 Form 级 this.Font。
///   因为子控件都显式指定了字体，Form.Font 的继承链完全失效 ——
///   改 Form 字体不会影响任何一个子控件。
///
/// 本类的做法：**不改 Designer**（保持所见即所得：VS 设计器里什么样式，运行时就是什么样式），
///   而是在运行时遍历控件树：
///   · 族名不是「等宽 / 符号字体」的控件 → 换成用户选的界面字体
///   · 字号按缩放系数换算（保留 9/10/11/14f 的层级关系，不拍平成一个字号）
///   · Bold / Italic / Underline 原样保留
///   · Consolas 等宽字体（SQL 编辑框、代码框）不动
///   · 换完字体后，若控件文字被截断且右侧有空间，自动加宽（只扩不缩、不越界）
///
/// 私有字体（未安装的 TTF/OTF/TTC）：用 PrivateFontCollection 加载。
///   ★ 集合必须保活 —— PrivateFontCollection 被 GC 回收后，基于它的 Font 会失效，
///   文字会悄悄退回系统字体。所以这里用静态字段持有，绝不中途 Dispose。
///
/// 用法：
///   Program.Main 里   UiFontManager.Initialize(settings);        // 启动时
///   设置保存后       UiFontManager.ApplyFromSettings(settings);  // 立即生效
/// </summary>
public static class UiFontManager
{
    /// <summary>等宽字体 —— SQL / 代码框，绝不替换</summary>
    private static readonly HashSet<string> MonoFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Consolas", "Courier New", "Courier", "Lucida Console", "Lucida Sans Typewriter",
        "Cascadia Mono", "Cascadia Code", "NSimSun"
    };

    /// <summary>符号字体 —— 图标专用，不能换</summary>
    private static readonly HashSet<string> SymbolFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Marlett", "Wingdings", "Webdings", "Symbol"
    };

    /// <summary>当前生效的私有字体集合（必须保活，见类注释）</summary>
    private static PrivateFontCollection? _privateFonts;

    /// <summary>
    /// ★ 历史字体集合（保活用，绝不能让它被 GC 回收）。
    ///
    /// GDI 的字体注册是进程级的：PrivateFontCollection 被终结时会连带卸载它注册的字体。
    /// 用户换一个字体文件时，旧集合若被 GC → 还在用旧字体的控件会突然变回系统字体
    /// （表现为「换了字体，有些界面又变回默认」这种查不出来的怪问题）。
    /// 所以旧集合一律留在列表里，活到进程结束。
    /// </summary>
    private static readonly List<PrivateFontCollection> _retiredFonts = new();

    /// <summary>私有字体族名 → FontFamily（私有字体优先于同名系统字体）</summary>
    private static readonly Dictionary<string, FontFamily> _privateFamilies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>系统字体族缓存 —— 避免每次 new FontFamily 造成 GDI 句柄泄漏</summary>
    private static readonly Dictionary<string, FontFamily> _systemFamilies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>控件「原始字体」记录 —— 重新应用时从这里还原，避免缩放被叠加多次</summary>
    private static readonly ConditionalWeakTable<Control, FontSpec> _originalFonts = new();

    /// <summary>
    /// ★ 2026-11-13：固定宽度、不允许自动加宽的控件。
    ///
    /// 陛下要求：「工具设置固定宽度，显示不全放上去后浮动显示」。
    /// 工具箱里的卡片（默认工具 / 自定义工具）必须保持统一宽度 ——
    /// 之前那套「文字截断就自动加宽」会把同一排卡片改得参差不齐，排版反而更乱。
    /// 这些控件一律不参与加宽，文字放不下时靠 ToolTip 悬浮显示全文。
    ///
    /// ★ 用 HashSet 而不是 ConditionalWeakTable：后者的 TValue 必须是引用类型，
    ///   装不了 bool。改用 HashSet + 引用相等（不用默认的 Equals/GetHashCode）。
    /// </summary>
    private static readonly HashSet<Control> _fixedWidthControls = new(ReferenceEqualityComparer.Instance);

    /// <summary>已经应用过字体的窗体（避免重复遍历）</summary>
    private static readonly HashSet<Form> _appliedForms = new();

    private static System.Windows.Forms.Timer? _watcher;

    private static string _targetFamily = string.Empty;
    private static float _sizeScale = 1.0f;

    /// <summary>当前界面字体族名（空 = 不换字体）</summary>
    public static string TargetFamily => _targetFamily;

    /// <summary>当前字号缩放系数（1.0 = 保持原字号）</summary>
    public static float SizeScale => _sizeScale;

    /// <summary>是否已加载私有字体</summary>
    public static bool HasPrivateFont => _privateFamilies.Count > 0;

    /// <summary>最近一次错误（设置页可显示）</summary>
    public static string LastError { get; private set; } = string.Empty;

    /// <summary>控件的原始字体描述（族名 / 字号 / 样式）</summary>
    private sealed record FontSpec(string Family, float Size, FontStyle Style);

    // ===== 启动 =====

    /// <summary>
    /// 启动时调用：加载私有字体 → 设置全局默认字体 → 开始监听新打开的窗体。
    /// </summary>
    public static void Initialize(AppSettings settings)
    {
        LoadPrivateFontFile(settings.UiFontFilePath);
        _sizeScale = NormalizeScale(settings.UiFontSizeScale);
        _targetFamily = (settings.UiFontFamily ?? string.Empty).Trim();

        ApplyDefaultFont();
        StartWatcher();
    }

    /// <summary>
    /// 设置变更后调用：重新加载私有字体并立即应用到所有已打开的窗体。
    /// </summary>
    public static void ApplyFromSettings(AppSettings settings)
    {
        LoadPrivateFontFile(settings.UiFontFilePath);
        _sizeScale = NormalizeScale(settings.UiFontSizeScale);
        _targetFamily = (settings.UiFontFamily ?? string.Empty).Trim();

        ApplyDefaultFont();

        foreach (var form in Application.OpenForms.Cast<Form>().ToList())
        {
            if (form.IsDisposed) continue;
            ApplyToForm(form, force: true);
        }
    }

    /// <summary>
    /// Application.SetDefaultFont —— 让「没有显式设置字体」的控件用上全局字体。
    /// ★ 必须在第一个窗口创建之前调用，否则 .NET 会抛异常（这里吞掉，后面的遍历是兜底）。
    /// </summary>
    private static void ApplyDefaultFont()
    {
        if (_targetFamily.Length == 0 && _sizeScale == 1.0f) return;
        try
        {
            var font = BuildFont(_targetFamily, 9F * _sizeScale, FontStyle.Regular);
            Application.SetDefaultFont(font);
        }
        catch
        {
            // 已经有窗口创建了 —— 无所谓，遍历逻辑会兜住
        }
    }

    /// <summary>
    /// 监听新打开的窗体并套用字体。
    /// 为什么用轮询而不是每个窗体加 Shown 事件：31 个窗体分散在 3 个程序集里，
    /// 轮询是唯一能一处覆盖全部窗体的办法；间隔 60ms，肉眼不可见。
    /// </summary>
    private static void StartWatcher()
    {
        if (_watcher != null) return;
        _watcher = new System.Windows.Forms.Timer { Interval = 60 };
        _watcher.Tick += (_, _) =>
        {
            try
            {
                foreach (var form in Application.OpenForms.Cast<Form>().ToList())
                {
                    if (form.IsDisposed) continue;
                    if (_appliedForms.Contains(form)) continue;
                    ApplyToForm(form);
                }
            }
            catch
            {
                // 轮询里绝不能抛
            }
        };
        _watcher.Start();
    }

    // ===== 应用 =====

    /// <summary>给单个窗体套用字体（幂等：重复调用不会把字号越缩越大）。</summary>
    public static void ApplyToForm(Form form, bool force = false)
    {
        if (form == null || form.IsDisposed) return;
        if (!force && _appliedForms.Contains(form)) return;
        _appliedForms.Add(form);

        if (_targetFamily.Length == 0 && _sizeScale == 1.0f) return;

        try
        {
            ApplyToControl(form);
            form.PerformLayout();
        }
        catch
        {
            // 单个窗体失败不影响其他
        }
    }

    private static void ApplyToControl(Control control)
    {
        ReplaceFont(control);
        foreach (Control child in control.Controls)
        {
            ApplyToControl(child);
        }
    }

    /// <summary>
    /// 替换单个控件的字体。
    /// ★ 幂等的关键：字号永远从「原始字号」换算，不是从当前字号换算 ——
    ///   否则重新应用一次就会再乘一次缩放系数。
    /// </summary>
    private static void ReplaceFont(Control control)
    {
        Font current;
        try { current = control.Font; } catch { return; }
        if (current == null) return;

        if (!_originalFonts.TryGetValue(control, out var spec))
        {
            string fam = current.FontFamily?.Name ?? string.Empty;
            spec = new FontSpec(fam, current.Size, current.Style);
            _originalFonts.Add(control, spec);
        }

        if (MonoFamilies.Contains(spec.Family) || SymbolFamilies.Contains(spec.Family)) return;

        float newSize = (float)Math.Round(spec.Size * _sizeScale, 1);
        if (newSize <= 0) newSize = spec.Size;

        string newFamily = _targetFamily.Length > 0 ? _targetFamily : spec.Family;
        if (newFamily.Length == 0) return;

        Font? target = BuildFont(newFamily, newSize, spec.Style);
        if (target == null) return;

        if (target.Name == current.Name
            && Math.Abs(target.Size - current.Size) < 0.01f
            && target.Style == current.Style)
        {
            return;
        }

        control.Font = target;
        EnsureTextFits(control);

        // ★ 2026-11-13：字号变了，文字是否放得下也变了 —— 重新判定悬浮提示
        ApplyFixedWidthTooltip(control);
    }

    /// <summary>
    /// ★ 2026-11-13：把控件标记为「固定宽度」。
    ///
    /// 标记后：
    ///   · 换字体时不会被自动加宽（保持统一宽度，排版整齐）
    ///   · 文字放不下时，自动挂上 ToolTip，悬浮显示完整文字
    ///
    /// 工具箱的默认工具 / 自定义工具卡片就是这么用的。
    /// </summary>
    public static void MarkFixedWidth(Control control)
    {
        if (control == null) return;
        _fixedWidthControls.Add(control);
    }

    /// <summary>是否为固定宽度控件（不参与自动加宽）</summary>
    private static bool IsFixedWidth(Control control)
        => _fixedWidthControls.Contains(control);

    /// <summary>共享的 ToolTip —— 固定宽度控件文字放不下时，用它悬浮显示全文</summary>
    private static ToolTip? _sharedToolTip;

    /// <summary>
    /// 共享 ToolTip 实例（懒创建）。
    /// 实测：ToolTip 是【实例作用域】的 —— t1 设的提示 t2 读不到，
    /// 所以用独立实例不会污染 Designer 里已写的提示（如 AccountDialog 的账号说明）。
    /// </summary>
    private static ToolTip SharedToolTip
    {
        get
        {
            if (_sharedToolTip == null)
            {
                _sharedToolTip = new ToolTip
                {
                    AutoPopDelay = 8000,
                    InitialDelay = 300,
                    ReshowDelay = 200,
                    ShowAlways = true,
                };
            }
            return _sharedToolTip;
        }
    }

    /// <summary>
    /// ★ 2026-11-13：固定宽度控件的文字放不下时，悬浮显示全文。
    ///
    /// 控件文字截断了但用户看不到后面是什么 —— 这是「显示不全」的真正问题。
    /// 宽度不动（排版整齐），内容交给悬浮提示。
    /// </summary>
    public static void ApplyFixedWidthTooltip(Control control)
    {
        try
        {
            if (!IsFixedWidth(control)) return;

            string? text = control switch
            {
                ButtonBase b => b.Text,
                Label l => l.Text,
                _ => null
            };
            if (string.IsNullOrEmpty(text)) return;

            if (TextOverflows(control, text!))
            {
                SharedToolTip.SetToolTip(control, text);
            }
            else
            {
                // 放得下就不要提示，免得鼠标一放上去就弹个没用的框
                SharedToolTip.SetToolTip(control, null);
            }
        }
        catch
        {
            // 提示失败不影响字体本身
        }
    }

    /// <summary>
    /// ★ 2026-11-13：给动态创建的控件套用当前界面字体与悬浮提示。
    ///
    /// 工具箱的卡片是运行时动态创建的（不在 Designer 里），
    /// 创建的那一刻还没有换字体 —— 那时判定「文字放不放得下」是不准的。
    /// 调用这个方法立即按当前字体设置判定一次。
    /// </summary>
    public static void ApplyToNewControl(Control control)
    {
        if (control == null || control.IsDisposed) return;
        try
        {
            ReplaceFont(control);
            ApplyFixedWidthTooltip(control);
        }
        catch
        {
            // 单个控件失败不影响其他
        }
    }

    /// <summary>
    /// ★ 2026-11-13：清掉固定宽度控件的悬浮提示。
    /// 控件即将被丢弃时必须调用 —— ToolTip 里还挂着引用会漏。
    /// </summary>
    public static void ClearFixedWidthTooltip(Control control)
    {
        if (control == null) return;
        try
        {
            _fixedWidthControls.Remove(control);
            if (_sharedToolTip != null) _sharedToolTip.SetToolTip(control, null);
        }
        catch
        {
            // 控件可能已释放
        }
    }

    /// <summary>
    /// 文字是否放不下（宽度或高度超出可用区域）。
    /// 多行文字（\n）逐行量宽、总高度也要算。
    /// </summary>
    private static bool TextOverflows(Control control, string text)
    {
        try
        {
            int maxLineWidth = 0;
            int totalHeight = 0;
            foreach (var line in text.Split('\n'))
            {
                var size = TextRenderer.MeasureText(line, control.Font);
                if (size.Width > maxLineWidth) maxLineWidth = size.Width;
                totalHeight += size.Height;
            }

            int availWidth = control.ClientSize.Width - 2;
            int availHeight = control.ClientSize.Height - 2;
            return maxLineWidth > availWidth || totalHeight > availHeight;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 换完字体后，若文字被截断且右侧有空间就自动加宽。
    /// 规则：只扩不缩；不越过右侧相邻控件；不越过父容器右边界。
    /// </summary>
    private static void EnsureTextFits(Control control)
    {
        try
        {
            if (control.AutoSize) return;   // AutoSize 的控件会自己重算
            if (IsFixedWidth(control)) return;   // ★ 固定宽度控件不加宽（改用 ToolTip）

            string? text = control switch
            {
                ButtonBase b => b.Text,
                Label l => l.Text,
                _ => null
            };
            if (string.IsNullOrEmpty(text)) return;

            int need = TextRenderer.MeasureText(text, control.Font).Width + 18;
            if (need <= control.Width) return;

            Control? parent = control.Parent;
            if (parent == null) return;

            int limit = parent.ClientSize.Width;

            // 右侧最近的兄弟控件 —— 不能压到它身上
            var next = parent.Controls.Cast<Control>()
                .Where(x => x != control && x.Visible && x.Left >= control.Left + control.Width)
                .OrderBy(x => x.Left)
                .FirstOrDefault();
            if (next != null && next.Left < limit) limit = next.Left;

            int maxWidth = limit - control.Left;
            if (maxWidth > control.Width && need <= maxWidth)
            {
                control.Width = need;
            }
        }
        catch
        {
            // 加宽失败就算了，不影响字体本身
        }
    }

    // ===== 字体构造 =====

    /// <summary>按族名 + 字号 + 样式构造字体（私有字体优先，样式不可用时降级）。</summary>
    private static Font? BuildFont(string familyName, float size, FontStyle style)
    {
        if (string.IsNullOrWhiteSpace(familyName)) return null;

        FontFamily? family = ResolveFamily(familyName);
        if (family == null) return null;

        if (!family.IsStyleAvailable(style))
        {
            // 依次降级：Regular → Bold → Italic → 任意可用样式
            style = family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular
                  : family.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold
                  : family.IsStyleAvailable(FontStyle.Italic) ? FontStyle.Italic
                  : FontStyle.Regular;
        }

        try
        {
            return new Font(family, size, style, GraphicsUnit.Point);
        }
        catch
        {
            try { return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point); }
            catch { return null; }
        }
    }

    /// <summary>族名 → FontFamily（同名时私有字体优先）。</summary>
    private static FontFamily? ResolveFamily(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName)) return null;

        // ★ 私有字体优先：msyh.ttc 里的「微软雅黑」和系统里的同名，
        //   用字符串 new FontFamily("微软雅黑") 拿到的是系统那份，必须靠集合对象才拿得到私有那份
        if (_privateFamilies.TryGetValue(familyName, out var priv)) return priv;

        if (_systemFamilies.TryGetValue(familyName, out var cached)) return cached;

        try
        {
            var family = new FontFamily(familyName);
            _systemFamilies[familyName] = family;
            return family;
        }
        catch
        {
            return null;
        }
    }

    // ===== 私有字体 =====

    /// <summary>
    /// 加载私有字体文件（TTF / OTF / TTC）。
    /// 返回是否成功；失败原因写入 LastError。
    /// </summary>
    public static bool LoadPrivateFontFile(string path)
    {
        LastError = string.Empty;
        _privateFamilies.Clear();

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        if (!System.IO.File.Exists(path))
        {
            LastError = $"字体文件不存在：{path}";
            return false;
        }

        try
        {
            var collection = new PrivateFontCollection();
            collection.AddFontFile(path);

            // ★ 旧集合不能 Dispose，也不能丢引用（否则会被 GC 终结 → GDI 卸载字体
            //   → 还在用旧字体的控件突然变回系统字体）。一律保活到进程结束。
            if (_privateFonts != null) _retiredFonts.Add(_privateFonts);
            _privateFonts = collection;

            foreach (var family in collection.Families)
            {
                _privateFamilies[family.Name] = family;
            }

            if (_privateFamilies.Count == 0)
            {
                LastError = "字体文件里没有可用的字体族";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"字体文件加载失败：{ex.Message}";
            return false;
        }
    }

    /// <summary>私有字体文件里包含的族名（供设置页提示）</summary>
    public static List<string> GetPrivateFontFamilies() => _privateFamilies.Keys.ToList();

    // ===== 可选字体列表 =====

    /// <summary>系统已装字体 + 已加载的私有字体（私有字体排前面）。</summary>
    public static List<string> GetAvailableFamilies()
    {
        var system = new List<string>();
        try
        {
            using var installed = new InstalledFontCollection();
            foreach (var family in installed.Families)
            {
                system.Add(family.Name);
            }
        }
        catch
        {
            // 取不到就用空列表，至少设置页还能用私有字体
        }

        system.Sort(StringComparer.CurrentCultureIgnoreCase);

        var result = new List<string>();
        foreach (var name in _privateFamilies.Keys)
        {
            if (!result.Contains(name)) result.Add(name);
        }
        foreach (var name in system)
        {
            if (!result.Contains(name)) result.Add(name);
        }
        return result;
    }

    /// <summary>
    /// 给 WebView2 页面用的 font-family 值。
    /// 私有字体在网页里用不了（浏览器进程不认识 GDI 私有字体），
    /// 所以这里只给族名，页面会回退到系统字体。
    /// </summary>
    public static string GetCssFontFamily()
    {
        if (_targetFamily.Length == 0) return "'Microsoft YaHei UI', 'Segoe UI', sans-serif";
        return $"'{_targetFamily}', 'Microsoft YaHei UI', 'Segoe UI', sans-serif";
    }

    /// <summary>给 WebView2 页面用的字号缩放系数。</summary>
    public static float GetCssSizeScale() => _sizeScale;

    /// <summary>把缩放系数收敛到合理区间（0.8 ~ 1.5）。</summary>
    private static float NormalizeScale(float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale)) return 1.0f;
        if (scale < 0.8f) return 0.8f;
        if (scale > 1.5f) return 1.5f;
        return scale;
    }
}
