using System.Text.Json.Serialization;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Discriminator for Ship's Office documents per ADR 0083 §1. Five
/// kinds — <c>DynamicTemplate</c> (Phase 5) joined the enum once
/// ADR 0055 reached <c>Status: Accepted</c>; sourced from the canonical
/// <c>Sunfish.Foundation.Forms.IFormDefinitionStore</c> registry (FN-4
/// relocation on top of the shipyard#218 keystone).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShipsOfficeDocumentKind
{
    /// <summary>A business-case bundle manifest per ADR 0007 (Catalog).</summary>
    BundleManifest,

    /// <summary>A lease document version (W#22 / W#27).</summary>
    LeaseDocument,

    /// <summary>A vendor W9 (W#18); TIN is always redacted in browse view per §Trust impact.</summary>
    VendorW9,

    /// <summary>A signature envelope per ADR 0021 (empty-list stub until Phase 2/Phase 5 wiring).</summary>
    SignatureEnvelope,

    /// <summary>
    /// A dynamic form template per ADR 0055 (W#55 Phase 5). Sourced from
    /// the canonical <c>Sunfish.Foundation.Forms.IFormDefinitionStore</c>
    /// keystone (shipyard#218 + FN-4 sweep).
    /// </summary>
    DynamicTemplate,
}
