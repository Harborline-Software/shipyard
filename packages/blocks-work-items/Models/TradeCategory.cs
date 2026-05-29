namespace Sunfish.Blocks.WorkItems.Models;

/// <summary>
/// Trade specialization for a <see cref="Contractor"/>. Drives the
/// contractor-by-trade lookup that <see cref="WorkItem"/> dispatch
/// uses to surface candidates.
/// </summary>
public enum TradeCategory
{
    General,
    Plumbing,
    Electrical,
    Hvac,
    Roofing,
    Landscaping,
    Cleaning,
    Pest,
    Paint,
    Flooring,
    Appliance,
    Other,
}
