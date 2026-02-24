using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HelloWpf.Data;
using HelloWpf.Models;
using HelloWpf.Services;

namespace HelloWpf;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, SubMenuConfig> _menuConfigs;
    private readonly CustomerService _customerService;
    private readonly CategoryService _categoryService;
    private readonly SupplierService _supplierService;
    private bool _menuReady;
    private string _currentMenuKey = "dashboard_overview";
    private int _customerPageNumber = 1;
    private int _categoryPageNumber = 1;
    private int _supplierPageNumber = 1;
    private const int CustomerPageSize = 50;

    public MainWindow()
    {
        InitializeComponent();
        var dbPath = GetDatabasePath();
        _customerService = new CustomerService(new SqliteCustomerRepository(dbPath));
        _categoryService = new CategoryService(new SqliteCategoryRepository(dbPath));
        _supplierService = new SupplierService(new SqliteSupplierRepository(dbPath));
        _menuConfigs = BuildMenuConfigs();
        Loaded += (_, _) => _menuReady = true;
        ApplyConfig("dashboard_overview");
    }

    private void OnSubMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string key)
        {
            ExpandParentForKey(key);
            ApplyConfig(key);
        }
    }

    private void OnModuleExpanded(object sender, RoutedEventArgs e)
    {
        if (!_menuReady || sender is not Expander expanded)
        {
            return;
        }

        var expanders = new Expander?[]
        {
            DashboardExpander, MastersExpander, SalesExpander, PurchaseExpander, InventoryExpander, FinanceExpander, ReportsExpander
        };

        foreach (var expander in expanders)
        {
            if (expander is not null && !ReferenceEquals(expander, expanded))
            {
                expander.IsExpanded = false;
            }
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        Field1Input.Text = string.Empty;
        Field2Input.Text = string.Empty;
        Field3ComboInput.SelectedIndex = -1;
        Field4Input.Text = string.Empty;
        Field5DateInput.SelectedDate = null;
        Field6Input.Text = string.Empty;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_currentMenuKey == "masters_customer")
        {
            SaveCustomer();
            return;
        }
        if (_currentMenuKey == "masters_category")
        {
            SaveCategory();
            return;
        }
        if (_currentMenuKey == "masters_supplier")
        {
            SaveSupplier();
            return;
        }

        MessageBox.Show("Saved (demo only). No live database is connected.", "NxERP Pharmacy", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ApplyConfig(string key)
    {
        if (!_menuConfigs.TryGetValue(key, out var config))
        {
            return;
        }

        _currentMenuKey = key;
        var isDashboard = key == "dashboard_overview";
        DashboardPanel.Visibility = isDashboard ? Visibility.Visible : Visibility.Collapsed;
        FormSectionBorder.Visibility = isDashboard ? Visibility.Collapsed : Visibility.Visible;
        HistorySectionBorder.Visibility = isDashboard ? Visibility.Collapsed : Visibility.Visible;

        ModuleTitleText.Text = config.Module;
        SubMenuTitleText.Text = config.SubMenu;
        DescriptionText.Text = config.Description;
        BreadcrumbText.Text = $"{config.Module}  >  {config.SubMenu}";
        FormTitleText.Text = config.FormTitle;
        HistoryTitleText.Text = config.HistoryTitle;

        Field1Label.Text = config.FieldLabels[0];
        Field2Label.Text = config.FieldLabels[1];
        Field3Label.Text = config.FieldLabels[2];
        Field4Label.Text = config.FieldLabels[3];
        Field5Label.Text = config.FieldLabels[4];
        Field6Label.Text = config.FieldLabels[5];

        Field1Input.Text = config.DefaultValues[0];
        Field2Input.Text = config.DefaultValues[1];
        Field4Input.Text = config.DefaultValues[3];
        Field6Input.Text = config.DefaultValues[5];
        Field5DateInput.SelectedDate = DateTime.Today;

        Field3ComboInput.Items.Clear();
        foreach (var option in config.StatusOptions)
        {
            Field3ComboInput.Items.Add(option);
        }
        Field3ComboInput.SelectedIndex = Field3ComboInput.Items.Count > 0 ? 0 : -1;

        if (key == "masters_customer")
        {
            _customerPageNumber = 1;
            LoadCustomerPage();
        }
        else if (key == "masters_category")
        {
            _categoryPageNumber = 1;
            LoadCategoryPage();
        }
        else if (key == "masters_supplier")
        {
            _supplierPageNumber = 1;
            LoadSupplierPage();
        }
        else
        {
            HistoryGrid.ItemsSource = config.HistoryRows;
            PageInfoText.Text = string.Empty;
        }
        ReportPreviewText.Text = config.PreviewText;

        var crudMode = key is "masters_customer" or "masters_category" or "masters_supplier";
        AddButton.Visibility = crudMode ? Visibility.Visible : Visibility.Collapsed;
        PrevButton.Visibility = crudMode ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Visibility = crudMode ? Visibility.Visible : Visibility.Collapsed;
        DeleteButton.Visibility = crudMode ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.Content = crudMode ? "Save" : "Save (Demo)";
    }

    private void SaveCustomer()
    {
        var code = (Field1Input.Text ?? string.Empty).Trim();
        var name = (Field2Input.Text ?? string.Empty).Trim();
        var type = (Field3ComboInput.SelectedItem?.ToString() ?? Field3ComboInput.Text ?? "Retail").Trim();
        var contact = (Field4Input.Text ?? string.Empty).Trim();
        var openingDate = Field5DateInput.SelectedDate ?? DateTime.Today;
        _ = decimal.TryParse(Field6Input.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var openingBalance);

        try
        {
            _customerService.Upsert(new Customer
            {
                Code = code,
                Name = name,
                Type = string.IsNullOrWhiteSpace(type) ? "Retail" : type,
                Contact = contact,
                OpeningDate = openingDate,
                OpeningBalance = openingBalance,
                IsActive = true
            });

            LoadCustomerPage();
            MessageBox.Show("Customer saved in offline memory store.", "NxERP Pharmacy", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Customer Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveCategory()
    {
        try
        {
            _categoryService.Upsert(new Category
            {
                Code = (Field1Input.Text ?? string.Empty).Trim(),
                Name = (Field2Input.Text ?? string.Empty).Trim(),
                Type = (Field3ComboInput.Text ?? "Medicine").Trim(),
                ParentCategory = (Field4Input.Text ?? string.Empty).Trim(),
                CreatedDate = Field5DateInput.SelectedDate ?? DateTime.Today,
                IsActive = true
            });
            LoadCategoryPage();
            MessageBox.Show("Category saved in offline SQLite.", "NxERP Pharmacy", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Category Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveSupplier()
    {
        _ = decimal.TryParse(Field6Input.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var openingPayable);
        try
        {
            _supplierService.Upsert(new Supplier
            {
                Code = (Field1Input.Text ?? string.Empty).Trim(),
                Name = (Field2Input.Text ?? string.Empty).Trim(),
                Type = (Field3ComboInput.Text ?? "Distributor").Trim(),
                Contact = (Field4Input.Text ?? string.Empty).Trim(),
                OnboardDate = Field5DateInput.SelectedDate ?? DateTime.Today,
                OpeningPayable = openingPayable,
                IsActive = true
            });
            LoadSupplierPage();
            MessageBox.Show("Supplier saved in offline SQLite.", "NxERP Pharmacy", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Supplier Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadCustomerPage()
    {
        var search = (Field2Input.Text ?? string.Empty).Trim();
        var page = _customerService.GetPage(search, _customerPageNumber, CustomerPageSize);

        if (_customerPageNumber > page.TotalPages)
        {
            _customerPageNumber = page.TotalPages;
            page = _customerService.GetPage(search, _customerPageNumber, CustomerPageSize);
        }

        HistoryGrid.ItemsSource = page.Items
            .Select(c => new HistoryRow(
                c.Code,
                $"{c.Name} ({c.Type})",
                c.Contact,
                c.IsActive ? "Active" : "Inactive",
                c.OpeningBalance.ToString("0.##", CultureInfo.InvariantCulture)))
            .ToList();

        PageInfoText.Text = $"Page {page.PageNumber}/{page.TotalPages} | Total {page.TotalCount}";
    }

    private void LoadCategoryPage()
    {
        var search = (Field2Input.Text ?? string.Empty).Trim();
        var page = _categoryService.GetPage(search, _categoryPageNumber, CustomerPageSize);
        if (_categoryPageNumber > page.TotalPages)
        {
            _categoryPageNumber = page.TotalPages;
            page = _categoryService.GetPage(search, _categoryPageNumber, CustomerPageSize);
        }

        HistoryGrid.ItemsSource = page.Items
            .Select(c => new HistoryRow(c.Code, $"{c.Name} ({c.Type})", c.CreatedDate.ToString("yyyy-MM-dd"), c.IsActive ? "Active" : "Inactive", c.ParentCategory))
            .ToList();
        PageInfoText.Text = $"Page {page.PageNumber}/{page.TotalPages} | Total {page.TotalCount}";
    }

    private void LoadSupplierPage()
    {
        var search = (Field2Input.Text ?? string.Empty).Trim();
        var page = _supplierService.GetPage(search, _supplierPageNumber, CustomerPageSize);
        if (_supplierPageNumber > page.TotalPages)
        {
            _supplierPageNumber = page.TotalPages;
            page = _supplierService.GetPage(search, _supplierPageNumber, CustomerPageSize);
        }

        HistoryGrid.ItemsSource = page.Items
            .Select(s => new HistoryRow(s.Code, $"{s.Name} ({s.Type})", s.Contact, s.IsActive ? "Active" : "Inactive", s.OpeningPayable.ToString("0.##", CultureInfo.InvariantCulture)))
            .ToList();
        PageInfoText.Text = $"Page {page.PageNumber}/{page.TotalPages} | Total {page.TotalCount}";
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (_currentMenuKey == "masters_customer")
        {
            Field1Input.Text = $"CUS-{DateTime.Now:HHmmss}";
            Field2Input.Text = string.Empty;
            Field3ComboInput.Text = "Retail";
            Field4Input.Text = string.Empty;
            Field5DateInput.SelectedDate = DateTime.Today;
            Field6Input.Text = "0";
            return;
        }
        if (_currentMenuKey == "masters_category")
        {
            Field1Input.Text = $"CAT-{DateTime.Now:HHmmss}";
            Field2Input.Text = string.Empty;
            Field3ComboInput.Text = "Medicine";
            Field4Input.Text = string.Empty;
            Field5DateInput.SelectedDate = DateTime.Today;
            Field6Input.Text = string.Empty;
            return;
        }
        if (_currentMenuKey == "masters_supplier")
        {
            Field1Input.Text = $"SUP-{DateTime.Now:HHmmss}";
            Field2Input.Text = string.Empty;
            Field3ComboInput.Text = "Distributor";
            Field4Input.Text = string.Empty;
            Field5DateInput.SelectedDate = DateTime.Today;
            Field6Input.Text = "0";
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_currentMenuKey is not ("masters_customer" or "masters_category" or "masters_supplier"))
        {
            return;
        }

        var code = (Field1Input.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            MessageBox.Show("Enter/select customer code to delete.", "NxERP Pharmacy", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var deleted = _currentMenuKey switch
        {
            "masters_customer" => _customerService.Delete(code),
            "masters_category" => _categoryService.Delete(code),
            "masters_supplier" => _supplierService.Delete(code),
            _ => false
        };

        if (!deleted)
        {
            MessageBox.Show("Record not found.", "NxERP Pharmacy", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_currentMenuKey == "masters_customer") LoadCustomerPage();
        else if (_currentMenuKey == "masters_category") LoadCategoryPage();
        else if (_currentMenuKey == "masters_supplier") LoadSupplierPage();

        MessageBox.Show("Record deleted from offline SQLite.", "NxERP Pharmacy", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnPrevClick(object sender, RoutedEventArgs e)
    {
        if (_currentMenuKey == "masters_customer")
        {
            if (_customerPageNumber > 1)
            {
                _customerPageNumber--;
                LoadCustomerPage();
            }
        }
        else if (_currentMenuKey == "masters_category")
        {
            if (_categoryPageNumber > 1)
            {
                _categoryPageNumber--;
                LoadCategoryPage();
            }
        }
        else if (_currentMenuKey == "masters_supplier")
        {
            if (_supplierPageNumber > 1)
            {
                _supplierPageNumber--;
                LoadSupplierPage();
            }
        }
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_currentMenuKey == "masters_customer")
        {
            _customerPageNumber++;
            LoadCustomerPage();
        }
        else if (_currentMenuKey == "masters_category")
        {
            _categoryPageNumber++;
            LoadCategoryPage();
        }
        else if (_currentMenuKey == "masters_supplier")
        {
            _supplierPageNumber++;
            LoadSupplierPage();
        }
    }

    private void OnHistorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not HistoryRow row)
        {
            return;
        }

        if (_currentMenuKey == "masters_customer")
        {
            var customer = _customerService.GetByCode(row.DocNo);
            if (customer is null) return;
            Field1Input.Text = customer.Code;
            Field2Input.Text = customer.Name;
            Field3ComboInput.Text = customer.Type;
            Field4Input.Text = customer.Contact;
            Field5DateInput.SelectedDate = customer.OpeningDate;
            Field6Input.Text = customer.OpeningBalance.ToString("0.##", CultureInfo.InvariantCulture);
        }
        else if (_currentMenuKey == "masters_category")
        {
            var category = _categoryService.GetByCode(row.DocNo);
            if (category is null) return;
            Field1Input.Text = category.Code;
            Field2Input.Text = category.Name;
            Field3ComboInput.Text = category.Type;
            Field4Input.Text = category.ParentCategory;
            Field5DateInput.SelectedDate = category.CreatedDate;
            Field6Input.Text = string.Empty;
        }
        else if (_currentMenuKey == "masters_supplier")
        {
            var supplier = _supplierService.GetByCode(row.DocNo);
            if (supplier is null) return;
            Field1Input.Text = supplier.Code;
            Field2Input.Text = supplier.Name;
            Field3ComboInput.Text = supplier.Type;
            Field4Input.Text = supplier.Contact;
            Field5DateInput.SelectedDate = supplier.OnboardDate;
            Field6Input.Text = supplier.OpeningPayable.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    private void ExpandParentForKey(string key)
    {
        DashboardExpander.IsExpanded = key.StartsWith("dashboard_", StringComparison.Ordinal);
        MastersExpander.IsExpanded = key.StartsWith("masters_", StringComparison.Ordinal);
        SalesExpander.IsExpanded = key.StartsWith("sales_", StringComparison.Ordinal);
        PurchaseExpander.IsExpanded = key.StartsWith("purchase_", StringComparison.Ordinal);
        InventoryExpander.IsExpanded = key.StartsWith("inventory_", StringComparison.Ordinal);
        FinanceExpander.IsExpanded = key.StartsWith("finance_", StringComparison.Ordinal);
        ReportsExpander.IsExpanded = key.StartsWith("reports_", StringComparison.Ordinal);
    }

    private static Dictionary<string, SubMenuConfig> BuildMenuConfigs()
    {
        return new Dictionary<string, SubMenuConfig>
        {
            ["dashboard_overview"] = C("Dashboard", "Overview", "Owner-level pharmacy KPI snapshot including sale, purchase, stock expiry and profit trend.", "Dashboard", "Dashboard", F("Code", "Name", "Status", "Reference", "Date", "Amount"), D(), O("Live"), R(new HistoryRow("DB-001", "System", "2026-02-24", "Live", "-")), "NXERP PHARMACY DASHBOARD\nDate: 2026-02-24\nToday Sale: $8,420\nToday Purchase: $4,880\nGross Profit: $3,540\nExpiry Alerts: 34 items\nCash Account: MAIN CASH\n"),

            ["masters_category"] = C("Masters", "Category Master", "Maintain medicine categories such as Antibiotic, Drip/Infusion, Syrup, Analgesic, OTC.", "Category Setup", "Category History", F("Category Code", "Category Name", "Type", "Parent Category", "Create Date", "Status Note"), D("CAT-", "", "Medicine"), O("Medicine", "Consumable", "Service"), R(new HistoryRow("CAT-101", "Antibiotic", "2026-02-20", "Active", "-"), new HistoryRow("CAT-102", "Drip/Infusion", "2026-02-20", "Active", "-"), new HistoryRow("CAT-103", "Painkiller", "2026-02-21", "Active", "-")), "CATEGORY MASTER\nExamples: Antibiotic, Drip, Syrup, Vitamin, OTC\n"),
            ["masters_product"] = C("Masters", "Product Master", "Define medicines with category, unit, tax, batch policy and reorder level.", "Product Setup", "Product History", F("Product Code", "Product Name", "Category", "Generic Name", "Launch Date", "Reorder Level"), D("MED-", "", "Antibiotic", "", "", "20"), O("Antibiotic", "Drip/Infusion", "Syrup", "OTC", "Vitamin"), R(new HistoryRow("MED-9001", "Amoxiclav 625", "2026-02-22", "Active", "125"), new HistoryRow("MED-9002", "NS 500ml", "2026-02-22", "Active", "80"), new HistoryRow("MED-9003", "Panadol 500", "2026-02-21", "Active", "300")), "PRODUCT MASTER\nCategory -> Product mapping and reorder controls.\n"),
            ["masters_batch"] = C("Masters", "Batch & Expiry Setup", "Track batch, expiry date, purchase rate and MRP.", "Batch Setup", "Batch History", F("Batch No", "Product", "Batch Status", "Supplier Ref", "Expiry Date", "MRP"), D("B-", "", "Open"), O("Open", "Blocked", "Expired"), R(new HistoryRow("B-AX23", "Amoxiclav 625", "2027-01-31", "Open", "$5.20"), new HistoryRow("B-NS91", "NS 500ml", "2026-11-15", "Open", "$1.80"), new HistoryRow("B-PN40", "Panadol 500", "2027-03-10", "Open", "$0.90")), "BATCH CONTROL\nUse FEFO for issue sequence.\n"),
            ["masters_customer"] = C("Masters", "Customer Master", "Cash and credit customer profiles for billing and receivables.", "Customer Setup", "Customer History", F("Customer Code", "Customer Name", "Customer Type", "Contact", "Register Date", "Opening Balance"), D("CUS-", "", "Retail"), O("Retail", "Corporate", "Insurance"), R(new HistoryRow("CUS-1001", "Walk-In Customer", "2026-02-18", "Active", "$0"), new HistoryRow("CUS-1002", "City Clinic", "2026-02-19", "Active", "$2,150"), new HistoryRow("CUS-1003", "Community Org", "2026-02-20", "Active", "$1,500")), "CUSTOMER MASTER\nRetail + institutional customers.\n"),
            ["masters_supplier"] = C("Masters", "Supplier Master", "Vendor records for medicine procurement and payable management.", "Supplier Setup", "Supplier History", F("Supplier Code", "Supplier Name", "Supplier Type", "Contact", "Onboard Date", "Opening Payable"), D("SUP-", "", "Distributor"), O("Distributor", "Manufacturer", "Wholesaler"), R(new HistoryRow("SUP-301", "HealthLine Pharma", "2026-02-19", "Active", "$4,200"), new HistoryRow("SUP-302", "Global Medics", "2026-02-20", "Active", "$7,860"), new HistoryRow("SUP-303", "Sterile Supply Co", "2026-02-21", "Active", "$2,410")), "SUPPLIER MASTER\nSupplier linked with PO/GRN/PINV.\n"),
            ["masters_doctor"] = C("Masters", "Doctor Master", "Doctor details for prescription analytics and referrals.", "Doctor Setup", "Doctor History", F("Doctor Code", "Doctor Name", "Specialty", "Clinic", "Onboard Date", "License No"), D("DOC-", "", "General"), O("General", "Cardiology", "Pediatrics", "ENT"), R(new HistoryRow("DOC-51", "Dr. Sana", "2026-02-15", "Active", "LIC-882"), new HistoryRow("DOC-52", "Dr. Khalid", "2026-02-16", "Active", "LIC-771"), new HistoryRow("DOC-53", "Dr. Noor", "2026-02-17", "Active", "LIC-619")), "DOCTOR MASTER\nUseful for prescription-based analytics.\n"),
            ["sales_pos"] = C("Sales", "POS Billing", "Counter sale with barcode, batch, discount, tax and cash account posting.", "POS Entry", "POS Bill History", F("POS Bill No", "Customer", "Bill Status", "Counter", "Bill Date", "Bill Total"), D("POS-", "Walk-In", "Open", "C1"), O("Open", "Posted", "Voided"), R(new HistoryRow("POS-4401", "Walk-In", "2026-02-24", "Posted", "$86.50"), new HistoryRow("POS-4400", "Walk-In", "2026-02-24", "Posted", "$34.20"), new HistoryRow("POS-4399", "Walk-In", "2026-02-24", "Posted", "$120.00")), "POS INVOICE\nBill No: POS-4401\nItems + tax + net\nPayment: CASH (MAIN CASH)\n"),
            ["sales_invoice"] = C("Sales", "Sales Invoice", "Credit/institutional sales invoice with ledger impact and stock deduction.", "Sales Invoice", "Invoice History", F("Invoice No", "Customer", "Invoice Status", "Reference", "Invoice Date", "Invoice Total"), D("INV-", "", "Open"), O("Open", "Posted", "Paid"), R(new HistoryRow("INV-9025", "City Clinic", "2026-02-24", "Posted", "$1,240"), new HistoryRow("INV-9024", "Community Org", "2026-02-23", "Open", "$890"), new HistoryRow("INV-9023", "Care Home", "2026-02-22", "Paid", "$1,560")), "SALES INVOICE\nReceivable Dr\nSales + Tax Cr\n"),
            ["sales_return"] = C("Sales", "Sales Return", "Record returned medicines and reverse sale entries.", "Sales Return", "Sales Return History", F("Return No", "Customer", "Reason", "Ref Invoice", "Return Date", "Return Amount"), D("SR-", "", "Damage"), O("Damage", "Wrong Item", "Expired"), R(new HistoryRow("SR-331", "City Clinic", "2026-02-24", "Approved", "$110"), new HistoryRow("SR-330", "Walk-In", "2026-02-23", "Approved", "$25"), new HistoryRow("SR-329", "Care Home", "2026-02-22", "Pending", "$75")), "SALES RETURN NOTE\nAgainst invoice with stock reversal.\n"),
            ["sales_credit_note"] = C("Sales", "Credit Note", "Issue credit note against validated sales return.", "Credit Note", "Credit Note History", F("Credit Note No", "Customer", "Status", "Ref Return", "Issue Date", "Credit Amount"), D("CN-", "", "Draft"), O("Draft", "Issued", "Adjusted"), R(new HistoryRow("CN-190", "City Clinic", "2026-02-24", "Issued", "$110"), new HistoryRow("CN-189", "Walk-In", "2026-02-23", "Issued", "$25"), new HistoryRow("CN-188", "Care Home", "2026-02-22", "Draft", "$75")), "CREDIT NOTE\nParty ledger Cr / Sales return Dr\n"),

            ["purchase_po"] = C("Purchase", "Purchase Order", "Create PO for medicine procurement by category and supplier.", "PO Entry", "PO History", F("PO No", "Supplier", "PO Status", "Reference", "PO Date", "PO Value"), D("PO-", "", "Draft"), O("Draft", "Approved", "Closed"), R(new HistoryRow("PO-740", "HealthLine Pharma", "2026-02-24", "Approved", "$2,840"), new HistoryRow("PO-739", "Global Medics", "2026-02-23", "Draft", "$1,920"), new HistoryRow("PO-738", "Sterile Supply Co", "2026-02-22", "Closed", "$1,450")), "PURCHASE ORDER\nItems by categories like Antibiotic/Drip\n"),
            ["purchase_grn"] = C("Purchase", "GRN", "Post received quantity by batch with expiry and purchase rate.", "GRN Entry", "GRN History", F("GRN No", "Supplier", "GRN Status", "PO Ref", "GRN Date", "GRN Value"), D("GRN-", "", "Open"), O("Open", "Checked", "Posted"), R(new HistoryRow("GRN-510", "HealthLine Pharma", "2026-02-24", "Posted", "$1,930"), new HistoryRow("GRN-509", "Global Medics", "2026-02-23", "Checked", "$1,210"), new HistoryRow("GRN-508", "Sterile Supply Co", "2026-02-22", "Open", "$680")), "GRN\nStock Dr / GRN clearing Cr\n"),
            ["purchase_invoice"] = C("Purchase", "Purchase Invoice", "Supplier bill booking against GRN with tax and payable ledger.", "Purchase Invoice", "Purchase Invoice History", F("PINV No", "Supplier", "Status", "Ref GRN", "Invoice Date", "Invoice Amount"), D("PINV-", "", "Open"), O("Open", "Verified", "Paid"), R(new HistoryRow("PINV-440", "HealthLine Pharma", "2026-02-24", "Verified", "$1,950"), new HistoryRow("PINV-439", "Global Medics", "2026-02-23", "Open", "$1,220"), new HistoryRow("PINV-438", "Sterile Supply Co", "2026-02-22", "Paid", "$690")), "PURCHASE INVOICE\nInventory Dr / Supplier Cr\n"),
            ["purchase_debit_note"] = C("Purchase", "Debit Note", "Issue debit note for purchase return or rate difference.", "Debit Note", "Debit Note History", F("Debit Note No", "Supplier", "Status", "Reference", "Issue Date", "Amount"), D("DN-", "", "Draft"), O("Draft", "Issued", "Adjusted"), R(new HistoryRow("DN-210", "Global Medics", "2026-02-24", "Issued", "$140"), new HistoryRow("DN-209", "HealthLine Pharma", "2026-02-23", "Issued", "$95"), new HistoryRow("DN-208", "Sterile Supply Co", "2026-02-22", "Draft", "$60")), "DEBIT NOTE\nSupplier return / rate adjustment\n"),

            ["inventory_stock_in"] = C("Inventory", "Stock In", "Stock inward posting from GRN and opening entries.", "Stock In", "Stock In History", F("Stock In No", "Warehouse", "Status", "Source", "Posting Date", "Quantity"), D("SIN-", "Main Store", "Draft", "GRN"), O("Draft", "Posted", "Verified"), R(new HistoryRow("SIN-210", "Main Store", "2026-02-24", "Posted", "420"), new HistoryRow("SIN-209", "Main Store", "2026-02-23", "Verified", "210"), new HistoryRow("SIN-208", "Spare Store", "2026-02-22", "Posted", "90")), "STOCK IN\nBatch + expiry maintained\n"),
            ["inventory_stock_out"] = C("Inventory", "Stock Out", "Issue stock against sales and internal consumption.", "Stock Out", "Stock Out History", F("Stock Out No", "Warehouse", "Status", "Destination", "Posting Date", "Quantity"), D("SOUT-", "Main Store", "Draft", "POS"), O("Draft", "Posted", "Approved"), R(new HistoryRow("SOUT-180", "Main Store", "2026-02-24", "Posted", "260"), new HistoryRow("SOUT-179", "Main Store", "2026-02-23", "Approved", "140"), new HistoryRow("SOUT-178", "Spare Store", "2026-02-22", "Posted", "75")), "STOCK OUT\nIssue policy: FEFO\n"),
            ["inventory_transfer"] = C("Inventory", "Stock Transfer", "Move stock between warehouse/location with batch continuity.", "Transfer Entry", "Transfer History", F("Transfer No", "From/To", "Status", "Reason", "Transfer Date", "Qty"), D("TRN-", "Main->Counter", "Draft", "Replenish"), O("Draft", "Posted", "Approved"), R(new HistoryRow("TRN-77", "Main->Counter", "2026-02-24", "Posted", "120"), new HistoryRow("TRN-76", "Main->OTC", "2026-02-23", "Approved", "65"), new HistoryRow("TRN-75", "Main->ER", "2026-02-22", "Posted", "40")), "STOCK TRANSFER\nBatch/expiry remains same\n"),
            ["inventory_adjustment"] = C("Inventory", "Stock Adjustment", "Audit variance adjustment and damage write-off.", "Adjustment Entry", "Adjustment History", F("Adjustment No", "Warehouse", "Type", "Reason", "Date", "Variance Qty"), D("ADJ-", "Main Store", "Decrease", "Count Variance"), O("Increase", "Decrease", "Write-Off"), R(new HistoryRow("ADJ-88", "Main Store", "2026-02-24", "Decrease", "-12"), new HistoryRow("ADJ-87", "Counter", "2026-02-23", "Increase", "+5"), new HistoryRow("ADJ-86", "OTC", "2026-02-22", "Write-Off", "-4")), "STOCK ADJUSTMENT\nApproval + reason required\n"),
            ["inventory_expiry"] = C("Inventory", "Expiry Management", "Track near-expiry and expired medicine batches.", "Expiry Control", "Expiry History", F("Expiry Doc No", "Product", "Status", "Batch", "Expiry Date", "Qty"), D("EXP-", "", "Watch"), O("Watch", "Expired", "Disposed"), R(new HistoryRow("EXP-42", "NS 500ml", "2026-04-10", "Watch", "80"), new HistoryRow("EXP-41", "Cefixime", "2026-03-28", "Watch", "60"), new HistoryRow("EXP-40", "Old Syrup", "2026-02-10", "Disposed", "12")), "EXPIRY REPORT\n30/60/90 days window\n"),
            ["inventory_reorder"] = C("Inventory", "Reorder Planning", "Auto planning based on min/max and lead time.", "Reorder Plan", "Reorder History", F("Plan No", "Category", "Status", "Supplier", "Plan Date", "Planned Qty"), D("ROP-", "Antibiotic", "Draft"), O("Draft", "Approved", "Generated PO"), R(new HistoryRow("ROP-23", "Antibiotic", "2026-02-24", "Approved", "320"), new HistoryRow("ROP-22", "Drip/Infusion", "2026-02-23", "Draft", "180"), new HistoryRow("ROP-21", "Painkiller", "2026-02-22", "Generated PO", "260")), "REORDER PLAN\nBased on lead time and safety stock\n"),
            ["finance_coa_group"] = C("COA & Accounts", "COA Group", "Define parent groups: Assets, Liabilities, Income, Expenses.", "COA Group Setup", "COA Group History", F("Group Code", "Group Name", "Nature", "Parent", "Effective Date", "Note"), D("GRP-", "", "Asset"), O("Asset", "Liability", "Income", "Expense", "Equity"), R(new HistoryRow("GRP-100", "Current Assets", "2026-02-20", "Active", "-"), new HistoryRow("GRP-200", "Current Liabilities", "2026-02-20", "Active", "-"), new HistoryRow("GRP-400", "Revenue", "2026-02-20", "Active", "-")), "COA GROUP\nGroup -> Head -> SubHead -> Account\n"),
            ["finance_coa_head"] = C("COA & Accounts", "COA Head", "Create heads under each group (e.g., Cash, Receivable, Payable).", "COA Head Setup", "COA Head History", F("Head Code", "Head Name", "Group", "Parent", "Date", "Opening"), D("HD-", "", "Current Assets"), O("Current Assets", "Current Liabilities", "Revenue", "Expense"), R(new HistoryRow("HD-110", "Cash & Cash Equivalents", "2026-02-21", "Active", "$0"), new HistoryRow("HD-120", "Accounts Receivable", "2026-02-21", "Active", "$0"), new HistoryRow("HD-210", "Accounts Payable", "2026-02-21", "Active", "$0")), "COA HEAD\nOne level below group\n"),
            ["finance_coa_subhead"] = C("COA & Accounts", "COA Sub Head", "Detailed sub-heads for account mapping and reporting.", "COA Sub Head Setup", "COA Sub Head History", F("SubHead Code", "SubHead Name", "Head", "Parent", "Date", "Opening"), D("SH-", "", "Cash & Cash Equivalents"), O("Cash", "Receivable", "Payable", "Expense"), R(new HistoryRow("SH-1101", "Main Cash", "2026-02-21", "Active", "$5,200"), new HistoryRow("SH-1102", "Counter Cash", "2026-02-21", "Active", "$1,480"), new HistoryRow("SH-5101", "Electricity Expense", "2026-02-21", "Active", "$0")), "COA SUB HEAD\nGranular account segmentation\n"),
            ["finance_account_master"] = C("COA & Accounts", "Account Master", "Final ledger accounts including cash, bank, receivable, supplier payable.", "Account Setup", "Account History", F("Account Code", "Account Name", "Account Type", "Sub Head", "Date", "Opening Balance"), D("AC-", "", "Cash", "Main Cash"), O("Cash", "Bank", "Receivable", "Payable", "Expense"), R(new HistoryRow("AC-11001", "MAIN CASH", "2026-02-24", "Active", "$6,300"), new HistoryRow("AC-12010", "CITY CLINIC A/R", "2026-02-24", "Active", "$1,240"), new HistoryRow("AC-21020", "HEALTHLINE A/P", "2026-02-24", "Active", "$1,950")), "ACCOUNT MASTER\nCash account: MAIN CASH\n"),
            ["finance_cash_book"] = C("COA & Accounts", "Cash Book", "Daily cash receipts/payments with running balance.", "Cash Book Entry", "Cash Book History", F("Cash Voucher No", "Narration", "Type", "Reference", "Date", "Amount"), D("CV-", "", "Receipt"), O("Receipt", "Payment"), R(new HistoryRow("CV-1501", "POS Sale Collection", "2026-02-24", "Receipt", "$860"), new HistoryRow("CV-1502", "Petty Expense", "2026-02-24", "Payment", "$45"), new HistoryRow("CV-1503", "Opening Float", "2026-02-24", "Receipt", "$200")), "CASH BOOK\nOpening + Receipts - Payments = Closing\n"),
            ["finance_bank_book"] = C("COA & Accounts", "Bank Book", "Bank receipts/payments and reconciliation entries.", "Bank Book Entry", "Bank Book History", F("Bank Voucher No", "Narration", "Type", "Ref No", "Date", "Amount"), D("BV-", "", "Receipt"), O("Receipt", "Payment", "Transfer"), R(new HistoryRow("BV-901", "Insurance Collection", "2026-02-24", "Receipt", "$1,200"), new HistoryRow("BV-900", "Supplier NEFT", "2026-02-23", "Payment", "$980"), new HistoryRow("BV-899", "Cash Deposit", "2026-02-22", "Transfer", "$500")), "BANK BOOK\nSupports bank reconciliation\n"),
            ["finance_expense_voucher"] = C("COA & Accounts", "Expense Voucher", "Record rent, electricity, salary, internet and misc expenses.", "Expense Voucher", "Expense Voucher History", F("Expense Voucher No", "Expense Head", "Status", "Reference", "Voucher Date", "Amount"), D("EV-", "Electricity", "Draft"), O("Draft", "Posted", "Approved"), R(new HistoryRow("EV-330", "Electricity", "2026-02-24", "Posted", "$120"), new HistoryRow("EV-329", "Internet", "2026-02-23", "Posted", "$75"), new HistoryRow("EV-328", "Cleaning", "2026-02-22", "Approved", "$35")), "EXPENSE VOUCHER\nDr Expense / Cr Cash-Bank\n"),
            ["finance_receipt_voucher"] = C("COA & Accounts", "Receipt Voucher", "Record incoming funds against customer or other receipts.", "Receipt Voucher", "Receipt Voucher History", F("Receipt Voucher No", "Received From", "Status", "Reference", "Date", "Amount"), D("RV-", "", "Draft"), O("Draft", "Posted", "Approved"), R(new HistoryRow("RV-410", "City Clinic", "2026-02-24", "Posted", "$900"), new HistoryRow("RV-409", "Walk-In Aggregate", "2026-02-23", "Posted", "$1,420"), new HistoryRow("RV-408", "Community Org", "2026-02-22", "Approved", "$700")), "RECEIPT VOUCHER\nDr Cash-Bank / Cr Party\n"),
            ["finance_payment_voucher"] = C("COA & Accounts", "Payment Voucher", "Record outgoing payments to suppliers and expenses.", "Payment Voucher", "Payment Voucher History", F("Payment Voucher No", "Paid To", "Status", "Reference", "Date", "Amount"), D("PV-", "", "Draft"), O("Draft", "Posted", "Approved"), R(new HistoryRow("PV-510", "HealthLine Pharma", "2026-02-24", "Posted", "$1,200"), new HistoryRow("PV-509", "Utility Board", "2026-02-23", "Posted", "$95"), new HistoryRow("PV-508", "Courier Service", "2026-02-22", "Approved", "$40")), "PAYMENT VOUCHER\nDr Supplier-Expense / Cr Cash-Bank\n"),
            ["finance_jv"] = C("COA & Accounts", "Journal Voucher", "Pass adjustment entries (accruals, provisions, reclassifications).", "Journal Voucher", "JV History", F("JV No", "Narration", "JV Type", "Reference", "Date", "Amount"), D("JV-", "", "General"), O("General", "Adjustment", "Reversal"), R(new HistoryRow("JV-320", "Inventory Provision", "2026-02-24", "Posted", "$300"), new HistoryRow("JV-319", "Discount Accrual", "2026-02-23", "Draft", "$120"), new HistoryRow("JV-318", "Roundoff", "2026-02-22", "Posted", "$15")), "JOURNAL VOUCHER\nDr Account A / Cr Account B\n"),

            ["reports_sales_register"] = C("Reports", "Sales Register", "Detailed sales by invoice/POS with tax and payment mode.", "Sales Register Filters", "Sales Register Output", F("Report No", "Period", "Status", "Branch", "As On Date", "Total Sales"), D("SRPT-", "", "Draft", "Main"), O("Draft", "Generated"), R(new HistoryRow("SRPT-12", "Feb-2026", "2026-02-24", "Generated", "$54,820"), new HistoryRow("SRPT-11", "Jan-2026", "2026-01-31", "Generated", "$49,310"), new HistoryRow("SRPT-10", "Dec-2025", "2025-12-31", "Generated", "$46,900")), "SALES REGISTER\nDate, Invoice, Customer, Item, Qty, Net, Tax, Gross\n"),
            ["reports_purchase_register"] = C("Reports", "Purchase Register", "Purchase register with supplier, invoice, tax and payable status.", "Purchase Register Filters", "Purchase Register Output", F("Report No", "Period", "Status", "Branch", "As On Date", "Total Purchase"), D("PRPT-", "", "Draft", "Main"), O("Draft", "Generated"), R(new HistoryRow("PRPT-08", "Feb-2026", "2026-02-24", "Generated", "$28,400"), new HistoryRow("PRPT-07", "Jan-2026", "2026-01-31", "Generated", "$25,220"), new HistoryRow("PRPT-06", "Dec-2025", "2025-12-31", "Generated", "$23,990")), "PURCHASE REGISTER\nDate, Supplier, PINV No, Amount, Tax, Net Payable\n"),
            ["reports_stock_valuation"] = C("Reports", "Stock Valuation", "Current stock valuation by batch, expiry and moving average cost.", "Stock Valuation Filters", "Stock Valuation Output", F("Report No", "Store", "Status", "Method", "As On Date", "Stock Value"), D("SV-", "Main Store", "Draft", "Moving Avg"), O("Draft", "Generated"), R(new HistoryRow("SV-17", "Main Store", "2026-02-24", "Generated", "$73,990"), new HistoryRow("SV-16", "Main Store", "2026-02-23", "Generated", "$72,410"), new HistoryRow("SV-15", "Main Store", "2026-02-22", "Generated", "$71,860")), "STOCK VALUATION\nBy Product + Batch + Expiry\n"),
            ["reports_expiry_report"] = C("Reports", "Expiry Report", "Near-expiry and expired stock for disposal planning.", "Expiry Report Filters", "Expiry Report Output", F("Report No", "Window", "Status", "Store", "As On Date", "Affected Qty"), D("ER-", "30 days", "Draft", "Main"), O("Draft", "Generated"), R(new HistoryRow("ER-09", "30 days", "2026-02-24", "Generated", "320"), new HistoryRow("ER-08", "60 days", "2026-02-24", "Generated", "640"), new HistoryRow("ER-07", "90 days", "2026-02-24", "Generated", "980")), "EXPIRY REPORT\n30/60/90-day segmentation\n"),
            ["reports_profit_loss"] = C("Reports", "Profit & Loss", "P&L statement with sales, COGS, gross profit and operating expenses.", "P&L Filters", "P&L Output", F("Report No", "Period", "Status", "Branch", "As On Date", "Net Profit"), D("PL-", "", "Draft", "Main"), O("Draft", "Generated"), R(new HistoryRow("PL-05", "Feb-2026", "2026-02-24", "Generated", "$12,800"), new HistoryRow("PL-04", "Jan-2026", "2026-01-31", "Generated", "$11,920"), new HistoryRow("PL-03", "Dec-2025", "2025-12-31", "Generated", "$10,740")), "P&L FORMAT\nSales - COGS = GP\nGP - Expenses = NP\n"),
            ["reports_trial_balance"] = C("Reports", "Trial Balance", "Trial balance with debit/credit totals for closing checks.", "TB Filters", "TB Output", F("Report No", "Period", "Status", "Branch", "As On Date", "Difference"), D("TB-", "", "Draft", "Main"), O("Draft", "Generated"), R(new HistoryRow("TB-078", "Feb-2026", "2026-02-24", "Generated", "$0"), new HistoryRow("TB-077", "Jan-2026", "2026-01-31", "Generated", "$0"), new HistoryRow("TB-076", "Dec-2025", "2025-12-31", "Generated", "$0")), "TRIAL BALANCE\nDebit total must equal Credit total\n"),
            ["reports_ledger"] = C("Reports", "Ledger Report", "Account and party ledger statement with running balance.", "Ledger Filters", "Ledger Output", F("Report No", "Account", "Status", "Period", "As On Date", "Closing Balance"), D("LR-", "MAIN CASH", "Draft"), O("Draft", "Generated"), R(new HistoryRow("LR-22", "MAIN CASH", "2026-02-24", "Generated", "$6,300"), new HistoryRow("LR-21", "CITY CLINIC A/R", "2026-02-24", "Generated", "$1,240"), new HistoryRow("LR-20", "HEALTHLINE A/P", "2026-02-24", "Generated", "$1,950")), "LEDGER\nDate | Voucher | Narration | Dr | Cr | Balance\n"),
            ["reports_invoice_print"] = C("Reports", "Invoice Print Format", "Formatted retail and tax invoice templates.", "Invoice Template", "Invoice Template History", F("Template Code", "Template Name", "Status", "Paper Size", "Effective Date", "Copies"), D("TPL-INV-", "", "Draft", "A5", "", "1"), O("Draft", "Active"), R(new HistoryRow("TPL-INV-01", "Pharmacy Tax Invoice", "2026-02-24", "Active", "1"), new HistoryRow("TPL-INV-02", "POS Short Invoice", "2026-02-24", "Active", "1"), new HistoryRow("TPL-INV-03", "Institution Invoice", "2026-02-24", "Draft", "2")), "INVOICE TEMPLATE\nHeader + Item grid + Tax + Net + Signature\n"),
            ["reports_voucher_print"] = C("Reports", "Voucher Print Format", "Receipt, payment, journal and expense voucher templates.", "Voucher Template", "Voucher Template History", F("Template Code", "Voucher Type", "Status", "Paper Size", "Effective Date", "Copies"), D("TPL-VCH-", "Receipt", "Draft", "A5", "", "1"), O("Draft", "Active"), R(new HistoryRow("TPL-VCH-01", "Receipt Voucher", "2026-02-24", "Active", "1"), new HistoryRow("TPL-VCH-02", "Payment Voucher", "2026-02-24", "Active", "1"), new HistoryRow("TPL-VCH-03", "Journal Voucher", "2026-02-24", "Draft", "1")), "VOUCHER TEMPLATE\nVoucher No/Date + DrCr lines + approval\n")
        };
    }

    private static SubMenuConfig C(string module, string submenu, string description, string formTitle, string historyTitle, string[] fieldLabels, string[] defaultValues, string[] statusOptions, List<HistoryRow> historyRows, string previewText)
        => new(module, submenu, description, formTitle, historyTitle, fieldLabels, defaultValues, statusOptions, historyRows, previewText);

    private static string[] F(string a, string b, string c, string d, string e, string f) => [a, b, c, d, e, f];
    private static string[] D(string a = "", string b = "", string c = "", string d = "", string e = "", string f = "") => [a, b, c, d, e, f];
    private static string[] O(params string[] options) => options;
    private static List<HistoryRow> R(params HistoryRow[] rows) => [.. rows];

    private static string GetDatabasePath()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NxERP");
        return Path.Combine(baseDir, "nxerp-local.db");
    }

    private sealed record SubMenuConfig(string Module, string SubMenu, string Description, string FormTitle, string HistoryTitle, string[] FieldLabels, string[] DefaultValues, string[] StatusOptions, List<HistoryRow> HistoryRows, string PreviewText);
    public sealed record HistoryRow(string DocNo, string Party, string Date, string Status, string Amount);
}

