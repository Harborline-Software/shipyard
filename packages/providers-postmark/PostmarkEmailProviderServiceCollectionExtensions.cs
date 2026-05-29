using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Integrations.DependencyInjection;
using Sunfish.Foundation.Integrations.Email;

namespace Sunfish.Providers.Postmark;

/// <summary>
/// Composition-root DI registration for the Postmark email adapter. Call at the
/// host composition root AFTER
/// <c>AddSunfishVendorProviderSubstrate()</c> +
/// <c>AddSunfishVendorProvider&lt;IEmailProvider, MockEmailProvider&gt;()</c>.
/// </summary>
public static class PostmarkEmailProviderServiceCollectionExtensions
{
    /// <summary>The env-var whose presence gates the mock → Postmark swap.</summary>
    public const string PostmarkApiKeyEnvVar = "POSTMARK_API_KEY";

    /// <summary>
    /// Registers the Postmark adapter as the conditional real <see cref="IEmailProvider"/>:
    /// </summary>
    /// <list type="number">
    ///   <item><description>Binds <see cref="PostmarkOptions"/> +
    ///   <see cref="EmailDispatchOptions"/> from configuration and validates the
    ///   base URL is HTTPS (F4) + the server token is present.</description></item>
    ///   <item><description>Configures a named, resilient
    ///   <see cref="System.Net.Http.HttpClient"/> (retry on 429/5xx/network;
    ///   circuit-breaker; bounded timeout — WS-E hand-off §2.3).</description></item>
    ///   <item><description>Calls
    ///   <see cref="VendorProviderServiceCollectionExtensions.UseVendorProviderIfConfigured{TContract, TReal}(IServiceCollection, string)"/>
    ///   with <see cref="PostmarkApiKeyEnvVar"/> — swapping the mock for
    ///   <see cref="PostmarkEmailProvider"/> only when the key is present.
    ///   Always records the <c>(IEmailProvider → POSTMARK_API_KEY)</c> mapping in
    ///   the env-var registry so the production guard can name the expected key.
    ///   </description></item>
    /// </list>
    /// <param name="services">DI container.</param>
    /// <param name="configSectionName">Configuration section bound to
    /// <see cref="PostmarkOptions"/> (default <c>"Postmark"</c>). The server
    /// token lives at <c>{section}:ServerToken</c> — user-secrets in dev,
    /// secret-store in prod.</param>
    /// <param name="emailDispatchSectionName">Configuration section bound to
    /// <see cref="EmailDispatchOptions"/> (default <c>"EmailDispatch"</c>).</param>
    public static IServiceCollection AddPostmarkEmailProvider(
        this IServiceCollection services,
        string configSectionName = "Postmark",
        string emailDispatchSectionName = "EmailDispatch")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(configSectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailDispatchSectionName);

        services.AddOptions<PostmarkOptions>()
            .BindConfiguration(configSectionName)
            .Validate(
                static o => string.IsNullOrEmpty(o.ValidateBaseUrl()),
                "Postmark BaseUrl must be a well-formed absolute HTTPS URL (ADR 0096 security floor F4).")
            .ValidateOnStart();

        // EmailDispatchOptions is substrate-level (vendor-neutral); bind it here
        // so the From-address is available even before the swap fires. Idempotent
        // if a sibling adapter / W#79 already bound it.
        services.AddOptions<EmailDispatchOptions>()
            .BindConfiguration(emailDispatchSectionName);

        services.AddHttpClient(PostmarkOptions.HttpClientName, static (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<PostmarkOptions>>().CurrentValue;
                client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl));
                client.Timeout = options.RequestTimeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResilienceHandler(static resilience =>
            {
                // Retry on transient (429/5xx/network) per WS-E §2.3. The
                // standard handler already classifies 429/5xx/HttpRequestException
                // as transient; bound the total attempts + the per-attempt
                // timeout so a hung Postmark call cannot stall the caller.
                resilience.Retry.MaxRetryAttempts = 3;
                resilience.Retry.UseJitter = true;
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            });

        // Conditional swap: mock → Postmark when POSTMARK_API_KEY is present.
        // The substrate's UseVendorProviderIfConfigured does a services.Replace
        // registering typeof(PostmarkEmailProvider) as the IEmailProvider
        // implementation type; the container activates it via its public
        // (IHttpClientFactory, IOptionsMonitor<PostmarkOptions>,
        // IOptionsMonitor<EmailDispatchOptions>, ILogger<>) constructor — all of
        // which are resolvable from the registrations above + the framework.
        // Always records the (IEmailProvider → POSTMARK_API_KEY) mapping so the
        // production guard can name the expected key even when the swap does not
        // fire in this deployment.
        services.UseVendorProviderIfConfigured<IEmailProvider, PostmarkEmailProvider>(PostmarkApiKeyEnvVar);

        return services;
    }

    private static string EnsureTrailingSlash(string baseUrl)
        => baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
}
