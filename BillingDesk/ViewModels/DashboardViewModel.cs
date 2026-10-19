using BillingDesk.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace BillingDesk.ViewModels
{
    /// <summary>
    /// ViewModel for the Dashboard view.
    /// Handles loading and displaying billing metrics and recent transactions.
    /// </summary>
    public class DashboardViewModel : ObservableObject
    {
        private readonly DashboardService _dashboardService;
        private readonly ILogger<DashboardViewModel> _logger;

        // Observable properties for metrics
        private decimal _totalOutstandingBalance;
        public decimal TotalOutstandingBalance
        {
            get => _totalOutstandingBalance;
            set => SetProperty(ref _totalOutstandingBalance, value);
        }

        private int _openInvoiceCount;
        public int OpenInvoiceCount
        {
            get => _openInvoiceCount;
            set => SetProperty(ref _openInvoiceCount, value);
        }

        private int _overdueInvoiceCount;
        public int OverdueInvoiceCount
        {
            get => _overdueInvoiceCount;
            set => SetProperty(ref _overdueInvoiceCount, value);
        }

        private decimal _recentPaymentsTotal;
        public decimal RecentPaymentsTotal
        {
            get => _recentPaymentsTotal;
            set => SetProperty(ref _recentPaymentsTotal, value);
        }

        private bool _isLoading;
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

        // Collections
        private List<RecentInvoiceDto> _recentInvoices = [];
        public List<RecentInvoiceDto> RecentInvoices
        {
            get => _recentInvoices;
            set => SetProperty(ref _recentInvoices, value);
        }

        private List<RecentPaymentDto> _recentPayments = [];
        public List<RecentPaymentDto> RecentPayments
        {
            get => _recentPayments;
            set => SetProperty(ref _recentPayments, value);
        }

        public DashboardViewModel(DashboardService dashboardService, ILogger<DashboardViewModel> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        /// <summary>
        /// Loads all dashboard data asynchronously.
        /// </summary>
        public async Task LoadDashboardAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                // Load all metrics in parallel for better performance
                var balanceTask = _dashboardService.GetTotalOutstandingBalanceAsync();
                var openTask = _dashboardService.GetOpenInvoiceCountAsync();
                var overdueTask = _dashboardService.GetOverdueInvoiceCountAsync();
                var paymentsTask = _dashboardService.GetRecentPaymentsTotalAsync();
                var invoicesTask = _dashboardService.GetRecentInvoicesAsync();
                var recentPaymentsTaskData = _dashboardService.GetRecentPaymentsAsync();

                await Task.WhenAll(
                    balanceTask,
                    openTask,
                    overdueTask,
                    paymentsTask,
                    invoicesTask,
                    recentPaymentsTaskData
                );

                TotalOutstandingBalance = await balanceTask;
                OpenInvoiceCount = await openTask;
                OverdueInvoiceCount = await overdueTask;
                RecentPaymentsTotal = await paymentsTask;
                RecentInvoices = await invoicesTask;
                RecentPayments = await recentPaymentsTaskData;

                _logger.LogInformation("Dashboard data loaded successfully. Outstanding: {Outstanding}, Open: {Open}, Overdue: {Overdue}",
                    TotalOutstandingBalance, OpenInvoiceCount, OverdueInvoiceCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                ErrorMessage = "Failed to load dashboard data. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refreshes the dashboard data.
        /// </summary>
        public async Task RefreshDashboardAsync()
        {
            await LoadDashboardAsync();
        }
    }
}

