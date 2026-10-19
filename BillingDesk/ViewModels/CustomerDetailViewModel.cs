using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BillingDesk.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using BillingDesk;

namespace BillingDesk.ViewModels
{
    /// <summary>
    /// ViewModel for customer detail view.
    /// Displays customer information, billing summary, and invoice history.
    /// </summary>
    public partial class CustomerDetailViewModel : ObservableObject
    {
        private readonly ICustomerService _customerService;
        private readonly ILogger<CustomerDetailViewModel> _logger;

        private CustomerDetailDto? _customerDetail;
        public CustomerDetailDto? CustomerDetail
        {
            get => _customerDetail;
            set => SetProperty(ref _customerDetail, value);
        }

        private ObservableCollection<CustomerInvoiceDto> _invoices = new();
        public ObservableCollection<CustomerInvoiceDto> Invoices
        {
            get => _invoices;
            set => SetProperty(ref _invoices, value);
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        // Event for back navigation
        public event EventHandler? BackRequested;

        public CustomerDetailViewModel(
            ICustomerService customerService,
            ILogger<CustomerDetailViewModel> logger)
        {
            _customerService = customerService;
            _logger = logger;
        }

        /// <summary>
        /// Load customer detail and invoice history.
        /// </summary>
        public async Task LoadCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                // Load customer detail and invoices in parallel
                var detailTask = _customerService.GetCustomerDetailAsync(customerId, cancellationToken);
                var invoicesTask = _customerService.GetCustomerInvoicesAsync(customerId, cancellationToken);

                await Task.WhenAll(detailTask, invoicesTask);

                CustomerDetail = await detailTask;
                var invoices = await invoicesTask;

                MainThreadHelper.BeginInvokeOnMainThread(() =>
                {
                    Invoices.Clear();
                    foreach (var invoice in invoices)
                    {
                        Invoices.Add(invoice);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer detail");
                ErrorMessage = "Failed to load customer details. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Go back to customers list.
        /// </summary>
        [RelayCommand]
        public void GoBack()
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Refresh customer data.
        /// </summary>
        [RelayCommand]
        public async Task Refresh()
        {
            if (CustomerDetail != null)
            {
                await LoadCustomerAsync(CustomerDetail.CustomerId);
            }
        }
    }
}
