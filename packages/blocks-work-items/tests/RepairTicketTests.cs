using Sunfish.Blocks.WorkItems.Models;
using Xunit;

namespace Sunfish.Blocks.WorkItems.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="RepairTicket"/>.
/// </summary>
public sealed class RepairTicketTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Create_EmptyTitle_Throws()
    {
        Assert.Throws<ArgumentException>(() => RepairTicket.Create("  ", Actor));
    }

    [Fact]
    public void ConvertTo_LinksToWorkItem()
    {
        var ticket = RepairTicket.Create("Sink clogged", Actor);
        var woId = WorkItemId.NewId();

        ticket.ConvertTo(woId, Actor);

        Assert.Equal(woId, ticket.ConvertedToWorkItemId);
    }

    [Fact]
    public void ConvertTo_AlreadyConverted_Throws()
    {
        var ticket = RepairTicket.Create("Sink clogged", Actor);
        ticket.ConvertTo(WorkItemId.NewId(), Actor);

        Assert.Throws<InvalidOperationException>(
            () => ticket.ConvertTo(WorkItemId.NewId(), Actor));
    }
}
