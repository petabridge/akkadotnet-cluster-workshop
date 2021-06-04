using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.CQRS.Infrastructure;
using Akka.CQRS.Pricing.Web.Hubs;
using Akka.CQRS.Pricing.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Akka.CQRS.Pricing.Web
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
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });


            services.AddMvc();
            services.AddSignalR();
            services.AddPhobosApm();
            services.AddTransient<StockHubHelper>();
            services.AddHostedService<AkkaService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseRouting();

            // enable App.Metrics routes
            app.UseMetricsAllMiddleware();
            app.UseMetricsAllEndpoints();

            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseEndpoints(ep =>
            {
                ep.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
                ep.MapHub<StockHub>("/hubs/stockHub");
            });
        }
    }
}
