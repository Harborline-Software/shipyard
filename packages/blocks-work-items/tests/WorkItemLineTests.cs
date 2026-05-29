using Sunfish.Blocks.WorkItems.Models;
using Xunit;

namespace Sunfish.Blocks.WorkItems.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="WorkItemLine"/>.
/// </summary>
public sealed class WorkItemLineTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly WorkItemId Wo = WorkItemId.NewId();

    [Fact]
    public void Create_EstimatedAmount_ComputedFromQtyAndPrice()
    {
        var line = WorkItemLine.Create(
            workOrderId: Wo,
            lineNumber: 1,
            kind: WorkItemLineKind.Labor,
            description: "Plumber 2 hours",
            createdBy: Actor,
            quantity: 2m,
            unitPrice: 95m,
            unitOfMeasure: "hr",
            currency: "USD");

        Assert.Equal(190m, line.EstimatedAmount);
    }

    [Fact]
    public void Create_ExplicitEstimate_OverridesDerivation()
    {
        var line = WorkItemLine.Create(
            workOrderId: Wo,
            lineNumber: 1,
            kind: WorkItemLineKind.Material,
            description: "Faucet kit",
            createdBy: Actor,
            quantity: 1m,
            unitPrice: 45m,
            estimatedAmount: 60m);

        Assert.Equal(60m, line.EstimatedAmount);
    }

    [Fact]
    public void Create_EmptyDescription_Throws()
    {
        Assert.Throws<ArgumentException>(() => WorkItemLine.Create(
            workOrderId: Wo,
            lineNumber: 1,
            kind: WorkItemLineKind.Labor,
            description: "  ",
            createdBy: Actor));
    }

    [Fact]
    public void SetActual_RecordsActualAmount()
    {
        var line = WorkItemLine.Create(
            Wo, 1, WorkItemLineKind.Material, "Pipe", Actor,
            quantity: 1m, unitPrice: 20m);

        line.SetActual(25m, Actor);

        Assert.Equal(25m, line.ActualAmount);
    }
}
