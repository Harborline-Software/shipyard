using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Sunfish.Foundation.Import.Tests;

/// <summary>
/// Architecture tests for the ERPNext-import primitives (ADR 0100 Rev-2
/// amendments G/H + OQ-A + C4). The contract makes these provable rather than
/// author-memory obligations. They are source-tree scans (no NetArchTest in the
/// repo) rooted at the repo root discovered from the test assembly location.
/// </summary>
public sealed class ImportPrimitivesArchitectureTests
{
    // ---- Owning package + DAG geometry (.NET-arch amendment G) ----

    /// <summary>
    /// The single owning home for the contract types. A0 establishes
    /// <c>foundation-import</c> as the canonical definition; the five legacy
    /// per-cluster <c>ImportOutcome</c> copies are retired by the A1–A4
    /// convergence PRs (D7 collapse). This test asserts (a) the canonical
    /// definition lives in foundation-import, and (b) NO definition exists
    /// outside the canonical package + the known-legacy allowlist — so a NEW
    /// sixth copy can never be introduced, and the count only shrinks as A1–A4
    /// delete copies. (G-arch convergence scaffold.)
    /// </summary>
    [Fact]
    public void ImportOutcome_canonical_definition_is_in_foundation_import_and_no_new_copies_appear()
    {
        var defs = FindFilesDefining(@"record\s+ImportOutcome");

        var canonical = defs.Where(p => p.Replace('\\', '/').Contains("packages/foundation-import/")).ToList();
        Assert.True(
            canonical.Count == 1,
            $"Expected exactly ONE canonical ImportOutcome in foundation-import; found {canonical.Count}: " +
            string.Join(", ", canonical));

        // Known-legacy per-cluster copies pending retirement by A1–A4 (D7 collapse).
        // The A-unit PRs delete these; this allowlist must only ever SHRINK.
        // A1 (Workstream A1, Pass-1 chart of accounts) retired the
        // blocks-financial-ledger copy by migrating its ErpnextAccount /
        // ErpnextJournalEntry importers onto Sunfish.Foundation.Import.
        var knownLegacy = new[]
        {
            "packages/blocks-financial-ar/Migration/ImportOutcome.cs",
            "packages/blocks-financial-ap/Migration/ImportOutcome.cs",
            "packages/blocks-people-foundation/Migration/ImportOutcome.cs",
            "packages/blocks-work-projects/Migration/ImportOutcome.cs",
        };

        var unexpected = defs
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !p.Contains("packages/foundation-import/"))
            .Where(p => !knownLegacy.Any(p.EndsWith))
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            "A new ImportOutcome definition appeared outside foundation-import and the known-legacy " +
            "allowlist — converge it onto Sunfish.Foundation.Import instead of adding a copy. Offenders: " +
            string.Join(", ", unexpected));
    }

    /// <summary>
    /// <see cref="ImportFailure"/> has exactly ONE definition fleet-wide and it
    /// lives in the owning package (it is introduced by this contract, so there
    /// is no legacy copy to tolerate).
    /// </summary>
    [Fact]
    public void ImportFailure_has_exactly_one_definition_in_foundation_import()
    {
        var defs = FindFilesDefining(@"(record|class)\s+ImportFailure");
        Assert.True(
            defs.Count == 1,
            $"Expected exactly ONE ImportFailure definition; found {defs.Count}: {string.Join(", ", defs)}");
        Assert.Contains("foundation-import", defs[0].Replace('\\', '/'));
    }

    /// <summary>
    /// Acyclic geometry: the owning package sits BELOW every <c>blocks-*</c>
    /// cluster in the DAG, so it must NOT reference any blocks package (that would
    /// create a cycle once the clusters reference foundation-import in A1–A4).
    /// </summary>
    [Fact]
    public void Foundation_import_does_not_reference_any_blocks_package()
    {
        var csprojPath = Path.Combine(
            PackageDir(), "Sunfish.Foundation.Import.csproj");
        Assert.True(File.Exists(csprojPath), $"csproj not found at {csprojPath}");

        // Scope the check to actual ProjectReference / PackageReference Include
        // values — not the human-readable <Description> prose (which legitimately
        // mentions "below every blocks-* cluster").
        var refs = Regex.Matches(
                File.ReadAllText(csprojPath),
                @"(?:Project|Package)Reference\s+Include\s*=\s*""(?<inc>[^""]+)""",
                RegexOptions.IgnoreCase)
            .Select(m => m.Groups["inc"].Value)
            .ToList();

        foreach (var inc in refs)
        {
            Assert.False(
                inc.Contains("Sunfish.Blocks.", StringComparison.OrdinalIgnoreCase)
                || inc.Contains("blocks-", StringComparison.OrdinalIgnoreCase),
                $"foundation-import must not reference a blocks package (would create a DAG cycle); offending ref: {inc}");
        }
    }

    // ---- DU shape (OQ-A) ----

    /// <summary>
    /// The <see cref="ImportOutcome{T}"/> union is CLOSED: its arm types are all
    /// nested sealed records, and the base record's constructor is not public — so
    /// no external arm can be declared and the orchestrator's exhaustive switch
    /// stays sound.
    /// </summary>
    [Fact]
    public void ImportOutcome_is_a_closed_union_of_four_sealed_arms()
    {
        var open = typeof(ImportOutcome<>);

        var arms = open.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(t => t.BaseType is { IsGenericType: true }
                        && t.BaseType.GetGenericTypeDefinition() == open)
            .ToList();

        // Inserted / Updated / Skipped / Rejected.
        Assert.Equal(4, arms.Count);
        Assert.All(arms, arm => Assert.True(arm.IsSealed, $"{arm.Name} must be sealed"));
        Assert.Contains(arms, a => a.Name == "Inserted");
        Assert.Contains(arms, a => a.Name == "Updated");
        Assert.Contains(arms, a => a.Name == "Skipped");
        Assert.Contains(arms, a => a.Name == "Rejected");

        // The base has no public constructor (closed to this assembly).
        var publicCtors = open.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(publicCtors);
    }

    /// <summary>
    /// <see cref="ImportAction"/> must NOT carry a <c>Rejected</c> member
    /// (OQ-A: the reject is a DU arm, not an enum value — adding the member would
    /// silently weaken exhaustive switches without a <c>default</c>).
    /// </summary>
    [Fact]
    public void ImportAction_enum_has_no_rejected_member()
    {
        var names = Enum.GetNames<ImportAction>();
        Assert.DoesNotContain("Rejected", names);
        Assert.Equal(new[] { "Inserted", "Updated", "Skipped" }, names);
    }

    // ---- Clean-room / read-only (C4 / C-CLEANROOM) ----

    /// <summary>
    /// The <see cref="ISourceReader"/> seam exposes ONLY read operations — no
    /// write / update / delete / upsert / save method against the source
    /// (C-CLEANROOM (a)). Reflection over the interface members.
    /// </summary>
    [Fact]
    public void ISourceReader_exposes_only_read_operations()
    {
        var forbidden = new[] { "write", "update", "delete", "insert", "upsert", "save", "remove", "drop", "truncate" };
        var members = typeof(ISourceReader).GetMembers().Select(m => m.Name.ToLowerInvariant());

        foreach (var verb in forbidden)
        {
            Assert.DoesNotContain(members, m => m.Contains(verb));
        }
    }

    /// <summary>
    /// C-CLEANROOM (d): no type under the owning package's <c>Extraction/</c>
    /// folder opens an HTTP/REST client or a network/live-DB connection to a
    /// Frappe/ERPNext endpoint in v1 (the dump-only collapse). Source-text scan
    /// for the connection-surface markers.
    /// </summary>
    [Fact]
    public void Extraction_adapter_opens_no_network_or_live_db_connection()
    {
        var extractionDir = Path.Combine(PackageDir(), "Extraction");
        Assert.True(Directory.Exists(extractionDir), $"Extraction dir not found at {extractionDir}");

        // Connection-surface markers that would indicate a live REST/DB connection.
        var forbiddenMarkers = new[]
        {
            "HttpClient",
            "WebRequest",
            "Socket",
            "MySqlConnection",
            "MySqlConnector",
            "DbConnection",
            "SqlConnection",
            "NpgsqlConnection",
            "TcpClient",
            ".OpenConnection",
        };

        foreach (var file in Directory.EnumerateFiles(extractionDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var marker in forbiddenMarkers)
            {
                Assert.False(
                    text.Contains(marker, StringComparison.Ordinal),
                    $"Extraction file '{Path.GetFileName(file)}' contains live-connection marker '{marker}' " +
                    "— v1 is dump-only/offline (ADR 0100 C4/C6; C-CLEANROOM (d)).");
            }
        }
    }

    /// <summary>
    /// C-CLEANROOM (b): the extraction adapter carries a license/attribution
    /// header naming ERPNext/Frappe + GPLv3 + format-reference-only.
    /// </summary>
    [Fact]
    public void Extraction_adapter_carries_clean_room_attribution()
    {
        var adapter = Path.Combine(PackageDir(), "Extraction", "MariaDbDumpSourceReader.cs");
        var text = File.ReadAllText(adapter);
        Assert.Contains("CLEAN-ROOM ATTRIBUTION", text);
        Assert.Contains("GPLv3", text);
        Assert.Contains("FORMAT-REFERENCE-ONLY", text);

        var attributionFile = Path.Combine(PackageDir(), "Extraction", "LICENSE-ATTRIBUTION.md");
        Assert.True(File.Exists(attributionFile), "Extraction/LICENSE-ATTRIBUTION.md must be present (C-CLEANROOM (b)).");
    }

    // ---- Helpers ----

    private static string PackageDir()
    {
        // Test assembly runs from .../packages/foundation-import/tests/bin/<cfg>/net11.0/.
        // Walk up to the package dir (the one containing the csproj).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Sunfish.Foundation.Import.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the foundation-import package directory from " + AppContext.BaseDirectory);
    }

    private static string PackagesRoot()
    {
        // foundation-import's parent is the `packages/` root.
        var packageDir = new DirectoryInfo(PackageDir());
        var packages = packageDir.Parent
            ?? throw new DirectoryNotFoundException("packages/ root not found");
        return packages.FullName;
    }

    private static List<string> FindFilesDefining(string typeDefRegex)
    {
        var regex = new Regex(typeDefRegex, RegexOptions.Compiled);
        var matches = new List<string>();

        foreach (var file in Directory.EnumerateFiles(PackagesRoot(), "*.cs", SearchOption.AllDirectories))
        {
            // Skip test sources + build output.
            var norm = file.Replace('\\', '/');
            if (norm.Contains("/tests/") || norm.Contains("/bin/") || norm.Contains("/obj/"))
            {
                continue;
            }

            if (regex.IsMatch(File.ReadAllText(file)))
            {
                matches.Add(file);
            }
        }

        return matches;
    }
}
