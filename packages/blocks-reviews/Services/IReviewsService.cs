using Sunfish.Blocks.Reviews.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Reviews.Services;

/// <summary>
/// Contract for managing inspection templates, scheduled inspections, deficiency records,
/// and inspection reports.
/// </summary>
/// <remarks>
/// Deferred in this pass (G16 first pass):
/// <list type="bullet">
///   <item><description>Work-order rollup from deficiencies (blocks-maintenance, G16 second pass)</description></item>
///   <item><description>Offline mobile capture and photo/voice attachments</description></item>
///   <item><description>Event-bus integration and reactive triggers</description></item>
///   <item><description>BusinessRuleEngine hookup</description></item>
/// </list>
/// </remarks>
public interface IReviewsService
{
    // ── Templates ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new inspection template from <paramref name="request"/> and returns the persisted record.
    /// </summary>
    /// <param name="request">Template creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="ReviewTemplate"/>.</returns>
    ValueTask<ReviewTemplate> CreateTemplateAsync(CreateTemplateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns the template with the specified <paramref name="id"/>, or <see langword="null"/>
    /// if no such template exists.
    /// </summary>
    /// <param name="id">The template identifier to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<ReviewTemplate?> GetTemplateAsync(ReviewTemplateId id, CancellationToken ct = default);

    /// <summary>
    /// Streams all known templates.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<ReviewTemplate> ListTemplatesAsync(CancellationToken ct = default);

    // ── Reviews ───────────────────────────────────────────────────────────

    /// <summary>
    /// Schedules a new inspection from <paramref name="request"/> and returns the created record.
    /// The new inspection is always in <see cref="ReviewPhase.Scheduled"/>.
    /// </summary>
    /// <param name="request">Review scheduling payload.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Review> ScheduleAsync(ScheduleReviewRequest request, CancellationToken ct = default);

    /// <summary>
    /// Transitions the inspection from <see cref="ReviewPhase.Scheduled"/> to
    /// <see cref="ReviewPhase.InProgress"/> and records <c>StartedAtUtc</c>.
    /// </summary>
    /// <param name="id">The inspection to start.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the inspection is not in <see cref="ReviewPhase.Scheduled"/>.</exception>
    ValueTask<Review> StartAsync(ReviewId id, CancellationToken ct = default);

    /// <summary>
    /// Appends <paramref name="response"/> to the inspection's response list.
    /// The inspection must be in <see cref="ReviewPhase.InProgress"/>.
    /// </summary>
    /// <param name="id">The inspection to record a response for.</param>
    /// <param name="response">The response to append.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the inspection is not in <see cref="ReviewPhase.InProgress"/>.</exception>
    ValueTask<Review> RecordResponseAsync(ReviewId id, ReviewResponse response, CancellationToken ct = default);

    /// <summary>
    /// Transitions the inspection from <see cref="ReviewPhase.InProgress"/> to
    /// <see cref="ReviewPhase.Completed"/> and records <c>CompletedAtUtc</c>.
    /// </summary>
    /// <param name="id">The inspection to complete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the inspection is not in <see cref="ReviewPhase.InProgress"/>.</exception>
    ValueTask<Review> CompleteAsync(ReviewId id, CancellationToken ct = default);

    /// <summary>
    /// Returns the inspection with the specified <paramref name="id"/>, or <see langword="null"/>
    /// if no such inspection exists.
    /// </summary>
    /// <param name="id">The inspection identifier to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Review?> GetReviewAsync(ReviewId id, CancellationToken ct = default);

    /// <summary>
    /// Streams all inspections matching <paramref name="query"/>.
    /// Pass <see cref="ListReviewsQuery.Empty"/> to return all inspections.
    /// </summary>
    /// <param name="query">Optional filter criteria.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<Review> ListReviewsAsync(ListReviewsQuery query, CancellationToken ct = default);

    // ── Deficiencies ─────────────────────────────────────────────────────────

    /// <summary>
    /// Records a new deficiency linked to an inspection and returns the created record.
    /// </summary>
    /// <param name="request">Deficiency creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Deficiency> RecordDeficiencyAsync(RecordDeficiencyRequest request, CancellationToken ct = default);

    /// <summary>
    /// Streams all deficiencies associated with <paramref name="inspectionId"/>.
    /// </summary>
    /// <param name="inspectionId">The inspection whose deficiencies to stream.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<Deficiency> ListDeficienciesAsync(ReviewId inspectionId, CancellationToken ct = default);

    // ── Equipment condition assessments (workstream #25 EXTEND) ─────────────

    /// <summary>
    /// Records a new <see cref="EquipmentConditionAssessment"/> linked to an
    /// inspection and returns the persisted record.
    /// </summary>
    /// <param name="request">Assessment payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the inspection does not exist or is not in <see cref="ReviewPhase.InProgress"/>.</exception>
    ValueTask<EquipmentConditionAssessment> RecordEquipmentConditionAsync(
        RecordEquipmentConditionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Streams all equipment condition assessments associated with the given inspection.
    /// </summary>
    /// <param name="inspectionId">The inspection whose assessments to stream.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<EquipmentConditionAssessment> ListEquipmentConditionsAsync(
        ReviewId inspectionId,
        CancellationToken ct = default);

    /// <summary>
    /// Streams equipment condition assessments for a specific equipment item across
    /// all inspections, oldest first. Useful for "show me this water heater's condition
    /// history."
    /// </summary>
    /// <param name="equipmentId">The equipment whose history to stream.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<EquipmentConditionAssessment> ListConditionHistoryForEquipmentAsync(
        EquipmentId equipmentId,
        CancellationToken ct = default);

    // ── Move-in / move-out delta projection (workstream #25 EXTEND) ──────────

    /// <summary>
    /// Returns paired move-in vs move-out inspection responses + condition
    /// assessments for a given unit. Used by security-deposit reconciliation.
    /// Returns <see langword="null"/> if either the most-recent move-in or
    /// most-recent move-out inspection is missing for the unit.
    /// </summary>
    /// <param name="unitId">The unit to compute the delta for.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<MoveInOutDelta?> GetMoveInOutDeltaAsync(
        EntityId unitId,
        CancellationToken ct = default);

    // ── Reports ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a summary <see cref="ReviewReport"/> for the given inspection.
    /// Can be called at any point but is most meaningful after the inspection is completed.
    /// </summary>
    /// <param name="inspectionId">The inspection to summarise.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the inspection does not exist.</exception>
    ValueTask<ReviewReport> GenerateReportAsync(ReviewId inspectionId, CancellationToken ct = default);
}
