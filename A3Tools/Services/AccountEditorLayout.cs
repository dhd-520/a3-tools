using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// 账套录入界面（AccountDialog）的字段排版引擎 —— 纯布局逻辑，与控件定义分离，便于单测。
///
/// 需求（2026-10-28 陛下）：账套字段设置页可设置「是否显示在录入界面」和「录入界面排序」。
///
/// 做法：**不动态创建控件**，只用设计器里已经定义好的 Label/输入框，
///   按配置重新摆放位置（Top）并设置可见性。
///   控件的 X 坐标 / 宽高 / 字体等仍完全由 AccountDialog.Designer.cs 决定（所见即所得）。
///
/// 排版规则（与 AccountDialog.Designer.cs 里的原始布局逐像素一致）：
///   · 起始 Y = 20，每行行距 45px；
///   · 「远程方式」前额外留白 10px（原来的分组间距）；
///   · 「备注」是多行框，占 80px；
///   · 「代理模式」分组固定在字段流末尾，再往下 10px。
///
/// ★ 固定字段（2026-10-29 陛下：只固定代码就行）：
///   「代码」是唯一的固定字段 —— 永远排第一，排序号不可改，也不可隐藏。
///   其余字段（含「账套用户名」「账套名称」）全部可排序、可显隐。
///
/// ★ 随行说明（Note）—— 已于 2026-10-31 移除：
///   原来支持「某个字段下面跟一句静态说明」（当年只有「账套用户名」下面的自动登录配置提示）。
///   但字段可以自由排序，一排序那行说明就夹在别的字段中间、看着像那个字段的组表头
///   （陛下当时看到的就是它夹在「账套密码」上面）→ 已从界面去掉。
///   说明文字改挂到输入框的悬浮提示里（Designer 的 toolTipServerUsername）。
///   所以这里不再有 Note 概念：每个字段就是「标签 + 输入框」两行，排版更干净。
/// </summary>
public sealed class AccountEditorLayout
{
    /// <summary>一个字段在录入窗体里的两个控件（标签 + 输入框）</summary>
    public sealed class Slot
    {
        public Label Label { get; }
        public Control Input { get; }

        public Slot(Label label, Control input)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Input = input ?? throw new ArgumentNullException(nameof(input));
        }
    }

    private readonly IDictionary<string, Slot> _slots;
    private readonly Panel? _proxyGroup;
    private readonly int _startY;

    /// <summary>分组面板之间的间距（像素）</summary>
    private const int GroupGap = 10;

    /// <summary>
    /// 内容区左右边距（像素）——设计器里是 30；运行时实际值由 ApplyColumns 的 margin 参数带入
    /// （后者已经被窗体 AutoScale 缩放过）。
    /// </summary>
    public const int SideMargin = 30;

    /// <summary>
    /// 标题列占「可用宽度」的比例 = 1/4（陛下要求，2026-11-05）。
    /// </summary>
    public const double LabelWidthRatio = 0.25;

    /// <summary>标题列与输入列之间的间距（设计器里是 10）</summary>
    public const int ColumnGap = 10;

    /// <summary>
    /// 按内容区实际宽度重排「标题列 + 输入列」（2026-11-05 陛下）。
    ///
    /// ★ 宽度按百分比：标题占可用宽度的 1/4、输入框占剩下的 3/4。
    ///   所以换分辨率、换缩放、甚至拖动窗体大小，字段行都保持对齐。
    ///
    /// ★ 内置字段和自定义字段走的是同一段代码 → 结构上不可能再错位。
    ///   （以前是两套几何来源：内置的来自设计器、自定义的写死一份，缩放后必然分叉）
    ///
    /// ★ margin / gap 取自设计器「代码」字段的运行时真实值：
    ///   设计器 30 / 210 / 250 → margin=30、gap=10；
    ///   窗体 AutoScale 之后这两个值也跟着缩放（本机实测 17 / 6，
    ///   缩放系数逐机不同）→ 任何 DPI 下都对得上。
    /// </summary>
    public void ApplyColumns(int contentWidth, int margin, int gap)
    {
        if (contentWidth <= 0) return;

        int usable = Math.Max(0, contentWidth - margin * 2);
        int labelWidth = (int)Math.Round(usable * LabelWidthRatio);
        int inputWidth = Math.Max(0, usable - labelWidth - gap);

        foreach (var kv in _slots)
        {
            var label = kv.Value.Label;
            var input = kv.Value.Input;

            // 标题：固定列宽 + 右对齐 → 标题文字始终紧贴输入框（与设计器一致）
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleRight;
            label.Left = margin;
            label.Width = labelWidth;

            // 输入框
            input.Left = margin + labelWidth + gap;
            input.Width = inputWidth;
        }

        // ★ 不动「代理模式」分组：它内部的控件是设计器里的固定坐标布局
        //   （密钥/公钥输入框、按钮都在写死的位置上），改宽度会被裁切。
        //   它由窗体 AutoScale 统一缩放，跟其他设计器控件保持一致。
    }

    /// <summary>
    /// 构造排版引擎。
    /// </summary>
    /// <param name="slots">字段名 → (标签, 输入框[, 随行说明])</param>
    /// <param name="proxyGroup">「代理模式」分组面板（固定在字段流末尾）</param>
    /// <param name="startY">第一个字段的起始 Y 坐标</param>
    public AccountEditorLayout(
        IDictionary<string, Slot> slots,
        Panel? proxyGroup = null,
        int startY = 20)
    {
        _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        _proxyGroup = proxyGroup;
        _startY = startY;
    }

    /// <summary>
    /// 按配置重排录入界面的字段：设置可见性 + 重新摆放位置。
    /// </summary>
    /// <param name="config">已归一化的字段配置</param>
    public void Apply(List<AccountFieldConfig> config)
    {
        if (config == null) return;

        // 可见且参与重排的字段（固定字段已在 GetEditorFields 里排到最前）
        var flow = AccountFieldResolver.GetEditorFields(config);
        var flowFields = new HashSet<string>(flow.Select(f => f.Field), StringComparer.Ordinal);

        int y = _startY;
        foreach (var f in flow)
        {
            if (!_slots.TryGetValue(f.Field, out var slot)) continue;

            var def = AccountFieldCatalog.Find(f.Field);
            int gap = def?.EditorGapBefore ?? 0;
            int advance = def?.EditorRowAdvance ?? 45;

            y += gap;
            slot.Label.Top = y;
            slot.Input.Top = y;
            slot.Label.Visible = true;
            slot.Input.Visible = true;

            y += advance;
        }

        // 其余字段隐藏
        foreach (var kv in _slots)
        {
            if (flowFields.Contains(kv.Key)) continue;

            var cfg = config.FirstOrDefault(c => string.Equals(c.Field, kv.Key, StringComparison.OrdinalIgnoreCase));
            bool show = cfg?.ShowInEditor == true;
            kv.Value.Label.Visible = show;
            kv.Value.Input.Visible = show;
        }

        // 「代理模式」分组固定在字段流末尾，再往下 10px
        if (_proxyGroup != null) _proxyGroup.Top = y + GroupGap;
    }
}
