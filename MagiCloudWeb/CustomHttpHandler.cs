using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace MagiCloudWeb;

//public class CustomHttpHandler : HttpClientHandler
//{
//    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
//    {
//        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
//        return await base.SendAsync(request, cancellationToken);
//    }
//}

public class CustomHttpHandler : AuthorizationMessageHandler
{
    public CustomHttpHandler(IAccessTokenProvider provider, NavigationManager navigationManager)
        : base(provider, navigationManager)
    {
        ConfigureHandler(new[] { "https://localhost" });
    }
}