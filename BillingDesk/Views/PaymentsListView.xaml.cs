using System.Windows;
using System.Windows.Controls;
using BillingDesk.Services;
using BillingDesk.ViewModels;

namespace BillingDesk.Views
{
    public partial class PaymentsListView : UserControl
    {
        private readonly PaymentsListViewModel _viewModel;

        public PaymentsListView(PaymentsListViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();

            Loaded += PaymentsListView_Loaded;
        }

        private async void PaymentsListView_Loaded(object sender, RoutedEventArgs e)
        {
            // Reload with preserved search, date filters, and page when returning from details
            await _viewModel.LoadPaymentsAsync();
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is PaymentListItemDto payment)
            {
                _viewModel.SelectPaymentCommand.Execute(payment);
            }
        }
    }
}
