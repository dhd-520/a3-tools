namespace A3Tools.Forms;

partial class HotkeySettingsForm
{
    private void InitializeComponent()
    {
        titleBar = new Panel();
        lblTitle = new Label();
        lblHint = new Label();
        gridMain = new TableLayoutPanel();
        lblColFunc = new Label();
        lblColHotkey = new Label();
        lblTray = new Label();
        p1 = new Panel();
        txtTrayHotkey = new TextBox();
        btnClearTray = new Button();
        lblAdd = new Label();
        p2 = new Panel();
        txtAddHotkey = new TextBox();
        btnClearAdd = new Button();
        lblDelete = new Label();
        p3 = new Panel();
        txtDeleteHotkey = new TextBox();
        btnClearDelete = new Button();
        lblLaunch = new Label();
        p4 = new Panel();
        txtLaunchHotkey = new TextBox();
        btnClearLaunch = new Button();
        lblSettings = new Label();
        p5 = new Panel();
        txtSettingsHotkey = new TextBox();
        btnClearSettings = new Button();
        lblConnectDB = new Label();
        p6 = new Panel();
        txtConnectDBHotkey = new TextBox();
        btnClearConnectDB = new Button();
        lblRemote = new Label();
        p7 = new Panel();
        txtRemoteHotkey = new TextBox();
        btnClearRemote = new Button();
        bottomBar = new Panel();
        btnOK = new Button();
        btnCancel = new Button();
        titleBar.SuspendLayout();
        gridMain.SuspendLayout();
        p1.SuspendLayout();
        p2.SuspendLayout();
        p3.SuspendLayout();
        p4.SuspendLayout();
        p5.SuspendLayout();
        p6.SuspendLayout();
        p7.SuspendLayout();
        bottomBar.SuspendLayout();
        SuspendLayout();
        // 
        // titleBar
        // 
        titleBar.BackColor = Color.FromArgb(24, 145, 176);
        titleBar.Controls.Add(lblTitle);
        titleBar.Dock = DockStyle.Top;
        titleBar.Location = new Point(0, 0);
        titleBar.Name = "titleBar";
        titleBar.Size = new Size(864, 55);
        titleBar.TabIndex = 3;
        // 
        // lblTitle
        // 
        lblTitle.Font = new Font("微软雅黑", 14F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(20, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(500, 55);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "⚡ 快捷键设置";
        lblTitle.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblHint
        // 
        lblHint.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblHint.ForeColor = Color.FromArgb(100, 100, 100);
        lblHint.Location = new Point(0, 55);
        lblHint.Name = "lblHint";
        lblHint.Size = new Size(700, 35);
        lblHint.TabIndex = 2;
        lblHint.Text = "  点击输入框后按组合键即可设置快捷键，留空则不启用";
        lblHint.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // gridMain
        // 
        gridMain.BackColor = Color.White;
        gridMain.ColumnCount = 2;
        gridMain.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        gridMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        gridMain.Controls.Add(lblColFunc, 0, 0);
        gridMain.Controls.Add(lblColHotkey, 1, 0);
        gridMain.Controls.Add(lblTray, 0, 1);
        gridMain.Controls.Add(p1, 1, 1);
        gridMain.Controls.Add(lblAdd, 0, 2);
        gridMain.Controls.Add(p2, 1, 2);
        gridMain.Controls.Add(lblDelete, 0, 3);
        gridMain.Controls.Add(p3, 1, 3);
        gridMain.Controls.Add(lblLaunch, 0, 4);
        gridMain.Controls.Add(p4, 1, 4);
        gridMain.Controls.Add(lblSettings, 0, 5);
        gridMain.Controls.Add(p5, 1, 5);
        gridMain.Controls.Add(lblConnectDB, 0, 6);
        gridMain.Controls.Add(p6, 1, 6);
        gridMain.Controls.Add(lblRemote, 0, 7);
        gridMain.Controls.Add(p7, 1, 7);
        gridMain.Location = new Point(0, 90);
        gridMain.Name = "gridMain";
        gridMain.Padding = new Padding(20, 10, 20, 0);
        gridMain.RowCount = 9;
        gridMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        gridMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        gridMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        gridMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        gridMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        gridMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        gridMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        gridMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        gridMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        gridMain.Size = new Size(832, 414);
        gridMain.TabIndex = 1;
        // 
        // lblColFunc
        // 
        lblColFunc.BackColor = Color.FromArgb(240, 240, 240);
        lblColFunc.Font = new Font("微软雅黑", 10F, FontStyle.Bold, GraphicsUnit.Point);
        lblColFunc.Location = new Point(23, 10);
        lblColFunc.Name = "lblColFunc";
        lblColFunc.Size = new Size(190, 36);
        lblColFunc.TabIndex = 0;
        lblColFunc.Text = "功能";
        lblColFunc.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblColHotkey
        // 
        lblColHotkey.BackColor = Color.FromArgb(240, 240, 240);
        lblColHotkey.Font = new Font("微软雅黑", 10F, FontStyle.Bold, GraphicsUnit.Point);
        lblColHotkey.Location = new Point(223, 10);
        lblColHotkey.Name = "lblColHotkey";
        lblColHotkey.Size = new Size(430, 36);
        lblColHotkey.TabIndex = 1;
        lblColHotkey.Text = "快捷键";
        lblColHotkey.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblTray
        // 
        lblTray.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblTray.Location = new Point(23, 50);
        lblTray.Name = "lblTray";
        lblTray.Size = new Size(190, 46);
        lblTray.TabIndex = 2;
        lblTray.Text = "托盘显示快捷键";
        lblTray.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // p1
        // 
        p1.Controls.Add(txtTrayHotkey);
        p1.Controls.Add(btnClearTray);
        p1.Location = new Point(223, 53);
        p1.Name = "p1";
        p1.Size = new Size(586, 44);
        p1.TabIndex = 3;
        // 
        // txtTrayHotkey
        // 
        txtTrayHotkey.BackColor = Color.White;
        txtTrayHotkey.BorderStyle = BorderStyle.FixedSingle;
        txtTrayHotkey.Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point);
        txtTrayHotkey.Location = new Point(0, 7);
        txtTrayHotkey.Name = "txtTrayHotkey";
        txtTrayHotkey.ReadOnly = true;
        txtTrayHotkey.Size = new Size(410, 40);
        txtTrayHotkey.TabIndex = 0;
        txtTrayHotkey.KeyDown += TxtBox_KeyDown;
        // 
        // btnClearTray
        // 
        btnClearTray.BackColor = Color.FromArgb(245, 245, 245);
        btnClearTray.Cursor = Cursors.Hand;
        btnClearTray.FlatStyle = FlatStyle.Flat;
        btnClearTray.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnClearTray.Location = new Point(420, 7);
        btnClearTray.Name = "btnClearTray";
        btnClearTray.Size = new Size(151, 37);
        btnClearTray.TabIndex = 1;
        btnClearTray.Text = "清除";
        btnClearTray.UseVisualStyleBackColor = false;
        btnClearTray.Click += BtnClear_Click;
        // 
        // lblAdd
        // 
        lblAdd.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblAdd.Location = new Point(23, 100);
        lblAdd.Name = "lblAdd";
        lblAdd.Size = new Size(190, 46);
        lblAdd.TabIndex = 4;
        lblAdd.Text = "新增账套快捷键";
        lblAdd.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // p2
        // 
        p2.Controls.Add(txtAddHotkey);
        p2.Controls.Add(btnClearAdd);
        p2.Location = new Point(223, 103);
        p2.Name = "p2";
        p2.Size = new Size(586, 44);
        p2.TabIndex = 5;
        // 
        // txtAddHotkey
        // 
        txtAddHotkey.BackColor = Color.White;
        txtAddHotkey.BorderStyle = BorderStyle.FixedSingle;
        txtAddHotkey.Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point);
        txtAddHotkey.Location = new Point(0, 7);
        txtAddHotkey.Name = "txtAddHotkey";
        txtAddHotkey.ReadOnly = true;
        txtAddHotkey.Size = new Size(410, 40);
        txtAddHotkey.TabIndex = 0;
        txtAddHotkey.KeyDown += TxtBox_KeyDown;
        // 
        // btnClearAdd
        // 
        btnClearAdd.BackColor = Color.FromArgb(245, 245, 245);
        btnClearAdd.Cursor = Cursors.Hand;
        btnClearAdd.FlatStyle = FlatStyle.Flat;
        btnClearAdd.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnClearAdd.Location = new Point(420, 3);
        btnClearAdd.Name = "btnClearAdd";
        btnClearAdd.Size = new Size(151, 40);
        btnClearAdd.TabIndex = 1;
        btnClearAdd.Text = "清除";
        btnClearAdd.UseVisualStyleBackColor = false;
        btnClearAdd.Click += BtnClear_Click;
        // 
        // lblDelete
        // 
        lblDelete.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblDelete.Location = new Point(23, 150);
        lblDelete.Name = "lblDelete";
        lblDelete.Size = new Size(190, 46);
        lblDelete.TabIndex = 6;
        lblDelete.Text = "删除账套快捷键";
        lblDelete.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // p3
        // 
        p3.Controls.Add(txtDeleteHotkey);
        p3.Controls.Add(btnClearDelete);
        p3.Location = new Point(223, 153);
        p3.Name = "p3";
        p3.Size = new Size(586, 44);
        p3.TabIndex = 7;
        // 
        // txtDeleteHotkey
        // 
        txtDeleteHotkey.BackColor = Color.White;
        txtDeleteHotkey.BorderStyle = BorderStyle.FixedSingle;
        txtDeleteHotkey.Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point);
        txtDeleteHotkey.Location = new Point(0, 7);
        txtDeleteHotkey.Name = "txtDeleteHotkey";
        txtDeleteHotkey.ReadOnly = true;
        txtDeleteHotkey.Size = new Size(410, 40);
        txtDeleteHotkey.TabIndex = 0;
        txtDeleteHotkey.KeyDown += TxtBox_KeyDown;
        // 
        // btnClearDelete
        // 
        btnClearDelete.BackColor = Color.FromArgb(245, 245, 245);
        btnClearDelete.Cursor = Cursors.Hand;
        btnClearDelete.FlatStyle = FlatStyle.Flat;
        btnClearDelete.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnClearDelete.Location = new Point(420, 3);
        btnClearDelete.Name = "btnClearDelete";
        btnClearDelete.Size = new Size(151, 41);
        btnClearDelete.TabIndex = 1;
        btnClearDelete.Text = "清除";
        btnClearDelete.UseVisualStyleBackColor = false;
        btnClearDelete.Click += BtnClear_Click;
        // 
        // lblLaunch
        // 
        lblLaunch.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblLaunch.Location = new Point(23, 200);
        lblLaunch.Name = "lblLaunch";
        lblLaunch.Size = new Size(190, 46);
        lblLaunch.TabIndex = 8;
        lblLaunch.Text = "启动账套快捷键";
        lblLaunch.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // p4
        // 
        p4.Controls.Add(txtLaunchHotkey);
        p4.Controls.Add(btnClearLaunch);
        p4.Location = new Point(223, 203);
        p4.Name = "p4";
        p4.Size = new Size(586, 44);
        p4.TabIndex = 9;
        // 
        // txtLaunchHotkey
        // 
        txtLaunchHotkey.BackColor = Color.White;
        txtLaunchHotkey.BorderStyle = BorderStyle.FixedSingle;
        txtLaunchHotkey.Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point);
        txtLaunchHotkey.Location = new Point(0, 7);
        txtLaunchHotkey.Name = "txtLaunchHotkey";
        txtLaunchHotkey.ReadOnly = true;
        txtLaunchHotkey.Size = new Size(410, 40);
        txtLaunchHotkey.TabIndex = 0;
        txtLaunchHotkey.KeyDown += TxtBox_KeyDown;
        // 
        // btnClearLaunch
        // 
        btnClearLaunch.BackColor = Color.FromArgb(245, 245, 245);
        btnClearLaunch.Cursor = Cursors.Hand;
        btnClearLaunch.FlatStyle = FlatStyle.Flat;
        btnClearLaunch.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnClearLaunch.Location = new Point(420, 3);
        btnClearLaunch.Name = "btnClearLaunch";
        btnClearLaunch.Size = new Size(151, 38);
        btnClearLaunch.TabIndex = 1;
        btnClearLaunch.Text = "清除";
        btnClearLaunch.UseVisualStyleBackColor = false;
        btnClearLaunch.Click += BtnClear_Click;
        // 
        // lblSettings
        // 
        lblSettings.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblSettings.Location = new Point(23, 250);
        lblSettings.Name = "lblSettings";
        lblSettings.Size = new Size(190, 46);
        lblSettings.TabIndex = 10;
        lblSettings.Text = "设置快捷键";
        lblSettings.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // p5
        // 
        p5.Controls.Add(txtSettingsHotkey);
        p5.Controls.Add(btnClearSettings);
        p5.Location = new Point(223, 253);
        p5.Name = "p5";
        p5.Size = new Size(586, 44);
        p5.TabIndex = 11;
        // 
        // txtSettingsHotkey
        // 
        txtSettingsHotkey.BackColor = Color.White;
        txtSettingsHotkey.BorderStyle = BorderStyle.FixedSingle;
        txtSettingsHotkey.Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point);
        txtSettingsHotkey.Location = new Point(0, 7);
        txtSettingsHotkey.Name = "txtSettingsHotkey";
        txtSettingsHotkey.ReadOnly = true;
        txtSettingsHotkey.Size = new Size(410, 40);
        txtSettingsHotkey.TabIndex = 0;
        txtSettingsHotkey.KeyDown += TxtBox_KeyDown;
        // 
        // btnClearSettings
        // 
        btnClearSettings.BackColor = Color.FromArgb(245, 245, 245);
        btnClearSettings.Cursor = Cursors.Hand;
        btnClearSettings.FlatStyle = FlatStyle.Flat;
        btnClearSettings.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnClearSettings.Location = new Point(420, 7);
        btnClearSettings.Name = "btnClearSettings";
        btnClearSettings.Size = new Size(151, 37);
        btnClearSettings.TabIndex = 1;
        btnClearSettings.Text = "清除";
        btnClearSettings.UseVisualStyleBackColor = false;
        btnClearSettings.Click += BtnClear_Click;
        // 
        // lblConnectDB
        // 
        lblConnectDB.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblConnectDB.Location = new Point(23, 300);
        lblConnectDB.Name = "lblConnectDB";
        lblConnectDB.Size = new Size(190, 46);
        lblConnectDB.TabIndex = 12;
        lblConnectDB.Text = "链接数据库快捷键";
        lblConnectDB.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // p6
        // 
        p6.Controls.Add(txtConnectDBHotkey);
        p6.Controls.Add(btnClearConnectDB);
        p6.Location = new Point(223, 303);
        p6.Name = "p6";
        p6.Size = new Size(586, 44);
        p6.TabIndex = 13;
        // 
        // txtConnectDBHotkey
        // 
        txtConnectDBHotkey.BackColor = Color.White;
        txtConnectDBHotkey.BorderStyle = BorderStyle.FixedSingle;
        txtConnectDBHotkey.Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point);
        txtConnectDBHotkey.Location = new Point(0, 7);
        txtConnectDBHotkey.Name = "txtConnectDBHotkey";
        txtConnectDBHotkey.ReadOnly = true;
        txtConnectDBHotkey.Size = new Size(410, 40);
        txtConnectDBHotkey.TabIndex = 0;
        txtConnectDBHotkey.KeyDown += TxtBox_KeyDown;
        // 
        // btnClearConnectDB
        // 
        btnClearConnectDB.BackColor = Color.FromArgb(245, 245, 245);
        btnClearConnectDB.Cursor = Cursors.Hand;
        btnClearConnectDB.FlatStyle = FlatStyle.Flat;
        btnClearConnectDB.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnClearConnectDB.Location = new Point(420, 2);
        btnClearConnectDB.Name = "btnClearConnectDB";
        btnClearConnectDB.Size = new Size(151, 41);
        btnClearConnectDB.TabIndex = 1;
        btnClearConnectDB.Text = "清除";
        btnClearConnectDB.UseVisualStyleBackColor = false;
        btnClearConnectDB.Click += BtnClear_Click;
        // 
        // lblRemote
        // 
        lblRemote.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblRemote.Location = new Point(23, 350);
        lblRemote.Name = "lblRemote";
        lblRemote.Size = new Size(190, 46);
        lblRemote.TabIndex = 14;
        lblRemote.Text = "远程连接快捷键";
        lblRemote.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // p7
        // 
        p7.Controls.Add(txtRemoteHotkey);
        p7.Controls.Add(btnClearRemote);
        p7.Location = new Point(223, 353);
        p7.Name = "p7";
        p7.Size = new Size(586, 44);
        p7.TabIndex = 15;
        // 
        // txtRemoteHotkey
        // 
        txtRemoteHotkey.BackColor = Color.White;
        txtRemoteHotkey.BorderStyle = BorderStyle.FixedSingle;
        txtRemoteHotkey.Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point);
        txtRemoteHotkey.Location = new Point(0, 7);
        txtRemoteHotkey.Name = "txtRemoteHotkey";
        txtRemoteHotkey.ReadOnly = true;
        txtRemoteHotkey.Size = new Size(410, 40);
        txtRemoteHotkey.TabIndex = 0;
        txtRemoteHotkey.KeyDown += TxtBox_KeyDown;
        // 
        // btnClearRemote
        // 
        btnClearRemote.BackColor = Color.FromArgb(245, 245, 245);
        btnClearRemote.Cursor = Cursors.Hand;
        btnClearRemote.FlatStyle = FlatStyle.Flat;
        btnClearRemote.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnClearRemote.Location = new Point(420, 6);
        btnClearRemote.Name = "btnClearRemote";
        btnClearRemote.Size = new Size(151, 38);
        btnClearRemote.TabIndex = 1;
        btnClearRemote.Text = "清除";
        btnClearRemote.UseVisualStyleBackColor = false;
        btnClearRemote.Click += BtnClear_Click;
        // 
        // bottomBar
        // 
        bottomBar.BackColor = Color.FromArgb(248, 248, 248);
        bottomBar.Controls.Add(btnOK);
        bottomBar.Controls.Add(btnCancel);
        bottomBar.Dock = DockStyle.Bottom;
        bottomBar.Location = new Point(0, 524);
        bottomBar.Name = "bottomBar";
        bottomBar.Size = new Size(864, 70);
        bottomBar.TabIndex = 0;
        bottomBar.Resize += BottomBar_Resize;
        // 
        // btnOK
        // 
        btnOK.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnOK.BackColor = Color.FromArgb(24, 145, 176);
        btnOK.Cursor = Cursors.Hand;
        btnOK.DialogResult = DialogResult.OK;
        btnOK.FlatAppearance.BorderSize = 0;
        btnOK.FlatStyle = FlatStyle.Flat;
        btnOK.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        btnOK.ForeColor = Color.White;
        btnOK.Location = new Point(456, 0);
        btnOK.Name = "btnOK";
        btnOK.Size = new Size(130, 44);
        btnOK.TabIndex = 0;
        btnOK.Text = "确定";
        btnOK.UseVisualStyleBackColor = false;
        btnOK.Click += BtnOK_Click;
        // 
        // btnCancel
        // 
        btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCancel.BackColor = Color.White;
        btnCancel.Cursor = Cursors.Hand;
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        btnCancel.Location = new Point(664, 0);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(130, 44);
        btnCancel.TabIndex = 1;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = false;
        // 
        // HotkeySettingsForm
        // 
        AcceptButton = btnOK;
        BackColor = Color.White;
        CancelButton = btnCancel;
        ClientSize = new Size(864, 594);
        Controls.Add(bottomBar);
        Controls.Add(gridMain);
        Controls.Add(lblHint);
        Controls.Add(titleBar);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "HotkeySettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "快捷键设置";
        titleBar.ResumeLayout(false);
        gridMain.ResumeLayout(false);
        p1.ResumeLayout(false);
        p1.PerformLayout();
        p2.ResumeLayout(false);
        p2.PerformLayout();
        p3.ResumeLayout(false);
        p3.PerformLayout();
        p4.ResumeLayout(false);
        p4.PerformLayout();
        p5.ResumeLayout(false);
        p5.PerformLayout();
        p6.ResumeLayout(false);
        p6.PerformLayout();
        p7.ResumeLayout(false);
        p7.PerformLayout();
        bottomBar.ResumeLayout(false);
        ResumeLayout(false);
    }

    private Panel p1;
    private Panel p2;
    private Panel p3;
    private Panel p4;
    private Panel p5;
    private Panel p6;
    private Panel p7;
}