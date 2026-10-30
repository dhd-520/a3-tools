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
        this.dgvFields.CellEndEdit += DgvFields_CellEndEdit;
        this.dgvFields.CellFormatting += DgvFields_CellFormatting;
        this.dgvFields.DataError += DgvFields_DataError;
        this.btnSave.Click += BtnSave_Click;
        this.btnCancel.Click += BtnCancel_Click;
        this.btnReset.Click += BtnReset_Click;
        this.btnAddField.Click += BtnAddField_Click;
        this.btnDeleteField.Click += BtnDeleteField_Click;

        LoadFromStore();
    }

    /// <summary>从配置文件加载（读不到就用默认值）</summary>
    private void LoadFromStore()
    {
        _editing = _store.Load();

        // ⚠️ 配置损坏时必须告知用户（否则界面只显示默认字段，用户可能误以为没配过而覆盖原文件）
        if (_store.LastError != null)
        {
            MessageBox.Show(
                $"账套字段配置读取失败，已保留原文件副本，下面显示的是默认配置。\n\n{_store.LastError}\n\n" +
                "若继续保存会覆盖原配置，请先确认或从备份文件恢复。",
                "账套字段配置警告",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

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
                AccountFieldCatalog.IsBuiltIn(cfg.Field) ? "内置" : "自定义",
                cfg.ShowInList,
                cfg.ListWidth,
                cfg.ListOrder + 1,     // 表格里用 1 基序号（1 = 最前），人看着直观
                cfg.ShowInEditor,
                cfg.EditorOrder + 1);

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

    /// <summary>
    /// 排序号格编辑完毕 → 按「位置语义」重排：填几就是第几位，其余字段自动顺延。
    /// （否则排序号只是排序键：填 3 可能被稳定排序吃掉，用户的意图落空。）
    /// </summary>
    private void DgvFields_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= this.dgvFields.Rows.Count) return;

        bool isListOrder = e.ColumnIndex == this.dgvFields.Columns["ColListOrder"].Index;
        bool isEditorOrder = e.ColumnIndex == this.dgvFields.Columns["ColEditorOrder"].Index;
        if (!isListOrder && !isEditorOrder) return;

        var row = this.dgvFields.Rows[e.RowIndex];
        var cfg = GetConfigOfRow(row);
        if (cfg == null) return;

        string colName = isListOrder ? "ColListOrder" : "ColEditorOrder";
        int newPos = ToInt(row.Cells[colName].Value, 1);
        if (newPos < 1) newPos = 1;

        // 先把表格里的新序号写回内存，再让解析器重排（重排结果会回填到所有行）
        if (isListOrder) cfg.ListOrder = newPos - 1;
        else cfg.EditorOrder = newPos - 1;

        AccountFieldResolver.MoveTo(_editing, cfg.Field, newPos, isEditorOrder);

        // 回填所有行的序号（1 基）
        foreach (DataGridViewRow r in this.dgvFields.Rows)
        {
            var c = GetConfigOfRow(r);
            if (c == null) continue;
            r.Cells["ColListOrder"].Value = c.ListOrder + 1;
            r.Cells["ColEditorOrder"].Value = c.EditorOrder + 1;
        }

        this.lblStatus.Text = $"{cfg.DisplayName} 已移到第 {newPos} 位";
    }

    private void DgvFields_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= this.dgvFields.Rows.Count) return;

        var cfg = GetConfigOfRow(this.dgvFields.Rows[e.RowIndex]);
        if (cfg == null) return;

        // 不可改的单元格置灰（固定字段：代码 —— 不可隐藏、录入界面永远排第一）
        bool readOnly = false;
        if (e.ColumnIndex == this.dgvFields.Columns["ColShowList"].Index && !cfg.CanShowInList) readOnly = true;
        if (e.ColumnIndex == this.dgvFields.Columns["ColShowEditor"].Index && cfg.Required) readOnly = true;
        if (e.ColumnIndex == this.dgvFields.Columns["ColEditorOrder"].Index && cfg.Required) readOnly = true;

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
            cfg.ListOrder = ToInt(row.Cells["ColListOrder"].Value, cfg.ListOrder + 1) - 1;
            cfg.ShowInEditor = ToBool(row.Cells["ColShowEditor"].Value);
            cfg.EditorOrder = ToInt(row.Cells["ColEditorOrder"].Value, cfg.EditorOrder + 1) - 1;
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

    /// <summary>
    /// 新增自定义字段（2026-10-29 陛下：内置字段不能删，自定义字段可增可删）。
    /// 自定义字段的键形如 "Cust_1751..."，与内置字段不会撞。
    /// </summary>
    private void BtnAddField_Click(object? sender, EventArgs e)
    {
        string name;
        using (var dlg = new PromptInputForm("新增自定义字段", "请输入字段名称（例如：门店编号）"))
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            name = dlg.InputText.Trim();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("字段名称不能为空。", "新增字段", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 重名检查（自定义字段之间 + 不能和内置字段同名）
        bool dup = _editing.Any(c =>
            string.Equals(c.DisplayName, name, StringComparison.OrdinalIgnoreCase));
        if (dup)
        {
            MessageBox.Show($"已经有叫「{name}」的字段了，换个名字。", "新增字段",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int maxList = _editing.Count == 0 ? 0 : _editing.Max(c => c.ListOrder) + 1;
        int maxEditor = _editing.Count == 0 ? 0 : _editing.Max(c => c.EditorOrder) + 1;

        var custom = new AccountFieldConfig
        {
            Field = "Cust_" + DateTime.Now.Ticks,
            CustomName = name,
            DisplayName = name,
            ListHeader = name,
            ShowInList = true,
            ListWidth = 0,
            ListOrder = maxList,
            ShowInEditor = true,
            EditorOrder = maxEditor,
            CanShowInList = true,
            ListWeight = 20,
            EditorRowAdvance = 45,
            Required = false,
        };

        _editing.Add(custom);
        BindRows();
        this.lblStatus.Text = $"已新增自定义字段「{name}」，点击保存后生效";
    }

    /// <summary>
    /// 删除自定义字段（内置字段不可删）。
    /// </summary>
    private void BtnDeleteField_Click(object? sender, EventArgs e)
    {
        if (this.dgvFields.CurrentRow == null)
        {
            MessageBox.Show("请先选中要删除的字段。", "删除字段", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var cfg = GetConfigOfRow(this.dgvFields.CurrentRow);
        if (cfg == null) return;

        // 内置字段不能删（删了账套数据就缺字段了）
        if (AccountFieldCatalog.IsBuiltIn(cfg.Field))
        {
            MessageBox.Show($"「{cfg.DisplayName}」是内置字段，不能删除。\n只有你自己新增的自定义字段可以删。",
                "删除字段", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var dr = MessageBox.Show($"确定删除自定义字段「{cfg.DisplayName}」吗？\n（已录入的值会留在数据里，但不再显示）",
            "删除字段", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (dr != DialogResult.Yes) return;

        _editing.RemoveAll(c => string.Equals(c.Field, cfg.Field, StringComparison.OrdinalIgnoreCase));
        BindRows();
        this.lblStatus.Text = $"已删除自定义字段「{cfg.DisplayName}」，点击保存后生效";
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
