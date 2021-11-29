using Cloudcrate.AspNetCore.Blazor.Browser.Storage;
using MagiCommon;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace MagiCloudWeb
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddHttpClient();
            builder.Services.AddStorage();
            builder.Services.AddScoped<ITokenProvider, BlazorStorageTokenProvider>();
            builder.Services.AddScoped<CustomHttpHandler>();
            builder.Services.AddHttpClient<IMagiCloudAPI, MagiCloudAPI>(c =>
            {
                c.BaseAddress = new Uri(builder.Configuration["Settings:ServerUrl"]);
            }).ConfigurePrimaryHttpMessageHandler<CustomHttpHandler>();

            await builder.Build().RunAsync();
        }
    }
}
