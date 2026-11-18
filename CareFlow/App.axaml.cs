
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CareFlow.Data;
using CareFlow.ViewModels;
using CareFlow.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareFlow;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(
                    "appsettings.json",
                    optional: false,
                    reloadOnChange: true)
                .Build();

            var connectionString =
                configuration.GetConnectionString("CareFlowConnection")
                ?? throw new InvalidOperationException(
                    "CareFlow database connection string is missing.");

            var services = new ServiceCollection();

            services.AddDbContext<CareFlowDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddTransient<MainWindowViewModel>();

            _serviceProvider = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider
                    .GetRequiredService<MainWindowViewModel>()
            };

            desktop.Exit += (_, _) => _serviceProvider.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
