using System.IO;
using System.Text.RegularExpressions;

namespace Sunfish.Blocks.Migration.Erpnext.Tests;

/// <summary>
/// Architecture / conformance tests for the A0 ERPNext extraction adapter
/// (ADR 0100 C4 / C-CLEANROOM / C-MODE). Mirrors the shape of
/// <c>ImportPrimitivesArchitectureTests</c> in foundation-import.
/// </summary>
public sealed class MigrationErpnextArchitectureTests
{
    // ---- C-CLEANROOM (a): no write methods on IErpnextSourceExtractor ----

    [Fact]
    public void IErpnextSourceExtractor_has_no_write_update_delete_method()
    {
        var forbidden = new[]
        {
            "write", "update", "delete", "insert", "upsert", "save",
            "remove", "drop", "truncate",
        };
        var memberNames = typeof(IErpnextSourceExtractor)
            .GetMembers()
            .Select(m => m.Name.ToLowerInvariant())
            .ToList();

        foreach (var verb in forbidden)
        {
            var offenders = memberNames.Where(m => m.Contains(verb)).ToList();
            Assert.True(offenders.Count == 0,
                $"IErpnextSourceExtractor must not expose '{verb}' — read-only clean-room " +
                $"(ADR 0100 C4). Offending members: {string.Join(", ", offenders)}");
        }
    }

    // ---- C-CLEANROOM (d): no Extraction/ type references a live DB or HTTP client ----

    [Fact]
    public void Extraction_types_open_no_network_or_live_db_connection()
    {
        var extractionDir = Path.Combine(PackageDir(), "Extraction");
        Assert.True(Directory.Exists(extractionDir),
            $"Extraction dir not found at {extractionDir}");

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

        foreach (var file in Directory.EnumerateFiles(extractionDir, "*.cs",
            SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var marker in forbiddenMarkers)
            {
                Assert.False(
                    text.Contains(marker, StringComparison.Ordinal),
                    $"Extraction file '{Path.GetFileName(file)}' references live-connection " +
                    $"marker '{marker}' — v1 is dump-only/offline (ADR 0100 C4/C6; C-CLEANROOM (d)).");
            }
        }
    }

    // ---- C-CLEANROOM (b): extraction adapter carries the attribution header ----

    [Fact]
    public void MariaDbDumpExtractor_carries_clean_room_attribution_header()
    {
        var adapterPath = Path.Combine(PackageDir(), "Extraction", "MariaDbDumpExtractor.cs");
        Assert.True(File.Exists(adapterPath), $"MariaDbDumpExtractor.cs not found at {adapterPath}");

        var text = File.ReadAllText(adapterPath);
        Assert.Contains("CLEAN-ROOM ATTRIBUTION", text);
        Assert.Contains("GPLv3", text);
        Assert.Contains("FORMAT-REFERENCE-ONLY", text);
    }

    // ---- C-CLEANROOM (b): LICENSES/ERPNEXT-FRAPPE-NOTICE.md is present ----

    [Fact]
    public void Package_contains_erpnext_frappe_attribution_file()
    {
        // Accepts either LICENSES/ERPNEXT-FRAPPE-NOTICE.md or
        // Extraction/LICENSE-ATTRIBUTION.md (existing on branch).
        var licenseAtExtraction = Path.Combine(
            PackageDir(), "Extraction", "LICENSE-ATTRIBUTION.md");
        var licenseAtRoot = Path.Combine(
            PackageDir(), "LICENSES", "ERPNEXT-FRAPPE-NOTICE.md");

        var exists = File.Exists(licenseAtExtraction) || File.Exists(licenseAtRoot);
        Assert.True(exists,
            "The package must include an ERPNext/Frappe attribution file at " +
            "Extraction/LICENSE-ATTRIBUTION.md or LICENSES/ERPNEXT-FRAPPE-NOTICE.md " +
            "(ADR 0100 C4 / C-CLEANROOM (b)).");
    }

    // ---- DAG geometry: blocks-migration-erpnext must NOT be referenced by
    //      any blocks-* cluster (no cluster should depend on this host pkg) ----

    [Fact]
    public void No_cluster_package_references_blocks_migration_erpnext()
    {
        // Walk packages/blocks-*/ csproj files; assert none has a ProjectReference
        // or PackageReference to Sunfish.Blocks.Migration.Erpnext.
        var packagesRoot = PackagesRoot();
        var blocksPackages = Directory.GetDirectories(packagesRoot, "blocks-*");

        foreach (var dir in blocksPackages)
        {
            var pkgName = Path.GetFileName(dir)!;
            if (string.Equals(pkgName, "blocks-migration-erpnext",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue; // skip self
            }

            foreach (var csproj in Directory.GetFiles(dir, "*.csproj", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(csproj);
                Assert.False(
                    content.Contains("Sunfish.Blocks.Migration.Erpnext",
                        StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("blocks-migration-erpnext",
                        StringComparison.OrdinalIgnoreCase),
                    $"Cluster package '{Path.GetFileName(csproj)}' references blocks-migration-erpnext " +
                    "— the host package sits ABOVE the clusters in the DAG; no cluster may depend on it.");
            }
        }
    }

    // ---- helpers ----

    private static string PackageDir()
    {
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
            "Could not locate blocks-migration-erpnext package directory from " +
            AppContext.BaseDirectory);
    }

    private static string PackagesRoot()
    {
        var packageDir = new DirectoryInfo(PackageDir());
        var packages = packageDir.Parent
            ?? throw new DirectoryNotFoundException("packages/ root not found");
        return packages.FullName;
    }
}
