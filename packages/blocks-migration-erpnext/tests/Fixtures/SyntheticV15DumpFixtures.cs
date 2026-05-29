// CS8767: The IDbConnection/IDbCommand/IDataParameter interfaces in .NET 11
// declare setters as `set(string? value)` while getters return `string`.
// This forces nullable setter mismatches in explicit implementations.
// Suppressed in this test-fixture file; these in-memory stubs are not
// production code.
#pragma warning disable CS8767

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.Migration.Erpnext.Extraction;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Fixtures;

/// <summary>
/// Synthetic ERPNext v15-shaped in-memory fixtures — sample <c>tab*</c>
/// table/column/row data that exercises the extractor without a real dump or DB.
/// </summary>
/// <remarks>
/// All field names use the canonical v15 ERPNext column names (snake_case) as
/// they would appear in a mysqldump-restored DB. No real data; entirely synthetic.
/// </remarks>
public static class SyntheticV15DumpFixtures
{
    // ---- tabAccount rows ----

    public static IReadOnlyList<Dictionary<string, object?>> AccountRows { get; } = new[]
    {
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Acero - A",
            ["modified"] = "2024-01-01 00:00:00.000000",
            ["account_name"] = "Acero - Assets",
            ["account_number"] = "1000",
            ["parent_account"] = null,
            ["account_type"] = "Balance Sheet",
            ["is_group"] = 1,
            ["disabled"] = 0,
        },
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Cash - A",
            ["modified"] = "2024-01-02 00:00:00.000000",
            ["account_name"] = "Cash",
            ["account_number"] = "1001",
            ["parent_account"] = "Acero - A",
            ["account_type"] = "Cash",
            ["is_group"] = 0,
            ["disabled"] = 0,
        },
    };

    // ---- tabCost Center rows ----

    public static IReadOnlyList<Dictionary<string, object?>> CostCenterRows { get; } = new[]
    {
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Main - A",
            ["modified"] = "2024-01-01 00:00:00.000000",
            ["cost_center_name"] = "Main",
            ["parent_cost_center"] = null,
            ["is_group"] = 1,
            ["disabled"] = 0,
        },
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Property-101 - A",
            ["modified"] = "2024-01-02 00:00:00.000000",
            ["cost_center_name"] = "Property 101",
            ["parent_cost_center"] = "Main - A",
            ["is_group"] = 0,
            ["disabled"] = 0,
        },
    };

    // ---- tabFiscal Year rows + tabFiscal Year Company ----

    public static IReadOnlyList<Dictionary<string, object?>> FiscalYearRows { get; } = new[]
    {
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "2023-2024",
            ["modified"] = "2023-01-01 00:00:00.000000",
            ["year_start_date"] = "2023-01-01",
            ["year_end_date"] = "2023-12-31",
            ["company_abbr"] = "A",
            ["is_short_year"] = 0,
        },
    };

    // ---- tabCustomer rows ----

    public static IReadOnlyList<Dictionary<string, object?>> CustomerRows { get; } = new[]
    {
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "CUST-0001",
            ["modified"] = "2024-03-01 00:00:00.000000",
            ["customer_name"] = "Acme Corp",   // PII — never log
            ["customer_type"] = "Company",
            ["email_id"] = "billing@acme.example", // PII
            ["mobile_no"] = null,
            ["tax_id"] = null,
            ["disabled"] = 0,
        },
    };

    // ---- tabSupplier rows ----

    public static IReadOnlyList<Dictionary<string, object?>> SupplierRows { get; } = new[]
    {
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "SUP-0001",
            ["modified"] = "2024-03-15 00:00:00.000000",
            ["supplier_name"] = "Parts Inc",   // PII — never log
            ["supplier_type"] = "Company",
            ["email_id"] = "ap@partsinc.example", // PII
            ["mobile_no"] = null,
            ["tax_id"] = "12-3456789",         // PII
            ["disabled"] = 0,
        },
    };

    // ---- tabSales Invoice rows (for USD-only assertion testing) ----

    public static Dictionary<string, object?> UsdSalesInvoiceRow { get; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "SINV-0001",
            ["modified"] = "2024-04-01 00:00:00.000000",
            ["customer"] = "CUST-0001",
            ["posting_date"] = "2024-04-01",
            ["due_date"] = "2024-04-30",
            ["currency"] = "USD",
            ["status"] = "Submitted",
            ["grand_total"] = "1000.00",
            ["outstanding_amount"] = "1000.00",
        };

    public static Dictionary<string, object?> EurSalesInvoiceRow { get; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "SINV-0099",
            ["modified"] = "2024-04-01 00:00:00.000000",
            ["customer"] = "CUST-0001",
            ["posting_date"] = "2024-04-01",
            ["due_date"] = "2024-04-30",
            ["currency"] = "EUR",     // non-USD — should fail loud
            ["status"] = "Submitted",
            ["grand_total"] = "900.00",
            ["outstanding_amount"] = "900.00",
        };

    // ---- Inventory census fixture ----
    // Simulates a dump with: 3 mapped, 2 known-irrelevant, 1 unmapped-unknown

    public static IReadOnlyList<(string TableName, string Partition)> InventoryClassification { get; } =
        new[]
        {
            ("tabAccount", "mapped"),
            ("tabCustomer", "mapped"),
            ("tabFiscal Year", "mapped"),
            ("tabDocType", "known-irrelevant"),
            ("tabSingles", "known-irrelevant"),
            ("tabProperty", "unmapped-unknown"),  // custom DocType — appears in _unmapped/
        };
}

/// <summary>
/// A fake <see cref="IRestoredDbConnectionFactory"/> that returns an in-memory
/// <see cref="IDbConnection"/> backed by <see cref="InMemoryDbConnection"/>.
/// Used by tests that don't require a real DB restore.
/// </summary>
public sealed class FakeRestoredDbConnectionFactory : IRestoredDbConnectionFactory
{
    private readonly InMemoryDbConnection _connection;

    public FakeRestoredDbConnectionFactory(InMemoryDbConnection connection)
    {
        _connection = connection;
    }

    public Task<IDbConnection> RestoreAndConnectAsync(
        string runId, CancellationToken ct = default)
        => Task.FromResult<IDbConnection>(_connection);

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}

/// <summary>
/// A minimal in-memory <see cref="IDbConnection"/> backed by a pre-configured
/// table of synthetic rows. Supports only <c>ExecuteReader</c> and
/// <c>ExecuteScalar</c> (COUNT(*)) — sufficient for the extractor's SELECT usage.
/// </summary>
public sealed class InMemoryDbConnection : IDbConnection
{
    private readonly Dictionary<string, IReadOnlyList<Dictionary<string, object?>>> _tables;

    public InMemoryDbConnection(
        Dictionary<string, IReadOnlyList<Dictionary<string, object?>>> tables)
    {
        _tables = tables;
    }

    private string _connectionString = string.Empty;
    public string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value ?? string.Empty;
    }
    public int ConnectionTimeout => 30;
    public string Database => "test_db";
    public ConnectionState State => ConnectionState.Open;

    public IDbTransaction BeginTransaction() => throw new NotSupportedException();
    public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
    public void ChangeDatabase(string databaseName) { }
    public void Close() { }
    public IDbCommand CreateCommand() => new InMemoryDbCommand(this);
    public void Open() { }
    public void Dispose() { }

    internal IReadOnlyList<Dictionary<string, object?>> GetRows(string tableName)
    {
        if (_tables.TryGetValue(tableName, out var rows))
        {
            return rows;
        }

        return Array.Empty<Dictionary<string, object?>>();
    }

    internal IReadOnlyCollection<string> GetTableNames() => _tables.Keys;
}

/// <summary>
/// An in-memory <see cref="IDbCommand"/> that parses the table name from a
/// simple SELECT or COUNT(*) query and returns rows from <see cref="InMemoryDbConnection"/>.
/// Only supports the SELECT patterns used by <see cref="MariaDbDumpExtractor"/>.
/// </summary>
internal sealed class InMemoryDbCommand : IDbCommand
{
    private readonly InMemoryDbConnection _connection;

    public InMemoryDbCommand(InMemoryDbConnection connection)
    {
        _connection = connection;
    }

    private string _commandText = string.Empty;
    public string CommandText
    {
        get => _commandText;
        set => _commandText = value ?? string.Empty;
    }
    public int CommandTimeout { get; set; }
    public CommandType CommandType { get; set; }
    public IDbConnection? Connection { get; set; }
    public IDataParameterCollection Parameters { get; } = new InMemoryParameterCollection();
    public IDbTransaction? Transaction { get; set; }
    public UpdateRowSource UpdatedRowSource { get; set; }

    public void Cancel() { }
    public IDbDataParameter CreateParameter() => new InMemoryParameter();
    public void Dispose() { }
    public void Prepare() { }

    public int ExecuteNonQuery() => 0;

    public object? ExecuteScalar()
    {
        // Handle COUNT(*) FROM `tabXxx`
        var tableName = ExtractTableName(CommandText);
        if (tableName is null)
        {
            return 0;
        }

        return _connection.GetRows(tableName).Count;
    }

    public IDataReader ExecuteReader()
    {
        var tableName = ExtractTableName(CommandText);
        if (tableName is null)
        {
            return new InMemoryDataReader(Array.Empty<Dictionary<string, object?>>());
        }

        return new InMemoryDataReader(_connection.GetRows(tableName));
    }

    public IDataReader ExecuteReader(CommandBehavior behavior) => ExecuteReader();

    private static string? ExtractTableName(string sql)
    {
        // Match: FROM `tabXxx` or FROM `tabXxx Yyy`
        var match = System.Text.RegularExpressions.Regex.Match(
            sql, @"FROM\s+`([^`]+)`", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}

internal sealed class InMemoryDataReader : IDataReader
{
    private readonly IReadOnlyList<Dictionary<string, object?>> _rows;
    private int _index = -1;
    private List<string>? _columns;

    public InMemoryDataReader(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        _rows = rows;
    }

    public bool Read()
    {
        _index++;
        if (_index < _rows.Count)
        {
            _columns = new List<string>(_rows[_index].Keys);
            return true;
        }

        return false;
    }

    public object GetValue(int i)
    {
        var col = _columns![i];
        return _rows[_index][col] ?? DBNull.Value;
    }

    public bool IsDBNull(int i)
    {
        var col = _columns![i];
        return _rows[_index][col] is null;
    }

    public string GetString(int i)
    {
        var val = GetValue(i);
        return val?.ToString() ?? string.Empty;
    }

    public int GetInt32(int i)
    {
        var val = GetValue(i);
        return val is null || val is DBNull ? 0 : Convert.ToInt32(val);
    }

    public bool GetBoolean(int i) => GetInt32(i) != 0;

    public int FieldCount => _columns?.Count ?? 0;

    public void Close() { }
    public void Dispose() { }
    public int Depth => 0;
    public bool IsClosed => false;
    public int RecordsAffected => -1;

    // Minimal implementations for the interface contract
    public object this[int i] => GetValue(i);
    public object this[string name] => _rows[_index][name] ?? DBNull.Value;
    public bool NextResult() => false;
    public DataTable? GetSchemaTable() => null;
    public string GetName(int i) => _columns?[i] ?? string.Empty;
    public string GetDataTypeName(int i) => "string";
    public Type GetFieldType(int i) => typeof(string);
    public int GetOrdinal(string name) => _columns?.IndexOf(name) ?? -1;
    public int GetValues(object[] values) => 0;
    public bool GetBoolean(int i, bool _) => GetBoolean(i);
    public byte GetByte(int i) => 0;
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public char GetChar(int i) => ' ';
    public long GetChars(int i, long fieldOffset, char[]? buffer, int bufferOffset, int length) => 0;
    public Guid GetGuid(int i) => Guid.Empty;
    public short GetInt16(int i) => (short)GetInt32(i);
    public long GetInt64(int i) => GetInt32(i);
    public float GetFloat(int i) => 0;
    public double GetDouble(int i) => 0;
    public decimal GetDecimal(int i) => 0;
    public DateTime GetDateTime(int i) => DateTime.MinValue;
    public IDataReader GetData(int i) => this;
    public bool HasRows => _rows.Count > 0;
}

internal sealed class InMemoryParameterCollection : System.Collections.ArrayList, IDataParameterCollection
{
    public bool Contains(string parameterName) => false;
    public int IndexOf(string parameterName) => -1;
    public void RemoveAt(string parameterName) { }
    public object this[string parameterName]
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
}

internal sealed class InMemoryParameter : IDbDataParameter
{
    private string _parameterName = string.Empty;
    public string ParameterName
    {
        get => _parameterName;
        set => _parameterName = value ?? string.Empty;
    }

    public object? Value { get; set; }
    public DbType DbType { get; set; }
    public ParameterDirection Direction { get; set; }
    public bool IsNullable => false;

    private string _sourceColumn = string.Empty;
    public string SourceColumn
    {
        get => _sourceColumn;
        set => _sourceColumn = value ?? string.Empty;
    }
    public DataRowVersion SourceVersion { get; set; }
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public int Size { get; set; }
}
