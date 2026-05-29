using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Reviews.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Reviews.Services;

/// <summary>
/// In-memory implementation of <see cref="IReviewsService"/> backed by
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> stores.
/// </summary>
/// <remarks>
/// Per-inspection state mutations (<see cref="StartAsync"/>, <see cref="RecordResponseAsync"/>,
/// <see cref="CompleteAsync"/>) are serialized via a per-inspection <see cref="SemaphoreSlim"/>
/// so concurrent calls on the same inspection cannot interleave.
/// <para>
/// Suitable for demos, integration tests, and kitchen-sink scenarios.
/// Not intended for production use — no persistence, no event bus.
/// </para>
/// </remarks>
public sealed class InMemoryReviewsService : IReviewsService
{
    private readonly ConcurrentDictionary<ReviewTemplateId, ReviewTemplate> _templates = new();
    private readonly ConcurrentDictionary<ReviewId, Review> _inspections = new();
    private readonly ConcurrentDictionary<DeficiencyId, Deficiency> _deficiencies = new();
    private readonly ConcurrentDictionary<EquipmentConditionAssessmentId, EquipmentConditionAssessment> _conditionAssessments = new();

    // Per-inspection locks for state-mutating operations.
    private readonly ConcurrentDictionary<ReviewId, SemaphoreSlim> _inspectionLocks = new();

    // ── Templates ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public ValueTask<ReviewTemplate> CreateTemplateAsync(CreateTemplateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var template = new ReviewTemplate(
            Id: ReviewTemplateId.NewId(),
            Name: request.Name,
            Description: request.Description,
            Items: request.Items,
            CreatedAtUtc: Instant.Now);

        _templates[template.Id] = template;
        return ValueTask.FromResult(template);
    }

    /// <inheritdoc />
    public ValueTask<ReviewTemplate?> GetTemplateAsync(ReviewTemplateId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _templates.TryGetValue(id, out var template);
        return ValueTask.FromResult(template);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ReviewTemplate> ListTemplatesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var template in _templates.Values)
        {
            ct.ThrowIfCancellationRequested();
            yield return template;
            await Task.Yield();
        }
    }

    // ── Reviews ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public ValueTask<Review> ScheduleAsync(ScheduleReviewRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var inspection = new Review(
            Id: ReviewId.NewId(),
            TemplateId: request.TemplateId,
            UnitId: request.UnitId,
            InspectorName: request.InspectorName,
            ScheduledDate: request.ScheduledDate,
            Phase: ReviewPhase.Scheduled,
            StartedAtUtc: null,
            CompletedAtUtc: null,
            Responses: [],
            Trigger: request.Trigger);

        _inspections[inspection.Id] = inspection;
        return ValueTask.FromResult(inspection);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the inspection is not in <see cref="ReviewPhase.Scheduled"/>.
    /// </exception>
    public async ValueTask<Review> StartAsync(ReviewId id, CancellationToken ct = default)
    {
        var sem = _inspectionLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_inspections.TryGetValue(id, out var inspection))
                throw new InvalidOperationException($"Review '{id}' not found.");

            if (inspection.Phase != ReviewPhase.Scheduled)
                throw new InvalidOperationException(
                    $"Cannot start inspection '{id}': current phase is {inspection.Phase}, expected {ReviewPhase.Scheduled}.");

            var updated = inspection with
            {
                Phase = ReviewPhase.InProgress,
                StartedAtUtc = Instant.Now,
            };

            _inspections[id] = updated;
            return updated;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the inspection is not in <see cref="ReviewPhase.InProgress"/>.
    /// </exception>
    public async ValueTask<Review> RecordResponseAsync(
        ReviewId id,
        ReviewResponse response,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        var sem = _inspectionLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_inspections.TryGetValue(id, out var inspection))
                throw new InvalidOperationException($"Review '{id}' not found.");

            if (inspection.Phase != ReviewPhase.InProgress)
                throw new InvalidOperationException(
                    $"Cannot record a response for inspection '{id}': current phase is {inspection.Phase}, expected {ReviewPhase.InProgress}.");

            var updated = inspection with
            {
                Responses = [.. inspection.Responses, response],
            };

            _inspections[id] = updated;
            return updated;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the inspection is not in <see cref="ReviewPhase.InProgress"/>.
    /// </exception>
    public async ValueTask<Review> CompleteAsync(ReviewId id, CancellationToken ct = default)
    {
        var sem = _inspectionLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_inspections.TryGetValue(id, out var inspection))
                throw new InvalidOperationException($"Review '{id}' not found.");

            if (inspection.Phase != ReviewPhase.InProgress)
                throw new InvalidOperationException(
                    $"Cannot complete inspection '{id}': current phase is {inspection.Phase}, expected {ReviewPhase.InProgress}.");

            var updated = inspection with
            {
                Phase = ReviewPhase.Completed,
                CompletedAtUtc = Instant.Now,
            };

            _inspections[id] = updated;
            return updated;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask<Review?> GetReviewAsync(ReviewId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _inspections.TryGetValue(id, out var inspection);
        return ValueTask.FromResult(inspection);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Review> ListReviewsAsync(
        ListReviewsQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var inspection in _inspections.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (query.UnitId.HasValue && inspection.UnitId != query.UnitId.Value)
                continue;

            if (query.Phase.HasValue && inspection.Phase != query.Phase.Value)
                continue;

            yield return inspection;
            await Task.Yield();
        }
    }

    // ── Deficiencies ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public ValueTask<Deficiency> RecordDeficiencyAsync(RecordDeficiencyRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var deficiency = new Deficiency(
            Id: DeficiencyId.NewId(),
            ReviewId: request.ReviewId,
            ItemId: request.ItemId,
            Severity: request.Severity,
            Description: request.Description,
            ObservedAtUtc: Instant.Now,
            Status: DeficiencyStatus.Open);

        _deficiencies[deficiency.Id] = deficiency;
        return ValueTask.FromResult(deficiency);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Deficiency> ListDeficienciesAsync(
        ReviewId inspectionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var deficiency in _deficiencies.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (deficiency.ReviewId != inspectionId)
                continue;

            yield return deficiency;
            await Task.Yield();
        }
    }

    // ── Equipment condition assessments (workstream #25 EXTEND) ─────────────

    /// <inheritdoc />
    public async ValueTask<EquipmentConditionAssessment> RecordEquipmentConditionAsync(
        RecordEquipmentConditionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sem = _inspectionLocks.GetOrAdd(request.ReviewId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_inspections.TryGetValue(request.ReviewId, out var inspection))
                throw new InvalidOperationException($"Review '{request.ReviewId}' not found.");

            if (inspection.Phase != ReviewPhase.InProgress)
                throw new InvalidOperationException(
                    $"Cannot record an equipment condition for inspection '{request.ReviewId}': current phase is {inspection.Phase}, expected {ReviewPhase.InProgress}.");

            var assessment = new EquipmentConditionAssessment
            {
                Id = EquipmentConditionAssessmentId.NewId(),
                ReviewId = request.ReviewId,
                EquipmentId = request.EquipmentId,
                Condition = request.Condition,
                ExpectedRemainingLifeYears = request.ExpectedRemainingLifeYears,
                Observations = request.Observations,
                Recommendations = request.Recommendations,
                ObservedAtUtc = Instant.Now,
            };

            _conditionAssessments[assessment.Id] = assessment;
            return assessment;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EquipmentConditionAssessment> ListEquipmentConditionsAsync(
        ReviewId inspectionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var assessment in _conditionAssessments.Values)
        {
            ct.ThrowIfCancellationRequested();
            if (assessment.ReviewId != inspectionId)
                continue;
            yield return assessment;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EquipmentConditionAssessment> ListConditionHistoryForEquipmentAsync(
        EquipmentId equipmentId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Return chronological (oldest first) so consumers can walk the trend.
        var ordered = _conditionAssessments.Values
            .Where(a => a.EquipmentId.Equals(equipmentId))
            .OrderBy(a => a.ObservedAtUtc.Value)
            .ToList();

        foreach (var assessment in ordered)
        {
            ct.ThrowIfCancellationRequested();
            yield return assessment;
            await Task.Yield();
        }
    }

    // ── Move-in / move-out delta projection (workstream #25 EXTEND) ──────────

    /// <inheritdoc />
    public ValueTask<MoveInOutDelta?> GetMoveInOutDeltaAsync(EntityId unitId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        Review? mostRecentMoveIn = null;
        Review? mostRecentMoveOut = null;

        foreach (var inspection in _inspections.Values)
        {
            if (inspection.UnitId != unitId)
                continue;

            if (inspection.Trigger == ReviewTrigger.MoveIn &&
                (mostRecentMoveIn is null || inspection.ScheduledDate > mostRecentMoveIn.ScheduledDate))
            {
                mostRecentMoveIn = inspection;
            }
            else if (inspection.Trigger == ReviewTrigger.MoveOut &&
                     (mostRecentMoveOut is null || inspection.ScheduledDate > mostRecentMoveOut.ScheduledDate))
            {
                mostRecentMoveOut = inspection;
            }
        }

        if (mostRecentMoveIn is null || mostRecentMoveOut is null)
            return ValueTask.FromResult<MoveInOutDelta?>(null);

        var responseDeltas = ComputeResponseDeltas(mostRecentMoveIn, mostRecentMoveOut);
        var conditionDeltas = ComputeConditionDeltas(mostRecentMoveIn.Id, mostRecentMoveOut.Id);

        return ValueTask.FromResult<MoveInOutDelta?>(new MoveInOutDelta(
            UnitId: unitId,
            MoveIn: mostRecentMoveIn,
            MoveOut: mostRecentMoveOut,
            ResponseDeltas: responseDeltas,
            EquipmentConditionDeltas: conditionDeltas));
    }

    private static IReadOnlyList<ResponseDelta> ComputeResponseDeltas(Review moveIn, Review moveOut)
    {
        var moveInByItem = moveIn.Responses
            .GroupBy(r => r.ItemId)
            .ToDictionary(g => g.Key, g => g.Last().ResponseValue);
        var moveOutByItem = moveOut.Responses
            .GroupBy(r => r.ItemId)
            .ToDictionary(g => g.Key, g => g.Last().ResponseValue);

        var allItemIds = new HashSet<ReviewChecklistItemId>(moveInByItem.Keys);
        allItemIds.UnionWith(moveOutByItem.Keys);

        var deltas = new List<ResponseDelta>();
        foreach (var itemId in allItemIds)
        {
            var inValue = moveInByItem.TryGetValue(itemId, out var iv) ? iv : string.Empty;
            var outValue = moveOutByItem.TryGetValue(itemId, out var ov) ? ov : string.Empty;
            deltas.Add(new ResponseDelta(itemId, inValue, outValue, !string.Equals(inValue, outValue, StringComparison.Ordinal)));
        }
        return deltas;
    }

    private IReadOnlyList<EquipmentConditionDelta> ComputeConditionDeltas(ReviewId moveInId, ReviewId moveOutId)
    {
        var moveInByEquipment = _conditionAssessments.Values
            .Where(a => a.ReviewId == moveInId)
            .GroupBy(a => a.EquipmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ObservedAtUtc.Value).First().Condition);
        var moveOutByEquipment = _conditionAssessments.Values
            .Where(a => a.ReviewId == moveOutId)
            .GroupBy(a => a.EquipmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ObservedAtUtc.Value).First().Condition);

        // Only emit deltas for equipment present in BOTH inspections; partials aren't meaningful.
        var deltas = new List<EquipmentConditionDelta>();
        foreach (var (equipmentId, inCondition) in moveInByEquipment)
        {
            if (!moveOutByEquipment.TryGetValue(equipmentId, out var outCondition))
                continue;
            deltas.Add(new EquipmentConditionDelta(
                EquipmentId: equipmentId,
                MoveInCondition: inCondition,
                MoveOutCondition: outCondition,
                Degraded: outCondition > inCondition));  // enum order Good < Fair < Poor < Failed
        }
        return deltas;
    }

    // ── Reports ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown if the inspection does not exist.</exception>
    public async ValueTask<ReviewReport> GenerateReportAsync(ReviewId inspectionId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_inspections.TryGetValue(inspectionId, out var inspection))
            throw new InvalidOperationException($"Review '{inspectionId}' not found.");

        // Count deficiencies linked to this inspection.
        var deficiencyCount = 0;
        await foreach (var _ in ListDeficienciesAsync(inspectionId, ct).ConfigureAwait(false))
            deficiencyCount++;

        // Resolve checklist items from the template to get TotalItems.
        // TemplateId is nullable for ad-hoc inspections — skip template lookup when null.
        var totalItems = 0;
        ReviewTemplate? template = null;
        if (inspection.TemplateId.HasValue && _templates.TryGetValue(inspection.TemplateId.Value, out var found))
        {
            template = found;
            totalItems = template.Items.Count;
        }

        // Compute PassedItems: apply a per-kind pass heuristic.
        var passedItems = 0;
        if (template is not null)
        {
            var responsesByItemId = inspection.Responses
                .GroupBy(r => r.ItemId)
                .ToDictionary(g => g.Key, g => g.Last()); // last response wins if duplicated

            foreach (var item in template.Items)
            {
                if (!responsesByItemId.TryGetValue(item.Id, out var response))
                    continue;

                var passed = item.Kind switch
                {
                    ReviewItemKind.YesNo => string.Equals(response.ResponseValue, "yes", StringComparison.OrdinalIgnoreCase),
                    ReviewItemKind.PassFail => string.Equals(response.ResponseValue, "pass", StringComparison.OrdinalIgnoreCase),
                    ReviewItemKind.Rating1to5 => int.TryParse(response.ResponseValue, out var rating) && rating >= 3,
                    ReviewItemKind.FreeText => !string.IsNullOrWhiteSpace(response.ResponseValue),
                    ReviewItemKind.Photo => !string.IsNullOrWhiteSpace(response.ResponseValue),
                    _ => false,
                };

                if (passed)
                    passedItems++;
            }
        }

        var summary = $"Review {inspectionId} — {passedItems}/{totalItems} items passed, {deficiencyCount} deficiencies recorded.";

        var report = new ReviewReport(
            Id: ReviewReportId.NewId(),
            ReviewId: inspectionId,
            GeneratedAtUtc: Instant.Now,
            Summary: summary,
            TotalItems: totalItems,
            PassedItems: passedItems,
            DeficiencyCount: deficiencyCount);

        return report;
    }
}
