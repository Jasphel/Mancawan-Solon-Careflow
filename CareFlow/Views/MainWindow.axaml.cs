using Avalonia.Controls;
using CareFlow.ViewModels;

namespace CareFlow.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}