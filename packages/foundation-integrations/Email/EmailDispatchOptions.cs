namespace Sunfish.Foundation.Integrations.Email;

/// <summary>
/// Vendor-neutral dispatch defaults for the <see cref="IEmailProvider"/>
/// substrate — the configured From-address + default
/// <see cref="EmailMessage.MessageStream"/> applied when a caller does not
/// supply them. Bound via <c>IOptions&lt;EmailDispatchOptions&gt;</c> from
/// <c>IConfiguration</c> at the composition root.
/// </summary>
/// <remarks>
/// <para>
/// Resolves W#79 Halt H7 / WS-E Halt H-WSE-4: the signup pipeline hard-coded
/// <c>From: "noreply@sunfish.app"</c> with no options type. This is the
/// vendor-NEUTRAL home for the From-address (the substrate, not the Postmark
/// adapter, owns it) so a future provider swap does not move the From-address
/// configuration. The Postmark adapter additionally validates that this
/// From-address aligns with a Postmark sender-signature at request time, but
/// the address itself is substrate-level config.
/// </para>
/// <para>
/// This type carries NO vendor secret — the Postmark server token lives in the
/// adapter-private <c>PostmarkOptions</c> in the <c>providers-postmark</c>
/// package. Keeping the secret out of this shared substrate options type means
/// no consumer of <see cref="IEmailProvider"/> ever has a code path that can
/// observe the vendor credential.
/// </para>
/// </remarks>
public sealed class EmailDispatchOptions
{
    /// <summary>
    /// The default sender address for outbound transactional email. MUST be a
    /// vendor-authorised From address (a Postmark sender signature, an SES
    /// verified identity, etc.). Callers MAY override per-message via
    /// <see cref="EmailMessage.From"/>; this is the fallback default.
    /// </summary>
    public string FromAddress { get; set; } = "noreply@sunfish.app";

    /// <summary>
    /// Optional display name paired with <see cref="FromAddress"/> (e.g.,
    /// <c>"Harborline"</c>). Null leaves the address bare.
    /// </summary>
    public string? FromDisplayName { get; set; }

    /// <summary>
    /// The default <see cref="EmailMessage.MessageStream"/> applied when a
    /// caller leaves it null. Null defers to the vendor's default
    /// transactional stream. Per ADR 0096 Halt 6 the initial stream taxonomy
    /// lands in W#80 Stage-05.
    /// </summary>
    public string? DefaultMessageStream { get; set; }
}
