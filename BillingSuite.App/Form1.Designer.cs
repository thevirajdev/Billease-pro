namespace BillingSuite.App;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.menuStrip1 = new System.Windows.Forms.MenuStrip();
        this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.signOutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.toolStrip1 = new System.Windows.Forms.ToolStrip();
        this.btnCreateInvoice = new System.Windows.Forms.ToolStripButton();
        this.btnProducts = new System.Windows.Forms.ToolStripButton();
        this.btnCustomers = new System.Windows.Forms.ToolStripButton();
        this.systemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.backupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.restoreToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.tabControl1 = new System.Windows.Forms.TabControl();
        this.tabInvoices = new System.Windows.Forms.TabPage();
        this.gridInvoices = new System.Windows.Forms.DataGridView();
        this.btnInvoiceRefresh = new System.Windows.Forms.Button();
        this.btnInvoiceAdd = new System.Windows.Forms.Button();
        this.btnInvoiceEdit = new System.Windows.Forms.Button();
        this.btnInvoiceDelete = new System.Windows.Forms.Button();
        this.btnInvoiceAddPayment = new System.Windows.Forms.Button();
        this.txtInvoiceSearch = new System.Windows.Forms.TextBox();
        this.btnInvoiceSearch = new System.Windows.Forms.Button();
        this.tabCustomers = new System.Windows.Forms.TabPage();
        this.tabProducts = new System.Windows.Forms.TabPage();
        this.gridProducts = new System.Windows.Forms.DataGridView();
        this.btnRefreshProducts = new System.Windows.Forms.Button();
        this.btnAddProduct = new System.Windows.Forms.Button();
        this.btnEditProduct = new System.Windows.Forms.Button();
        this.btnDeleteProduct = new System.Windows.Forms.Button();
        this.btnExportProducts = new System.Windows.Forms.Button();
        this.btnImportProducts = new System.Windows.Forms.Button();
        this.btnExportProductsPdf = new System.Windows.Forms.Button();
        this.txtSearchProducts = new System.Windows.Forms.TextBox();
        this.btnSearchProducts = new System.Windows.Forms.Button();
        this.lblProductFilterText = new System.Windows.Forms.Label();
        this.lblProductFilterColumn = new System.Windows.Forms.Label();
        this.cboProductFilterColumn = new System.Windows.Forms.ComboBox();
        this.chkShowOutOfStock = new System.Windows.Forms.CheckBox();
        this.lstProductCategories = new System.Windows.Forms.ListBox();
        this.lblCategoryFilterHeader = new System.Windows.Forms.Label();
        this.gridCustomers = new System.Windows.Forms.DataGridView();
        this.btnCustomerRefresh = new System.Windows.Forms.Button();
        this.btnCustomerAdd = new System.Windows.Forms.Button();
        this.btnCustomerEdit = new System.Windows.Forms.Button();
        this.btnCustomerDelete = new System.Windows.Forms.Button();
        this.txtCustomerSearch = new System.Windows.Forms.TextBox();
        this.btnCustomerSearch = new System.Windows.Forms.Button();
        this.menuStrip1.SuspendLayout();
        this.toolStrip1.SuspendLayout();
        this.tabControl1.SuspendLayout();
        this.tabProducts.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.gridProducts)).BeginInit();
        this.SuspendLayout();
        // 
        // menuStrip1
        // 
        this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.systemToolStripMenuItem});
        this.menuStrip1.Location = new System.Drawing.Point(0, 0);
        this.menuStrip1.Name = "menuStrip1";
        this.menuStrip1.Size = new System.Drawing.Size(1200, 24);
        this.menuStrip1.TabIndex = 0;
        this.menuStrip1.Text = "menuStrip1";
        // 
        // fileToolStripMenuItem
        // 
        this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.settingsToolStripMenuItem,
            new System.Windows.Forms.ToolStripSeparator(),
            this.signOutToolStripMenuItem,
            new System.Windows.Forms.ToolStripSeparator(),
            this.exitToolStripMenuItem});
        this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
        this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
        this.fileToolStripMenuItem.Text = "File";
        // 
        // settingsToolStripMenuItem
        // 
        this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
        this.settingsToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
        this.settingsToolStripMenuItem.Text = "Settings...";
        this.settingsToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Oemcomma;
        this.settingsToolStripMenuItem.Click += new System.EventHandler(this.btnSettings_Click);
        // 
        // signOutToolStripMenuItem
        // 
        this.signOutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.signOutToolStripMenuItem.Name = "signOutToolStripMenuItem";
        this.signOutToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
        this.signOutToolStripMenuItem.Text = "Sign Out";
        this.signOutToolStripMenuItem.Click += new System.EventHandler(this.signOutToolStripMenuItem_Click);
        // 
        // exitToolStripMenuItem
        // 
        this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
        this.exitToolStripMenuItem.Size = new System.Drawing.Size(93, 22);
        this.exitToolStripMenuItem.Text = "Exit";
        this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
        // 
        // toolStrip1
        // 
        this.toolStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
        // items will be added after buttons are instantiated below
        this.toolStrip1.Location = new System.Drawing.Point(0, 24);
        this.toolStrip1.Name = "toolStrip1";
        this.toolStrip1.Size = new System.Drawing.Size(1200, 27);
        this.toolStrip1.TabIndex = 1;
        this.toolStrip1.Text = "toolStrip1";
        // 
        // btnCreateInvoice
        // 
        this.btnCreateInvoice.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.btnCreateInvoice.Name = "btnCreateInvoice";
        this.btnCreateInvoice.Size = new System.Drawing.Size(93, 24);
        this.btnCreateInvoice.Text = "Create Invoice";
        // 
        // btnProducts
        // 
        this.btnProducts.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.btnProducts.Name = "btnProducts";
        this.btnProducts.Size = new System.Drawing.Size(61, 24);
        this.btnProducts.Text = "Products";
        this.btnProducts.Click += new System.EventHandler(this.btnProducts_Click);
        // 
        // btnCustomers
        // 
        this.btnCustomers.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.btnCustomers.Name = "btnCustomers";
        this.btnCustomers.Size = new System.Drawing.Size(73, 24);
        this.btnCustomers.Text = "Customers";
        // 

        // btnPurchases
        this.btnPurchases = new System.Windows.Forms.ToolStripButton();
        this.btnPurchases.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.btnPurchases.Name = "btnPurchases";
        this.btnPurchases.Size = new System.Drawing.Size(69, 24);
        this.btnPurchases.Text = "Purchases";
        this.btnPurchases.Click += new System.EventHandler(this.btnPurchases_Click);

        // btnAlerts
        this.btnAlerts = new System.Windows.Forms.ToolStripButton();
        this.btnAlerts.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.btnAlerts.Name = "btnAlerts";
        this.btnAlerts.Size = new System.Drawing.Size(44, 24);
        this.btnAlerts.Text = "Alerts";
        this.btnAlerts.Click += new System.EventHandler(this.btnAlerts_Click);

        // btnSettingsNav
        this.btnSettingsNav = new System.Windows.Forms.ToolStripButton();
        this.btnSettingsNav.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.btnSettingsNav.Name = "btnSettingsNav";
        this.btnSettingsNav.Size = new System.Drawing.Size(75, 24);
        this.btnSettingsNav.Text = "⚙ Settings";
        this.btnSettingsNav.Click += new System.EventHandler(this.btnSettingsNav_Click);

        // 
        // systemToolStripMenuItem
        // 
        this.systemToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.backupToolStripMenuItem,
            this.restoreToolStripMenuItem});
        this.systemToolStripMenuItem.Name = "systemToolStripMenuItem";
        this.systemToolStripMenuItem.Size = new System.Drawing.Size(57, 20);
        this.systemToolStripMenuItem.Text = "System";

        // 
        // backupToolStripMenuItem
        // 
        this.backupToolStripMenuItem.Name = "backupToolStripMenuItem";
        this.backupToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
        this.backupToolStripMenuItem.Text = "Backup Database";
        this.backupToolStripMenuItem.Click += new System.EventHandler(this.btnBackup_Click);

        // 
        // restoreToolStripMenuItem
        // 
        this.restoreToolStripMenuItem.Name = "restoreToolStripMenuItem";
        this.restoreToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
        this.restoreToolStripMenuItem.Text = "Restore Database";
        this.restoreToolStripMenuItem.Click += new System.EventHandler(this.btnRestore_Click);
        
        // Now add items to toolstrip (after instantiation)
        this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnCreateInvoice,
            this.btnProducts,
            this.btnCustomers,
            this.btnPurchases,
            this.btnAlerts,
            this.btnSettingsNav});
        // 
        // tabControl1
        // 
        this.tabControl1.Alignment = System.Windows.Forms.TabAlignment.Top;
        this.tabControl1.Controls.Add(this.tabInvoices);
        this.tabAlerts = new System.Windows.Forms.TabPage();
        this.tabControl1.Controls.Add(this.tabAlerts);
        this.tabControl1.Controls.Add(this.tabCustomers);
        this.tabControl1.Controls.Add(this.tabProducts);
        this.tabSettings = new System.Windows.Forms.TabPage();
        this.tabSettings.Text = "Settings";
        this.tabSettings.Name = "tabSettings";
        this.tabControl1.Controls.Add(this.tabSettings);
        this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
        this.tabControl1.Location = new System.Drawing.Point(0, 51);
        this.tabControl1.Name = "tabControl1";
        this.tabControl1.SelectedIndex = 0;
        this.tabControl1.Size = new System.Drawing.Size(1200, 649);
        this.tabControl1.TabIndex = 2;
        // 
        // tabInvoices
        // 
        this.tabInvoices.Controls.Add(this.btnInvoiceSearch);
        this.tabInvoices.Controls.Add(this.txtInvoiceSearch);
        this.tabInvoices.Controls.Add(this.btnInvoiceAddPayment);
        this.tabInvoices.Controls.Add(this.btnInvoiceDelete);
        this.tabInvoices.Controls.Add(this.btnInvoiceEdit);
        this.tabInvoices.Controls.Add(this.btnInvoiceAdd);
        this.tabInvoices.Controls.Add(this.btnInvoiceRefresh);
        this.tabInvoices.Controls.Add(this.gridInvoices);
        this.tabInvoices.Location = new System.Drawing.Point(4, 24);
        this.tabInvoices.Name = "tabInvoices";
        this.tabInvoices.Padding = new System.Windows.Forms.Padding(3);
        this.tabInvoices.Size = new System.Drawing.Size(1192, 621);
        this.tabInvoices.TabIndex = 0;
        this.tabInvoices.Text = "Invoices";
        this.tabInvoices.UseVisualStyleBackColor = true;
        // 
        // tabCustomers
        // 
        // add a top panel to host action/search controls and avoid overlap with the grid
        this.pnlCustomersTop = new System.Windows.Forms.Panel();
        this.pnlCustomersTop.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlCustomersTop.Height = 40;
        this.pnlCustomersTop.Padding = new System.Windows.Forms.Padding(0);

        // move existing controls into the top panel
        this.pnlCustomersTop.Controls.Add(this.btnCustomerRefresh);
        this.pnlCustomersTop.Controls.Add(this.btnCustomerAdd);
        this.pnlCustomersTop.Controls.Add(this.btnCustomerEdit);
        this.pnlCustomersTop.Controls.Add(this.btnCustomerDelete);
        this.pnlCustomersTop.Controls.Add(this.txtCustomerSearch);
        this.pnlCustomersTop.Controls.Add(this.btnCustomerSearch);

        // anchor search inputs to the right within the panel
        this.txtCustomerSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnCustomerSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));

        // give the tab some padding so Fill dock respects margins
        this.tabCustomers.Padding = new System.Windows.Forms.Padding(0);

        // add panel first, then the grid which will fill remaining space
        this.tabCustomers.Controls.Add(this.gridCustomers);
        this.tabCustomers.Controls.Add(this.pnlCustomersTop);
        this.tabCustomers.Location = new System.Drawing.Point(4, 24);
        this.tabCustomers.Name = "tabCustomers";
        this.tabCustomers.Size = new System.Drawing.Size(1192, 621);
        this.tabCustomers.TabIndex = 4;
        this.tabCustomers.Text = "Customers";
        this.tabCustomers.UseVisualStyleBackColor = true;
        // 
        // tabAlerts
        // 
        this.tabAlerts.Location = new System.Drawing.Point(4, 24);
        this.tabAlerts.Name = "tabAlerts";
        this.tabAlerts.Size = new System.Drawing.Size(1192, 621);
        this.tabAlerts.TabIndex = 8;
        this.tabAlerts.Text = "Alerts";
        this.tabAlerts.UseVisualStyleBackColor = true;

        // pnlAlertsTop
        this.pnlAlertsTop = new System.Windows.Forms.Panel();
        this.pnlAlertsTop.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlAlertsTop.Height = 44;
        this.pnlAlertsTop.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);

        this.lblAlertsHeader = new System.Windows.Forms.Label();
        this.lblAlertsHeader.AutoSize = true;
        this.lblAlertsHeader.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold);
        this.lblAlertsHeader.Location = new System.Drawing.Point(10, 13);
        this.lblAlertsHeader.Text = "Near Expiry Filters:";

        this.lblAlertsDefaultDays = new System.Windows.Forms.Label();
        this.lblAlertsDefaultDays.AutoSize = true;
        this.lblAlertsDefaultDays.Location = new System.Drawing.Point(140, 14);
        this.lblAlertsDefaultDays.Text = "Default days:";

        this.numAlertsDefaultDays = new System.Windows.Forms.NumericUpDown();
        this.numAlertsDefaultDays.Minimum = 1; this.numAlertsDefaultDays.Maximum = 365; this.numAlertsDefaultDays.Value = 30;
        this.numAlertsDefaultDays.Location = new System.Drawing.Point(220, 10);
        this.numAlertsDefaultDays.Size = new System.Drawing.Size(60, 23);

        this.lblAlertsMode = new System.Windows.Forms.Label();
        this.lblAlertsMode.AutoSize = true;
        this.lblAlertsMode.Location = new System.Drawing.Point(295, 14);
        this.lblAlertsMode.Text = "Mode:";

        this.cboAlertsFilterMode = new System.Windows.Forms.ComboBox();
        this.cboAlertsFilterMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cboAlertsFilterMode.Items.AddRange(new object[] { "Default Days", "Date Range", "Month", "Specific Date" });
        this.cboAlertsFilterMode.Location = new System.Drawing.Point(338, 10);
        this.cboAlertsFilterMode.Size = new System.Drawing.Size(130, 23);
        this.cboAlertsFilterMode.SelectedIndexChanged += new System.EventHandler(this.cboAlertsFilterMode_SelectedIndexChanged);

        this.dtpAlertsFrom = new System.Windows.Forms.DateTimePicker();
        this.dtpAlertsFrom.Location = new System.Drawing.Point(480, 10);
        this.dtpAlertsFrom.Size = new System.Drawing.Size(140, 23);
        this.dtpAlertsTo = new System.Windows.Forms.DateTimePicker();
        this.dtpAlertsTo.Location = new System.Drawing.Point(628, 10);
        this.dtpAlertsTo.Size = new System.Drawing.Size(140, 23);

        this.dtpAlertsMonth = new System.Windows.Forms.DateTimePicker();
        this.dtpAlertsMonth.CustomFormat = "MMMM yyyy";
        this.dtpAlertsMonth.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
        this.dtpAlertsMonth.ShowUpDown = true;
        this.dtpAlertsMonth.Location = new System.Drawing.Point(480, 10);
        this.dtpAlertsMonth.Size = new System.Drawing.Size(140, 23);

        this.dtpAlertsDate = new System.Windows.Forms.DateTimePicker();
        this.dtpAlertsDate.Location = new System.Drawing.Point(480, 10);
        this.dtpAlertsDate.Size = new System.Drawing.Size(140, 23);

        this.btnAlertsApply = new System.Windows.Forms.Button();
        this.btnAlertsApply.Text = "Apply Filter";
        this.btnAlertsApply.Location = new System.Drawing.Point(790, 8);
        this.btnAlertsApply.Size = new System.Drawing.Size(90, 28);
        this.btnAlertsApply.Click += new System.EventHandler(this.btnAlertsApply_Click);

        this.btnAlertsRefresh = new System.Windows.Forms.Button();
        this.btnAlertsRefresh.Text = "Reset / Refresh";
        this.btnAlertsRefresh.Location = new System.Drawing.Point(888, 8);
        this.btnAlertsRefresh.Size = new System.Drawing.Size(110, 28);
        this.btnAlertsRefresh.Click += new System.EventHandler(this.btnAlertsRefresh_Click);

        this.pnlAlertsTop.Controls.AddRange(new System.Windows.Forms.Control[] {
            this.lblAlertsHeader, this.lblAlertsDefaultDays, this.numAlertsDefaultDays,
            this.lblAlertsMode, this.cboAlertsFilterMode,
            this.dtpAlertsFrom, this.dtpAlertsTo, this.dtpAlertsMonth, this.dtpAlertsDate,
            this.btnAlertsApply, this.btnAlertsRefresh
        });

        // gridNearExpiry
        this.gridNearExpiry = new System.Windows.Forms.DataGridView();
        this.gridNearExpiry.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right))));
        this.gridNearExpiry.Location = new System.Drawing.Point(10, 50);
        this.gridNearExpiry.Size = new System.Drawing.Size(1172, 280);
        this.gridNearExpiry.RowHeadersVisible = false;
        this.gridNearExpiry.AllowUserToAddRows = false;
        this.gridNearExpiry.AutoGenerateColumns = false;
        this.gridNearExpiry.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 260 });
        this.gridNearExpiry.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "MRP", DataPropertyName = "Mrp", Width = 90, DefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle { Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
        this.gridNearExpiry.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Batch", DataPropertyName = "Batch", Width = 120 });
        this.gridNearExpiry.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Stock", DataPropertyName = "Stock", Width = 90, DefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle { Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight, Format = "0.##" } });
        this.gridNearExpiry.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Expiry", DataPropertyName = "Expiry", Width = 120 });
        this.gridNearExpiry.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Days Left", DataPropertyName = "DaysLeft", Width = 90 });

        // Low stock header label
        this.lblLowStockHeader = new System.Windows.Forms.Label();
        this.lblLowStockHeader.AutoSize = true;
        this.lblLowStockHeader.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
        this.lblLowStockHeader.Location = new System.Drawing.Point(10, 335);
        this.lblLowStockHeader.Text = "Low Stock Alerts:";

        // gridLowStock
        this.gridLowStock = new System.Windows.Forms.DataGridView();
        this.gridLowStock.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.gridLowStock.Location = new System.Drawing.Point(10, 355);
        this.gridLowStock.Size = new System.Drawing.Size(1172, 256);
        this.gridLowStock.RowHeadersVisible = false;
        this.gridLowStock.AllowUserToAddRows = false;
        this.gridLowStock.AutoGenerateColumns = false;
        this.gridLowStock.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 260 });
        this.gridLowStock.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Batch", DataPropertyName = "Batch", Width = 120 });
        this.gridLowStock.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Stock", DataPropertyName = "Stock", Width = 100, DefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle { Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight, Format = "0.##" } });
        this.gridLowStock.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Threshold", DataPropertyName = "Threshold", Width = 110, DefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle { Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight, Format = "0.##" } });

        // finally add alerts controls to tab
        this.tabAlerts.Controls.Add(this.gridLowStock);
        this.tabAlerts.Controls.Add(this.lblLowStockHeader);
        this.tabAlerts.Controls.Add(this.gridNearExpiry);
        this.tabAlerts.Controls.Add(this.pnlAlertsTop);

        // tabProducts
        // 
        this.tabProducts.Controls.Add(this.lblCategoryFilterHeader);
        this.tabProducts.Controls.Add(this.lstProductCategories);
        this.tabProducts.Controls.Add(this.chkShowOutOfStock);
        this.tabProducts.Controls.Add(this.cboProductFilterColumn);
        this.tabProducts.Controls.Add(this.lblProductFilterColumn);
        this.tabProducts.Controls.Add(this.lblExpiryFilterMode);
        this.tabProducts.Controls.Add(this.cboExpiryFilterMode);
        this.tabProducts.Controls.Add(this.dtpExpiryFrom);
        this.tabProducts.Controls.Add(this.dtpExpiryTo);
        this.tabProducts.Controls.Add(this.dtpExpiryMonth);
        this.tabProducts.Controls.Add(this.dtpExpiryDate);
        this.tabProducts.Controls.Add(this.btnApplyExpiryFilter);
        this.tabProducts.Controls.Add(this.btnResetExpiryFilter);
        this.tabProducts.Controls.Add(this.lblProductFilterText);
        this.tabProducts.Controls.Add(this.btnSearchProducts);
        this.tabProducts.Controls.Add(this.txtSearchProducts);
        this.tabProducts.Controls.Add(this.btnImportProducts);
        this.tabProducts.Controls.Add(this.btnExportProducts);
        this.tabProducts.Controls.Add(this.btnExportProductsPdf);
        this.tabProducts.Controls.Add(this.btnDeleteProduct);
        this.tabProducts.Controls.Add(this.btnEditProduct);
        this.tabProducts.Controls.Add(this.btnAddProduct);
        this.tabProducts.Controls.Add(this.btnRefreshProducts);
        this.tabProducts.Controls.Add(this.gridProducts);
        this.tabProducts.Location = new System.Drawing.Point(4, 24);
        this.tabProducts.Name = "tabProducts";
        this.tabProducts.Size = new System.Drawing.Size(1192, 621);
        this.tabProducts.TabIndex = 5;
        this.tabProducts.Text = "Products/Services";
        this.tabProducts.UseVisualStyleBackColor = true;
        // 
        // gridProducts
        // 
        this.gridProducts.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.gridProducts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this.gridProducts.Location = new System.Drawing.Point(10, 88);
        this.gridProducts.Name = "gridProducts";
        this.gridProducts.RowHeadersVisible = false;
        this.gridProducts.Size = new System.Drawing.Size(970, 520);
        this.gridProducts.TabIndex = 0;
        // 
        // btnRefreshProducts
        // 
        this.btnRefreshProducts.Location = new System.Drawing.Point(10, 44);
        this.btnRefreshProducts.Name = "btnRefreshProducts";
        this.btnRefreshProducts.Size = new System.Drawing.Size(120, 30);
        this.btnRefreshProducts.TabIndex = 1;
        this.btnRefreshProducts.Text = "Refresh";
        this.btnRefreshProducts.UseVisualStyleBackColor = true;
        this.btnRefreshProducts.Click += new System.EventHandler(this.btnRefreshProducts_Click);
        // 
        // btnAddProduct
        // 
        this.btnAddProduct.Location = new System.Drawing.Point(140, 44);
        this.btnAddProduct.Name = "btnAddProduct";
        this.btnAddProduct.Size = new System.Drawing.Size(100, 30);
        this.btnAddProduct.TabIndex = 2;
        this.btnAddProduct.Text = "Add";
        this.btnAddProduct.UseVisualStyleBackColor = true;
        this.btnAddProduct.Click += new System.EventHandler(this.btnAddProduct_Click);
        // 
        // btnEditProduct
        // 
        this.btnEditProduct.Location = new System.Drawing.Point(246, 44);
        this.btnEditProduct.Name = "btnEditProduct";
        this.btnEditProduct.Size = new System.Drawing.Size(100, 30);
        this.btnEditProduct.TabIndex = 3;
        this.btnEditProduct.Text = "Edit";
        this.btnEditProduct.UseVisualStyleBackColor = true;
        this.btnEditProduct.Click += new System.EventHandler(this.btnEditProduct_Click);
        // 
        // btnDeleteProduct
        // 
        this.btnDeleteProduct.Location = new System.Drawing.Point(352, 44);
        this.btnDeleteProduct.Name = "btnDeleteProduct";
        this.btnDeleteProduct.Size = new System.Drawing.Size(100, 30);
        this.btnDeleteProduct.TabIndex = 4;
        this.btnDeleteProduct.Text = "Delete";
        this.btnDeleteProduct.UseVisualStyleBackColor = true;
        this.btnDeleteProduct.Click += new System.EventHandler(this.btnDeleteProduct_Click);
        // 
        // btnExportProducts
        // 
        this.btnExportProducts.Location = new System.Drawing.Point(458, 44);
        this.btnExportProducts.Name = "btnExportProducts";
        this.btnExportProducts.Size = new System.Drawing.Size(100, 30);
        this.btnExportProducts.TabIndex = 5;
        this.btnExportProducts.Text = "Export Excel";
        this.btnExportProducts.UseVisualStyleBackColor = true;
        this.btnExportProducts.Click += new System.EventHandler(this.btnExportProducts_Click);
        // 
        // btnImportProducts
        // 
        this.btnImportProducts.Location = new System.Drawing.Point(670, 44);
        this.btnImportProducts.Name = "btnImportProducts";
        this.btnImportProducts.Size = new System.Drawing.Size(100, 30);
        this.btnImportProducts.TabIndex = 6;
        this.btnImportProducts.Text = "Import Excel";
        this.btnImportProducts.UseVisualStyleBackColor = true;
        this.btnImportProducts.Click += new System.EventHandler(this.btnImportProducts_Click);
        // 
        // btnExportProductsPdf
        // 
        this.btnExportProductsPdf.Location = new System.Drawing.Point(564, 44);
        this.btnExportProductsPdf.Name = "btnExportProductsPdf";
        this.btnExportProductsPdf.Size = new System.Drawing.Size(100, 30);
        this.btnExportProductsPdf.TabIndex = 16;
        this.btnExportProductsPdf.Text = "Export PDF";
        this.btnExportProductsPdf.UseVisualStyleBackColor = true;
        this.btnExportProductsPdf.Click += new System.EventHandler(this.btnExportProductsPdf_Click);
        // 
        // txtSearchProducts
        // 
        this.txtSearchProducts.Location = new System.Drawing.Point(130, 14);
        this.txtSearchProducts.Name = "txtSearchProducts";
        this.txtSearchProducts.PlaceholderText = "Enter filter text";
        this.txtSearchProducts.Size = new System.Drawing.Size(250, 23);
        this.txtSearchProducts.TabIndex = 7;
        // 
        // btnSearchProducts
        // 
        this.btnSearchProducts.Location = new System.Drawing.Point(386, 10);
        this.btnSearchProducts.Name = "btnSearchProducts";
        this.btnSearchProducts.Size = new System.Drawing.Size(80, 30);
        this.btnSearchProducts.TabIndex = 8;
        this.btnSearchProducts.Text = "Search";
        this.btnSearchProducts.UseVisualStyleBackColor = true;
        this.btnSearchProducts.Click += new System.EventHandler(this.btnSearchProducts_Click);
        // 
        // lblProductFilterText
        // 
        this.lblProductFilterText.AutoSize = true;
        this.lblProductFilterText.Location = new System.Drawing.Point(10, 18);
        this.lblProductFilterText.Name = "lblProductFilterText";
        this.lblProductFilterText.Size = new System.Drawing.Size(88, 15);
        this.lblProductFilterText.TabIndex = 9;
        this.lblProductFilterText.Text = "Enter filter text";
        // 
        // lblProductFilterColumn
        // 
        this.lblProductFilterColumn.AutoSize = true;
        this.lblProductFilterColumn.Location = new System.Drawing.Point(480, 18);
        this.lblProductFilterColumn.Name = "lblProductFilterColumn";
        this.lblProductFilterColumn.Size = new System.Drawing.Size(86, 15);
        this.lblProductFilterColumn.TabIndex = 10;
        this.lblProductFilterColumn.Text = "Filtered column";
        // 
        // cboProductFilterColumn
        // 
        this.cboProductFilterColumn.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cboProductFilterColumn.Items.AddRange(new object[] {
            "Product/Service Name",
            "ID/SKU"});
        this.cboProductFilterColumn.Location = new System.Drawing.Point(572, 14);
        this.cboProductFilterColumn.Name = "cboProductFilterColumn";
        this.cboProductFilterColumn.Size = new System.Drawing.Size(200, 23);
        this.cboProductFilterColumn.TabIndex = 11;
        this.cboProductFilterColumn.SelectedIndexChanged += new System.EventHandler(this.cboProductFilterColumn_SelectedIndexChanged);
        // 
        // chkShowOutOfStock
        // 
        this.chkShowOutOfStock.AutoSize = true;
        this.chkShowOutOfStock.Location = new System.Drawing.Point(790, 16);
        this.chkShowOutOfStock.Name = "chkShowOutOfStock";
        this.chkShowOutOfStock.Size = new System.Drawing.Size(150, 19);
        this.chkShowOutOfStock.TabIndex = 12;
        this.chkShowOutOfStock.Text = "Show products out of stock";
        this.chkShowOutOfStock.UseVisualStyleBackColor = true;
        this.chkShowOutOfStock.CheckedChanged += new System.EventHandler(this.chkShowOutOfStock_CheckedChanged);
        // 
        // lstProductCategories
        // 
        this.lstProductCategories.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right))));
        this.lstProductCategories.FormattingEnabled = true;
        this.lstProductCategories.IntegralHeight = false;
        this.lstProductCategories.Location = new System.Drawing.Point(990, 88);
        this.lstProductCategories.Name = "lstProductCategories";
        this.lstProductCategories.Size = new System.Drawing.Size(190, 520);
        this.lstProductCategories.TabIndex = 13;
        this.lstProductCategories.SelectedIndexChanged += new System.EventHandler(this.lstProductCategories_SelectedIndexChanged);
        // 
        // lblCategoryFilterHeader
        // 
        this.lblCategoryFilterHeader.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right))));
        this.lblCategoryFilterHeader.Location = new System.Drawing.Point(990, 58);
        this.lblCategoryFilterHeader.Name = "lblCategoryFilterHeader";
        this.lblCategoryFilterHeader.Size = new System.Drawing.Size(190, 23);
        this.lblCategoryFilterHeader.TabIndex = 14;
        this.lblCategoryFilterHeader.Text = "View filter by category";

        // lblExpiryFilterMode
        this.lblExpiryFilterMode = new System.Windows.Forms.Label();
        this.lblExpiryFilterMode.AutoSize = true;
        this.lblExpiryFilterMode.Location = new System.Drawing.Point(10, 58);
        this.lblExpiryFilterMode.Name = "lblExpiryFilterMode";
        this.lblExpiryFilterMode.Size = new System.Drawing.Size(72, 15);
        this.lblExpiryFilterMode.TabIndex = 17;
        this.lblExpiryFilterMode.Text = "Expiry filter";

        // cboExpiryFilterMode
        this.cboExpiryFilterMode = new System.Windows.Forms.ComboBox();
        this.cboExpiryFilterMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cboExpiryFilterMode.Items.AddRange(new object[] {
            "None",
            "Date Range",
            "Month",
            "Specific Date"});
        this.cboExpiryFilterMode.Location = new System.Drawing.Point(90, 54);
        this.cboExpiryFilterMode.Name = "cboExpiryFilterMode";
        this.cboExpiryFilterMode.Size = new System.Drawing.Size(120, 23);
        this.cboExpiryFilterMode.TabIndex = 18;
        this.cboExpiryFilterMode.SelectedIndexChanged += new System.EventHandler(this.cboExpiryFilterMode_SelectedIndexChanged);

        // dtpExpiryFrom
        this.dtpExpiryFrom = new System.Windows.Forms.DateTimePicker();
        this.dtpExpiryFrom.Location = new System.Drawing.Point(220, 54);
        this.dtpExpiryFrom.Name = "dtpExpiryFrom";
        this.dtpExpiryFrom.Size = new System.Drawing.Size(180, 23);
        this.dtpExpiryFrom.TabIndex = 19;

        // dtpExpiryTo
        this.dtpExpiryTo = new System.Windows.Forms.DateTimePicker();
        this.dtpExpiryTo.Location = new System.Drawing.Point(410, 54);
        this.dtpExpiryTo.Name = "dtpExpiryTo";
        this.dtpExpiryTo.Size = new System.Drawing.Size(180, 23);
        this.dtpExpiryTo.TabIndex = 20;

        // dtpExpiryMonth
        this.dtpExpiryMonth = new System.Windows.Forms.DateTimePicker();
        this.dtpExpiryMonth.CustomFormat = "MMMM yyyy";
        this.dtpExpiryMonth.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
        this.dtpExpiryMonth.ShowUpDown = true;
        this.dtpExpiryMonth.Location = new System.Drawing.Point(220, 54);
        this.dtpExpiryMonth.Name = "dtpExpiryMonth";
        this.dtpExpiryMonth.Size = new System.Drawing.Size(180, 23);
        this.dtpExpiryMonth.TabIndex = 21;

        // dtpExpiryDate
        this.dtpExpiryDate = new System.Windows.Forms.DateTimePicker();
        this.dtpExpiryDate.Location = new System.Drawing.Point(220, 54);
        this.dtpExpiryDate.Name = "dtpExpiryDate";
        this.dtpExpiryDate.Size = new System.Drawing.Size(180, 23);
        this.dtpExpiryDate.TabIndex = 22;

        // btnApplyExpiryFilter
        this.btnApplyExpiryFilter = new System.Windows.Forms.Button();
        this.btnApplyExpiryFilter.Location = new System.Drawing.Point(600, 50);
        this.btnApplyExpiryFilter.Name = "btnApplyExpiryFilter";
        this.btnApplyExpiryFilter.Size = new System.Drawing.Size(80, 30);
        this.btnApplyExpiryFilter.TabIndex = 23;
        this.btnApplyExpiryFilter.Text = "Apply";
        this.btnApplyExpiryFilter.UseVisualStyleBackColor = true;
        this.btnApplyExpiryFilter.Click += new System.EventHandler(this.btnApplyExpiryFilter_Click);

        // btnResetExpiryFilter
        this.btnResetExpiryFilter = new System.Windows.Forms.Button();
        this.btnResetExpiryFilter.Location = new System.Drawing.Point(686, 50);
        this.btnResetExpiryFilter.Name = "btnResetExpiryFilter";
        this.btnResetExpiryFilter.Size = new System.Drawing.Size(80, 30);
        this.btnResetExpiryFilter.TabIndex = 24;
        this.btnResetExpiryFilter.Text = "Reset";
        this.btnResetExpiryFilter.UseVisualStyleBackColor = true;
        this.btnResetExpiryFilter.Click += new System.EventHandler(this.btnResetExpiryFilter_Click);
        // 
        // gridInvoices
        // 
        this.gridInvoices.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.gridInvoices.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this.gridInvoices.Location = new System.Drawing.Point(10, 48);
        this.gridInvoices.Name = "gridInvoices";
        this.gridInvoices.RowHeadersVisible = false;
        this.gridInvoices.Size = new System.Drawing.Size(1172, 560);
        this.gridInvoices.TabIndex = 16;
        // 
        // btnInvoiceRefresh
        // 
        this.btnInvoiceRefresh.Location = new System.Drawing.Point(10, 10);
        this.btnInvoiceRefresh.Name = "btnInvoiceRefresh";
        this.btnInvoiceRefresh.Size = new System.Drawing.Size(120, 30);
        this.btnInvoiceRefresh.TabIndex = 17;
        this.btnInvoiceRefresh.Text = "Refresh";
        this.btnInvoiceRefresh.UseVisualStyleBackColor = true;
        this.btnInvoiceRefresh.Click += new System.EventHandler(this.btnInvoiceRefresh_Click);
        // 
        // btnInvoiceAdd
        // 
        this.btnInvoiceAdd.Location = new System.Drawing.Point(140, 10);
        this.btnInvoiceAdd.Name = "btnInvoiceAdd";
        this.btnInvoiceAdd.Size = new System.Drawing.Size(100, 30);
        this.btnInvoiceAdd.TabIndex = 18;
        this.btnInvoiceAdd.Text = "Add";
        this.btnInvoiceAdd.UseVisualStyleBackColor = true;
        this.btnInvoiceAdd.Click += new System.EventHandler(this.btnInvoiceAdd_Click);
        // 
        // btnInvoiceEdit
        // 
        this.btnInvoiceEdit.Location = new System.Drawing.Point(246, 10);
        this.btnInvoiceEdit.Name = "btnInvoiceEdit";
        this.btnInvoiceEdit.Size = new System.Drawing.Size(100, 30);
        this.btnInvoiceEdit.TabIndex = 19;
        this.btnInvoiceEdit.Text = "Edit";
        this.btnInvoiceEdit.UseVisualStyleBackColor = true;
        this.btnInvoiceEdit.Click += new System.EventHandler(this.btnInvoiceEdit_Click);
        // 
        // btnInvoiceDelete
        // 
        this.btnInvoiceDelete.Location = new System.Drawing.Point(352, 10);
        this.btnInvoiceDelete.Name = "btnInvoiceDelete";
        this.btnInvoiceDelete.Size = new System.Drawing.Size(100, 30);
        this.btnInvoiceDelete.TabIndex = 20;
        this.btnInvoiceDelete.Text = "Delete";
        this.btnInvoiceDelete.UseVisualStyleBackColor = true;
        this.btnInvoiceDelete.Click += new System.EventHandler(this.btnInvoiceDelete_Click);
        // 
        // btnInvoiceAddPayment
        // 
        this.btnInvoiceAddPayment.Location = new System.Drawing.Point(458, 10);
        this.btnInvoiceAddPayment.Name = "btnInvoiceAddPayment";
        this.btnInvoiceAddPayment.Size = new System.Drawing.Size(120, 30);
        this.btnInvoiceAddPayment.TabIndex = 21;
        this.btnInvoiceAddPayment.Text = "Add Payment";
        this.btnInvoiceAddPayment.UseVisualStyleBackColor = true;
        this.btnInvoiceAddPayment.Click += new System.EventHandler(this.btnInvoiceAddPayment_Click);
        // 
        // txtInvoiceSearch
        // 
        this.txtInvoiceSearch.Location = new System.Drawing.Point(780, 14);
        this.txtInvoiceSearch.Name = "txtInvoiceSearch";
        this.txtInvoiceSearch.PlaceholderText = "Number/Customer";
        this.txtInvoiceSearch.Size = new System.Drawing.Size(250, 23);
        this.txtInvoiceSearch.TabIndex = 21;
        // 
        // btnInvoiceSearch
        // 
        this.btnInvoiceSearch.Location = new System.Drawing.Point(1036, 10);
        this.btnInvoiceSearch.Name = "btnInvoiceSearch";
        this.btnInvoiceSearch.Size = new System.Drawing.Size(80, 30);
        this.btnInvoiceSearch.TabIndex = 22;
        this.btnInvoiceSearch.Text = "Search";
        this.btnInvoiceSearch.UseVisualStyleBackColor = true;
        this.btnInvoiceSearch.Click += new System.EventHandler(this.btnInvoiceSearch_Click);
        // 
        // gridCustomers
        // 
        this.gridCustomers.Dock = System.Windows.Forms.DockStyle.Fill;
        this.gridCustomers.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this.gridCustomers.Location = new System.Drawing.Point(0, 40);
        this.gridCustomers.Name = "gridCustomers";
        this.gridCustomers.RowHeadersVisible = false;
        this.gridCustomers.TabIndex = 9;
        // 
        // btnCustomerRefresh
        // 
        this.btnCustomerRefresh.Location = new System.Drawing.Point(10, 10);
        this.btnCustomerRefresh.Name = "btnCustomerRefresh";
        this.btnCustomerRefresh.Size = new System.Drawing.Size(120, 30);
        this.btnCustomerRefresh.TabIndex = 10;
        this.btnCustomerRefresh.Text = "Refresh";
        this.btnCustomerRefresh.UseVisualStyleBackColor = true;
        this.btnCustomerRefresh.Click += new System.EventHandler(this.btnCustomerRefresh_Click);
        // 
        // btnCustomerAdd
        // 
        this.btnCustomerAdd.Location = new System.Drawing.Point(140, 10);
        this.btnCustomerAdd.Name = "btnCustomerAdd";
        this.btnCustomerAdd.Size = new System.Drawing.Size(100, 30);
        this.btnCustomerAdd.TabIndex = 11;
        this.btnCustomerAdd.Text = "Add";
        this.btnCustomerAdd.UseVisualStyleBackColor = true;
        this.btnCustomerAdd.Click += new System.EventHandler(this.btnCustomerAdd_Click);
        // 
        // btnCustomerEdit
        // 
        this.btnCustomerEdit.Location = new System.Drawing.Point(246, 10);
        this.btnCustomerEdit.Name = "btnCustomerEdit";
        this.btnCustomerEdit.Size = new System.Drawing.Size(100, 30);
        this.btnCustomerEdit.TabIndex = 12;
        this.btnCustomerEdit.Text = "Edit";
        this.btnCustomerEdit.UseVisualStyleBackColor = true;
        this.btnCustomerEdit.Click += new System.EventHandler(this.btnCustomerEdit_Click);
        // 
        // btnCustomerDelete
        // 
        this.btnCustomerDelete.Location = new System.Drawing.Point(352, 10);
        this.btnCustomerDelete.Name = "btnCustomerDelete";
        this.btnCustomerDelete.Size = new System.Drawing.Size(100, 30);
        this.btnCustomerDelete.TabIndex = 13;
        this.btnCustomerDelete.Text = "Delete";
        this.btnCustomerDelete.UseVisualStyleBackColor = true;
        this.btnCustomerDelete.Click += new System.EventHandler(this.btnCustomerDelete_Click);
        // 
        // txtCustomerSearch
        // 
        this.txtCustomerSearch.Location = new System.Drawing.Point(780, 10);
        this.txtCustomerSearch.Name = "txtCustomerSearch";
        this.txtCustomerSearch.PlaceholderText = "Search...";
        this.txtCustomerSearch.Size = new System.Drawing.Size(250, 23);
        this.txtCustomerSearch.TabIndex = 14;
        // 
        // btnCustomerSearch
        // 
        this.btnCustomerSearch.Location = new System.Drawing.Point(1036, 8);
        this.btnCustomerSearch.Name = "btnCustomerSearch";
        this.btnCustomerSearch.Size = new System.Drawing.Size(80, 30);
        this.btnCustomerSearch.TabIndex = 15;
        this.btnCustomerSearch.Text = "Search";
        this.btnCustomerSearch.UseVisualStyleBackColor = true;
        this.btnCustomerSearch.Click += new System.EventHandler(this.btnCustomerSearch_Click);
        // 
        // Form1
        // 
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1200, 700);
        this.Controls.Add(this.tabControl1);
        this.Controls.Add(this.toolStrip1);
        this.Controls.Add(this.menuStrip1);
        this.MainMenuStrip = this.menuStrip1;
        this.Name = "Form1";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "BillingSuite";
        this.Load += new System.EventHandler(this.Form1_Load);
        this.menuStrip1.ResumeLayout(false);
        this.menuStrip1.PerformLayout();
        this.toolStrip1.ResumeLayout(false);
        this.toolStrip1.PerformLayout();
        this.tabControl1.ResumeLayout(false);
        this.tabProducts.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this.gridProducts)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.MenuStrip menuStrip1;
    private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
    private System.Windows.Forms.ToolStrip toolStrip1;
    private System.Windows.Forms.ToolStripButton btnCreateInvoice;
    private System.Windows.Forms.ToolStripButton btnProducts;
    private System.Windows.Forms.ToolStripButton btnCustomers;
    private System.Windows.Forms.ToolStripMenuItem systemToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem backupToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem restoreToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem signOutToolStripMenuItem;
    private System.Windows.Forms.ToolStripButton btnPurchases;
    private System.Windows.Forms.ToolStripButton btnAlerts;
    private System.Windows.Forms.TabControl tabControl1;
    private System.Windows.Forms.TabPage tabInvoices;
    private System.Windows.Forms.TabPage tabCustomers;
    private System.Windows.Forms.TabPage tabProducts;
    private System.Windows.Forms.DataGridView gridProducts;
    private System.Windows.Forms.Button btnRefreshProducts;
    private System.Windows.Forms.Button btnAddProduct;
    private System.Windows.Forms.Button btnEditProduct;
    private System.Windows.Forms.Button btnDeleteProduct;
    private System.Windows.Forms.Button btnExportProducts;
    private System.Windows.Forms.Button btnImportProducts;
    private System.Windows.Forms.TextBox txtSearchProducts;
    private System.Windows.Forms.Button btnSearchProducts;
    private System.Windows.Forms.DataGridView gridCustomers;
    private System.Windows.Forms.Button btnCustomerRefresh;
    private System.Windows.Forms.Button btnCustomerAdd;
    private System.Windows.Forms.Button btnCustomerEdit;
    private System.Windows.Forms.Button btnCustomerDelete;
    private System.Windows.Forms.TextBox txtCustomerSearch;
    private System.Windows.Forms.Button btnCustomerSearch;
    private System.Windows.Forms.DataGridView gridInvoices;
    private System.Windows.Forms.Button btnInvoiceRefresh;
    private System.Windows.Forms.Button btnInvoiceAdd;
    private System.Windows.Forms.Button btnInvoiceEdit;
    private System.Windows.Forms.Button btnInvoiceDelete;
    private System.Windows.Forms.Button btnInvoiceAddPayment;
    private System.Windows.Forms.TextBox txtInvoiceSearch;
    private System.Windows.Forms.Button btnInvoiceSearch;
    private System.Windows.Forms.DateTimePicker dtpReportFrom;
    private System.Windows.Forms.DateTimePicker dtpReportTo;
    private System.Windows.Forms.CheckBox chkReportPaid;
    private System.Windows.Forms.CheckBox chkReportUnpaid;
    private System.Windows.Forms.CheckBox chkReportVoid;
    private System.Windows.Forms.Button btnRunReport;
    private System.Windows.Forms.Button btnExportReportPdf;
    private System.Windows.Forms.Button btnExportReportExcel;
    private System.Windows.Forms.Label lblProductFilterText;
    private System.Windows.Forms.Label lblProductFilterColumn;
    private System.Windows.Forms.ComboBox cboProductFilterColumn;
    private System.Windows.Forms.CheckBox chkShowOutOfStock;
    private System.Windows.Forms.ListBox lstProductCategories;
    private System.Windows.Forms.Label lblCategoryFilterHeader;
    private System.Windows.Forms.Button btnExportProductsPdf;
    private System.Windows.Forms.Label lblExpiryFilterMode;
    private System.Windows.Forms.ComboBox cboExpiryFilterMode;
    private System.Windows.Forms.DateTimePicker dtpExpiryFrom;
    private System.Windows.Forms.DateTimePicker dtpExpiryTo;
    private System.Windows.Forms.DateTimePicker dtpExpiryMonth;
    private System.Windows.Forms.DateTimePicker dtpExpiryDate;
    private System.Windows.Forms.Button btnApplyExpiryFilter;
    private System.Windows.Forms.Button btnResetExpiryFilter;
    private System.Windows.Forms.Panel pnlCustomersTop;
    private System.Windows.Forms.TabPage tabAlerts;
    private System.Windows.Forms.Panel pnlAlertsTop;
    private System.Windows.Forms.Label lblAlertsHeader;
    private System.Windows.Forms.Label lblAlertsDefaultDays;
    private System.Windows.Forms.NumericUpDown numAlertsDefaultDays;
    private System.Windows.Forms.Label lblAlertsMode;
    private System.Windows.Forms.ComboBox cboAlertsFilterMode;
    private System.Windows.Forms.DateTimePicker dtpAlertsFrom;
    private System.Windows.Forms.DateTimePicker dtpAlertsTo;
    private System.Windows.Forms.DateTimePicker dtpAlertsMonth;
    private System.Windows.Forms.DateTimePicker dtpAlertsDate;
    private System.Windows.Forms.Button btnAlertsApply;
    private System.Windows.Forms.Button btnAlertsRefresh;
    private System.Windows.Forms.DataGridView gridNearExpiry;
    private System.Windows.Forms.DataGridView gridLowStock;
    private System.Windows.Forms.Label lblLowStockHeader;
    private System.Windows.Forms.TabPage tabSettings;
    private System.Windows.Forms.ToolStripButton btnSettingsNav;
}
