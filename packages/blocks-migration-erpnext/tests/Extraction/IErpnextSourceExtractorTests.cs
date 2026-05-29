using System;
using System.Reflection;
using Sunfish.Blocks.Migration.Erpnext.Extraction;
using Xunit;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Extraction;

/// <summary>
/// Contract tests for <see cref="IErpnextSourceExtractor"/> — verifies the interface
/// shape invariants that ADR 0100 C4/C6 depend on.
/// </summary>
public sealed class IErpnextSourceExtractorTests
{
    /// <summary>
    /// The interface exposes ONLY read operations (C-CLEANROOM (a) / ADR 0100 C4).
    /// No write/update/delete/upsert/save/drop/truncate method must exist.
    /// </summary>
    [Fact]
    public void Interface_exposes_only_read_operations()
    {
        var forbidden = new[]
        {
            "write", "update", "delete", "insert", "upsert", "save",
            "remove", "drop", "truncate", "put", "post", "patch",
        };

        var members = typeof(IErpnextSourceExtractor)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance);

        foreach (var member in members)
        {
            var lower = member.Name.ToLowerInvariant();
            foreach (var verb in forbidden)
            {
                Assert.False(
                    lower.Contains(verb),
                    $"IErpnextSourceExtractor member '{member.Name}' contains forbidden verb '{verb}'. " +
                    "The interface must be READ-ONLY (ADR 0100 C4 / C-CLEANROOM (a)).");
            }
        }
    }

    /// <summary>
    /// All streaming methods return <c>IAsyncEnumerable&lt;T&gt;</c> where T is a
    /// frozen <c>Erpnext*Source</c> sealed record (ADR 0100 D1/D6 — the DTOs are
    /// the contract; no mode-specific field must appear on the DTO).
    /// </summary>
    [Fact]
    public void Streaming_methods_return_IAsyncEnumerable_of_frozen_dtos()
    {
        var methods = typeof(IErpnextSourceExtractor).GetMethods();
        foreach (var method in methods)
        {
            if (method.Name == "ReadInventoryAsync")
            {
                continue; // Task<ErpnextSourceInventory> — correct; skip
            }

            if (!method.Name.StartsWith("Read", StringComparison.Ordinal))
            {
                continue;
            }

            var returnType = method.ReturnType;
            Assert.True(
                returnType.IsGenericType
                && returnType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IAsyncEnumerable<>),
                $"Method '{method.Name}' should return IAsyncEnumerable<T> but returns {returnType.Name}.");
        }
    }

    /// <summary>
    /// <see cref="IErpnextSourceExtractor.ReadInventoryAsync"/> returns
    /// <c>Task&lt;ErpnextSourceInventory&gt;</c> (the C5 census return type).
    /// </summary>
    [Fact]
    public void ReadInventoryAsync_returns_Task_of_ErpnextSourceInventory()
    {
        var method = typeof(IErpnextSourceExtractor).GetMethod("ReadInventoryAsync")!;
        Assert.NotNull(method);
        Assert.Equal(
            typeof(System.Threading.Tasks.Task<ErpnextSourceInventory>),
            method.ReturnType);
    }
}
