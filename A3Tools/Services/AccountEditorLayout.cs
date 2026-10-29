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
///   · 排完所有字段后，「网页版自动登录」分组贴到最后一个字段下面，
///     「代理模式」分组再往下 10px。
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
    private readonly Panel? _webGroup;
    private readonly Panel? _proxyGroup;
    private readonly int _startY;

    /// <summary>两个分组面板之间的间距（像素）</summary>
    private const int GroupGap = 10;

    /// <summary>
    /// 构造排版引擎。
    /// </summary>
    /// <param name="slots">字段名 → (标签, 输入框)</param>
    /// <param name="webGroup">「网页版自动登录」分组面板（跟在字段流下面）</param>
    /// <param name="proxyGroup">「代理模式」分组面板（再往下）</param>
    /// <param name="startY">第一个字段的起始 Y 坐标</param>
    public AccountEditorLayout(
        IDictionary<string, Slot> slots,
        Panel? webGroup = null,
        Panel? proxyGroup = null,
        int startY = 20)
    {
        _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        _webGroup = webGroup;
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

        // 可见且参与重排的字段（按录入排序）
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

        // 其余字段隐藏（分组内字段由自己的 ShowInEditor 决定）
        foreach (var kv in _slots)
        {
            if (flowFields.Contains(kv.Key)) continue;

            var cfg = config.FirstOrDefault(c => string.Equals(c.Field, kv.Key, StringComparison.OrdinalIgnoreCase));
            bool show = cfg?.ShowInEditor == true;
            kv.Value.Label.Visible = show;
            kv.Value.Input.Visible = show;
        }

        // 分组面板跟到最后一个字段下面
        if (_webGroup != null) _webGroup.Top = y;
        if (_proxyGroup != null) _proxyGroup.Top = y + (_webGroup?.Height ?? 0) + GroupGap;
    }
}
