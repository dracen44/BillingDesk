using System.Windows;
using System.Windows.Controls;
using BillingDesk.ViewModels;
using BillingDesk.Services;

namespace BillingDesk.Views
{
    public partial class InvoicesListView : UserControl
    {
        private readonly InvoicesListViewModel _viewModel;

        public InvoicesListView(InvoicesListViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = viewModel;

            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"XAML Init Error: {ex.InnerException?.Message ?? ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Full Exception:\n{ex}");
                System.Diagnostics.Debug.WriteLine($"Error initializing InvoicesListView: {ex}");
                throw;
            }

            Loaded += InvoicesListView_Loaded;

            
              
        }

        private async void InvoicesListView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is InvoicesListViewModel viewModel)
            {
                // Load invoices when view is loaded
                await viewModel.LoadInvoicesAsync();
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is InvoiceListItemDto invoice)
            {
                if (DataContext is InvoicesListViewModel viewModel)
                {
                    viewModel.SelectInvoiceCommand.Execute(invoice);
                }
            }
        }
    }
}
