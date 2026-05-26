using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Integrations.Email;

/// <summary>
/// Tier-2 egress contract for transactional email dispatch (ADR 0096). The
/// <c>IXProvider</c> noun-form naming convention (see <see cref="IMockVendorProvider"/>).
/// Per ADR 0013, vendor SDK / HTTP imports are quarantined to the concrete
/// adapter (e.g. <c>providers-postmark</c>); this contract is vendor-neutral.
/// Structurally distinct from <c>Messaging.IMessagingGateway</c> (ADR 0096
/// Halt 2): email is a one-way transactional egress, not a threaded
/// inbound/outbound messaging surface.
/// </summary>
public interface IEmailProvider
{
    /// <summary>
    /// Dispatch <paramref name="message"/>. Returns a closed
    /// <see cref="EmailDispatchResult"/> describing the outcome; does not throw
    /// for provider-level rejections / rate-limits / transport failures (those
    /// are union cases).
    /// </summary>
    Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
