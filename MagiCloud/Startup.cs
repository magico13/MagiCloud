using MagiCloud.Configuration;
using MagiCloud.DataManager;
using MagiCloud.TextExtraction;
using MagiCommon;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MagiCloud
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ElasticSettings>(Configuration.GetSection(nameof(ElasticSettings)));

            services.AddScoped<IElasticManager, ElasticManager>();
            services.AddScoped<IDataManager, FileSystemDataManager>();
            services.AddScoped<IHashService, HashService>();
            
            // Add text extractors
            services.AddScoped<ITextExtractor, PlainTextExtractor>();
            services.AddScoped<ITextExtractor, PdfExtractor>();

            services.AddCors(options =>
                options.AddDefaultPolicy(p =>
                    p.SetIsOriginAllowed(s =>
                    {
                        return s.Contains("https://localhost") || s.Contains("https://magico13.net");
                    })
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                )
            );

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(o =>
                {
                    //o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
                    o.SlidingExpiration = true;
                    o.LoginPath = "/login";
                });

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseHttpsRedirection();

            app.UseDefaultFiles();
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            app.UseRouting();

            if (env.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
                app.UseDeveloperExceptionPage();
            }

            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            //rewrite /login to /index.html to let blazor handle it
            var rwOptions = new RewriteOptions().AddRewrite(".*/login", "/index.html", false);
            app.UseRewriter(rwOptions);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
