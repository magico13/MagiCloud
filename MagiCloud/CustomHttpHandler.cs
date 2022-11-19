using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MagiCloud;

public class CustomHttpHandler : DelegatingHandler
{
    public CustomHttpHandler(AuthenticationStateProvider authProvider)
    {
        AuthProvider = authProvider;
    }

    private AuthenticationStateProvider AuthProvider { get; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // see https://learn.microsoft.com/en-us/aspnet/core/blazor/security/server/additional-scenarios?view=aspnetcore-6.0
        // for how to probably actually get the token to reuse for these calls
        return await base.SendAsync(request, cancellationToken);
    }
}
