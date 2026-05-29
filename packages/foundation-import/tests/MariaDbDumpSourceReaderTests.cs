using System.IO;

namespace Sunfish.Foundation.Import.Tests;

/// <summary>
/// Unit tests for the v1 <see cref="MariaDbDumpSourceReader"/> against a small
/// in-test ERPNext-shaped <c>mysqldump</c> fixture — no CIC data (ADR 0100 C4 /
/// C6). Exercises DocType discovery, column/row parsing, MySQL string escaping,
/// NULL handling, streaming, counting, and the offline/read-only posture.
/// </summary>
public sealed class MariaDbDumpSourceReaderTests
{
    // A minimal ERPNext-shaped dump fixture: two DocType tables (Account, Customer)
    // plus a non-DocType framework table that MUST be ignored (no `tab` prefix).
    // Covers: escaped quote (''), backslash escape (\n), NULL, decimal, and a
    // DocType table name containing a space (`tabSales Invoice`).
    private const string FixtureDump = """
        -- MariaDB dump fixture (synthetic; no CIC data)
        CREATE TABLE `tabAccount` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `account_name` varchar(140) DEFAULT NULL,
          `account_type` varchar(140) DEFAULT NULL,
          `is_group` int(1) NOT NULL DEFAULT 0,
          PRIMARY KEY (`name`),
          KEY `modified` (`modified`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

        INSERT INTO `tabAccount` VALUES
          ('Bank - ACME','2026-01-02 10:00:00.000000','Bank','Bank',0),
          ('Assets - ACME','2026-01-01 09:00:00.000000','Assets',NULL,1);

        CREATE TABLE `tabCustomer` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `customer_name` varchar(140) DEFAULT NULL,
          `customer_type` varchar(140) DEFAULT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;

        INSERT INTO `tabCustomer` VALUES
          ('O''Brien LLC','2026-02-01 12:00:00.000000','O''Brien LLC','Company'),
          ('Line\nBreak Co','2026-02-02 12:00:00.000000','Line\nBreak Co','Individual');

        CREATE TABLE `tabSales Invoice` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `grand_total` decimal(21,9) DEFAULT 0.000000000,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;

        INSERT INTO `tabSales Invoice` VALUES ('INV-2026-0001','2026-03-01 08:00:00.000000',1234.560000000);

        CREATE TABLE `__global_search` (
          `doctype` varchar(100) DEFAULT NULL,
          `content` longtext
        ) ENGINE=InnoDB;

        INSERT INTO `__global_search` VALUES ('Account','should be ignored');
        """;

    private static MariaDbDumpSourceReader Reader() => MariaDbDumpSourceReader.FromSql(FixtureDump);

    [Fact]
    public void Mode_is_maria_db_dump()
    {
        Assert.Equal(SourceAccessMode.MariaDbDump, Reader().Mode);
    }

    [Fact]
    public void Discovers_only_tab_prefixed_doctypes_stripping_the_prefix()
    {
        var docTypes = Reader().AvailableDocTypes;

        Assert.Contains("Account", docTypes);
        Assert.Contains("Customer", docTypes);
        Assert.Contains("Sales Invoice", docTypes); // space-containing DocType
        // The framework table `__global_search` has no `tab` prefix → ignored.
        Assert.DoesNotContain("__global_search", docTypes);
        Assert.Equal(3, docTypes.Count);
    }

    [Fact]
    public async Task Reads_account_rows_with_columns_aligned_to_create_table_order()
    {
        var rows = await ReadAll("Account");

        Assert.Equal(2, rows.Count);

        var bank = rows[0];
        Assert.Equal("Account", bank.DocType);
        Assert.Equal("Bank - ACME", bank.GetString("name"));
        Assert.Equal("Bank", bank.GetString("account_name"));
        Assert.Equal("Bank", bank.GetString("account_type"));
        Assert.Equal("0", bank.GetString("is_group"));
        Assert.Equal("2026-01-02 10:00:00.000000", bank.GetString("modified"));
    }

    [Fact]
    public async Task Null_source_value_is_surfaced_as_null()
    {
        var rows = await ReadAll("Account");
        var assets = rows[1];

        Assert.Equal("Assets - ACME", assets.GetString("name"));
        // account_type was NULL in the dump.
        Assert.Null(assets.GetString("account_type"));
        Assert.True(assets.HasColumn("account_type"));
        Assert.Equal("1", assets.GetString("is_group"));
    }

    [Fact]
    public async Task Handles_doubled_single_quote_escaping()
    {
        var rows = await ReadAll("Customer");
        Assert.Equal("O'Brien LLC", rows[0].GetString("name"));
        Assert.Equal("O'Brien LLC", rows[0].GetString("customer_name"));
        Assert.Equal("Company", rows[0].GetString("customer_type"));
    }

    [Fact]
    public async Task Handles_backslash_newline_escaping()
    {
        var rows = await ReadAll("Customer");
        Assert.Equal("Line\nBreak Co", rows[1].GetString("name"));
        Assert.Equal("Individual", rows[1].GetString("customer_type"));
    }

    [Fact]
    public async Task Reads_decimal_and_space_containing_doctype()
    {
        var rows = await ReadAll("Sales Invoice");
        Assert.Single(rows);
        Assert.Equal("INV-2026-0001", rows[0].GetString("name"));
        Assert.Equal("1234.560000000", rows[0].GetString("grand_total"));
    }

    [Fact]
    public void Column_lookup_is_case_insensitive()
    {
        var row = new SourceRow("Account", new Dictionary<string, string?> { ["account_name"] = "Bank" });
        Assert.Equal("Bank", row.GetString("ACCOUNT_NAME"));
        Assert.Equal("Bank", row.GetString("Account_Name"));
    }

    [Fact]
    public async Task CountDocTypeAsync_counts_without_materializing()
    {
        var reader = Reader();
        Assert.Equal(2, await reader.CountDocTypeAsync("Account"));
        Assert.Equal(2, await reader.CountDocTypeAsync("Customer"));
        Assert.Equal(1, await reader.CountDocTypeAsync("Sales Invoice"));
        Assert.Equal(0, await reader.CountDocTypeAsync("Nonexistent"));
    }

    [Fact]
    public async Task Absent_doctype_yields_empty_stream()
    {
        var rows = await ReadAll("Nonexistent");
        Assert.Empty(rows);
    }

    [Fact]
    public async Task LoadAsync_opens_a_static_file_read_only()
    {
        var path = Path.Combine(Path.GetTempPath(), $"erpnext-fixture-{Guid.NewGuid():N}.sql");
        await File.WriteAllTextAsync(path, FixtureDump);
        try
        {
            // Open the file with a read-only-incompatible exclusive write lock held
            // by another handle would fail; here we just prove LoadAsync reads it and
            // never needs write access — and that the file is untouched after.
            var before = await File.ReadAllTextAsync(path);
            var reader = await MariaDbDumpSourceReader.LoadAsync(path);
            var after = await File.ReadAllTextAsync(path);

            Assert.Equal(3, reader.AvailableDocTypes.Count);
            Assert.Equal(before, after); // source file unmodified (read-only / clean-room)
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_throws_for_missing_file()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.sql");
        await Assert.ThrowsAsync<FileNotFoundException>(() => MariaDbDumpSourceReader.LoadAsync(missing));
    }

    private static async Task<List<SourceRow>> ReadAll(string docType)
    {
        var result = new List<SourceRow>();
        await foreach (var row in Reader().ReadDocTypeAsync(docType))
        {
            result.Add(row);
        }

        return result;
    }
}
