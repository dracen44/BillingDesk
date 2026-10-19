using System.Windows;

namespace BillingDesk
{
    /// <summary>
    /// Helper class for invoking code on the main UI thread.
    /// </summary>
    public static class MainThreadHelper
    {
        /// <summary>
        /// Invokes an action on the main UI thread synchronously.
        /// </summary>
        public static void InvokeOnMainThread(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null && mainWindow.Dispatcher != null)
            {
                if (mainWindow.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    mainWindow.Dispatcher.Invoke(action);
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Invokes an action on the main UI thread asynchronously.
        /// </summary>
        public static void BeginInvokeOnMainThread(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null && mainWindow.Dispatcher != null)
            {
                if (mainWindow.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    mainWindow.Dispatcher.BeginInvoke(action);
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Invokes a function on the main UI thread synchronously.
        /// </summary>
        public static T InvokeOnMainThread<T>(Func<T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null && mainWindow.Dispatcher != null)
            {
                if (mainWindow.Dispatcher.CheckAccess())
                {
                    return func();
                }
                else
                {
                    return (T)mainWindow.Dispatcher.Invoke(func);
                }
            }
            else
            {
                return func();
            }
        }

        /// <summary>
        /// Invokes a function on the main UI thread asynchronously.
        /// </summary>
        public static void BeginInvokeOnMainThread<T>(Func<T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null && mainWindow.Dispatcher != null)
            {
                if (mainWindow.Dispatcher.CheckAccess())
                {
                    func();
                }
                else
                {
                    mainWindow.Dispatcher.BeginInvoke(func);
                }
            }
            else
            {
                func();
            }
        }
    }
}
