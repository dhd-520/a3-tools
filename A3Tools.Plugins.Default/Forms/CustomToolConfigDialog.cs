using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// 自定义工具配置窗体。
/// 字段：工具名 / 描述 / 主表 / 复制关键字（主键） / 关联表（分号分隔）/ 关联字段（外键，可分号分隔）。
/// 关联字段规则：填 1 个 → 所有关联表共用；填多个 → 与关联表按顺序一一对应。
/// 保存到 DATA\custom-tools.json；编辑现有配置时显示「删除」按钮。
/// </summary>
public partial class CustomToolConfigDialog : Form
{
    private readonly CustomToolStorage _storage = new();
    private readonly bool _isEdit;

    /// <summary>窗体关闭后回写的配置（用户点保存才有值，否则为 null）</summary>
    public CustomToolConfig? Result { get; private set; }

    public CustomToolConfigDialog(CustomToolConfig? existing)
    {
        _isEdit = existing != null;
        Result = existing;
        InitializeComponent();

        this.KeyPreview = true;
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter && !txtRelatedTables.ContainsFocus && !txtDescription.ContainsFocus
                     && !txtSecondaryTables.ContainsFocus && !txtSecondaryForeignKey.ContainsFocus)
            { BtnSave_Click(this, EventArgs.Empty); e.SuppressKeyPress = true; }
        };

        if (_isEdit && existing != null)
        {
            txtName.Text = existing.Name;
            txtDescription.Text = existing.Description;
            txtMainTable.Text = existing.MainTable;
            txtPrimaryKey.Text = existing.PrimaryKey;
            txtRelatedTables.Text = existing.RelatedTables;
            txtForeignKey.Text = existing.ForeignKey;
            txtSecondaryTables.Text = existing.SecondaryRelatedTables;
            txtSecondaryForeignKey.Text = existing.SecondaryForeignKey;
            txtSecondaryParentKey.Text = existing.SecondaryParentKey;
            txtSearchColumns.Text = existing.SearchColumns;
            txtColumnDisplayNames.Text = existing.ColumnDisplayNames;
            txtHiddenColumns.Text = existing.HiddenColumns;
            this.Text = $"编辑自定义工具 — {existing.Name}";
        }
        else
        {
            this.Text = "新建自定义工具";
        }

        // 默认焦点：不能在构造函数里直接 BeginInvoke，窗体句柄还没创建。
        // 等 Shown 后句柄已存在，再聚焦，避免“在创建窗口句柄之前，不能在控件上调用 Invoke 或 BeginInvoke”。
        Shown += (s, e) => txtName.Focus();
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // 校验
        var name = txtName.Text.Trim();
        var main = txtMainTable.Text.Trim();
        var pk = txtPrimaryKey.Text.Trim();
        var related = txtRelatedTables.Text.Trim();
        var fk = txtForeignKey.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("请填写工具名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtName.Focus();
            return;
        }
        if (string.IsNullOrEmpty(main))
        {
            MessageBox.Show("请填写主表", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtMainTable.Focus();
            return;
        }
        if (string.IsNullOrEmpty(pk))
        {
            MessageBox.Show("请填写复制关键字（主键字段）", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtPrimaryKey.Focus();
            return;
        }
        // 关联表和关联字段要么都填要么都不填
        bool hasRelated = !string.IsNullOrEmpty(related);
        bool hasFk = !string.IsNullOrEmpty(fk);
        if (hasRelated != hasFk)
        {
            MessageBox.Show("关联表和关联字段要么都填写，要么都不填写", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 关联字段：只填 1 个 → 所有关联表共用；填多个 → 必须与关联表数量一致、一一对应
        var fkResolution = CustomToolForeignKeyResolver.Resolve(related, fk);
        if (!fkResolution.IsValid)
        {
            MessageBox.Show(fkResolution.Error!, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtForeignKey.Focus();
            return;
        }

        // 二级关联表/字段：分组与数量必须对得上
        var secTables = txtSecondaryTables.Text.Trim();
        var secFields = txtSecondaryForeignKey.Text.Trim();
        var secParentKey = txtSecondaryParentKey.Text.Trim();
        var secResolution = CustomToolSecondaryLinkResolver.Resolve(related, fk, secTables, secFields);
        if (!secResolution.IsValid)
        {
            MessageBox.Show(secResolution.Error!, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtSecondaryTables.Focus();
            return;
        }

        // 搜索列设置校验
        var searchCols = txtSearchColumns.Text.Trim();
        var displayNames = txtColumnDisplayNames.Text.Trim();
        var hiddenCols = txtHiddenColumns.Text.Trim();
        if (!string.IsNullOrEmpty(searchCols) || !string.IsNullOrEmpty(displayNames))
        {
            if (string.IsNullOrEmpty(searchCols))
            {
                MessageBox.Show("请填写搜索列名，或与列显示名称一起留空（保持旧行为）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSearchColumns.Focus();
                return;
            }
            if (string.IsNullOrEmpty(displayNames))
            {
                MessageBox.Show("请填写列显示名称，或与搜索列名一起留空（保持旧行为）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtColumnDisplayNames.Focus();
                return;
            }
            var searchList = searchCols.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var displayList = displayNames.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (searchList.Length != displayList.Length)
            {
                MessageBox.Show($"搜索列名与列显示名称数量不一致（{searchList.Length} vs {displayList.Length}）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtColumnDisplayNames.Focus();
                return;
            }
            if (!searchList.Contains(pk, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show($"搜索列名必须包含复制关键字【{pk}】（即便在隐藏列中也会强制显示）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSearchColumns.Focus();
                return;
            }
        }

        var config = _isEdit && Result != null
            ? Result
            : new CustomToolConfig();

        config.Name = name;
        config.Description = txtDescription.Text.Trim();
        config.MainTable = main;
        config.PrimaryKey = pk;
        config.RelatedTables = related;
        config.ForeignKey = fk;
        config.SecondaryRelatedTables = secTables;
        config.SecondaryForeignKey = secFields;
        config.SecondaryParentKey = secParentKey;
        config.SearchColumns = searchCols;
        config.ColumnDisplayNames = displayNames;
        config.HiddenColumns = hiddenCols;

        try
        {
            _storage.Upsert(config);
            Result = config;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}