using Duende.IdentityServer.Services;
using Goggles;
using MagiCloud;
using MagiCloud.Configuration;
using MagiCloud.DataManager;
using MagiCloud.Db;
using MagiCommon;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// IdentityServer/Auth setup
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDefaultIdentity<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddIdentityServer()
    .AddApiAuthorization<IdentityUser, ApplicationDbContext>();

builder.Services.AddAuthentication()
    .AddIdentityServerJwt()
    .AddCookie();

builder.Services.AddTransient<IProfileService, ProfileService>();
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("role");

builder.Services.Configure<IdentityOptions>(options =>
    options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier);

// Other setup
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.Configure<ElasticSettings>(builder.Configuration.GetSection(nameof(ElasticSettings)));

builder.Services.AddScoped<IElasticManager, ElasticManager>();
builder.Services.AddScoped<IDataManager, FileSystemDataManager>();
builder.Services.AddScoped<IHashService, HashService>();

// Add text extraction abilities
var extractionSettings = builder.Configuration
    .GetSection(nameof(ExtractionSettings))
    .Get<ExtractionSettings>();
builder.Services.AddLens(c =>
{
    c.MaxTextLength = extractionSettings.MaxTextLength;
    c.EnableOCR = extractionSettings.EnableOCR;
});
builder.Services.AddScoped<ExtractionHelper>();


builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p =>
        p.SetIsOriginAllowed(s => s.Contains("https://localhost") || s.Contains("https://magico13.net"))
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
    )
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseCors();

app.UseIdentityServer();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
