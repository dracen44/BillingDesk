using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BillingDesk.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace BillingDesk.ViewModels
{
    /// <summary>
    /// ViewModel for the Invoices list view.
    /// Handles search, filtering, paging, and invoice selection.
    /// </summary>
    public partial class InvoicesListViewModel : ObservableObject
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<InvoicesListViewModel> _logger;
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
                                await LoadInvoicesAsync();
                            }
                        });
                }
            }
        }

        private string _statusFilter = string.Empty;
        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (SetProperty(ref _statusFilter, value))
                {
                    // Reset to page 1 when filter changes
                    ResetToFirstPage();
                    _ = LoadInvoicesAsync();
                }
            }
        }

        private ObservableCollection<InvoiceListItemDto> _invoices = new();
        public ObservableCollection<InvoiceListItemDto> Invoices
        {
            get => _invoices;
            set => SetProperty(ref _invoices, value);
        }

        private InvoiceListItemDto? _selectedInvoice;
        public InvoiceListItemDto? SelectedInvoice
        {
            get => _selectedInvoice;
            set => SetProperty(ref _selectedInvoice, value);
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
                    _ = LoadInvoicesAsync();
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

        public string PageSummary => $"Page {CurrentPageNumber} of {Math.Max(TotalPages, 1)} ({TotalCount} invoices)";

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

        // Event for invoice selection
        public event EventHandler? InvoiceSelected;

        public InvoicesListViewModel(
            IInvoiceService invoiceService,
            ILogger<InvoicesListViewModel> logger)
        {
            _invoiceService = invoiceService;
            _logger = logger;
        }

        /// <summary>
        /// Load invoices asynchronously.
        /// </summary>
        public async Task LoadInvoicesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                var result = await _invoiceService.GetInvoicesAsync(
                    SearchTerm,
                    string.IsNullOrEmpty(StatusFilter) ? null : StatusFilter,
                    CurrentPageNumber,
                    PageSize,
                    cancellationToken);

                TotalCount = result.TotalCount;

                MainThreadHelper.BeginInvokeOnMainThread(() =>
                {
                    Invoices.Clear();
                    foreach (var invoice in result.Items)
                    {
                        Invoices.Add(invoice);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoices");
                ErrorMessage = "Failed to load invoices. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Handle double-click on an invoice row.
        /// </summary>
        [RelayCommand]
        public void SelectInvoice(InvoiceListItemDto? invoice)
        {
            if (invoice != null)
            {
                SelectedInvoice = invoice;
                InvoiceSelected?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Clear all search and filters.
        /// </summary>
        [RelayCommand]
        public void ClearFilters()
        {
            SearchTerm = string.Empty;
            StatusFilter = string.Empty;
            ResetToFirstPage();
            _ = LoadInvoicesAsync();
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
            await LoadInvoicesAsync();
        }
    }
}
