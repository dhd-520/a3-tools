namespace A3Tools.Forms;

partial class AiSettingsForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.pnlHeader = new System.Windows.Forms.Panel();
        this.lblTitle = new System.Windows.Forms.Label();
        this.pnlBottom = new System.Windows.Forms.Panel();
        this.btnCancel = new System.Windows.Forms.Button();
        this.btnSave = new System.Windows.Forms.Button();
        this.pnlMain = new System.Windows.Forms.Panel();
        this.grpProviderList = new System.Windows.Forms.GroupBox();
        this.btnAddPreset = new System.Windows.Forms.Button();
        this.btnSetDefault = new System.Windows.Forms.Button();
        this.btnDelete = new System.Windows.Forms.Button();
        this.btnEdit = new System.Windows.Forms.Button();
        this.btnAdd = new System.Windows.Forms.Button();
        this.lstProviders = new System.Windows.Forms.ListBox();
        this.lblProviders = new System.Windows.Forms.Label();
        this.grpProviderEdit = new System.Windows.Forms.GroupBox();
        this.btnApplyProvider = new System.Windows.Forms.Button();
        this.txtRemark = new System.Windows.Forms.TextBox();
        this.lblRemark = new System.Windows.Forms.Label();
        this.chkEnabled = new System.Windows.Forms.CheckBox();
        this.txtModel = new System.Windows.Forms.TextBox();
        this.lblModel = new System.Windows.Forms.Label();
        this.txtApiKey = new System.Windows.Forms.TextBox();
        this.lblApiKey = new System.Windows.Forms.Label();
        this.txtApiUrl = new System.Windows.Forms.TextBox();
        this.lblApiUrl = new System.Windows.Forms.Label();
        this.txtName = new System.Windows.Forms.TextBox();
        this.lblName = new System.Windows.Forms.Label();
        this.cmbType = new System.Windows.Forms.ComboBox();
        this.lblType = new System.Windows.Forms.Label();
        this.grpGlobal = new System.Windows.Forms.GroupBox();
        this.lblHint = new System.Windows.Forms.Label();
        this.chkAutoResume = new System.Windows.Forms.CheckBox();
        this.txtSystemPromptAppend = new System.Windows.Forms.TextBox();
        this.lblSysPrompt = new System.Windows.Forms.Label();
        this.nudTemperature = new System.Windows.Forms.NumericUpDown();
        this.lblTemp = new System.Windows.Forms.Label();
        this.nudMaxContext = new System.Windows.Forms.NumericUpDown();
        this.lblMaxCtx = new System.Windows.Forms.Label();
        this.lblRetentionUnit = new System.Windows.Forms.Label();
        this.nudRetentionDays = new System.Windows.Forms.NumericUpDown();
        this.lblRetention = new System.Windows.Forms.Label();
        this.cmbStoreStrategy = new System.Windows.Forms.ComboBox();
        this.lblStoreStrategy = new System.Windows.Forms.Label();
        this.pnlHeader.SuspendLayout();
        this.pnlBottom.SuspendLayout();
        this.pnlMain.SuspendLayout();
        this.grpProviderList.SuspendLayout();
        this.grpProviderEdit.SuspendLayout();
        this.grpGlobal.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.nudTemperature)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.nudMaxContext)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.nudRetentionDays)).BeginInit();
        this.SuspendLayout();
        //
        // pnlHeader
        //
        this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(24, 144, 255);
        this.pnlHeader.Controls.Add(this.lblTitle);
        this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlHeader.Location = new System.Drawing.Point(0, 0);
        this.pnlHeader.Name = "pnlHeader";
        this.pnlHeader.Size = new System.Drawing.Size(880, 48);
        this.pnlHeader.TabIndex = 0;
        //
        // lblTitle
        //
        this.lblTitle.AutoSize = true;
        this.lblTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
        this.lblTitle.ForeColor = System.Drawing.Color.White;
        this.lblTitle.Location = new System.Drawing.Point(16, 12);
        this.lblTitle.Name = "lblTitle";
        this.lblTitle.Size = new System.Drawing.Size(155, 27);
        this.lblTitle.TabIndex = 0;
        this.lblTitle.Text = "AI 助理设置";
        //
        // pnlBottom
        //
        this.pnlBottom.BackColor = System.Drawing.Color.White;
        this.pnlBottom.Controls.Add(this.btnCancel);
        this.pnlBottom.Controls.Add(this.btnSave);
        this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
        this.pnlBottom.Location = new System.Drawing.Point(0, 584);
        this.pnlBottom.Name = "pnlBottom";
        this.pnlBottom.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);
        this.pnlBottom.Size = new System.Drawing.Size(880, 56);
        this.pnlBottom.TabIndex = 2;
        //
        // btnCancel
        //
        this.btnCancel.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
        this.btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(220, 220, 220);
        this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnCancel.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
        this.btnCancel.Location = new System.Drawing.Point(748, 10);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new System.Drawing.Size(96, 36);
        this.btnCancel.TabIndex = 1;
        this.btnCancel.Text = "取消";
        this.btnCancel.UseVisualStyleBackColor = false;
        //
        // btnSave
        //
        this.btnSave.BackColor = System.Drawing.Color.FromArgb(24, 144, 255);
        this.btnSave.FlatAppearance.BorderSize = 0;
        this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnSave.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
        this.btnSave.ForeColor = System.Drawing.Color.White;
        this.btnSave.Location = new System.Drawing.Point(640, 10);
        this.btnSave.Name = "btnSave";
        this.btnSave.Size = new System.Drawing.Size(96, 36);
        this.btnSave.TabIndex = 0;
        this.btnSave.Text = "保存";
        this.btnSave.UseVisualStyleBackColor = false;
        //
        // pnlMain
        //
        this.pnlMain.Controls.Add(this.grpGlobal);
        this.pnlMain.Controls.Add(this.grpProviderEdit);
        this.pnlMain.Controls.Add(this.grpProviderList);
        this.pnlMain.Dock = System.Windows.Forms.DockStyle.Fill;
        this.pnlMain.Location = new System.Drawing.Point(0, 48);
        this.pnlMain.Name = "pnlMain";
        this.pnlMain.Padding = new System.Windows.Forms.Padding(12);
        this.pnlMain.Size = new System.Drawing.Size(880, 536);
        this.pnlMain.TabIndex = 1;
        //
        // grpProviderList
        //
        this.grpProviderList.BackColor = System.Drawing.Color.White;
        this.grpProviderList.Controls.Add(this.btnAddPreset);
        this.grpProviderList.Controls.Add(this.btnSetDefault);
        this.grpProviderList.Controls.Add(this.btnDelete);
        this.grpProviderList.Controls.Add(this.btnEdit);
        this.grpProviderList.Controls.Add(this.btnAdd);
        this.grpProviderList.Controls.Add(this.lstProviders);
        this.grpProviderList.Controls.Add(this.lblProviders);
        this.grpProviderList.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
        this.grpProviderList.Location = new System.Drawing.Point(12, 12);
        this.grpProviderList.Name = "grpProviderList";
        this.grpProviderList.Size = new System.Drawing.Size(320, 480);
        this.grpProviderList.TabIndex = 0;
        this.grpProviderList.TabStop = false;
        this.grpProviderList.Text = "  AI 厂商  ";
        //
        // btnAddPreset
        //
        this.btnAddPreset.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnAddPreset.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.btnAddPreset.Location = new System.Drawing.Point(184, 428);
        this.btnAddPreset.Name = "btnAddPreset";
        this.btnAddPreset.Size = new System.Drawing.Size(120, 32);
        this.btnAddPreset.TabIndex = 6;
        this.btnAddPreset.Text = "一键添加预设厂商";
        this.btnAddPreset.UseVisualStyleBackColor = true;
        //
        // btnSetDefault
        //
        this.btnSetDefault.BackColor = System.Drawing.Color.FromArgb(255, 192, 0);
        this.btnSetDefault.FlatAppearance.BorderSize = 0;
        this.btnSetDefault.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnSetDefault.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.btnSetDefault.ForeColor = System.Drawing.Color.White;
        this.btnSetDefault.Location = new System.Drawing.Point(16, 428);
        this.btnSetDefault.Name = "btnSetDefault";
        this.btnSetDefault.Size = new System.Drawing.Size(160, 32);
        this.btnSetDefault.TabIndex = 5;
        this.btnSetDefault.Text = "设为默认";
        this.btnSetDefault.UseVisualStyleBackColor = false;
        //
        // btnDelete
        //
        this.btnDelete.BackColor = System.Drawing.Color.FromArgb(251, 67, 42);
        this.btnDelete.FlatAppearance.BorderSize = 0;
        this.btnDelete.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnDelete.ForeColor = System.Drawing.Color.White;
        this.btnDelete.Location = new System.Drawing.Point(198, 388);
        this.btnDelete.Name = "btnDelete";
        this.btnDelete.Size = new System.Drawing.Size(80, 32);
        this.btnDelete.TabIndex = 4;
        this.btnDelete.Text = "删除";
        this.btnDelete.UseVisualStyleBackColor = false;
        //
        // btnEdit
        //
        this.btnEdit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnEdit.Location = new System.Drawing.Point(112, 388);
        this.btnEdit.Name = "btnEdit";
        this.btnEdit.Size = new System.Drawing.Size(80, 32);
        this.btnEdit.TabIndex = 3;
        this.btnEdit.Text = "编辑";
        this.btnEdit.UseVisualStyleBackColor = true;
        //
        // btnAdd
        //
        this.btnAdd.BackColor = System.Drawing.Color.FromArgb(57, 181, 74);
        this.btnAdd.FlatAppearance.BorderSize = 0;
        this.btnAdd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnAdd.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.btnAdd.ForeColor = System.Drawing.Color.White;
        this.btnAdd.Location = new System.Drawing.Point(16, 388);
        this.btnAdd.Name = "btnAdd";
        this.btnAdd.Size = new System.Drawing.Size(90, 32);
        this.btnAdd.TabIndex = 2;
        this.btnAdd.Text = "新增空白";
        this.btnAdd.UseVisualStyleBackColor = false;
        //
        // lstProviders
        //
        this.lstProviders.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this.lstProviders.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
        this.lstProviders.IntegralHeight = false;
        this.lstProviders.Location = new System.Drawing.Point(16, 56);
        this.lstProviders.Name = "lstProviders";
        this.lstProviders.Size = new System.Drawing.Size(288, 320);
        this.lstProviders.TabIndex = 1;
        //
        // lblProviders
        //
        this.lblProviders.AutoSize = true;
        this.lblProviders.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.lblProviders.ForeColor = System.Drawing.Color.FromArgb(120, 120, 120);
        this.lblProviders.Location = new System.Drawing.Point(16, 28);
        this.lblProviders.Name = "lblProviders";
        this.lblProviders.Size = new System.Drawing.Size(98, 20);
        this.lblProviders.TabIndex = 0;
        this.lblProviders.Text = "已配置厂商列表";
        //
        // grpProviderEdit
        //
        this.grpProviderEdit.BackColor = System.Drawing.Color.White;
        this.grpProviderEdit.Controls.Add(this.btnApplyProvider);
        this.grpProviderEdit.Controls.Add(this.txtRemark);
        this.grpProviderEdit.Controls.Add(this.lblRemark);
        this.grpProviderEdit.Controls.Add(this.chkEnabled);
        this.grpProviderEdit.Controls.Add(this.txtModel);
        this.grpProviderEdit.Controls.Add(this.lblModel);
        this.grpProviderEdit.Controls.Add(this.txtApiKey);
        this.grpProviderEdit.Controls.Add(this.lblApiKey);
        this.grpProviderEdit.Controls.Add(this.txtApiUrl);
        this.grpProviderEdit.Controls.Add(this.lblApiUrl);
        this.grpProviderEdit.Controls.Add(this.txtName);
        this.grpProviderEdit.Controls.Add(this.lblName);
        this.grpProviderEdit.Controls.Add(this.cmbType);
        this.grpProviderEdit.Controls.Add(this.lblType);
        this.grpProviderEdit.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
        this.grpProviderEdit.Location = new System.Drawing.Point(344, 12);
        this.grpProviderEdit.Name = "grpProviderEdit";
        this.grpProviderEdit.Size = new System.Drawing.Size(500, 260);
        this.grpProviderEdit.TabIndex = 1;
        this.grpProviderEdit.TabStop = false;
        this.grpProviderEdit.Text = "  厂商配置  ";
        //
        // btnApplyProvider
        //
        this.btnApplyProvider.BackColor = System.Drawing.Color.FromArgb(57, 181, 74);
        this.btnApplyProvider.FlatAppearance.BorderSize = 0;
        this.btnApplyProvider.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnApplyProvider.ForeColor = System.Drawing.Color.White;
        this.btnApplyProvider.Location = new System.Drawing.Point(280, 226);
        this.btnApplyProvider.Name = "btnApplyProvider";
        this.btnApplyProvider.Size = new System.Drawing.Size(200, 28);
        this.btnApplyProvider.TabIndex = 13;
        this.btnApplyProvider.Text = "应用到选中厂商";
        this.btnApplyProvider.UseVisualStyleBackColor = false;
        //
        // txtRemark
        //
        this.txtRemark.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.txtRemark.Location = new System.Drawing.Point(80, 198);
        this.txtRemark.Name = "txtRemark";
        this.txtRemark.Size = new System.Drawing.Size(400, 27);
        this.txtRemark.TabIndex = 12;
        //
        // lblRemark
        //
        this.lblRemark.AutoSize = true;
        this.lblRemark.Location = new System.Drawing.Point(16, 202);
        this.lblRemark.Name = "lblRemark";
        this.lblRemark.Size = new System.Drawing.Size(44, 20);
        this.lblRemark.TabIndex = 11;
        this.lblRemark.Text = "备注：";
        //
        // chkEnabled
        //
        this.chkEnabled.AutoSize = true;
        this.chkEnabled.Checked = true;
        this.chkEnabled.CheckState = System.Windows.Forms.CheckState.Checked;
        this.chkEnabled.Location = new System.Drawing.Point(280, 32);
        this.chkEnabled.Name = "chkEnabled";
        this.chkEnabled.Size = new System.Drawing.Size(58, 22);
        this.chkEnabled.TabIndex = 10;
        this.chkEnabled.Text = "启用";
        this.chkEnabled.UseVisualStyleBackColor = true;
        //
        // txtModel
        //
        this.txtModel.Font = new System.Drawing.Font("Consolas", 9F);
        this.txtModel.Location = new System.Drawing.Point(80, 164);
        this.txtModel.Name = "txtModel";
        this.txtModel.PlaceholderText = "gpt-4o-mini";
        this.txtModel.Size = new System.Drawing.Size(400, 27);
        this.txtModel.TabIndex = 9;
        //
        // lblModel
        //
        this.lblModel.AutoSize = true;
        this.lblModel.Location = new System.Drawing.Point(16, 168);
        this.lblModel.Name = "lblModel";
        this.lblModel.Size = new System.Drawing.Size(44, 20);
        this.lblModel.TabIndex = 8;
        this.lblModel.Text = "模型：";
        //
        // txtApiKey
        //
        this.txtApiKey.Font = new System.Drawing.Font("Consolas", 9F);
        this.txtApiKey.Location = new System.Drawing.Point(80, 130);
        this.txtApiKey.Name = "txtApiKey";
        this.txtApiKey.PlaceholderText = "sk-...";
        this.txtApiKey.Size = new System.Drawing.Size(400, 27);
        this.txtApiKey.TabIndex = 7;
        this.txtApiKey.UseSystemPasswordChar = true;
        //
        // lblApiKey
        //
        this.lblApiKey.AutoSize = true;
        this.lblApiKey.Location = new System.Drawing.Point(16, 134);
        this.lblApiKey.Name = "lblApiKey";
        this.lblApiKey.Size = new System.Drawing.Size(62, 20);
        this.lblApiKey.TabIndex = 6;
        this.lblApiKey.Text = "API Key：";
        //
        // txtApiUrl
        //
        this.txtApiUrl.Font = new System.Drawing.Font("Consolas", 9F);
        this.txtApiUrl.Location = new System.Drawing.Point(80, 96);
        this.txtApiUrl.Name = "txtApiUrl";
        this.txtApiUrl.PlaceholderText = "https://api.openai.com/v1";
        this.txtApiUrl.Size = new System.Drawing.Size(400, 27);
        this.txtApiUrl.TabIndex = 5;
        //
        // lblApiUrl
        //
        this.lblApiUrl.AutoSize = true;
        this.lblApiUrl.Location = new System.Drawing.Point(16, 100);
        this.lblApiUrl.Name = "lblApiUrl";
        this.lblApiUrl.Size = new System.Drawing.Size(65, 20);
        this.lblApiUrl.TabIndex = 4;
        this.lblApiUrl.Text = "API URL：";
        //
        // txtName
        //
        this.txtName.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.txtName.Location = new System.Drawing.Point(80, 62);
        this.txtName.Name = "txtName";
        this.txtName.Size = new System.Drawing.Size(180, 27);
        this.txtName.TabIndex = 3;
        //
        // lblName
        //
        this.lblName.AutoSize = true;
        this.lblName.Location = new System.Drawing.Point(16, 66);
        this.lblName.Name = "lblName";
        this.lblName.Size = new System.Drawing.Size(44, 20);
        this.lblName.TabIndex = 2;
        this.lblName.Text = "名称：";
        //
        // cmbType
        //
        this.cmbType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbType.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.cmbType.Location = new System.Drawing.Point(80, 28);
        this.cmbType.Name = "cmbType";
        this.cmbType.Size = new System.Drawing.Size(180, 27);
        this.cmbType.TabIndex = 1;
        //
        // lblType
        //
        this.lblType.AutoSize = true;
        this.lblType.Location = new System.Drawing.Point(16, 32);
        this.lblType.Name = "lblType";
        this.lblType.Size = new System.Drawing.Size(44, 20);
        this.lblType.TabIndex = 0;
        this.lblType.Text = "类型：";
        //
        // grpGlobal
        //
        this.grpGlobal.BackColor = System.Drawing.Color.White;
        this.grpGlobal.Controls.Add(this.lblHint);
        this.grpGlobal.Controls.Add(this.chkAutoResume);
        this.grpGlobal.Controls.Add(this.txtSystemPromptAppend);
        this.grpGlobal.Controls.Add(this.lblSysPrompt);
        this.grpGlobal.Controls.Add(this.nudTemperature);
        this.grpGlobal.Controls.Add(this.lblTemp);
        this.grpGlobal.Controls.Add(this.nudMaxContext);
        this.grpGlobal.Controls.Add(this.lblMaxCtx);
        this.grpGlobal.Controls.Add(this.lblRetentionUnit);
        this.grpGlobal.Controls.Add(this.nudRetentionDays);
        this.grpGlobal.Controls.Add(this.lblRetention);
        this.grpGlobal.Controls.Add(this.cmbStoreStrategy);
        this.grpGlobal.Controls.Add(this.lblStoreStrategy);
        this.grpGlobal.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
        this.grpGlobal.Location = new System.Drawing.Point(344, 284);
        this.grpGlobal.Name = "grpGlobal";
        this.grpGlobal.Size = new System.Drawing.Size(500, 208);
        this.grpGlobal.TabIndex = 2;
        this.grpGlobal.TabStop = false;
        this.grpGlobal.Text = "  全局设置  ";
        //
        // lblHint
        //
        this.lblHint.AutoSize = true;
        this.lblHint.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.5F);
        this.lblHint.ForeColor = System.Drawing.Color.Gray;
        this.lblHint.Location = new System.Drawing.Point(16, 168);
        this.lblHint.Name = "lblHint";
        this.lblHint.Size = new System.Drawing.Size(322, 19);
        this.lblHint.TabIndex = 12;
        this.lblHint.Text = "ApiKey 在保存时自动 AES 加密，存到 DATA/ai_chat_config.json";
        //
        // chkAutoResume
        //
        this.chkAutoResume.AutoSize = true;
        this.chkAutoResume.Checked = true;
        this.chkAutoResume.CheckState = System.Windows.Forms.CheckState.Checked;
        this.chkAutoResume.Location = new System.Drawing.Point(16, 138);
        this.chkAutoResume.Name = "chkAutoResume";
        this.chkAutoResume.Size = new System.Drawing.Size(170, 22);
        this.chkAutoResume.TabIndex = 11;
        this.chkAutoResume.Text = "启动时自动打开上次会话";
        this.chkAutoResume.UseVisualStyleBackColor = true;
        //
        // txtSystemPromptAppend
        //
        this.txtSystemPromptAppend.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.txtSystemPromptAppend.Location = new System.Drawing.Point(120, 98);
        this.txtSystemPromptAppend.Name = "txtSystemPromptAppend";
        this.txtSystemPromptAppend.PlaceholderText = "例：回答要简洁，少废话，少用 emoji";
        this.txtSystemPromptAppend.Size = new System.Drawing.Size(360, 27);
        this.txtSystemPromptAppend.TabIndex = 10;
        //
        // lblSysPrompt
        //
        this.lblSysPrompt.AutoSize = true;
        this.lblSysPrompt.Location = new System.Drawing.Point(16, 102);
        this.lblSysPrompt.Name = "lblSysPrompt";
        this.lblSysPrompt.Size = new System.Drawing.Size(84, 20);
        this.lblSysPrompt.TabIndex = 9;
        this.lblSysPrompt.Text = "追加系统提示：";
        //
        // nudTemperature
        //
        this.nudTemperature.DecimalPlaces = 1;
        this.nudTemperature.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        this.nudTemperature.Location = new System.Drawing.Point(340, 62);
        this.nudTemperature.Maximum = new decimal(new int[] { 20, 0, 0, 0 });
        this.nudTemperature.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
        this.nudTemperature.Name = "nudTemperature";
        this.nudTemperature.Size = new System.Drawing.Size(80, 27);
        this.nudTemperature.TabIndex = 8;
        this.nudTemperature.Value = new decimal(new int[] { 7, 0, 0, 65536 });
        //
        // lblTemp
        //
        this.lblTemp.AutoSize = true;
        this.lblTemp.Location = new System.Drawing.Point(250, 66);
        this.lblTemp.Name = "lblTemp";
        this.lblTemp.Size = new System.Drawing.Size(87, 20);
        this.lblTemp.TabIndex = 7;
        this.lblTemp.Text = "Temperature：";
        //
        // nudMaxContext
        //
        this.nudMaxContext.Increment = new decimal(new int[] { 1000, 0, 0, 0 });
        this.nudMaxContext.Location = new System.Drawing.Point(140, 62);
        this.nudMaxContext.Maximum = new decimal(new int[] { 32000, 0, 0, 0 });
        this.nudMaxContext.Minimum = new decimal(new int[] { 1000, 0, 0, 0 });
        this.nudMaxContext.Name = "nudMaxContext";
        this.nudMaxContext.Size = new System.Drawing.Size(100, 27);
        this.nudMaxContext.TabIndex = 6;
        this.nudMaxContext.Value = new decimal(new int[] { 8000, 0, 0, 0 });
        //
        // lblMaxCtx
        //
        this.lblMaxCtx.AutoSize = true;
        this.lblMaxCtx.Location = new System.Drawing.Point(16, 66);
        this.lblMaxCtx.Name = "lblMaxCtx";
        this.lblMaxCtx.Size = new System.Drawing.Size(113, 20);
        this.lblMaxCtx.TabIndex = 5;
        this.lblMaxCtx.Text = "上下文 Token 上限：";
        //
        // lblRetentionUnit
        //
        this.lblRetentionUnit.AutoSize = true;
        this.lblRetentionUnit.ForeColor = System.Drawing.Color.Gray;
        this.lblRetentionUnit.Location = new System.Drawing.Point(444, 32);
        this.lblRetentionUnit.Name = "lblRetentionUnit";
        this.lblRetentionUnit.Size = new System.Drawing.Size(85, 20);
        this.lblRetentionUnit.TabIndex = 4;
        this.lblRetentionUnit.Text = "天（0=永久）";
        //
        // nudRetentionDays
        //
        this.nudRetentionDays.Location = new System.Drawing.Point(360, 28);
        this.nudRetentionDays.Maximum = new decimal(new int[] { 3650, 0, 0, 0 });
        this.nudRetentionDays.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
        this.nudRetentionDays.Name = "nudRetentionDays";
        this.nudRetentionDays.Size = new System.Drawing.Size(80, 27);
        this.nudRetentionDays.TabIndex = 3;
        this.nudRetentionDays.Value = new decimal(new int[] { 90, 0, 0, 0 });
        //
        // lblRetention
        //
        this.lblRetention.AutoSize = true;
        this.lblRetention.Location = new System.Drawing.Point(290, 32);
        this.lblRetention.Name = "lblRetention";
        this.lblRetention.Size = new System.Drawing.Size(70, 20);
        this.lblRetention.TabIndex = 2;
        this.lblRetention.Text = "保留天数：";
        //
        // cmbStoreStrategy
        //
        this.cmbStoreStrategy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbStoreStrategy.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.cmbStoreStrategy.Items.AddRange(new object[] {
            "本地加密存储（默认）",
            "仅内存（关闭程序后清除）",
            "不存储对话历史"});
        this.cmbStoreStrategy.Location = new System.Drawing.Point(96, 28);
        this.cmbStoreStrategy.Name = "cmbStoreStrategy";
        this.cmbStoreStrategy.Size = new System.Drawing.Size(180, 27);
        this.cmbStoreStrategy.TabIndex = 1;
        //
        // lblStoreStrategy
        //
        this.lblStoreStrategy.AutoSize = true;
        this.lblStoreStrategy.Location = new System.Drawing.Point(16, 32);
        this.lblStoreStrategy.Name = "lblStoreStrategy";
        this.lblStoreStrategy.Size = new System.Drawing.Size(70, 20);
        this.lblStoreStrategy.TabIndex = 0;
        this.lblStoreStrategy.Text = "存储策略：";
        //
        // AiSettingsForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
        this.ClientSize = new System.Drawing.Size(880, 640);
        this.Controls.Add(this.pnlMain);
        this.Controls.Add(this.pnlBottom);
        this.Controls.Add(this.pnlHeader);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        this.MinimumSize = new System.Drawing.Size(800, 540);
        this.Name = "AiSettingsForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "AI 助理设置";
        this.pnlHeader.ResumeLayout(false);
        this.pnlHeader.PerformLayout();
        this.pnlBottom.ResumeLayout(false);
        this.pnlMain.ResumeLayout(false);
        this.grpProviderList.ResumeLayout(false);
        this.grpProviderList.PerformLayout();
        this.grpProviderEdit.ResumeLayout(false);
        this.grpProviderEdit.PerformLayout();
        this.grpGlobal.ResumeLayout(false);
        this.grpGlobal.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.nudTemperature)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.nudMaxContext)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.nudRetentionDays)).EndInit();
        this.ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.Panel pnlHeader;
    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.Panel pnlBottom;
    private System.Windows.Forms.Button btnCancel;
    private System.Windows.Forms.Button btnSave;
    private System.Windows.Forms.Panel pnlMain;
    private System.Windows.Forms.GroupBox grpProviderList;
    private System.Windows.Forms.Button btnAddPreset;
    private System.Windows.Forms.Button btnSetDefault;
    private System.Windows.Forms.Button btnDelete;
    private System.Windows.Forms.Button btnEdit;
    private System.Windows.Forms.Button btnAdd;
    private System.Windows.Forms.ListBox lstProviders;
    private System.Windows.Forms.Label lblProviders;
    private System.Windows.Forms.GroupBox grpProviderEdit;
    private System.Windows.Forms.Button btnApplyProvider;
    private System.Windows.Forms.TextBox txtRemark;
    private System.Windows.Forms.Label lblRemark;
    private System.Windows.Forms.CheckBox chkEnabled;
    private System.Windows.Forms.TextBox txtModel;
    private System.Windows.Forms.Label lblModel;
    private System.Windows.Forms.TextBox txtApiKey;
    private System.Windows.Forms.Label lblApiKey;
    private System.Windows.Forms.TextBox txtApiUrl;
    private System.Windows.Forms.Label lblApiUrl;
    private System.Windows.Forms.TextBox txtName;
    private System.Windows.Forms.Label lblName;
    private System.Windows.Forms.ComboBox cmbType;
    private System.Windows.Forms.Label lblType;
    private System.Windows.Forms.GroupBox grpGlobal;
    private System.Windows.Forms.Label lblHint;
    private System.Windows.Forms.CheckBox chkAutoResume;
    private System.Windows.Forms.TextBox txtSystemPromptAppend;
    private System.Windows.Forms.Label lblSysPrompt;
    private System.Windows.Forms.NumericUpDown nudTemperature;
    private System.Windows.Forms.Label lblTemp;
    private System.Windows.Forms.NumericUpDown nudMaxContext;
    private System.Windows.Forms.Label lblMaxCtx;
    private System.Windows.Forms.Label lblRetentionUnit;
    private System.Windows.Forms.NumericUpDown nudRetentionDays;
    private System.Windows.Forms.Label lblRetention;
    private System.Windows.Forms.ComboBox cmbStoreStrategy;
    private System.Windows.Forms.Label lblStoreStrategy;
}
