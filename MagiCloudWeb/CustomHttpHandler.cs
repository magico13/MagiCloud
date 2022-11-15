using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components;

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