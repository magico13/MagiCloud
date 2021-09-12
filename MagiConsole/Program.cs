using MagiCommon;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MagiConsole
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            var context = host.Services.GetRequiredService<MagiContext>();
            context.Database.Migrate();

            var syncManager = host.Services.GetRequiredService<SyncManager>();
            //await syncManager.SyncAsync();
            Timer syncTimer = null;
            syncTimer = new Timer(o =>
            {
                syncTimer?.Change(TimeSpan.FromDays(1), TimeSpan.FromDays(1));
                syncManager.SyncAsync().Wait();
                syncTimer?.Change(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(15));

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var connectionString = hostContext.Configuration.GetConnectionString("data");

                    services.Configure<Settings>(hostContext.Configuration.GetSection(nameof(Settings)));
                    services.AddDbContext<MagiContext>(c => c.UseSqlite(connectionString));
                    services.AddHttpClient<IMagiCloudAPI, MagiCloudAPI>(c => 
                    {
                        c.BaseAddress = new Uri(hostContext.Configuration["Settings:ServerUrl"]);
                    });

                    services.AddSingleton<SyncManager>();
                    services.AddSingleton<IHashService, HashService>();

                });
    }
}
