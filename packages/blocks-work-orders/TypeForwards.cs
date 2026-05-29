// Sunfish.Blocks.WorkOrders — Type Forwards (deprecated re-export shim)
//
// This package was RENAMED to Sunfish.Blocks.WorkItems per ADR 0098 (block-naming
// generalization for cross-vertical substrate reuse). To preserve binary compatibility for
// consumers compiled against the old assembly name, every public type now lives in
// Sunfish.Blocks.WorkItems and is re-published through this assembly under its ORIGINAL
// fully-qualified location via [assembly: TypeForwardedTo]. Consumers that reference
// Sunfish.Blocks.WorkOrders continue to resolve the exact same types.
//
// ONE TypeForwards.cs file per shim package holds all assembly-level forwards (ADR 0098 A5b;
// mirrors the in-repo packages/kernel/TypeForwards.cs precedent). Only PUBLIC types are
// forwardable (ADR 0098 A5a); the internal *IdJsonConverter types are not forwarded (they are
// applied via [JsonConverter] attribute on the forwarded public record structs).
//
// Extension methods do NOT forward through [TypeForwardedTo] (they compile to fully-qualified
// references in the consumer IL); the AddBlocksWorkOrders() helper is preserved separately as an
// [Obsolete] delegating stub in DependencyInjection/WorkOrdersServiceCollectionExtensions.cs
// (ADR 0098 A6).
//
// This shim is archived 90 days after the later of MVP launch / Step 7 analyzer landing per
// ADR 0098 §"Per-rename migration pattern" S3b.

using System.Reflection;
using System.Runtime.CompilerServices;

// -----------------------------------------------------------------------------
// Events
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Events.DeficiencyRaisedEvent))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Events.IDeficiencyRaisedHandler))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Events.InMemoryDeficiencyRaisedHandler))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Events.InMemoryWorkItemEventPublisher))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Events.IWorkItemEventPublisher))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Events.WorkItemAssignedEvent))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Events.WorkItemCompletedEvent))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Events.WorkItemCreatedEvent))]

// -----------------------------------------------------------------------------
// Models
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.ChecklistItem))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.Contractor))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.ContractorId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.ContractorStatus))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.InvalidStatusTransitionException))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.MaintenanceSchedule))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.MaintenanceScheduleId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.MaintenanceTask))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.MaintenanceTaskId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.MaintenanceTaskStatus))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.MaintenanceTaskTemplate))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.Priority))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.RepairTicket))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.RepairTicketId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.ScheduleStatus))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.TradeCategory))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.WorkItem))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.WorkItemId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.WorkItemKind))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.WorkItemLine))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.WorkItemLineDraft))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.WorkItemLineId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.WorkItemLineKind))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.WorkItemSeverity))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.WorkItemStatus))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Models.WorkItemStatusMachine))]

// -----------------------------------------------------------------------------
// Services
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.IContractorReadModel))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.IMaintenanceScheduleService))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.InMemoryContractorRepository))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.InMemoryMaintenanceScheduleService))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.InMemoryPartyReadModel))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.InMemoryRruleExpansionService))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.InMemoryWorkItemRepository))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.InMemoryWorkItemService))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.IPartyReadModel))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.IRruleExpansionService))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.WorkItems.Services.IWorkItemService))]

// -----------------------------------------------------------------------------
// Assembly-level deprecation note.
//
// NOTE: the ADR 0098 cookbook snippet shows `[assembly: Obsolete(...)]`, but ObsoleteAttribute
// has no AttributeUsage for the assembly target (CS0592). The deprecation signal is instead
// carried by: (1) the shim package's <Description> + <PackageTags>deprecated</PackageTags>;
// (2) the [Obsolete] + [EditorBrowsable(Never)] extension stub in DependencyInjection/; and,
// load-bearingly, (3) the Step 7 SUNFISH_BLOCKDEP001 Roslyn analyzer that warns on `using
// Sunfish.Blocks.WorkOrders;` — the ADR's actual enforcement mechanism per Halt 7. We additionally
// stamp an AssemblyMetadata note so the deprecation rationale travels with the compiled assembly.
// -----------------------------------------------------------------------------
[assembly: AssemblyMetadata(
    "Obsolete",
    "Sunfish.Blocks.WorkOrders is renamed to Sunfish.Blocks.WorkItems per ADR 0098; this package "
    + "is a re-export shim and will be removed in the major version following the "
    + "cross-vertical-reuse rename wave's deprecation cycle.")]
