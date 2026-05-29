using System.Reflection;
using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialAr.Migration;
using Sunfish.Blocks.FinancialAp.Migration;
using Sunfish.Blocks.FinancialPeriods.Migration;
using Sunfish.Blocks.FinancialTax.Migration;
using Sunfish.Blocks.People.Foundation.Migration;
using Sunfish.Blocks.FinancialPayments.Migration;

namespace Sunfish.Blocks.Migration.Erpnext.Tests;

/// <summary>
/// Unit tests for <see cref="MariaDbDumpExtractor"/> against small, in-code
/// v15-shaped fixture SQL strings — no CIC data (ADR 0100 C4/C6). Exercises the
/// row-to-DTO mapping, in-process parent/child JOIN reconstruction, the USD-only
/// guard, the DocType census, and the clean-room / read-only posture.
/// </summary>
public sealed class MariaDbDumpExtractorTests
{
    // ---- Fixture SQL — minimal v15-shaped synthetic dump (no real CIC data) ----

    private const string AccountFixture = """
        CREATE TABLE `tabAccount` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `account_name` varchar(140) DEFAULT NULL,
          `account_number` varchar(140) DEFAULT NULL,
          `parent_account` varchar(140) DEFAULT NULL,
          `account_type` varchar(140) DEFAULT NULL,
          `is_group` tinyint(1) NOT NULL DEFAULT 0,
          `disabled` tinyint(1) NOT NULL DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabAccount` VALUES
          ('1000 - Bank','2026-01-01 10:00:00.000000','Bank Account','1000',NULL,'Bank',0,0),
          ('Assets','2026-01-01 09:00:00.000000','Assets',NULL,NULL,NULL,1,0);
        """;

    private const string CostCenterFixture = """
        CREATE TABLE `tabCost Center` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `cost_center_name` varchar(140) DEFAULT NULL,
          `parent_cost_center` varchar(140) DEFAULT NULL,
          `is_group` tinyint(1) DEFAULT 0,
          `disabled` tinyint(1) DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabCost Center` VALUES
          ('Main - ACC','2026-01-01 09:00:00.000000','Main',NULL,0,0);
        """;

    private const string JournalEntryFixture = """
        CREATE TABLE `tabJournal Entry` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `posting_date` date DEFAULT NULL,
          `user_remark` text DEFAULT NULL,
          `voucher_type` varchar(140) DEFAULT NULL,
          `is_opening` varchar(10) DEFAULT 'No',
          `docstatus` int(1) DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabJournal Entry` VALUES
          ('JV-2026-0001','2026-02-01 10:00:00.000000','2026-01-01','Opening entry','Opening Entry','Yes',1);

        CREATE TABLE `tabJournal Entry Account` (
          `name` varchar(140) NOT NULL,
          `parent` varchar(140) DEFAULT NULL,
          `parenttype` varchar(140) DEFAULT NULL,
          `account` varchar(140) DEFAULT NULL,
          `debit_in_account_currency` decimal(21,9) DEFAULT 0.000000000,
          `credit_in_account_currency` decimal(21,9) DEFAULT 0.000000000,
          `cost_center` varchar(140) DEFAULT NULL,
          `user_remark` text DEFAULT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabJournal Entry Account` VALUES
          ('JEA-0001','JV-2026-0001','Journal Entry','1000 - Bank',1000.000000000,0.000000000,NULL,NULL),
          ('JEA-0002','JV-2026-0001','Journal Entry','2000 - Equity',0.000000000,1000.000000000,NULL,'Opening');
        """;

    private const string SalesInvoiceFixture = """
        CREATE TABLE `tabSales Invoice` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `customer` varchar(140) DEFAULT NULL,
          `posting_date` date DEFAULT NULL,
          `due_date` date DEFAULT NULL,
          `currency` varchar(10) DEFAULT NULL,
          `status` varchar(140) DEFAULT NULL,
          `grand_total` decimal(21,9) DEFAULT 0.000000000,
          `outstanding_amount` decimal(21,9) DEFAULT 0.000000000,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabSales Invoice` VALUES
          ('SINV-2026-0001','2026-03-01 10:00:00.000000','CUST-0001',
           '2026-03-01','2026-03-31','USD','Submitted',500.000000000,500.000000000);

        CREATE TABLE `tabSales Invoice Item` (
          `name` varchar(140) NOT NULL,
          `parent` varchar(140) DEFAULT NULL,
          `item_name` varchar(140) DEFAULT NULL,
          `qty` decimal(21,9) DEFAULT 1.000000000,
          `rate` decimal(21,9) DEFAULT 0.000000000,
          `amount` decimal(21,9) DEFAULT 0.000000000,
          `income_account` varchar(140) DEFAULT NULL,
          `cost_center` varchar(140) DEFAULT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabSales Invoice Item` VALUES
          ('SINV-ITEM-0001','SINV-2026-0001','Consulting Services',
           2.000000000,250.000000000,500.000000000,'Income - ACC',NULL);
        """;

    private const string PurchaseInvoiceFixture = """
        CREATE TABLE `tabPurchase Invoice` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `supplier` varchar(140) DEFAULT NULL,
          `bill_no` varchar(140) DEFAULT NULL,
          `posting_date` date DEFAULT NULL,
          `due_date` date DEFAULT NULL,
          `bill_date` date DEFAULT NULL,
          `currency` varchar(10) DEFAULT NULL,
          `status` varchar(140) DEFAULT NULL,
          `grand_total` decimal(21,9) DEFAULT 0.000000000,
          `outstanding_amount` decimal(21,9) DEFAULT 0.000000000,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabPurchase Invoice` VALUES
          ('PINV-2026-0001','2026-03-01 10:00:00.000000','SUP-0001','BILL-001',
           '2026-03-01','2026-03-31','2026-03-01','USD','Submitted',
           200.000000000,200.000000000);

        CREATE TABLE `tabPurchase Invoice Item` (
          `name` varchar(140) NOT NULL,
          `parent` varchar(140) DEFAULT NULL,
          `item_name` varchar(140) DEFAULT NULL,
          `qty` decimal(21,9) DEFAULT 1.000000000,
          `rate` decimal(21,9) DEFAULT 0.000000000,
          `amount` decimal(21,9) DEFAULT 0.000000000,
          `expense_account` varchar(140) DEFAULT NULL,
          `cost_center` varchar(140) DEFAULT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabPurchase Invoice Item` VALUES
          ('PINV-ITEM-0001','PINV-2026-0001','Office Supplies',
           1.000000000,200.000000000,200.000000000,'Expense - ACC',NULL);
        """;

    private const string CustomerFixture = """
        CREATE TABLE `tabCustomer` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `customer_name` varchar(140) DEFAULT NULL,
          `customer_type` varchar(140) DEFAULT NULL,
          `email_id` varchar(140) DEFAULT NULL,
          `mobile_no` varchar(140) DEFAULT NULL,
          `tax_id` varchar(140) DEFAULT NULL,
          `disabled` tinyint(1) DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabCustomer` VALUES
          ('CUST-0001','2026-01-01 10:00:00.000000','Acme LLC','Company',NULL,NULL,NULL,0);
        """;

    private const string FiscalYearFixture = """
        CREATE TABLE `tabFiscal Year` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `year_start_date` date DEFAULT NULL,
          `year_end_date` date DEFAULT NULL,
          `is_short_year` tinyint(1) DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabFiscal Year` VALUES
          ('FY2026','2026-01-01 00:00:00.000000','2026-01-01','2026-12-31',0);

        CREATE TABLE `tabFiscal Year Company` (
          `name` varchar(140) NOT NULL,
          `parent` varchar(140) DEFAULT NULL,
          `company` varchar(140) DEFAULT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabFiscal Year Company` VALUES
          ('FYC-0001','FY2026','Acero Inc');
        """;

    private const string TaxTemplateFixture = """
        CREATE TABLE `tabSales Taxes and Charges Template` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `title` varchar(140) DEFAULT NULL,
          `tax_category` varchar(140) DEFAULT NULL,
          `disabled` tinyint(1) DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabSales Taxes and Charges Template` VALUES
          ('VA Sales Tax','2026-01-01 00:00:00.000000','VA Sales Tax','State',0);

        CREATE TABLE `tabSales Taxes and Charges` (
          `name` varchar(140) NOT NULL,
          `parent` varchar(140) DEFAULT NULL,
          `account_head` varchar(140) DEFAULT NULL,
          `rate` decimal(21,9) DEFAULT 0.000000000,
          `included_in_print_rate` tinyint(1) DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabSales Taxes and Charges` VALUES
          ('STC-0001','VA Sales Tax','Tax Liability - ACC',6.000000000,0);

        CREATE TABLE `tabPurchase Taxes and Charges Template` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `title` varchar(140) DEFAULT NULL,
          `tax_category` varchar(140) DEFAULT NULL,
          `disabled` tinyint(1) DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;

        CREATE TABLE `tabPurchase Taxes and Charges` (
          `name` varchar(140) NOT NULL,
          `parent` varchar(140) DEFAULT NULL,
          `account_head` varchar(140) DEFAULT NULL,
          `rate` decimal(21,9) DEFAULT 0.000000000,
          `included_in_print_rate` tinyint(1) DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        """;

    private const string ContactFixture = """
        CREATE TABLE `tabContact` (
          `name` varchar(140) NOT NULL,
          `email_id` varchar(140) DEFAULT NULL,
          `mobile_no` varchar(140) DEFAULT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabContact` VALUES
          ('CONT-0001','contact@acme.com','+12025550100');

        CREATE TABLE `tabDynamic Link` (
          `name` varchar(140) NOT NULL,
          `parent` varchar(140) DEFAULT NULL,
          `parenttype` varchar(140) DEFAULT NULL,
          `link_doctype` varchar(140) DEFAULT NULL,
          `link_name` varchar(140) DEFAULT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabDynamic Link` VALUES
          ('DL-0001','CONT-0001','Contact','Customer','CUST-0001');
        """;

    private const string InventoryFixture = """
        CREATE TABLE `tabAccount` (
          `name` varchar(140) NOT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabAccount` VALUES ('ACC-0001');

        CREATE TABLE `tabDocType` (
          `name` varchar(140) NOT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabDocType` VALUES ('Account');

        CREATE TABLE `tabProperty` (
          `name` varchar(140) NOT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabProperty` VALUES ('PROP-0001');
        """;

    // ---- Helper ----

    private static MariaDbDumpExtractor Extractor(string dumpSql)
    {
        var reader = MariaDbDumpSourceReader.FromSql(dumpSql);
        return new MariaDbDumpExtractor(reader);
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }

    // ---- Pass-1: Accounts ----

    [Fact]
    public async Task ReadAccountsAsync_maps_tabAccount_to_dto()
    {
        var extractor = Extractor(AccountFixture);
        var accounts = await ToListAsync(extractor.ReadAccountsAsync());

        Assert.Equal(2, accounts.Count);
        var bank = accounts[0];
        Assert.Equal("1000 - Bank", bank.Name);
        Assert.Equal("Bank Account", bank.AccountName);
        Assert.Equal("1000", bank.AccountNumber);
        Assert.Null(bank.ParentAccountName);
        Assert.Equal("Bank", bank.AccountType);
        Assert.False(bank.IsGroup);
        Assert.False(bank.Disabled);
    }

    [Fact]
    public async Task ReadAccountsAsync_surfaces_null_account_type_for_group_account()
    {
        var extractor = Extractor(AccountFixture);
        var accounts = await ToListAsync(extractor.ReadAccountsAsync());

        var assets = accounts[1];
        Assert.Equal("Assets", assets.Name);
        Assert.True(assets.IsGroup);
        Assert.Null(assets.AccountType); // NULL in source -> null in DTO, not a guess
    }

    // ---- Pass-1: Cost Centers ----

    [Fact]
    public async Task ReadCostCentersAsync_maps_tabCostCenter_to_dto()
    {
        var extractor = Extractor(CostCenterFixture);
        var costCenters = await ToListAsync(extractor.ReadCostCentersAsync());

        Assert.Single(costCenters);
        var cc = costCenters[0];
        Assert.Equal("Main - ACC", cc.Name);
        Assert.Equal("Main", cc.CostCenterName);
        Assert.Null(cc.ParentCostCenterName);
        Assert.False(cc.IsGroup);
        Assert.False(cc.Disabled);
    }

    // ---- Pass-2: Fiscal Years (header + Fiscal Year Company child JOIN) ----

    [Fact]
    public async Task ReadFiscalYearsAsync_joins_company_short_name_from_child_table()
    {
        var extractor = Extractor(FiscalYearFixture);
        var years = await ToListAsync(extractor.ReadFiscalYearsAsync());

        Assert.Single(years);
        var fy = years[0];
        Assert.Equal("FY2026", fy.Name);
        Assert.Equal(new DateOnly(2026, 1, 1), fy.YearStartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), fy.YearEndDate);
        Assert.Equal("Acero Inc", fy.CompanyShortName); // reconstructed from child JOIN
        Assert.False(fy.IsShortYear);
    }

    // ---- Pass-2: Customers ----

    [Fact]
    public async Task ReadCustomersAsync_maps_tabCustomer_to_dto()
    {
        var extractor = Extractor(CustomerFixture);
        var customers = await ToListAsync(extractor.ReadCustomersAsync());

        Assert.Single(customers);
        var c = customers[0];
        Assert.Equal("CUST-0001", c.Name);
        Assert.Equal("Acme LLC", c.CustomerName);
        Assert.Equal("Company", c.CustomerType);
        Assert.Null(c.EmailId);
        Assert.False(c.Disabled);
    }

    // ---- Pass-2: Contacts (Dynamic Link child JOIN) ----

    [Fact]
    public async Task ReadContactsAsync_attaches_dynamic_link_rows()
    {
        var extractor = Extractor(ContactFixture);
        var contacts = await ToListAsync(extractor.ReadContactsAsync());

        Assert.Single(contacts);
        var contact = contacts[0];
        Assert.Equal("CONT-0001", contact.Name);
        Assert.Equal("contact@acme.com", contact.EmailId);
        Assert.Single(contact.Links);
        Assert.Equal("Customer", contact.Links[0].LinkDocType);
        Assert.Equal("CUST-0001", contact.Links[0].LinkName);
    }

    // ---- Pass-2: Tax Templates (header + rate row child JOIN) ----

    [Fact]
    public async Task ReadTaxTemplatesAsync_joins_rate_rows_from_child_table()
    {
        var extractor = Extractor(TaxTemplateFixture);
        var templates = await ToListAsync(extractor.ReadTaxTemplatesAsync());

        // One sales template; no purchase templates in fixture.
        Assert.Single(templates);
        var t = templates[0];
        Assert.Equal("VA Sales Tax", t.Name);
        Assert.Equal("VA Sales Tax", t.TaxName);
        Assert.Equal("State", t.TaxCategory);
        Assert.Single(t.Rates);
        Assert.Equal("Tax Liability - ACC", t.Rates[0].AccountHead);
        Assert.Equal(6m, t.Rates[0].Rate);
        Assert.False(t.Rates[0].IncludedInPrintRate);
        Assert.False(t.Disabled);
    }

    // ---- Pass-3/4: Journal Entries (header + Account lines child JOIN) ----

    [Fact]
    public async Task ReadJournalEntriesAsync_joins_line_rows_and_maps_is_opening()
    {
        var extractor = Extractor(JournalEntryFixture);
        var entries = await ToListAsync(extractor.ReadJournalEntriesAsync());

        Assert.Single(entries);
        var je = entries[0];
        Assert.Equal("JV-2026-0001", je.Name);
        Assert.Equal(new DateOnly(2026, 1, 1), je.PostingDate);
        Assert.Equal("Opening Entry", je.VoucherType);
        Assert.True(je.IsOpening); // "Yes" -> true
        Assert.Equal(1, je.DocStatus);
        Assert.Equal(2, je.Lines.Count);

        var debitLine = je.Lines[0];
        Assert.Equal("1000 - Bank", debitLine.AccountName);
        Assert.Equal(1000m, debitLine.DebitInAccountCurrency);
        Assert.Equal(0m, debitLine.CreditInAccountCurrency);

        var creditLine = je.Lines[1];
        Assert.Equal("2000 - Equity", creditLine.AccountName);
        Assert.Equal(0m, creditLine.DebitInAccountCurrency);
        Assert.Equal(1000m, creditLine.CreditInAccountCurrency);
        Assert.Equal("Opening", creditLine.UserRemark);
    }

    // ---- Pass-4.1: Sales Invoices (header + item child JOIN, USD guard) ----

    [Fact]
    public async Task ReadSalesInvoicesAsync_joins_item_lines()
    {
        var extractor = Extractor(SalesInvoiceFixture);
        var invoices = await ToListAsync(extractor.ReadSalesInvoicesAsync());

        Assert.Single(invoices);
        var inv = invoices[0];
        Assert.Equal("SINV-2026-0001", inv.Name);
        Assert.Equal("CUST-0001", inv.Customer);
        Assert.Equal(new DateOnly(2026, 3, 1), inv.PostingDate);
        Assert.Equal("USD", inv.Currency);
        Assert.Equal("Submitted", inv.Status);
        Assert.Equal(500m, inv.GrandTotal);
        Assert.Single(inv.Items);
        Assert.Equal("Consulting Services", inv.Items[0].ItemName);
        Assert.Equal(2m, inv.Items[0].Qty);
        Assert.Equal(250m, inv.Items[0].Rate);
        Assert.Equal(500m, inv.Items[0].Amount);
        Assert.Equal("Income - ACC", inv.Items[0].IncomeAccount);
    }

    [Fact]
    public async Task ReadSalesInvoicesAsync_fails_loud_on_non_usd_currency()
    {
        const string nonUsdDump = """
            CREATE TABLE `tabSales Invoice` (
              `name` varchar(140) NOT NULL,
              `modified` datetime(6) DEFAULT NULL,
              `customer` varchar(140) DEFAULT NULL,
              `posting_date` date DEFAULT NULL,
              `due_date` date DEFAULT NULL,
              `currency` varchar(10) DEFAULT NULL,
              `status` varchar(140) DEFAULT NULL,
              `grand_total` decimal(21,9) DEFAULT 0.000000000,
              `outstanding_amount` decimal(21,9) DEFAULT 0.000000000,
              PRIMARY KEY (`name`)
            ) ENGINE=InnoDB;
            INSERT INTO `tabSales Invoice` VALUES
              ('SINV-EUR-001','2026-03-01 00:00:00.000000','CUST-0001',
               '2026-03-01','2026-03-31','EUR','Submitted',100.000000000,100.000000000);
            """;

        var extractor = Extractor(nonUsdDump);
        // Non-USD -> ErpnextExtractionException with NonUsdCurrency reason
        await Assert.ThrowsAsync<ErpnextExtractionException>(async () =>
            await ToListAsync(extractor.ReadSalesInvoicesAsync()));
    }

    // ---- Pass-4.2: Purchase Invoices ----

    [Fact]
    public async Task ReadPurchaseInvoicesAsync_joins_item_lines()
    {
        var extractor = Extractor(PurchaseInvoiceFixture);
        var invoices = await ToListAsync(extractor.ReadPurchaseInvoicesAsync());

        Assert.Single(invoices);
        var inv = invoices[0];
        Assert.Equal("PINV-2026-0001", inv.Name);
        Assert.Equal("SUP-0001", inv.Supplier);
        Assert.Equal("BILL-001", inv.BillNo);
        Assert.Equal(new DateOnly(2026, 3, 1), inv.PostingDate);
        Assert.Equal(new DateOnly(2026, 3, 1), inv.BillDate);
        Assert.Equal("USD", inv.Currency);
        Assert.Equal(200m, inv.GrandTotal);
        Assert.Single(inv.Items);
        Assert.Equal("Office Supplies", inv.Items[0].ItemName);
        Assert.Equal("Expense - ACC", inv.Items[0].ExpenseAccount);
    }

    // ---- Census / inventory (ReadInventoryAsync) ----

    [Fact]
    public async Task ReadInventoryAsync_partitions_mapped_irrelevant_and_unmapped()
    {
        // InventoryFixture has: tabAccount (mapped), tabDocType (known-irrelevant),
        // tabProperty (unmapped-unknown).
        var extractor = Extractor(InventoryFixture);
        var inventory = await extractor.ReadInventoryAsync();

        var mapped = inventory.Mapped.ToList();
        var irrelevant = inventory.KnownIrrelevant.ToList();
        var unmapped = inventory.UnmappedUnknown.ToList();

        Assert.Single(mapped);
        Assert.Equal("Account", mapped[0].DocType);
        Assert.Equal(ErpnextDocTypeClassification.Mapped, mapped[0].Classification);
        Assert.Equal(1, mapped[0].SourceRowCount);

        Assert.Single(irrelevant);
        Assert.Equal("DocType", irrelevant[0].DocType);
        Assert.Equal(ErpnextDocTypeClassification.KnownIrrelevant, irrelevant[0].Classification);
        Assert.Equal(1, irrelevant[0].SourceRowCount);

        Assert.Single(unmapped);
        Assert.Equal("Property", unmapped[0].DocType);
        Assert.Equal(ErpnextDocTypeClassification.UnmappedUnknown, unmapped[0].Classification);
        Assert.Equal(1, unmapped[0].SourceRowCount);

        Assert.Equal(1, inventory.UnmappedUnknownCount);
        Assert.Equal(SourceAccessMode.MariaDbDump, inventory.SourceMode);
    }

    // ---- C-CLEANROOM: IErpnextSourceExtractor has no write methods ----

    [Fact]
    public void IErpnextSourceExtractor_exposes_only_read_operations()
    {
        var forbidden = new[]
        {
            "write", "update", "delete", "insert", "upsert", "save",
            "remove", "drop", "truncate",
        };
        var members = typeof(IErpnextSourceExtractor)
            .GetMembers()
            .Select(m => m.Name.ToLowerInvariant());

        foreach (var verb in forbidden)
        {
            Assert.DoesNotContain(members, m => m.Contains(verb));
        }
    }

    // ---- C-MODE: unknown enum values survive un-coerced ----

    [Fact]
    public async Task ReadAccountsAsync_unknown_account_type_survives_as_raw_string()
    {
        const string fixture = """
            CREATE TABLE `tabAccount` (
              `name` varchar(140) NOT NULL,
              `modified` datetime(6) DEFAULT NULL,
              `account_name` varchar(140) DEFAULT NULL,
              `account_type` varchar(140) DEFAULT NULL,
              `is_group` tinyint(1) NOT NULL DEFAULT 0,
              `disabled` tinyint(1) NOT NULL DEFAULT 0,
              PRIMARY KEY (`name`)
            ) ENGINE=InnoDB;
            INSERT INTO `tabAccount` VALUES
              ('XYZ-001','2026-01-01 00:00:00.000000','XYZ Account','UnknownType',0,0);
            """;

        var extractor = Extractor(fixture);
        var accounts = await ToListAsync(extractor.ReadAccountsAsync());

        Assert.Single(accounts);
        // The extractor MUST NOT coerce or reject an unknown account_type —
        // pass-through to the DTO for the A1 upserter's C5 handling.
        Assert.Equal("UnknownType", accounts[0].AccountType);
    }

    // ---- JournalEntry IsOpening: "No" -> false ----

    [Fact]
    public async Task ReadJournalEntriesAsync_is_opening_no_maps_to_false()
    {
        const string fixture = """
            CREATE TABLE `tabJournal Entry` (
              `name` varchar(140) NOT NULL,
              `modified` datetime(6) DEFAULT NULL,
              `posting_date` date DEFAULT NULL,
              `user_remark` text DEFAULT NULL,
              `voucher_type` varchar(140) DEFAULT NULL,
              `is_opening` varchar(10) DEFAULT 'No',
              `docstatus` int(1) DEFAULT 0,
              PRIMARY KEY (`name`)
            ) ENGINE=InnoDB;
            INSERT INTO `tabJournal Entry` VALUES
              ('JV-2026-0002','2026-03-01 00:00:00.000000',
               '2026-03-01','Regular JE','Journal Entry','No',1);

            CREATE TABLE `tabJournal Entry Account` (
              `name` varchar(140) NOT NULL,
              `parent` varchar(140) DEFAULT NULL,
              `account` varchar(140) DEFAULT NULL,
              `debit_in_account_currency` decimal(21,9) DEFAULT 0.000000000,
              `credit_in_account_currency` decimal(21,9) DEFAULT 0.000000000,
              PRIMARY KEY (`name`)
            ) ENGINE=InnoDB;
            INSERT INTO `tabJournal Entry Account` VALUES
              ('JEA-0010','JV-2026-0002','1000 - Bank',500.000000000,0.000000000);
            """;

        var extractor = Extractor(fixture);
        var entries = await ToListAsync(extractor.ReadJournalEntriesAsync());
        Assert.Single(entries);
        Assert.False(entries[0].IsOpening);
    }

    // ---- docstatus passes through un-filtered (C5 faithful-mirror) ----

    [Fact]
    public async Task ReadJournalEntriesAsync_surfaces_all_docstatus_values_unfiltered()
    {
        const string fixture = """
            CREATE TABLE `tabJournal Entry` (
              `name` varchar(140) NOT NULL,
              `modified` datetime(6) DEFAULT NULL,
              `posting_date` date DEFAULT NULL,
              `user_remark` text DEFAULT NULL,
              `voucher_type` varchar(140) DEFAULT NULL,
              `is_opening` varchar(10) DEFAULT 'No',
              `docstatus` int(1) DEFAULT 0,
              PRIMARY KEY (`name`)
            ) ENGINE=InnoDB;
            INSERT INTO `tabJournal Entry` VALUES
              ('JV-DRAFT','2026-01-01 00:00:00.000000','2026-01-01','D','Journal Entry','No',0),
              ('JV-SUBMITTED','2026-01-01 00:00:00.000000','2026-01-01','S','Journal Entry','No',1),
              ('JV-CANCELLED','2026-01-01 00:00:00.000000','2026-01-01','C','Journal Entry','No',2);

            CREATE TABLE `tabJournal Entry Account` (
              `name` varchar(140) NOT NULL,
              `parent` varchar(140) DEFAULT NULL,
              `account` varchar(140) DEFAULT NULL,
              `debit_in_account_currency` decimal(21,9) DEFAULT 0.000000000,
              `credit_in_account_currency` decimal(21,9) DEFAULT 0.000000000,
              PRIMARY KEY (`name`)
            ) ENGINE=InnoDB;
            """;

        var extractor = Extractor(fixture);
        var entries = await ToListAsync(extractor.ReadJournalEntriesAsync());

        // Extractor does NOT filter on docstatus — the upserter decides
        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, e => e.DocStatus == 0);
        Assert.Contains(entries, e => e.DocStatus == 1);
        Assert.Contains(entries, e => e.DocStatus == 2);
    }

    // ---- Null column -> null DTO field, not coerced ----

    [Fact]
    public async Task ReadPurchaseInvoicesAsync_null_bill_no_becomes_null_in_dto()
    {
        const string fixture = """
            CREATE TABLE `tabPurchase Invoice` (
              `name` varchar(140) NOT NULL,
              `modified` datetime(6) DEFAULT NULL,
              `supplier` varchar(140) DEFAULT NULL,
              `bill_no` varchar(140) DEFAULT NULL,
              `posting_date` date DEFAULT NULL,
              `due_date` date DEFAULT NULL,
              `bill_date` date DEFAULT NULL,
              `currency` varchar(10) DEFAULT NULL,
              `status` varchar(140) DEFAULT NULL,
              `grand_total` decimal(21,9) DEFAULT 0.000000000,
              `outstanding_amount` decimal(21,9) DEFAULT 0.000000000,
              PRIMARY KEY (`name`)
            ) ENGINE=InnoDB;
            INSERT INTO `tabPurchase Invoice` VALUES
              ('PINV-NULL-BILL','2026-01-01 00:00:00.000000','SUP-0001',NULL,
               '2026-01-01','2026-01-31',NULL,NULL,'Submitted',100.000000000,0.000000000);

            CREATE TABLE `tabPurchase Invoice Item` (
              `name` varchar(140) NOT NULL,
              `parent` varchar(140) DEFAULT NULL,
              `item_name` varchar(140) DEFAULT NULL,
              `qty` decimal(21,9) DEFAULT 1.000000000,
              `rate` decimal(21,9) DEFAULT 0.000000000,
              `amount` decimal(21,9) DEFAULT 0.000000000,
              PRIMARY KEY (`name`)
            ) ENGINE=InnoDB;
            """;

        var extractor = Extractor(fixture);
        var invoices = await ToListAsync(extractor.ReadPurchaseInvoicesAsync());
        Assert.Single(invoices);
        Assert.Null(invoices[0].BillNo);    // SQL NULL -> null, not empty string
        Assert.Null(invoices[0].BillDate);  // SQL NULL -> null
    }

    // ---- ExtractionException is C9-safe (no field values in message) ----

    [Fact]
    public void ErpnextExtractionException_message_contains_no_field_value()
    {
        var ex = new ErpnextExtractionException(
            docType: "Sales Invoice",
            externalRef: "SINV-0001",
            reason: ErpnextExtractionReason.NonUsdCurrency,
            fieldName: "currency");

        // Message must NOT contain any field value — only DocType, externalRef (opaque), reason, field name.
        Assert.Contains("Sales Invoice", ex.Message);
        Assert.Contains("SINV-0001", ex.Message);
        Assert.Contains("NonUsdCurrency", ex.Message);
        Assert.Contains("currency", ex.Message);
        // Sanity: it does NOT contain an actual currency value we might have had
        Assert.DoesNotContain("EUR", ex.Message);
        Assert.DoesNotContain("GBP", ex.Message);
    }
}
