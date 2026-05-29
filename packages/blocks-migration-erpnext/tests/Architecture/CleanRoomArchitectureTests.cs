using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Sunfish.Blocks.Migration.Erpnext.Extraction;
using Xunit;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Architecture;

/// <summary>
/// C-CLEANROOM and C-MODE architecture tests for <c>blocks-migration-erpnext</c>
/// (ADR 0100 C4 / sec-eng mandatory floors). These are source-tree scans rooted
/// at the package directory, discovered from the test assembly location.
/// </summary>
public sealed class CleanRoomArchitectureTests
{
    // ---- C-CLEANROOM (a): read-only interface ----

    /// <summary>
    /// C-CLEANROOM (a): <see cref="IErpnextSourceExtractor"/> exposes ONLY read
    /// operations. No write/update/delete/insert/upsert/save member.
    /// </summary>
    [Fact]
    public void IErpnextSourceExtractor_exposes_only_read_operations()
    {
        var forbidden = new[] { "write", "update", "delete", "insert", "upsert", "save", "remove", "drop", "truncate" };
        var members = typeof(IErpnextSourceExtractor).GetMembers().Select(m => m.Name.ToLowerInvariant());

        foreach (var verb in forbidden)
        {
            Assert.DoesNotContain(members, m => m.Contains(verb));
        }
    }

    // ---- C-CLEANROOM (b): license/attribution header ----

    /// <summary>
    /// C-CLEANROOM (b): the primary extractor source file carries the clean-room
    /// attribution header naming ERPNext + Frappe + GPLv3 + FORMAT-REFERENCE-ONLY.
    /// </summary>
    [Fact]
    public void MariaDbDumpExtractor_carries_clean_room_attribution()
    {
        var adapter = Path.Combine(PackageDir(), "Extraction", "MariaDbDumpExtractor.cs");
        Assert.True(File.Exists(adapter), $"MariaDbDumpExtractor.cs not found at {adapter}");

        var text = File.ReadAllText(adapter);
        Assert.Contains("CLEAN-ROOM ATTRIBUTION", text);
        Assert.Contains("GPLv3", text);
        Assert.Contains("FORMAT-REFERENCE-ONLY", text);
    }

    /// <summary>
    /// C-CLEANROOM (b): the <c>LICENSE-ATTRIBUTION.md</c> file is present at the
    /// package root.
    /// </summary>
    [Fact]
    public void Package_carries_license_attribution_file()
    {
        var attributionFile = Path.Combine(PackageDir(), "LICENSE-ATTRIBUTION.md");
        Assert.True(
            File.Exists(attributionFile),
            $"LICENSE-ATTRIBUTION.md not found at {attributionFile}. " +
            "Required by C-CLEANROOM (b) / ADR 0100 C4 / spec §3.4.");
    }

    // ---- C-CLEANROOM (d): no live network / DB connection in Extraction/ ----

    /// <summary>
    /// C-CLEANROOM (d): no type under <c>Extraction/</c> opens an HTTP client,
    /// REST client, or a LIVE DB connection to a Frappe/ERPNext endpoint.
    /// v1 is dump-only (offline). The <c>IRestoredDbConnectionFactory</c> creates
    /// the connection; the extractor never creates one directly.
    /// </summary>
    [Fact]
    public void Extraction_folder_opens_no_live_network_connection()
    {
        var extractionDir = Path.Combine(PackageDir(), "Extraction");
        Assert.True(Directory.Exists(extractionDir), $"Extraction dir not found at {extractionDir}");

        // Markers that indicate a live HTTP/REST/direct-DB connection surface.
        var forbiddenMarkers = new[]
        {
            "HttpClient",
            "WebRequest",
            "Socket",
            "TcpClient",
            ".OpenConnection",
        };

        foreach (var file in Directory.EnumerateFiles(extractionDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var marker in forbiddenMarkers)
            {
                Assert.False(
                    text.Contains(marker, System.StringComparison.Ordinal),
                    $"Extraction file '{Path.GetFileName(file)}' contains live-connection marker '{marker}' " +
                    "— v1 is dump-only/offline (ADR 0100 C4/C6; C-CLEANROOM (d)).");
            }
        }
    }

    // ---- C-CLEANROOM (c): SELECT-only assertion ----

    /// <summary>
    /// C-CLEANROOM (c): <see cref="MariaDbDumpExtractor"/> issues ONLY SELECT
    /// statements — no INSERT/UPDATE/DELETE/DROP/CREATE/ALTER/TRUNCATE in the
    /// CommandText assignments. Source-text scan on MariaDbDumpExtractor.cs.
    /// </summary>
    [Fact]
    public void MariaDbDumpExtractor_issues_select_only_statements()
    {
        var adapter = Path.Combine(PackageDir(), "Extraction", "MariaDbDumpExtractor.cs");
        var text = File.ReadAllText(adapter);

        // Check for DDL/DML verb assignments to CommandText — any line that sets
        // CommandText to a string starting with a write verb (case-insensitive).
        var dmlPattern = new Regex(
            @"CommandText\s*=\s*[""@]?\s*(INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|TRUNCATE|MERGE|REPLACE)\b",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        Assert.False(
            dmlPattern.IsMatch(text),
            "MariaDbDumpExtractor.cs contains a CommandText assignment with a write verb " +
            "(INSERT/UPDATE/DELETE/DROP/CREATE/ALTER/TRUNCATE). " +
            "The extractor issues ONLY SELECT statements (ADR 0100 C4 (c); C-CLEANROOM).");
    }

    // ---- C-MODE: no Tab* table name reference leaks above the seam ----

    /// <summary>
    /// C-MODE: the interface file <c>IErpnextSourceExtractor.cs</c> must NOT contain
    /// any <c>tab</c>-prefixed table name (e.g. <c>"tabAccount"</c>). The seam is
    /// mode-agnostic; <c>tab*</c> knowledge lives in the v1 implementation only.
    /// </summary>
    [Fact]
    public void Interface_file_contains_no_tab_table_names()
    {
        var interfaceFile = Path.Combine(PackageDir(), "Extraction", "IErpnextSourceExtractor.cs");
        var text = File.ReadAllText(interfaceFile);

        // Match "tab" followed by an uppercase letter (the ERPNext table naming pattern).
        var tabPattern = new Regex(@"\btab[A-Z]", RegexOptions.None);
        Assert.False(
            tabPattern.IsMatch(text),
            $"IErpnextSourceExtractor.cs contains a 'tab*' table name reference. " +
            "The seam interface must be mode-agnostic — tab* names belong in the v1 implementation " +
            "(ADR 0100 C-MODE invariant).");
    }

    // ---- Defensive: dump path never hard-coded ----

    /// <summary>
    /// The dump file path is NEVER hard-coded in the package source. It must come
    /// from a CLI flag or env var. Source-text scan for common hard-coded path patterns.
    /// </summary>
    [Fact]
    public void No_hard_coded_dump_file_path_in_source()
    {
        // Common hard-coded path patterns.
        var suspiciousPatterns = new[]
        {
            @".sql""",     // ends with .sql" (a string literal containing a path)
            @"dump.sql",
            @"erpnext.sql",
            @"/home/",
            @"/Users/",
            @"C:\\",
            @"C:/",
        };

        // Exclude the .gitignore (it legitimately lists *.sql patterns) and tests.
        foreach (var file in Directory.EnumerateFiles(PackageDir(), "*.cs", SearchOption.AllDirectories))
        {
            var normPath = file.Replace('\\', '/');
            if (normPath.Contains("/tests/") || normPath.Contains("/obj/") || normPath.Contains("/bin/"))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (var pattern in suspiciousPatterns)
            {
                Assert.False(
                    text.Contains(pattern, System.StringComparison.OrdinalIgnoreCase),
                    $"Source file '{Path.GetFileName(file)}' may contain a hard-coded dump path " +
                    $"(pattern: '{pattern}'). The dump path MUST come from CLI flag or env var (C9).");
            }
        }
    }

    // ---- Helpers ----

    private static string PackageDir()
    {
        // Test assembly runs from .../packages/blocks-migration-erpnext/tests/bin/<cfg>/net11.0/.
        // Walk up to the package dir (contains the .csproj).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Sunfish.Blocks.Migration.Erpnext.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate blocks-migration-erpnext package directory from " + AppContext.BaseDirectory);
    }
}
