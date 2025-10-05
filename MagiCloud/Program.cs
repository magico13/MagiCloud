using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using MagiCloud.Areas.Identity;
using MagiCloud.Configuration;
using MagiCloud.DataManager;
using MagiCloud.Db;
using MagiCloud.Services;
using MagiCloud.Services.ChatServices;
using MagiCommon;
using MagiCommon.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;
using System;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
var applicationCancellationTokenSource = new System.Threading.CancellationTokenSource();

// Add services to the container.

// Set up configuration first
builder.Services.Configure<AssistantSettings>(builder.Configuration.GetSection(nameof(AssistantSettings)));
builder.Services.Configure<GeneralSettings>(builder.Configuration.GetSection(nameof(GeneralSettings)));
builder.Services.Configure<ElasticSettings>(builder.Configuration.GetSection(nameof(ElasticSettings)));
builder.Services.Configure<ExtractionSettings>(builder.Configuration.GetSection(nameof(ExtractionSettings)));

var assistantSettings = builder.Configuration.GetSection(nameof(AssistantSettings)).Get<AssistantSettings>();
var generalSettings = builder.Configuration.GetSection(nameof(GeneralSettings)).Get<GeneralSettings>();
var elasticSettings = builder.Configuration.GetSection(nameof(ElasticSettings)).Get<ElasticSettings>();

// Log to Elasticsearch
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticSettings.Url))
    {
        AutoRegisterTemplate = true,
        NumberOfReplicas = 1,
        NumberOfShards = 1,
        IndexFormat = "magicloud-logs-{0:yyyy.MM.dd}",
        ModifyConnectionSettings = x =>
        {
            if (!string.IsNullOrWhiteSpace(elasticSettings.ApiKey))
            {
                x.ApiKeyAuthentication(elasticSettings.ApiKeyId, elasticSettings.ApiKey);
            }
            if (!string.IsNullOrWhiteSpace(elasticSettings.Thumbprint))
            {
                x.ServerCertificateValidationCallback((caller, cert, chain, errors)
                    => string.Equals(cert.GetCertHashString(), elasticSettings.Thumbprint, StringComparison.OrdinalIgnoreCase));
            }
            return x;
        },
    })
    .WriteTo.Console()
    .WriteTo.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog(Log.Logger);

// IdentityServer/Auth setup
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();

// Other setup
builder.Services
    .AddBlazorise(options => options.Immediate = true)
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<IElasticFileRepo, ElasticFileRepo>();
builder.Services.AddSingleton<IElasticFolderRepo, ElasticFolderRepo>();
builder.Services.AddSingleton<ElasticManager>();
builder.Services.AddSingleton<IDataManager, FileSystemDataManager>();
builder.Services.AddSingleton<IHashService, HashService>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddHttpClient();

builder.Services.AddTransient<IMessageQueueService<string>, InMemoryMessageQueueService<string>>();

if (!string.IsNullOrWhiteSpace(generalSettings.SendGridKey))
{
    builder.Services.AddSingleton<IEmailSender, SendGridEmailService>();
}

if (!string.IsNullOrWhiteSpace(assistantSettings.OpenAIKey))
{
    builder.Services.AddSingleton<OpenAIResponseClient>(sp =>
    {
        var settings = sp.GetRequiredService<IOptions<AssistantSettings>>().Value;
        return new OpenAIResponseClient(
            model: settings.Model,
            apiKey: settings.OpenAIKey);
    });
    builder.Services.AddScoped<IChatCompletionService, ChatGPTCompletionService>();
};

// Add text extraction abilities
builder.Services.AddSingleton<ExtractionHelper>();

builder.Services.AddSingleton<TextExtractionQueueWrapper>();
builder.Services.AddHostedService<TextExtractionQueueBackgroundService>();

builder.Services.AddSingleton<ChatAssistantCommandHandler>();


var app = builder.Build();

// Run migration for the identity database
// TODO: Not recommended way of doing this, but for our purposes is fine for now
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapRazorPages();
app.MapFallbackToPage("/_Host");



await app.RunAsync(applicationCancellationTokenSource.Token);
