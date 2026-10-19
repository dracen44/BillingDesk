using System.Windows;
using BillingDesk.ViewModels;
using BillingDesk.Views;
using Microsoft.Extensions.DependencyInjection;

namespace BillingDesk
{
    public partial class MainWindow : Window
    {
        private MainViewModel? _mainViewModel;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Get MainViewModel from DI
            var app = (App)Application.Current;
            var host = app.GetHost();
            if (host == null)
                return;

            _mainViewModel = host.Services.GetRequiredService<MainViewModel>();
            DataContext = _mainViewModel;

            // Subscribe to view model changes to update the view
            _mainViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.CurrentViewModel))
                {
                    UpdateView();
                }
            };

            // Show Dashboard on startup
            Dashboard_Click(null!, null!);
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel?.NavigateToDashboard();
            UpdateView();
        }

        private void Customers_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel?.NavigateToCustomers();
            UpdateView();
        }

        private void Invoices_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel?.NavigateToInvoices();
            UpdateView();
        }

        private void Payments_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel?.NavigateToPayments();
            UpdateView();
        }

        private void UpdateView()
        {
            if (_mainViewModel?.CurrentViewModel == null)
                return;

            if (_mainViewModel.CurrentViewModel is DashboardViewModel dashboardVm)
            {
                // DashboardView has a constructor that accepts the ViewModel
                var view = new DashboardView(dashboardVm);
                ContentArea.Content = view;
            }
            else if (_mainViewModel.CurrentViewModel is CustomersViewModel customersVm)
            {
                var view = new CustomersView();
                // Set DataContext before adding to Content so Loaded event fires with correct context
                view.DataContext = customersVm;
                ContentArea.Content = view;
            }
            else if (_mainViewModel.CurrentViewModel is CustomerDetailViewModel customerDetailVm)
            {
                var view = new CustomerDetailView();
                view.DataContext = customerDetailVm;
                ContentArea.Content = view;
            }
            else if (_mainViewModel.CurrentViewModel is InvoicesListViewModel invoicesListVm)
            {
                var view = new InvoicesListView(invoicesListVm);
                view.DataContext = invoicesListVm;
                ContentArea.Content = view;
            }
            else if (_mainViewModel.CurrentViewModel is InvoiceDetailsViewModel invoiceDetailsVm)
            {
                var view = new InvoiceDetailsView();
                view.DataContext = invoiceDetailsVm;
                ContentArea.Content = view;
            }
            else if (_mainViewModel.CurrentViewModel is PaymentsListViewModel paymentsListVm)
            {
                var view = new PaymentsListView(paymentsListVm);
                view.DataContext = paymentsListVm;
                ContentArea.Content = view;
            }
            else if (_mainViewModel.CurrentViewModel is PaymentDetailsViewModel paymentDetailsVm)
            {
                var view = new PaymentDetailsView();
                view.DataContext = paymentDetailsVm;
                ContentArea.Content = view;
            }
            else if (_mainViewModel.CurrentViewModel is PlaceholderViewModel placeholderVm)
            {
                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = placeholderVm.Message,
                    FontSize = 18,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DimGray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(24)
                };
                ContentArea.Content = textBlock;
            }
        }
    }
}
