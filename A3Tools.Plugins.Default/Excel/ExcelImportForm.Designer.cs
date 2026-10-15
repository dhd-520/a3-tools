namespace A3Tools.Plugins.Default.Excel;

partial class ExcelImportForm
{
    private System.ComponentModel.IContainer? components = null;

    private System.Windows.Forms.Label lblFile = null!;
    private System.Windows.Forms.TextBox txtFile = null!;
    private System.Windows.Forms.Button btnBrowse = null!;
    private System.Windows.Forms.Label lblSheet = null!;
    private System.Windows.Forms.ComboBox cmbSheet = null!;
    private System.Windows.Forms.Label lblAlias = null!;
    private System.Windows.Forms.TextBox txtAlias = null!;
    private System.Windows.Forms.Label lblMapping = null!;
    private System.Windows.Forms.DataGridView dgvMapping = null!;
    private System.Windows.Forms.Label lblPreview = null!;
    private System.Windows.Forms.DataGridView dgvPreview = null!;
    private System.Windows.Forms.Button btnImport = null!;
    private System.Windows.Forms.Button btnCancel = null!;
    private System.Windows.Forms.StatusStrip statusStrip = null!;
    private System.Windows.Forms.ToolStripStatusLabel lblStatus = null!;

    private System.Windows.Forms.DataGridViewTextBoxColumn colExcelName = null!;
    private System.Windows.Forms.DataGridViewTextBoxColumn colTargetName = null!;
    private System.Windows.Forms.DataGridViewComboBoxColumn colType = null!;
    private System.Windows.Forms.DataGridViewTextBoxColumn colSample1 = null!;
    private System.Windows.Forms.DataGridViewTextBoxColumn colSample2 = null!;
    private System.Windows.Forms.DataGridViewTextBoxColumn colSample3 = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        lblFile = new System.Windows.Forms.Label();
        txtFile = new System.Windows.Forms.TextBox();
        btnBrowse = new System.Windows.Forms.Button();
        lblSheet = new System.Windows.Forms.Label();
        cmbSheet = new System.Windows.Forms.ComboBox();
        lblAlias = new System.Windows.Forms.Label();
        txtAlias = new System.Windows.Forms.TextBox();
        lblMapping = new System.Windows.Forms.Label();
        dgvMapping = new System.Windows.Forms.DataGridView();
        lblPreview = new System.Windows.Forms.Label();
        dgvPreview = new System.Windows.Forms.DataGridView();
        btnImport = new System.Windows.Forms.Button();
        btnCancel = new System.Windows.Forms.Button();
        statusStrip = new System.Windows.Forms.StatusStrip();
        lblStatus = new System.Windows.Forms.ToolStripStatusLabel();

        colExcelName = new System.Windows.Forms.DataGridViewTextBoxColumn();
        colTargetName = new System.Windows.Forms.DataGridViewTextBoxColumn();
        colType = new System.Windows.Forms.DataGridViewComboBoxColumn();
        colSample1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
        colSample2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
        colSample3 = new System.Windows.Forms.DataGridViewTextBoxColumn();

        ((System.ComponentModel.ISupportInitialize)dgvMapping).BeginInit();
        ((System.ComponentModel.ISupportInitialize)dgvPreview).BeginInit();
        statusStrip.SuspendLayout();
        SuspendLayout();

        // ===== lblFile =====
        lblFile.AutoSize = true;
        lblFile.Location = new System.Drawing.Point(12, 15);
        lblFile.Name = "lblFile";
        lblFile.Size = new System.Drawing.Size(70, 17);
        lblFile.TabIndex = 0;
        lblFile.Text = "Excel 文件:";

        // ===== txtFile =====
        txtFile.Location = new System.Drawing.Point(88, 12);
        txtFile.Name = "txtFile";
        txtFile.ReadOnly = true;
        txtFile.Size = new System.Drawing.Size(560, 23);
        txtFile.TabIndex = 1;
        txtFile.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

        // ===== btnBrowse =====
        btnBrowse.Location = new System.Drawing.Point(654, 11);
        btnBrowse.Name = "btnBrowse";
        btnBrowse.Size = new System.Drawing.Size(75, 25);
        btnBrowse.TabIndex = 2;
        btnBrowse.Text = "浏览...";
        btnBrowse.UseVisualStyleBackColor = true;
        btnBrowse.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        btnBrowse.Click += BtnBrowse_Click;

        // ===== lblSheet =====
        lblSheet.AutoSize = true;
        lblSheet.Location = new System.Drawing.Point(12, 48);
        lblSheet.Name = "lblSheet";
        lblSheet.Size = new System.Drawing.Size(45, 17);
        lblSheet.TabIndex = 3;
        lblSheet.Text = "Sheet:";

        // ===== cmbSheet =====
        cmbSheet.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        cmbSheet.Location = new System.Drawing.Point(88, 45);
        cmbSheet.Name = "cmbSheet";
        cmbSheet.Size = new System.Drawing.Size(220, 23);
        cmbSheet.TabIndex = 4;
        cmbSheet.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        cmbSheet.SelectedIndexChanged += CmbSheet_SelectedIndexChanged;

        // ===== lblAlias =====
        lblAlias.AutoSize = true;
        lblAlias.Location = new System.Drawing.Point(330, 48);
        lblAlias.Name = "lblAlias";
        lblAlias.Size = new System.Drawing.Size(192, 17);
        lblAlias.TabIndex = 5;
        lblAlias.Text = "别名 (SQL 用 ##alias 引用):";

        // ===== txtAlias =====
        txtAlias.Location = new System.Drawing.Point(528, 45);
        txtAlias.Name = "txtAlias";
        txtAlias.Size = new System.Drawing.Size(201, 23);
        txtAlias.TabIndex = 6;
        txtAlias.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        txtAlias.MaxLength = 64;

        // ===== lblMapping =====
        lblMapping.AutoSize = true;
        lblMapping.Location = new System.Drawing.Point(12, 80);
        lblMapping.Name = "lblMapping";
        lblMapping.Size = new System.Drawing.Size(180, 17);
        lblMapping.TabIndex = 7;
        lblMapping.Text = "列定义映射 (双击修改):";

        // ===== dgvMapping =====
        dgvMapping.AllowUserToAddRows = false;
        dgvMapping.AllowUserToDeleteRows = false;
        dgvMapping.AllowUserToResizeRows = false;
        dgvMapping.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvMapping.Location = new System.Drawing.Point(12, 100);
        dgvMapping.Name = "dgvMapping";
        dgvMapping.RowHeadersVisible = false;
        dgvMapping.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
        dgvMapping.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
        dgvMapping.Size = new System.Drawing.Size(717, 260);
        dgvMapping.TabIndex = 8;
        dgvMapping.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom
                            | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        dgvMapping.BackgroundColor = System.Drawing.SystemColors.Window;

        dgvMapping.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            colExcelName, colTargetName, colType, colSample1, colSample2, colSample3
        });

        colExcelName.HeaderText = "Excel 列名";
        colExcelName.Name = "colExcelName";
        colExcelName.ReadOnly = true;
        colExcelName.Width = 140;

        colTargetName.HeaderText = "映射名 (可改)";
        colTargetName.Name = "colTargetName";
        colTargetName.Width = 160;

        colType.HeaderText = "类型";
        colType.Name = "colType";
        colType.Width = 100;
        colType.Items.AddRange(new object[] { "NVARCHAR", "INT", "DECIMAL", "DATETIME" });

        colSample1.HeaderText = "示例 1";
        colSample1.Name = "colSample1";
        colSample1.ReadOnly = true;
        colSample1.Width = 100;

        colSample2.HeaderText = "示例 2";
        colSample2.Name = "colSample2";
        colSample2.ReadOnly = true;
        colSample2.Width = 100;

        colSample3.HeaderText = "示例 3";
        colSample3.Name = "colSample3";
        colSample3.ReadOnly = true;
        colSample3.Width = 100;

        // ===== lblPreview =====
        lblPreview.AutoSize = true;
        lblPreview.Location = new System.Drawing.Point(12, 370);
        lblPreview.Name = "lblPreview";
        lblPreview.Size = new System.Drawing.Size(195, 17);
        lblPreview.TabIndex = 9;
        lblPreview.Text = "数据预览 (前 3 行, 已应用类型):";

        // ===== dgvPreview =====
        dgvPreview.AllowUserToAddRows = false;
        dgvPreview.AllowUserToDeleteRows = false;
        dgvPreview.AllowUserToResizeRows = false;
        dgvPreview.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvPreview.Location = new System.Drawing.Point(12, 390);
        dgvPreview.Name = "dgvPreview";
        dgvPreview.ReadOnly = true;
        dgvPreview.RowHeadersVisible = false;
        dgvPreview.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
        dgvPreview.Size = new System.Drawing.Size(717, 90);
        dgvPreview.TabIndex = 10;
        dgvPreview.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        dgvPreview.BackgroundColor = System.Drawing.SystemColors.Control;

        // ===== btnImport =====
        btnImport.Location = new System.Drawing.Point(569, 488);
        btnImport.Name = "btnImport";
        btnImport.Size = new System.Drawing.Size(75, 28);
        btnImport.TabIndex = 11;
        btnImport.Text = "导入";
        btnImport.UseVisualStyleBackColor = true;
        btnImport.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        btnImport.Click += BtnImport_Click;

        // ===== btnCancel =====
        btnCancel.Location = new System.Drawing.Point(654, 488);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new System.Drawing.Size(75, 28);
        btnCancel.TabIndex = 12;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = true;
        btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        btnCancel.Click += BtnCancel_Click;

        // ===== statusStrip =====
        statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { lblStatus });
        statusStrip.Location = new System.Drawing.Point(0, 519);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new System.Drawing.Size(740, 22);
        statusStrip.TabIndex = 13;
        statusStrip.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

        lblStatus.Name = "lblStatus";
        lblStatus.Size = new System.Drawing.Size(0, 17);
        lblStatus.Text = "请选择 Excel 文件";

        // ===== ExcelImportForm =====
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        CancelButton = btnCancel;
        ClientSize = new System.Drawing.Size(740, 541);
        Controls.Add(lblFile);
        Controls.Add(txtFile);
        Controls.Add(btnBrowse);
        Controls.Add(lblSheet);
        Controls.Add(cmbSheet);
        Controls.Add(lblAlias);
        Controls.Add(txtAlias);
        Controls.Add(lblMapping);
        Controls.Add(dgvMapping);
        Controls.Add(lblPreview);
        Controls.Add(dgvPreview);
        Controls.Add(btnImport);
        Controls.Add(btnCancel);
        Controls.Add(statusStrip);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ExcelImportForm";
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Text = "导入 Excel 到临时表";

        ((System.ComponentModel.ISupportInitialize)dgvMapping).EndInit();
        ((System.ComponentModel.ISupportInitialize)dgvPreview).EndInit();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
