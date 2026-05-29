using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Sunfish.Foundation.Import.Outcomes;

namespace Sunfish.Blocks.FinancialTax.Migration;

/// <summary>
/// Default <see cref="IPass2TaxUpserter"/>. Reuses <see cref="ITaxCodeStore"/> +
/// <see cref="ITaxRateLookup"/> + <see cref="ITaxJurisdictionStore"/> +
/// <see cref="IAccountResolver"/>; holds no state of its own and recreates no
/// domain logic. Returns the canonical A0 <see cref="ImportOutcome{T}"/> union.
/// </summary>
/// <remarks>
/// Tax records carry no PII. The cross-system FK is the
/// <c>externalRef:&lt;name&gt;|modified:&lt;modified&gt;</c> marker on
/// <see cref="TaxCode.Notes"/> until a dedicated <c>ExternalRef</c> field lands.
/// A rate row whose <c>account_head</c> does not resolve is dropped (Pass 1
/// should have seeded it); a template with no resolvable rows still imports the
/// <see cref="TaxCode"/> (flagged in the reconcile report).
/// </remarks>
public sealed class Pass2TaxUpserter : IPass2TaxUpserter
{
    private const string TaxDocType = "Sales Taxes and Charges Template";
    private const string ExternalRefPrefix = "externalRef:";
    private const string ModifiedMarker = "|modified:";

    /// <summary>Effective-date floor covering history (importer spec §4.2; user refines post-import).</summary>
    private static readonly DateOnly HistoryEffectiveDate = new(2000, 1, 1);

    private readonly ITaxCodeStore _codes;
    private readonly ITaxRateLookup _rates;
    private readonly ITaxJurisdictionStore _jurisdictions;
    private readonly IAccountResolver _accounts;

    /// <summary>Construct over the tax stores, jurisdiction store, and account resolver.</summary>
    public Pass2TaxUpserter(
        ITaxCodeStore codes,
        ITaxRateLookup rates,
        ITaxJurisdictionStore jurisdictions,
        IAccountResolver accounts)
    {
        _codes = codes ?? throw new ArgumentNullException(nameof(codes));
        _rates = rates ?? throw new ArgumentNullException(nameof(rates));
        _jurisdictions = jurisdictions ?? throw new ArgumentNullException(nameof(jurisdictions));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
    }

    /// <inheritdoc />
    public async Task<ImportOutcome<TaxCode>> UpsertTaxTemplateAsync(
        ErpnextTaxTemplateSource source,
        FL.ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            return Reject("(unnamed)", ImportRejectReason.MissingRequiredField, "name");
        }
        if (string.IsNullOrWhiteSpace(source.TaxName))
        {
            return Reject(source.Name, ImportRejectReason.MissingRequiredField, "tax_name");
        }

        var externalRefMarker = ExternalRefPrefix + source.Name;
        var existing = await FindByExternalRefAsync(targetChart, externalRefMarker, cancellationToken)
            .ConfigureAwait(false);

        // Idempotency: the dedupe marker carries the modified stamp.
        if (existing is not null && existing.Notes is not null
            && existing.Notes.Contains($"{ModifiedMarker}{source.Modified}", StringComparison.Ordinal))
        {
            return new ImportOutcome<TaxCode>.Skipped(existing, "ERPNext modified key unchanged.");
        }

        // Inclusive iff any child row is print-rate-inclusive (ERPNext compound
        // is a separate column we do not yet ingest).
        var application = source.Rates.Any(r => r.IncludedInPrintRate)
            ? TaxApplication.Inclusive
            : TaxApplication.OnSubtotal;
        var kind = application == TaxApplication.Inclusive ? TaxKind.VAT : TaxKind.Sales;
        var notes = $"{ExternalRefPrefix}{source.Name}{ModifiedMarker}{source.Modified}";

        TaxCode upserted;
        bool isInsert;
        if (existing is null)
        {
            upserted = TaxCode.Create(
                chartId: targetChart,
                code: source.TaxName,
                name: source.TaxName,
                kind: kind,
                application: application,
                notes: notes);
            if (source.Disabled) upserted = upserted with { IsActive = false };
            await _codes.UpsertAsync(upserted, cancellationToken).ConfigureAwait(false);
            isInsert = true;
        }
        else
        {
            upserted = existing with
            {
                Code = source.TaxName,
                Name = source.TaxName,
                Kind = kind,
                Application = application,
                IsActive = !source.Disabled,
                Notes = notes,
            };
            await _codes.UpsertAsync(upserted, cancellationToken).ConfigureAwait(false);
            isInsert = false;
        }

        // Synthesize the category jurisdiction (best-effort; user reviews
        // post-import). Categoryless templates get a placeholder. Per
        // importer-spec §4.2 sub-pass 2 each rate row is its OWN TaxRate; a
        // multi-row template represents a jurisdictional breakdown (state +
        // county + ...), so each row beyond the first gets a distinct child
        // jurisdiction. This also keeps the (TaxCode, Jurisdiction)
        // same-effective-date overlap detector from rejecting legitimate
        // multi-component imports.
        var categoryJurisdiction = await SynthesizeJurisdictionAsync(source.TaxCategory, cancellationToken)
            .ConfigureAwait(false);

        var rowIndex = 0;
        foreach (var row in source.Rates)
        {
            if (row.Rate <= 0m) continue; // ERPNext often carries 0-rate informational rows.
            var account = await _accounts.GetAsync(new FL.GLAccountId(row.AccountHead), cancellationToken)
                .ConfigureAwait(false);
            if (account is null) continue; // Pass 1 should have seeded it; drop silently.

            var rowJurisdiction = await ResolveRowJurisdictionAsync(
                categoryJurisdiction, rowIndex, cancellationToken).ConfigureAwait(false);

            var candidate = TaxRate.Create(
                taxCodeId: upserted.Id,
                jurisdictionId: rowJurisdiction.Id,
                ratePercent: row.Rate,
                effectiveDate: HistoryEffectiveDate,
                payableAccountId: account.Id);
            await _rates.UpsertAsync(candidate, cancellationToken).ConfigureAwait(false);
            rowIndex++;
        }

        var detail = $"Imported with {source.Rates.Count} rate row(s).";
        return isInsert
            ? new ImportOutcome<TaxCode>.Inserted(upserted, detail)
            : new ImportOutcome<TaxCode>.Updated(upserted, detail);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<TaxCode?> FindByExternalRefAsync(
        FL.ChartOfAccountsId chartId,
        string externalRefMarker,
        CancellationToken cancellationToken)
    {
        var all = await _codes.GetByChartAsync(chartId, cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(c => c.Notes is not null
            && c.Notes.StartsWith(externalRefMarker, StringComparison.Ordinal));
    }

    /// <summary>
    /// Reuse an existing jurisdiction synthesized for the same category, or mint
    /// a fresh one. A null/blank category mints a per-template placeholder.
    /// </summary>
    private async Task<TaxJurisdiction> SynthesizeJurisdictionAsync(
        string? taxCategory,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(taxCategory))
        {
            var existing = await _jurisdictions.GetByLevelAsync(JurisdictionLevel.State, cancellationToken)
                .ConfigureAwait(false);
            var match = existing.FirstOrDefault(j =>
                string.Equals(j.Name, taxCategory!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;

            var synthesized = TaxJurisdiction.Create(
                level: JurisdictionLevel.State,
                isoCountry: "US",
                name: taxCategory!.Trim(),
                notes: "Synthesized from ERPNext Tax Category (review post-import).");
            await _jurisdictions.UpsertAsync(synthesized, cancellationToken).ConfigureAwait(false);
            return synthesized;
        }

        var placeholder = TaxJurisdiction.Create(
            level: JurisdictionLevel.State,
            isoCountry: "US",
            name: "(uncategorized ERPNext tax)",
            notes: "Placeholder jurisdiction for a categoryless ERPNext template (review post-import).");
        await _jurisdictions.UpsertAsync(placeholder, cancellationToken).ConfigureAwait(false);
        return placeholder;
    }

    /// <summary>
    /// Row 0 uses the category jurisdiction directly (the common single-row
    /// case). Each subsequent row gets a distinct child jurisdiction (a
    /// jurisdictional breakdown component) parented under the category — so the
    /// per-(TaxCode, Jurisdiction) same-effective-date overlap detector does not
    /// reject a legitimate multi-component template.
    /// </summary>
    private async Task<TaxJurisdiction> ResolveRowJurisdictionAsync(
        TaxJurisdiction categoryJurisdiction,
        int rowIndex,
        CancellationToken cancellationToken)
    {
        if (rowIndex == 0) return categoryJurisdiction;

        var component = TaxJurisdiction.Create(
            level: JurisdictionLevel.County,
            isoCountry: categoryJurisdiction.IsoCountry,
            name: $"{categoryJurisdiction.Name} (component {rowIndex + 1})",
            parentJurisdictionId: categoryJurisdiction.Id,
            notes: "Synthesized per-row breakdown component from an ERPNext multi-row tax template (review post-import).");
        await _jurisdictions.UpsertAsync(component, cancellationToken).ConfigureAwait(false);
        return component;
    }

    private static ImportOutcome<TaxCode> Reject(
        string externalRef,
        ImportRejectReason reason,
        string? fieldName = null,
        string? ruleViolated = null)
        => new ImportOutcome<TaxCode>.Rejected(
            ImportFailure.Of(externalRef, TaxDocType, reason, fieldName, ruleViolated));
}
