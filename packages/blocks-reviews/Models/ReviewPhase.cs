namespace Sunfish.Blocks.Reviews.Models;

/// <summary>
/// Lifecycle phases for an <see cref="Review"/>.
/// </summary>
/// <remarks>
/// Valid transitions:
/// <list type="bullet">
///   <item><description><see cref="Scheduled"/> → <see cref="InProgress"/> via <c>StartAsync</c></description></item>
///   <item><description><see cref="InProgress"/> → <see cref="Completed"/> via <c>CompleteAsync</c></description></item>
///   <item><description><see cref="Scheduled"/> or <see cref="InProgress"/> → <see cref="Cancelled"/> (future pass)</description></item>
/// </list>
/// </remarks>
public enum ReviewPhase
{
    /// <summary>Review has been created and is waiting to begin.</summary>
    Scheduled,

    /// <summary>Inspector has started the inspection; responses are being collected.</summary>
    InProgress,

    /// <summary>All responses recorded; inspection is closed.</summary>
    Completed,

    /// <summary>Review was cancelled before or during execution.</summary>
    Cancelled,
}
