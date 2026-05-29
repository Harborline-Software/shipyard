// Sunfish.Blocks.Inspections — Type Forwards (deprecated re-export shim)
//
// This package was RENAMED to Sunfish.Blocks.Reviews per ADR 0098 (block-naming
// generalization for cross-vertical substrate reuse — a review is not inherently a
// property-domain inspection). Every public type now lives in Sunfish.Blocks.Reviews and is
// re-published through this assembly under its ORIGINAL fully-qualified location via
// [assembly: TypeForwardedTo]. Consumers referencing Sunfish.Blocks.Inspections continue to
// resolve the same types.
//
// ONE TypeForwards.cs per shim (ADR 0098 A5b; mirrors packages/kernel/TypeForwards.cs). Only
// PUBLIC types are forwardable (A5a). The Blazor component ReviewListBlock IS forwarded (it
// compiles to a public ComponentBase-derived type at the package root namespace); the shim
// carries a Microsoft.AspNetCore.App FrameworkReference so the forwarded component type resolves.
//
// Per-vertical-coupling types Deficiency + EquipmentConditionAssessment are PRESERVED (not
// renamed) per ADR 0098 Halt 6 Option alpha — they keep their property-domain coupling and are
// flagged for future generalization when a 2nd-vertical consumer surfaces.
//
// Extension methods do NOT forward (A6); the AddInMemoryInspections() helper is preserved as an
// [Obsolete] delegating stub in DependencyInjection/. Deprecation enforcement is the Step 7
// SUNFISH_BLOCKDEP001 analyzer.

using System.Reflection;
using System.Runtime.CompilerServices;

// -----------------------------------------------------------------------------
// Models (Deficiency + EquipmentConditionAssessment preserved unrenamed per Halt 6 alpha)
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ConditionRating))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.Deficiency))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.DeficiencyId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.DeficiencySeverity))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.DeficiencyStatus))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.EquipmentConditionAssessment))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.EquipmentConditionAssessmentId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.Review))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewChecklistItem))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewChecklistItemId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewItemKind))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewPhase))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewReport))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewReportId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewResponse))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewTemplate))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewTemplateId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Models.ReviewTrigger))]

// -----------------------------------------------------------------------------
// Services
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Services.CreateTemplateRequest))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Services.EquipmentConditionDelta))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Services.InMemoryReviewsService))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Services.IReviewsService))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Services.ListReviewsQuery))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Services.MoveInOutDelta))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Services.RecordDeficiencyRequest))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Services.RecordEquipmentConditionRequest))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Services.ResponseDelta))]
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Services.ScheduleReviewRequest))]

// -----------------------------------------------------------------------------
// Localization
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.Localization.SharedResource))]

// -----------------------------------------------------------------------------
// Blazor component (root namespace; forwards the generated ComponentBase-derived type)
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Blocks.Reviews.ReviewListBlock))]

// -----------------------------------------------------------------------------
// Assembly-level deprecation note (see Step 3 PR: [assembly: Obsolete] is CS0592-invalid).
// -----------------------------------------------------------------------------
[assembly: AssemblyMetadata(
    "Obsolete",
    "Sunfish.Blocks.Inspections is renamed to Sunfish.Blocks.Reviews per ADR 0098; this package "
    + "is a re-export shim and will be removed in the major version following the "
    + "cross-vertical-reuse rename wave's deprecation cycle.")]
