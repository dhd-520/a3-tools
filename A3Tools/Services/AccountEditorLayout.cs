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
/// ★ 随行说明（Note）：
///   某些字段带一句静态说明（如「账套用户名」下面的自动登录配置说明），
///   说明文字排在该字段行的下面一行，跟着字段一起显示/隐藏。
/// </summary>
public sealed class AccountEditorLayout
{
    /// <summary>字段与说明文字之间的间隙（像素）</summary>
    private const int NoteGap = 4;

    /// <summary>一个字段在录入窗体里的两个控件（标签 + 输入框）+ 可选的随行说明</summary>
    public sealed class Slot
    {
        public Label Label { get; }
        public Control Input { get; }

        /// <summary>随行说明（静态提示文字），排在该字段行的下面一行；null = 没有说明</summary>
        public Control? Note { get; }

        public Slot(Label label, Control input, Control? note = null)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Note = note;
        }
    }

    private readonly IDictionary<string, Slot> _slots;
    private readonly Panel? _proxyGroup;
    private readonly int _startY;

    /// <summary>分组面板之间的间距（像素）</summary>
    private const int GroupGap = 10;

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

            // 随行说明：紧跟在字段行下面
            if (slot.Note != null)
            {
                slot.Note.Top = y + NoteGap;
                slot.Note.Visible = true;
                y = slot.Note.Top + slot.Note.Height + NoteGap;
            }
        }

        // 其余字段隐藏（带说明的，说明文字一起隐藏）
        foreach (var kv in _slots)
        {
            if (flowFields.Contains(kv.Key)) continue;

            var cfg = config.FirstOrDefault(c => string.Equals(c.Field, kv.Key, StringComparison.OrdinalIgnoreCase));
            bool show = cfg?.ShowInEditor == true;
            kv.Value.Label.Visible = show;
            kv.Value.Input.Visible = show;
            if (kv.Value.Note != null) kv.Value.Note.Visible = show;
        }

        // 「代理模式」分组固定在字段流末尾，再往下 10px
        if (_proxyGroup != null) _proxyGroup.Top = y + GroupGap;
    }
}
