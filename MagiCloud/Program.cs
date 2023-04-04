using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Goggles;
using MagiCloud.Areas.Identity;
using MagiCloud.Configuration;
using MagiCloud.DataManager;
using MagiCloud.Db;
using MagiCloud.Services;
using MagiCommon;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

var builder = WebApplication.CreateBuilder(args);
var applicationCancellationTokenSource = new System.Threading.CancellationTokenSource();

// Add services to the container.

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

builder.Services.Configure<GeneralSettings>(builder.Configuration.GetSection(nameof(GeneralSettings)));
builder.Services.Configure<ElasticSettings>(builder.Configuration.GetSection(nameof(ElasticSettings)));

builder.Services.AddSingleton<IElasticManager, ElasticManager>();
builder.Services.AddSingleton<IDataManager, FileSystemDataManager>();
builder.Services.AddSingleton<IHashService, HashService>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddHttpClient();

builder.Services.AddTransient<IMessageQueueService<string>, InMemoryMessageQueueService<string>>();

if (!string.IsNullOrWhiteSpace(builder.Configuration.Get<GeneralSettings>().SendGridKey))
{
    builder.Services.AddSingleton<IEmailSender, SendGridEmailService>();
}

// Add text extraction abilities
var gogglesConfig = builder.Configuration
    .GetSection(nameof(GogglesConfiguration))
    .Get<GogglesConfiguration>();
builder.Services.AddLens(o =>
{
    o.MaxTextLength = gogglesConfig.MaxTextLength;
    o.EnableOCR = gogglesConfig.EnableOCR;
    o.AzureOCRConfiguration = gogglesConfig.AzureOCRConfiguration;
    o.WhisperTranscriptionConfiguration = gogglesConfig.WhisperTranscriptionConfiguration;
});
builder.Services.AddSingleton<ExtractionHelper>();

// Add Singletons
builder.Services.AddSingleton<TextExtractionQueueHelper>();

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


// Start queue monitors
var textExtrationQueue = app.Services.GetRequiredService<TextExtractionQueueHelper>();
_ = Task.Factory.StartNew(async ()
    => await textExtrationQueue.ProcessQueueAsync(applicationCancellationTokenSource.Token),
    TaskCreationOptions.LongRunning).ConfigureAwait(false);

await app.RunAsync(applicationCancellationTokenSource.Token);
