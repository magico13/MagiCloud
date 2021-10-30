using BlazorDownloadFile;
using Cloudcrate.AspNetCore.Blazor.Browser.Storage;
using MagiCommon;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace MagiCloudWeb
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddStorage();
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddScoped<ITokenProvider, BlazorStorageTokenProvider>();
            builder.Services.AddBlazorDownloadFile(ServiceLifetime.Scoped);
            builder.Services.AddHttpClient<IMagiCloudAPI, MagiCloudAPI>(c =>
            {
                c.BaseAddress = new Uri(builder.Configuration["Settings:ServerUrl"]);
            });

            await builder.Build().RunAsync();
        }
    }
}
