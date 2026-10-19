using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BillingDesk.Services;
using BillingDesk;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace BillingDesk.ViewModels
{
    /// <summary>
    /// ViewModel for the Customers list view.
    /// Handles search, paging, and customer selection.
    /// </summary>
    public partial class CustomersViewModel : ObservableObject
    {
        private readonly ICustomerService _customerService;
        private readonly ILogger<CustomersViewModel> _logger;
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
                    // Reset to page 1 when search changes
                    CurrentPageNumber = 1;

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
                                await LoadCustomersAsync();
                            }
                        });
                }
            }
        }

        private ObservableCollection<CustomerListItemDto> _customers = new();
        public ObservableCollection<CustomerListItemDto> Customers
        {
            get => _customers;
            set => SetProperty(ref _customers, value);
        }

        private int _totalCount = 0;
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (SetProperty(ref _totalCount, value))
                {
                    // Notify that computed TotalPages has changed
                    OnPropertyChanged(nameof(TotalPages));
                }
            }
        }

        private int _currentPageNumber = 1;
        public int CurrentPageNumber
        {
            get => _currentPageNumber;
            set => SetProperty(ref _currentPageNumber, value);
        }

        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (SetProperty(ref _pageSize, value))
                {
                    // Notify that computed TotalPages has changed
                    OnPropertyChanged(nameof(TotalPages));
                }
            }
        }

        public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

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

        private CustomerListItemDto? _selectedCustomer;
        public CustomerListItemDto? SelectedCustomer
        {
            get => _selectedCustomer;
            set => SetProperty(ref _selectedCustomer, value);
        }

        // Event for when customer is selected (for navigation)
        public event EventHandler? CustomerSelected;

        public CustomersViewModel(
            ICustomerService customerService,
            ILogger<CustomersViewModel> logger)
        {
            _customerService = customerService;
            _logger = logger;
        }

        /// <summary>
        /// Initial load of customers.
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadCustomersAsync();
        }

        /// <summary>
        /// Load customers from service.
        /// </summary>
        private async Task LoadCustomersAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                var result = await _customerService.GetCustomersAsync(
                    searchTerm: string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm,
                    pageNumber: CurrentPageNumber,
                    pageSize: PageSize);

                MainThreadHelper.InvokeOnMainThread(() =>
                {
                    Customers.Clear();
                    foreach (var customer in result.Items)
                    {
                        Customers.Add(customer);
                    }

                    TotalCount = result.TotalCount;
                });
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled, ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customers");
                ErrorMessage = "Failed to load customers. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Clear search and reset to first page.
        /// </summary>
        [RelayCommand]
        public void ClearSearch()
        {
            SearchTerm = string.Empty;
        }

        /// <summary>
        /// Go to previous page.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanGoPrevious))]
        public async Task PreviousPage()
        {
            if (CurrentPageNumber > 1)
            {
                CurrentPageNumber--;
                await LoadCustomersAsync();
            }
        }

        private bool CanGoPrevious() => CurrentPageNumber > 1;

        /// <summary>
        /// Go to next page.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanGoNext))]
        public async Task NextPage()
        {
            if (CurrentPageNumber < TotalPages)
            {
                CurrentPageNumber++;
                await LoadCustomersAsync();
            }
        }

        private bool CanGoNext() => CurrentPageNumber < TotalPages;

        /// <summary>
        /// Refresh customer data on current page.
        /// </summary>
        [RelayCommand]
        public async Task Refresh()
        {
            await LoadCustomersAsync();
        }

        /// <summary>
        /// Handle customer selection and navigate to detail view.
        /// </summary>
        public void SelectCustomer(CustomerListItemDto customer)
        {
            SelectedCustomer = customer;
            CustomerSelected?.Invoke(this, EventArgs.Empty);
        }
    }
}
