using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace BillingDesk.ViewModels
{
    /// <summary>
    /// Main application ViewModel that manages navigation and high-level application state.
    /// </summary>
    public class MainViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private CustomersViewModel? _customersViewModel;
        private CustomerDetailViewModel? _customerDetailViewModel;
        private InvoicesListViewModel? _invoicesListViewModel;
        private InvoiceDetailsViewModel? _invoiceDetailsViewModel;
        private PaymentsListViewModel? _paymentsListViewModel;
        private PaymentDetailsViewModel? _paymentDetailsViewModel;

        private ObservableObject? _currentViewModel;
        public ObservableObject? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        private string _currentPageTitle = "Dashboard";
        public string CurrentPageTitle
        {
            get => _currentPageTitle;
            set => SetProperty(ref _currentPageTitle, value);
        }

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            NavigateToDashboard();
        }

        /// <summary>
        /// Navigate to Dashboard view.
        /// </summary>
        public void NavigateToDashboard()
        {
            var viewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
            CurrentViewModel = viewModel;
            CurrentPageTitle = "Dashboard";

            // Load dashboard data
            _ = viewModel.LoadDashboardAsync();
        }

        /// <summary>
        /// Navigate to Customers view.
        /// </summary>
        public void NavigateToCustomers()
        {
            // Reuse the same CustomersViewModel instance if already created
            _customersViewModel ??= _serviceProvider.GetRequiredService<CustomersViewModel>();

            // Subscribe to customer selection if not already subscribed
            _customersViewModel.CustomerSelected -= OnCustomerSelected;
            _customersViewModel.CustomerSelected += OnCustomerSelected;

            CurrentPageTitle = "Customers";
            CurrentViewModel = _customersViewModel;
            _ = _customersViewModel.InitializeAsync();
        }

        /// <summary>
        /// Navigate to customer detail view.
        /// </summary>
        private void OnCustomerSelected(object? sender, EventArgs e)
        {
            if (sender is CustomersViewModel customersVm && customersVm.SelectedCustomer != null)
            {
                NavigateToCustomerDetail(customersVm.SelectedCustomer.CustomerId, customersVm);
            }
        }

        /// <summary>
        /// Navigate to customer detail view with reference back to customer list.
        /// </summary>
        private void NavigateToCustomerDetail(int customerId, CustomersViewModel? parentViewModel)
        {
            _customerDetailViewModel = _serviceProvider.GetRequiredService<CustomerDetailViewModel>();

            // Subscribe to back navigation
            _customerDetailViewModel.BackRequested -= (s, e) => OnBackFromCustomerDetail(parentViewModel);
            _customerDetailViewModel.BackRequested += (s, e) => OnBackFromCustomerDetail(parentViewModel);

            CurrentPageTitle = "Customer Details";
            CurrentViewModel = _customerDetailViewModel;

            // Load customer data
            _ = _customerDetailViewModel.LoadCustomerAsync(customerId);
        }

        /// <summary>
        /// Navigate back to customer list view.
        /// </summary>
        private void OnBackFromCustomerDetail(CustomersViewModel? parentViewModel)
        {
            if (parentViewModel != null)
            {
                CurrentPageTitle = "Customers";
                CurrentViewModel = parentViewModel;
            }
            else
            {
                NavigateToCustomers();
            }
        }

        /// <summary>
        /// Navigate to Invoices view (placeholder for future implementation).
        /// </summary>
        public void NavigateToInvoices()
        {
            // Reuse the same InvoicesListViewModel instance if already created
            _invoicesListViewModel ??= _serviceProvider.GetRequiredService<InvoicesListViewModel>();

            // Subscribe to invoice selection if not already subscribed
            _invoicesListViewModel.InvoiceSelected -= OnInvoiceSelected;
            _invoicesListViewModel.InvoiceSelected += OnInvoiceSelected;

            CurrentPageTitle = "Invoices";
            CurrentViewModel = _invoicesListViewModel;
            _ = _invoicesListViewModel.LoadInvoicesAsync();
        }

        /// <summary>
        /// Navigate to invoice detail view.
        /// </summary>
        private void OnInvoiceSelected(object? sender, EventArgs e)
        {
            if (sender is InvoicesListViewModel invoicesVm && invoicesVm.SelectedInvoice != null)
            {
                NavigateToInvoiceDetail(invoicesVm.SelectedInvoice.InvoiceId, invoicesVm);
            }
        }

        /// <summary>
        /// Navigate to invoice detail view with reference back to invoice list.
        /// </summary>
        private void NavigateToInvoiceDetail(int invoiceId, InvoicesListViewModel? parentViewModel)
        {
            _invoiceDetailsViewModel = _serviceProvider.GetRequiredService<InvoiceDetailsViewModel>();

            // Subscribe to back navigation
            _invoiceDetailsViewModel.BackRequested -= (s, e) => OnBackFromInvoiceDetail(parentViewModel);
            _invoiceDetailsViewModel.BackRequested += (s, e) => OnBackFromInvoiceDetail(parentViewModel);

            CurrentPageTitle = "Invoice Details";
            CurrentViewModel = _invoiceDetailsViewModel;

            // Load invoice data
            _ = _invoiceDetailsViewModel.LoadInvoiceAsync(invoiceId);
        }

        /// <summary>
        /// Navigate back to invoices list.
        /// </summary>
        private void OnBackFromInvoiceDetail(InvoicesListViewModel? parentViewModel)
        {
            if (parentViewModel != null)
            {
                CurrentPageTitle = "Invoices";
                CurrentViewModel = parentViewModel;
            }
            else
            {
                NavigateToInvoices();
            }
        }

        /// <summary>
        /// Navigate to Payments list. The same instance is reused so search, dates, and page are preserved.
        /// </summary>
        public void NavigateToPayments()
        {
            _paymentsListViewModel ??= _serviceProvider.GetRequiredService<PaymentsListViewModel>();

            _paymentsListViewModel.PaymentSelected -= OnPaymentSelected;
            _paymentsListViewModel.PaymentSelected += OnPaymentSelected;

            CurrentPageTitle = "Payments";
            CurrentViewModel = _paymentsListViewModel;
            _ = _paymentsListViewModel.LoadPaymentsAsync();
        }

        private void OnPaymentSelected(object? sender, EventArgs e)
        {
            if (_paymentsListViewModel?.SelectedPayment != null)
            {
                NavigateToPaymentDetail(_paymentsListViewModel.SelectedPayment.PaymentId);
            }
        }

        private void NavigateToPaymentDetail(int paymentId)
        {
            _paymentDetailsViewModel = _serviceProvider.GetRequiredService<PaymentDetailsViewModel>();
            _paymentDetailsViewModel.BackRequested += OnBackFromPaymentDetail;
            _paymentDetailsViewModel.InvoiceRequested += OnInvoiceRequestedFromPayment;
            _paymentDetailsViewModel.CustomerRequested += OnCustomerRequestedFromPayment;

            CurrentPageTitle = "Payment Details";
            CurrentViewModel = _paymentDetailsViewModel;

            _ = _paymentDetailsViewModel.LoadPaymentAsync(paymentId);
        }

        private void OnBackFromPaymentDetail(object? sender, EventArgs e)
        {
            NavigateToPayments();
        }

        private void OnInvoiceRequestedFromPayment(object? sender, EventArgs e)
        {
            if (_paymentDetailsViewModel?.PaymentDetail is { } detail)
            {
                var invoiceVm = _serviceProvider.GetRequiredService<InvoiceDetailsViewModel>();
                invoiceVm.BackRequested += OnBackToPaymentDetail;

                CurrentPageTitle = "Invoice Details";
                CurrentViewModel = invoiceVm;

                _ = invoiceVm.LoadInvoiceAsync(detail.InvoiceId);
            }
        }

        private void OnCustomerRequestedFromPayment(object? sender, EventArgs e)
        {
            if (_paymentDetailsViewModel?.PaymentDetail is { } detail)
            {
                var customerVm = _serviceProvider.GetRequiredService<CustomerDetailViewModel>();
                customerVm.BackRequested += OnBackToPaymentDetail;

                CurrentPageTitle = "Customer Details";
                CurrentViewModel = customerVm;

                _ = customerVm.LoadCustomerAsync(detail.CustomerId);
            }
        }

        private void OnBackToPaymentDetail(object? sender, EventArgs e)
        {
            if (_paymentDetailsViewModel != null)
            {
                CurrentPageTitle = "Payment Details";
                CurrentViewModel = _paymentDetailsViewModel;
            }
        }
    }

    /// <summary>
    /// Placeholder ViewModel for future features.
    /// </summary>
    public class PlaceholderViewModel : ObservableObject
    {
        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public PlaceholderViewModel(string message)
        {
            _message = message;
        }
    }
}
