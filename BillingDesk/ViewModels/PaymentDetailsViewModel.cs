using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BillingDesk.Services;
using Microsoft.Extensions.Logging;

namespace BillingDesk.ViewModels
{
    /// <summary>
    /// ViewModel for payment detail view.
    /// Displays payment information and the related invoice and customer financial summary.
    /// </summary>
    public partial class PaymentDetailsViewModel : ObservableObject
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentDetailsViewModel> _logger;

        private PaymentDetailDto? _paymentDetail;
        public PaymentDetailDto? PaymentDetail
        {
            get => _paymentDetail;
            set => SetProperty(ref _paymentDetail, value);
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

        // Events for navigation
        public event EventHandler? BackRequested;
        public event EventHandler? InvoiceRequested;
        public event EventHandler? CustomerRequested;

        public PaymentDetailsViewModel(
            IPaymentService paymentService,
            ILogger<PaymentDetailsViewModel> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        /// <summary>
        /// Load payment detail and related invoice totals.
        /// </summary>
        public async Task LoadPaymentAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                PaymentDetail = null;

                var detail = await _paymentService.GetPaymentDetailAsync(paymentId, cancellationToken);

                if (detail == null)
                {
                    ErrorMessage = "Payment not found.";
                    return;
                }

                PaymentDetail = detail;
            }
            catch (OperationCanceledException)
            {
                // Request was cancelled
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payment detail");
                ErrorMessage = "Failed to load payment details. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Go back to payments list.
        /// </summary>
        [RelayCommand]
        public void GoBack()
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Open the related invoice details.
        /// </summary>
        [RelayCommand]
        public void ViewInvoice()
        {
            if (PaymentDetail != null)
            {
                InvoiceRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Open the related customer details.
        /// </summary>
        [RelayCommand]
        public void ViewCustomer()
        {
            if (PaymentDetail != null)
            {
                CustomerRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Refresh payment data.
        /// </summary>
        [RelayCommand]
        public async Task Refresh()
        {
            if (PaymentDetail != null)
            {
                await LoadPaymentAsync(PaymentDetail.PaymentId);
            }
        }
    }
}
