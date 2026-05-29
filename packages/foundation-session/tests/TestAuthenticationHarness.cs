using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.Session.Tests;

/// <summary>
/// A recording <see cref="IAuthenticationService"/> that captures the SignIn/SignOut calls the
/// <c>SignInAsync</c>/<c>SignOutAsync</c> HttpContext extension methods make. Lets the
/// establisher tests assert what principal was issued into the cookie (A6: opaque-id-only)
/// without a real cookie middleware.
/// </summary>
internal sealed class RecordingAuthenticationService : IAuthenticationService
{
    public List<(string? Scheme, ClaimsPrincipal Principal)> SignIns { get; } = new();
    public List<string?> SignOuts { get; } = new();

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        => Task.FromResult(AuthenticateResult.NoResult());

    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        => Task.CompletedTask;

    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        => Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    {
        SignIns.Add((scheme, principal));
        return Task.CompletedTask;
    }

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        SignOuts.Add(scheme);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Builds a <see cref="DefaultHttpContext"/> whose <c>RequestServices</c> contain the recording
/// auth service, so the <c>SignInAsync</c>/<c>SignOutAsync</c> extension methods resolve.
/// </summary>
internal static class TestHttpContextFactory
{
    public static (DefaultHttpContext Context, RecordingAuthenticationService Auth) Create(
        ClaimsPrincipal? user = null)
    {
        var auth = new RecordingAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(auth);
        // SignInAsync also resolves IClaimsTransformation/ISystemClock via the auth service in
        // some paths; the recording service short-circuits those, so the minimal registration
        // above is sufficient.
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        if (user is not null)
        {
            context.User = user;
        }

        return (context, auth);
    }
}
