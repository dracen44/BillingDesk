using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BillingDesk.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;

namespace BillingDesk.ViewModels
{
    /// <summary>
    /// ViewModel for invoice detail view.
    /// Displays invoice information, line items, and payment history, and records new payments.
    /// </summary>
    public partial class InvoiceDetailsViewModel : ObservableObject
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<InvoiceDetailsViewModel> _logger;

        private InvoiceDetailDto? _invoiceDetail;
        public InvoiceDetailDto? InvoiceDetail
        {
            get => _invoiceDetail;
            set
            {
                if (SetProperty(ref _invoiceDetail, value))
                {
                    RecordPaymentCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<InvoiceLineItemDto> _lineItems = new();
        public ObservableCollection<InvoiceLineItemDto> LineItems
        {
            get => _lineItems;
            set => SetProperty(ref _lineItems, value);
        }

        private ObservableCollection<InvoicePaymentDto> _payments = new();
        public ObservableCollection<InvoicePaymentDto> Payments
        {
            get => _payments;
            set => SetProperty(ref _payments, value);
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    RecordPaymentCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private bool _isPaymentFormVisible = false;
        public bool IsPaymentFormVisible
        {
            get => _isPaymentFormVisible;
            set => SetProperty(ref _isPaymentFormVisible, value);
        }

        private bool _isSaving = false;
        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (SetProperty(ref _isSaving, value))
                {
                    RecordPaymentCommand.NotifyCanExecuteChanged();
                    SubmitPaymentCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _paymentAmountText = string.Empty;
        public string PaymentAmountText
        {
            get => _paymentAmountText;
            set => SetProperty(ref _paymentAmountText, value);
        }

        private DateTime? _paymentDate;
        public DateTime? PaymentDate
        {
            get => _paymentDate;
            set => SetProperty(ref _paymentDate, value);
        }

        private string? _paymentFormError;
        public string? PaymentFormError
        {
            get => _paymentFormError;
            set => SetProperty(ref _paymentFormError, value);
        }

        // Event for back navigation
        public event EventHandler? BackRequested;

        public InvoiceDetailsViewModel(
            IInvoiceService invoiceService,
            IPaymentService paymentService,
            ILogger<InvoiceDetailsViewModel> logger)
        {
            _invoiceService = invoiceService;
            _paymentService = paymentService;
            _logger = logger;
        }

        /// <summary>
        /// Load invoice detail and related data.
        /// </summary>
        public async Task LoadInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                var detail = await _invoiceService.GetInvoiceDetailAsync(invoiceId, cancellationToken);

                if (detail == null)
                {
                    ClearLoadedInvoice();
                    ErrorMessage = "Invoice not found.";
                    return;
                }

                InvoiceDetail = detail;

                MainThreadHelper.BeginInvokeOnMainThread(() =>
                {
                    LineItems.Clear();
                    foreach (var item in detail.LineItems)
                    {
                        LineItems.Add(item);
                    }

                    Payments.Clear();
                    foreach (var payment in detail.Payments)
                    {
                        Payments.Add(payment);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice detail");
                ClearLoadedInvoice();
                ErrorMessage = "Failed to load invoice details. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ClearLoadedInvoice()
        {
            InvoiceDetail = null;
            IsPaymentFormVisible = false;
            PaymentFormError = null;
            MainThreadHelper.BeginInvokeOnMainThread(() =>
            {
                LineItems.Clear();
                Payments.Clear();
            });
        }

        /// <summary>
        /// Go back to invoices list.
        /// </summary>
        [RelayCommand]
        public void GoBack()
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Refresh invoice data.
        /// </summary>
        [RelayCommand]
        public async Task Refresh()
        {
            if (InvoiceDetail != null)
            {
                await LoadInvoiceAsync(InvoiceDetail.InvoiceId);
            }
        }

        private bool CanRecordPayment => InvoiceDetail is { Balance: > 0m } && !IsLoading && !IsSaving;

        /// <summary>
        /// Opens the payment entry form, pre-filled with the remaining balance and today's date.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRecordPayment))]
        private void RecordPayment()
        {
            PaymentAmountText = InvoiceDetail!.Balance.ToString("F2", CultureInfo.CurrentCulture);
            PaymentDate = DateTime.Today;
            PaymentFormError = null;
            IsPaymentFormVisible = true;
        }

        [RelayCommand]
        private void CancelPayment()
        {
            PaymentFormError = null;
            IsPaymentFormVisible = false;
        }

        private bool CanSubmitPayment => !IsSaving;

        /// <summary>
        /// Validates input, asks for explicit confirmation, then saves the payment.
        /// The service re-checks the balance under a row lock, so the UI values are never trusted for the final decision.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSubmitPayment))]
        private async Task SubmitPaymentAsync()
        {
            if (InvoiceDetail == null)
            {
                return;
            }

            var validationError = ValidatePaymentInput(out var amount, out var paymentDate);
            if (validationError != null)
            {
                PaymentFormError = validationError;
                return;
            }

            var confirmed = MessageBox.Show(
                $"Record a payment of {amount:C2} against invoice {InvoiceDetail.InvoiceId} for {InvoiceDetail.CustomerName}?\n\n" +
                $"Payment date: {paymentDate:yyyy-MM-dd}\n" +
                $"Remaining balance after payment: {InvoiceDetail.Balance - amount:C2}",
                "Confirm Payment",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;

            if (!confirmed)
            {
                return;
            }

            var invoiceId = InvoiceDetail.InvoiceId;
            bool saved;

            IsSaving = true;
            try
            {
                var result = await _paymentService.RecordPaymentAsync(invoiceId, amount, paymentDate);
                saved = result.IsSuccess;
                PaymentFormError = result.ErrorMessage;
            }
            finally
            {
                IsSaving = false;
            }

            if (!saved)
            {
                return;
            }

            IsPaymentFormVisible = false;
            PaymentAmountText = string.Empty;
            PaymentDate = null;
            await LoadInvoiceAsync(invoiceId);
        }

        private string? ValidatePaymentInput(out decimal amount, out DateTime paymentDate)
        {
            amount = 0m;
            paymentDate = default;

            if (InvoiceDetail == null)
            {
                return "Invoice details are not loaded.";
            }

            if (!decimal.TryParse(PaymentAmountText, NumberStyles.Currency, CultureInfo.CurrentCulture, out amount))
            {
                return "Enter a valid payment amount.";
            }

            if (amount <= 0m)
            {
                return "Payment amount must be greater than zero.";
            }

            if (decimal.Round(amount, 2) != amount)
            {
                return "Payment amount can have at most two decimal places.";
            }

            if (amount > InvoiceDetail.Balance)
            {
                return $"Payment amount cannot exceed the remaining balance of {InvoiceDetail.Balance:C2}.";
            }

            if (PaymentDate == null)
            {
                return "Enter a valid payment date.";
            }

            paymentDate = PaymentDate.Value.Date;

            if (paymentDate < InvoiceDetail.InvoiceDate.Date)
            {
                return $"Payment date cannot be before the invoice date ({InvoiceDetail.InvoiceDate:yyyy-MM-dd}).";
            }

            return null;
        }
    }
}
