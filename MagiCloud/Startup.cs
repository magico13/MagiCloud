using MagiCloud.Configuration;
using MagiCloud.DataManager;
using MagiCommon;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
            app.UseRouting();
            app.UseHttpsRedirection();
            app.UseDefaultFiles();

            if (env.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
                app.UseDeveloperExceptionPage();
            }

            app.UseBlazorFrameworkFiles();

            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
