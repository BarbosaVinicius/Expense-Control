using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Expense_Control.Data;

namespace Expense_Control
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // Método chamado pelo runtime para adicionar serviços ao contêiner.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();

            // Registrar serviços com string de conexão
            string connectionString = Configuration.GetConnectionString("DefaultConnection");

            services.AddSingleton<UserService>(provider => new UserService(connectionString));
            services.AddScoped<ExpenseService>(provider =>
            {
                return new ExpenseService(connectionString);
            });
            services.AddScoped<AmountReceivedService>(provider =>
            {
                return new AmountReceivedService(connectionString);
            });
            services.AddScoped<ReportService>(provider =>
            {
                string connectionString = Configuration.GetConnectionString("DefaultConnection");
                return new ReportService(connectionString);
            });

            // Outros serviços
            services.AddSingleton<WeatherForecastService>();
            services.AddSingleton<EmailService>();
            services.AddScoped<SessionService>();
        }

        // Método chamado pelo runtime para configurar o pipeline de requisição HTTP.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ExpenseService expenseService, AmountReceivedService amountReceivedService)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(env.WebRootPath, "js")),
                RequestPath = "/js"
            });

            // Garantir que as tabelas necessárias existam
            expenseService.EnsureDataBaseAndTableAsync().Wait();
            amountReceivedService.EnsureDataBaseAndTableAsync().Wait();
        }
    }
}
