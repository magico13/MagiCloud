﻿using MagiCommon;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MagiConsole;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        var context = host.Services.GetRequiredService<MagiContext>();
        context.Database.Migrate();

        var api = host.Services.GetRequiredService<IMagiCloudAPI>();

        var user = await context.Users.SingleOrDefaultAsync();
        if (user is not null)
        {
            try
            {
                var fullToken = await api.ReauthTokenAsync(user.Token);
                if (fullToken is null)
                {
                    user = null;
                }
                else
                {
                    logger.LogInformation("Reauthenticated with stored token for user {User}", user.Username);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error while reauthenticating existing token, continuing.");
                user = null;
            }
        }

        if (user is null)
        {
            Console.WriteLine("No user account saved, please log in.");
            Console.Write("Username: ");
            string username = Console.ReadLine().Trim();
            Console.Write("Password: ");
            string password = Console.ReadLine().Trim();

            try
            {
                var token = await api.GetAuthTokenAsync(new MagiCommon.Models.LoginRequest
                {
                    Username = username,
                    Password = password,
                    DesiredTimeout = 2678400, //31 days
                    TokenName = $"MagiConsole - {Environment.MachineName}",
                    DesiredExpiration = null //does not expire at a set time, only from inactivity

                });
                context.Users.Add(new UserData
                {
                    Id = Guid.NewGuid().ToString(),
                    Token = token.Id,
                    Username = username
                });
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Error while logging in.");
                Console.WriteLine("Error while logging in. Press any key to exit.");
                Console.ReadKey();
                return;
            }
            
        }

        var syncManager = host.Services.GetRequiredService<SyncManager>();
        var settings = host.Services.GetRequiredService<IOptions<Settings>>();
        int syncSeconds = settings.Value.FullSyncSeconds;
        Timer syncTimer = null;
        syncTimer = new Timer(o =>
        {
            syncTimer?.Change(TimeSpan.FromDays(1), TimeSpan.FromDays(1));
            syncManager.SyncAsync().Wait();
            syncTimer?.Change(TimeSpan.FromSeconds(syncSeconds), TimeSpan.FromSeconds(syncSeconds));
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(syncSeconds));

        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                var connectionString = hostContext.Configuration.GetConnectionString("data") 
                    ?? throw new NullReferenceException("Missing Connection String");
                services.Configure<Settings>(hostContext.Configuration.GetSection(nameof(Settings)));
                services.AddDbContext<MagiContext>(c => c.UseSqlite(connectionString));
                services.AddSingleton<ITokenProvider, DBTokenProvider>();
                services.AddHttpClient<IMagiCloudAPI, MagiCloudAPI>(c => 
                {
                    c.BaseAddress = new Uri(hostContext.Configuration["Settings:ServerUrl"]);
                });

                services.AddSingleton<SyncManager>();
                services.AddSingleton<IHashService, HashService>();
            });
}
