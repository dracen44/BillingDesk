using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BillingDesk.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace BillingDesk.ViewModels
{
    /// <summary>
    /// ViewModel for the Payments list view.
    /// Handles search, payment date filtering, paging, and payment selection.
    /// </summary>
    public partial class PaymentsListViewModel : ObservableObject
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentsListViewModel> _logger;
        private CancellationTokenSource? _searchCancellationTokenSource;
        private Task? _searchDebounceTask;

        private string _searchTerm = string.Empty;
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value))
                {
                    // Reset to page 1 when search changes; the debounced load below runs the query
                    ResetToFirstPage();

                    // Debounce search - cancel previous search if still pending
                    _searchCancellationTokenSource?.Cancel();
                    _searchCancellationTokenSource = new CancellationTokenSource();
                    var cts = _searchCancellationTokenSource;

                    // Schedule search after debounce delay
                    _searchDebounceTask = Task.Delay(300, cts.Token)
                        .ContinueWith(async _ =>
                        {
                            if (!cts.Token.IsCancellationRequested)
                            {
                                await LoadPaymentsAsync();
                            }
                        });
                }
            }
        }

        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                if (SetProperty(ref _fromDate, value))
                {
                    ResetToFirstPage();
                    _ = LoadPaymentsAsync();
                }
            }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set
            {
                if (SetProperty(ref _toDate, value))
                {
                    ResetToFirstPage();
                    _ = LoadPaymentsAsync();
                }
            }
        }

        private ObservableCollection<PaymentListItemDto> _payments = new();
        public ObservableCollection<PaymentListItemDto> Payments
        {
            get => _payments;
            set => SetProperty(ref _payments, value);
        }

        private PaymentListItemDto? _selectedPayment;
        public PaymentListItemDto? SelectedPayment
        {
            get => _selectedPayment;
            set => SetProperty(ref _selectedPayment, value);
        }

        private int _totalCount = 0;
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (SetProperty(ref _totalCount, value))
                {
                    NotifyPagingChanged();
                }
            }
        }

        private int _currentPageNumber = 1;
        public int CurrentPageNumber
        {
            get => _currentPageNumber;
            set
            {
                if (SetProperty(ref _currentPageNumber, value))
                {
                    NotifyPagingChanged();
                    _ = LoadPaymentsAsync();
                }
            }
        }

        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (SetProperty(ref _pageSize, value))
                {
                    NotifyPagingChanged();
                }
            }
        }

        public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

        public bool CanGoPrevious => CurrentPageNumber > 1;

        public bool CanGoNext => CurrentPageNumber < TotalPages;

        public string PageSummary => $"Page {CurrentPageNumber} of {Math.Max(TotalPages, 1)} ({TotalCount} payments)";

        private void NotifyPagingChanged()
        {
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(PageSummary));
        }

        private void ResetToFirstPage()
        {
            _currentPageNumber = 1;
            OnPropertyChanged(nameof(CurrentPageNumber));
            NotifyPagingChanged();
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

        private bool _hasNoResults = false;
        public bool HasNoResults
        {
            get => _hasNoResults;
            set => SetProperty(ref _hasNoResults, value);
        }

        // Event for payment selection
        public event EventHandler? PaymentSelected;

        public PaymentsListViewModel(
            IPaymentService paymentService,
            ILogger<PaymentsListViewModel> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        /// <summary>
        /// Load payments asynchronously.
        /// </summary>
        public async Task LoadPaymentsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                HasNoResults = false;

                if (FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date)
                {
                    ErrorMessage = "From date must be on or before To date.";
                    TotalCount = 0;
                    MainThreadHelper.BeginInvokeOnMainThread(() => Payments.Clear());
                    return;
                }

                var result = await _paymentService.GetPaymentsAsync(
                    SearchTerm,
                    FromDate,
                    ToDate,
                    CurrentPageNumber,
                    PageSize,
                    cancellationToken);

                TotalCount = result.TotalCount;

                MainThreadHelper.BeginInvokeOnMainThread(() =>
                {
                    Payments.Clear();
                    foreach (var payment in result.Items)
                    {
                        Payments.Add(payment);
                    }

                    HasNoResults = result.Items.Count == 0;
                });
            }
            catch (OperationCanceledException)
            {
                // A newer search superseded this request
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payments");
                ErrorMessage = "Failed to load payments. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Handle double-click on a payment row.
        /// </summary>
        [RelayCommand]
        public void SelectPayment(PaymentListItemDto? payment)
        {
            if (payment != null)
            {
                SelectedPayment = payment;
                PaymentSelected?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Clear all search and date filters.
        /// </summary>
        [RelayCommand]
        public void ClearFilters()
        {
            _searchCancellationTokenSource?.Cancel();
            _searchTerm = string.Empty;
            OnPropertyChanged(nameof(SearchTerm));
            _fromDate = null;
            OnPropertyChanged(nameof(FromDate));
            _toDate = null;
            OnPropertyChanged(nameof(ToDate));
            ResetToFirstPage();
            _ = LoadPaymentsAsync();
        }

        /// <summary>
        /// Navigate to next page.
        /// </summary>
        [RelayCommand]
        public void NextPage()
        {
            if (CurrentPageNumber < TotalPages)
            {
                CurrentPageNumber++;
            }
        }

        /// <summary>
        /// Navigate to previous page.
        /// </summary>
        [RelayCommand]
        public void PreviousPage()
        {
            if (CurrentPageNumber > 1)
            {
                CurrentPageNumber--;
            }
        }

        /// <summary>
        /// Refresh the current page.
        /// </summary>
        [RelayCommand]
        public async Task Refresh()
        {
            await LoadPaymentsAsync();
        }
    }
}
