using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Forms;

/// <summary>
/// 账套字段设置窗体（2026-10-28 陛下需求）。
///
/// 需求：账套管理增加设置页面，可以单独设置每个账套字段
///   ① 是否显示在列表   ② 列表宽度   ③ 列表排序
///   ④ 是否显示在录入界面   ⑤ 录入界面排序
///
/// 实现要点：
///   · 字段清单来自 <see cref="AccountFieldCatalog"/>（单一数据源），本窗体只负责编辑与校验；
///   · 编辑期用「行内控件」直接改表格单元格，保存时读回 → 交给
///     <see cref="AccountFieldResolver.Normalize"/> 归一化（排序号整理成 0..n-1、纠正非法值）；
///   · 密码类字段禁止进列表（防明文泄露）；代码/账套名称必填、不能从录入界面隐藏 —— 这两条
///     由 Normalize 兜底强制，界面上也同步置灰，避免用户填了却没生效。
/// </summary>
public partial class AccountFieldSettingsForm : Form
{
    private readonly AccountFieldStore _store = new();

    /// <summary>当前编辑中的配置（与表格行一一对应）</summary>
    private List<AccountFieldConfig> _editing = new();

    public AccountFieldSettingsForm()
    {
        InitializeComponent();

        this.dgvFields.CellValueChanged += DgvFields_CellValueChanged;
        this.dgvFields.CellFormatting += DgvFields_CellFormatting;
        this.dgvFields.DataError += DgvFields_DataError;
        this.btnSave.Click += BtnSave_Click;
        this.btnCancel.Click += BtnCancel_Click;
        this.btnReset.Click += BtnReset_Click;

        LoadFromStore();
    }

    /// <summary>从配置文件加载（读不到就用默认值）</summary>
    private void LoadFromStore()
    {
        _editing = _store.Load();
        if (_editing == null || _editing.Count == 0)
            _editing = AccountFieldCatalog.CreateDefaults();

        BindRows();
        this.lblStatus.Text = $"已加载 {_editing.Count} 个账套字段";
    }

    /// <summary>把当前配置刷进表格</summary>
    private void BindRows()
    {
        this.dgvFields.SuspendLayout();
        this.dgvFields.Rows.Clear();

        foreach (var cfg in _editing)
        {
            int idx = this.dgvFields.Rows.Add(
                cfg.DisplayName,
                cfg.ShowInList,
                cfg.ListWidth,
                cfg.ListOrder,
                cfg.ShowInEditor,
                cfg.EditorOrder);

            // 行号 → 字段，便于事件里定位
            this.dgvFields.Rows[idx].Tag = cfg.Field;
        }

        this.dgvFields.ResumeLayout();
        this.dgvFields.ClearSelection();
    }

    /// <summary>取某行对应的字段配置</summary>
    private AccountFieldConfig? GetConfigOfRow(DataGridViewRow row)
    {
        string? field = row.Tag as string;
        if (string.IsNullOrEmpty(field)) return null;
        return _editing.FirstOrDefault(c => string.Equals(c.Field, field, StringComparison.OrdinalIgnoreCase));
    }

    private void DgvFields_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= this.dgvFields.Rows.Count) return;

        var row = this.dgvFields.Rows[e.RowIndex];
        var cfg = GetConfigOfRow(row);
        if (cfg == null) return;

        // 密码类字段不能进列表 → 用户勾了也强制取消
        if (e.ColumnIndex == this.dgvFields.Columns["ColShowList"].Index && !cfg.CanShowInList)
        {
            row.Cells["ColShowList"].Value = false;
            this.lblStatus.Text = $"{cfg.DisplayName} 是密码类字段，不能显示在列表";
            return;
        }

        // 必填字段不能从录入界面隐藏
        if (e.ColumnIndex == this.dgvFields.Columns["ColShowEditor"].Index && cfg.Required)
        {
            row.Cells["ColShowEditor"].Value = true;
            this.lblStatus.Text = $"{cfg.DisplayName} 是必填项，不能从录入界面隐藏";
            return;
        }
    }

    private void DgvFields_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= this.dgvFields.Rows.Count) return;

        var cfg = GetConfigOfRow(this.dgvFields.Rows[e.RowIndex]);
        if (cfg == null) return;

        // 不可改的单元格置灰
        bool readOnly = false;
        if (e.ColumnIndex == this.dgvFields.Columns["ColShowList"].Index && !cfg.CanShowInList) readOnly = true;
        if (e.ColumnIndex == this.dgvFields.Columns["ColShowEditor"].Index && cfg.Required) readOnly = true;
        if (e.ColumnIndex == this.dgvFields.Columns["ColEditorOrder"].Index && cfg.EditorOrderFixed) readOnly = true;

        if (readOnly)
        {
            e.CellStyle.ForeColor = System.Drawing.Color.FromArgb(160, 160, 160);
            e.CellStyle.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
        }
    }

    /// <summary>输入非法数字（比如宽度填了「abc」）→ 取消这次编辑，不让脏值进内存</summary>
    private void DgvFields_DataError(object? sender, DataGridViewDataErrorEventArgs e)
    {
        e.ThrowException = false;
        e.Cancel = true;
        this.lblStatus.Text = "输入无效：请填非负整数（列表宽度 0 表示自动）";
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        this.dgvFields.EndEdit();

        // 从表格读回当前值
        foreach (DataGridViewRow row in this.dgvFields.Rows)
        {
            var cfg = GetConfigOfRow(row);
            if (cfg == null) continue;

            cfg.ShowInList = ToBool(row.Cells["ColShowList"].Value);
            cfg.ListWidth = ToInt(row.Cells["ColListWidth"].Value, cfg.ListWidth);
            cfg.ListOrder = ToInt(row.Cells["ColListOrder"].Value, cfg.ListOrder);
            cfg.ShowInEditor = ToBool(row.Cells["ColShowEditor"].Value);
            cfg.EditorOrder = ToInt(row.Cells["ColEditorOrder"].Value, cfg.EditorOrder);
        }

        // 归一化 + 强制约束（密码不进列表 / 必填不隐藏 / 排序号整理 / 宽度钳制）
        var normalized = AccountFieldResolver.Normalize(_editing);

        // 至少要有一个列表字段，否则表格空了没法用
        if (!normalized.Any(c => c.ShowInList && c.CanShowInList))
        {
            MessageBox.Show("至少要保留一个显示在列表的字段。", "无法保存",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _store.Save(normalized);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "无法保存",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        this.lblStatus.Text = "已保存";
        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }

    private void BtnReset_Click(object? sender, EventArgs e)
    {
        var dr = MessageBox.Show("恢复默认会清空所有自定义（显示/宽度/排序）。\n确定要恢复吗？", "恢复默认",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (dr != DialogResult.Yes) return;

        _store.Reset();
        LoadFromStore();
        this.lblStatus.Text = "已恢复默认，点击保存后生效";
    }

    private static bool ToBool(object? value)
    {
        if (value is bool b) return b;
        return bool.TryParse(value?.ToString(), out var parsed) && parsed;
    }

    private static int ToInt(object? value, int fallback)
    {
        if (value is int i) return i;
        if (value == null) return fallback;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
    }
}
