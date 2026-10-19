using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BillingDesk.Data;
using BillingDesk.Services;
using BillingDesk.ViewModels;
using BillingDesk.Views;

namespace BillingDesk
{
    public partial class App : Application
    {
        private IHost? _host;

        public IHost? GetHost() => _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Converters are now defined in Views/Styles.xaml to ensure they're instantiated
            // during XAML parsing before any view tries to use them (fixes timing issue)

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                        .AddUserSecrets<App>(optional: true);
                })
                .ConfigureServices((context, services) =>
                {
                    var connectionString = context.Configuration.GetConnectionString("BillingDb");

                    // Database
                    services.AddDbContextFactory<BillingDbContext>(options =>
                    {
                        options.UseSqlServer(connectionString);
                    });

                    // Services
                    services.AddScoped<DashboardService>();
                    services.AddScoped<ICustomerService, CustomerService>();
                    services.AddScoped<IInvoiceService, InvoiceService>();
                    services.AddScoped<IPaymentService, PaymentService>();

                    // ViewModels
                    services.AddScoped<MainViewModel>();
                    services.AddScoped<DashboardViewModel>();
                    services.AddScoped<CustomersViewModel>();
                    services.AddScoped<CustomerDetailViewModel>();
                    services.AddScoped<InvoicesListViewModel>();
                    services.AddScoped<InvoiceDetailsViewModel>();
                    services.AddScoped<PaymentsListViewModel>();
                    services.AddScoped<PaymentDetailsViewModel>();

                    // Views - Not registered in DI container as they are WPF UserControls
                    // with parameterless constructors that should be instantiated directly
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();

                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        logging.SetMinimumLevel(LogLevel.Debug);
                    }
                    else
                    {
                        logging.SetMinimumLevel(LogLevel.Information);
                    }
                })
                .Build();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}

