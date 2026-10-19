using System.Windows;
using System.Windows.Controls;
using BillingDesk.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BillingDesk.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView(DashboardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += DashboardView_Loaded;
        }

        private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel viewModel)
            {
                await viewModel.LoadDashboardAsync();
            }
        }
    }
}
