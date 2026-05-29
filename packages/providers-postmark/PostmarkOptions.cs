using System.ComponentModel.DataAnnotations;

namespace Sunfish.Providers.Postmark;

/// <summary>
/// Adapter-private configuration for <see cref="PostmarkEmailProvider"/>. Bound
/// via <c>IOptionsMonitor&lt;PostmarkOptions&gt;</c> from <c>IConfiguration</c>
/// (user-secrets in dev, env var / secret-store in prod) per ADR 0096 §"Vendor
/// adapter security floors" — the secret is consumed at request time through the
/// options pattern, NOT captured as an env-var string at registration time.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Secret discipline (F1/F2):</strong> <see cref="ServerToken"/> is the
/// Postmark Server API token. It MUST NEVER be logged, echoed in exception
/// messages, surfaced in telemetry, or written to
/// <see cref="Foundation.Integrations.Email.EmailDispatchResult.ErrorDetail"/>.
/// The adapter reads it ONLY to set the <c>X-Postmark-Server-Token</c> request
/// header. This type intentionally does NOT override <see cref="object.ToString"/>
/// — the BCL default returns the type name, not field values, so an accidental
/// <c>logger.LogX(options)</c> cannot leak the token via interpolation.
/// </para>
/// <para>
/// <strong>TLS-only egress (F4):</strong> <see cref="ValidateBaseUrl"/> rejects a
/// non-HTTPS <see cref="BaseUrl"/> at options validation time, failing startup
/// closed rather than silently sending the server token over cleartext.
/// </para>
/// </remarks>
public sealed class PostmarkOptions
{
    /// <summary>The canonical Postmark transactional-email API base URL.</summary>
    public const string DefaultBaseUrl = "https://api.postmarkapp.com";

    /// <summary>The named <see cref="System.Net.Http.HttpClient"/> the adapter resolves.</summary>
    public const string HttpClientName = "Sunfish.Providers.Postmark";

    /// <summary>
    /// Postmark Server API token (the <c>X-Postmark-Server-Token</c> header
    /// value). REQUIRED. Never logged, never echoed (F1/F2).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ServerToken { get; set; } = string.Empty;

    /// <summary>
    /// Postmark API base URL. Defaults to <see cref="DefaultBaseUrl"/>;
    /// overridable for the EU data-residency host or a test stub. MUST be HTTPS
    /// (enforced by <see cref="ValidateBaseUrl"/>, F4).
    /// </summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>
    /// Per-request egress timeout. Defaults to 30s (WS-E hand-off §2.3). The
    /// fire-and-forget email substrate does not block on delivery confirmation,
    /// so a generous-but-bounded timeout is appropriate.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates that <see cref="BaseUrl"/> is a well-formed absolute HTTPS URL.
    /// Wired as an <c>IValidateOptions</c> rule by the registration extension
    /// (F4 — TLS-only egress). Returns the human-readable failure reason, or
    /// null when valid. The reason text contains the scheme/host but NEVER the
    /// <see cref="ServerToken"/>.
    /// </summary>
    public string? ValidateBaseUrl()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            return $"{nameof(PostmarkOptions)}.{nameof(BaseUrl)} is required.";
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
        {
            return $"{nameof(PostmarkOptions)}.{nameof(BaseUrl)} ('{BaseUrl}') is not a well-formed absolute URL.";
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return $"{nameof(PostmarkOptions)}.{nameof(BaseUrl)} must use HTTPS (got scheme '{uri.Scheme}'). "
                + "Cleartext egress of the Postmark server token is rejected (ADR 0096 security floor F4).";
        }

        return null;
    }
}
