using System.Windows;
using System.Windows.Controls;
using Trading.Engine.Operators;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf.Views;

public partial class ResearchCatalogView : UserControl
{
    public ResearchCatalogView() => InitializeComponent();

    private async void OpenExactReport(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var report = (sender as FrameworkElement)?.DataContext as ResearchSummary;
        if (DataContext is ResearchCatalogViewModel viewModel)
            await viewModel.LoadReportAsync(report);
    }
}
