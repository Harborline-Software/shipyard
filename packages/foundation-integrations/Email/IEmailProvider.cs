namespace Sunfish.Foundation.Integrations.Email;

/// <summary>
/// Vendor-neutral egress contract for unidirectional fire-and-forget
/// transactional email. Per ADR 0096 §D2 this substrate is structurally
/// distinct from <see cref="Messaging.IMessagingGateway"/>: no thread-id,
/// no participants array, no inbound webhook surface, pre-tenant scope.
/// First consumer is the public signup pipeline (ADR 0095
/// <c>IBootstrapContext</c> scope) sending welcome / verification /
/// invitation emails.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Mock-first substrate:</strong> the canonical mock is
/// <see cref="MockEmailProvider"/> (co-located here per ADR 0096 Halt 1
/// same-package layout). Register via
/// <see cref="DependencyInjection.VendorProviderServiceCollectionExtensions.AddSunfishVendorProvider{TContract, TConcrete}(Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.DependencyInjection.ServiceLifetime)"/>;
/// the real Postmark adapter (Step 2 PR) registers via
/// <see cref="DependencyInjection.VendorProviderServiceCollectionExtensions.UseVendorProviderIfConfigured{TContract, TReal}(Microsoft.Extensions.DependencyInjection.IServiceCollection, string)"/>
/// with <c>POSTMARK_API_KEY</c> as the gating env var.
/// </para>
/// <para>
/// <strong>Provider-neutrality (ADR 0013):</strong> this contract names no
/// Postmark / Mailgun / SendGrid vendor surface. The first adapter
/// (<c>providers-postmark</c>) ships in Step 2 PR; additional providers
/// slot in without changing this interface.
/// </para>
/// </remarks>
public interface IEmailProvider
{
    /// <summary>
    /// Sends a transactional email. Returns an
    /// <see cref="EmailDispatchResult"/> indicating the outcome
    /// (accepted / rejected / rate-limited / transport-error).
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
