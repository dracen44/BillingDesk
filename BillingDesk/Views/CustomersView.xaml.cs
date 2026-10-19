using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BillingDesk.ViewModels;
using BillingDesk.Services;

namespace BillingDesk.Views
{
    public partial class CustomersView : UserControl
    {
        public CustomersView()
        {
            InitializeComponent();
            Loaded += CustomersView_Loaded;
        }

        private async void CustomersView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is CustomersViewModel viewModel)
            {
                // Initialize asynchronously when view loads
                await viewModel.InitializeAsync();
            }
        }

        private void CustomerRow_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Get the customer from the DataGrid row that was double-clicked
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is CustomerListItemDto customer)
            {
                if (DataContext is CustomersViewModel viewModel)
                {
                    viewModel.SelectCustomer(customer);
                }
            }
        }
    }
}
