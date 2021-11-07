using MagiCloud.Configuration;
using MagiCloud.DataManager;
using MagiCommon;
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
                    p.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                )
            );

            services.AddAuthentication(o =>
                o.DefaultScheme = Constants.TokenAuthenticationScheme
            ).AddScheme<TokenAuthenticationOptions, TokenAuthenticationHandler>(Constants.TokenAuthenticationScheme, o => { });

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

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
