using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace JustTryingCodesignIn
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string MutexName = "CodesignIn_Mutex_v1";    // unique name for your app
        private const string EventName = "CodesignIn_ActivateEvent_v1"; // unique name for signalling
        private static Mutex _mutex;
        private static EventWaitHandle _eventWaitHandle;
        private MainWindow _main;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;

            // Try to create/open a named mutex. createdNew will be true for the first instance.
            _mutex = new Mutex(true, MutexName, out createdNew);

            // Create or open the named EventWaitHandle (AutoReset so each Set() wakes one waiter)
            // Using the same EventWaitHandle instance in both processes is fine.
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);

            if (!createdNew)
            {
                // Another instance is already running:
                // Signal the existing instance to show its window, then exit this instance.
                try
                {
                    _eventWaitHandle.Set();
                }
                catch
                {
                    // ignore errors signaling (existing instance might be shutting down)
                }

                // Shutdown current process (do NOT call base.OnStartup)
                Shutdown();
                return;
            }

            // We are the first instance — create and show main window
            base.OnStartup(e);

            _main = new MainWindow();
            _main.Show();

            // Start a background thread/task to listen for activation signals
            StartActivationListener();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
            catch { /* ignore */ }

            try
            {
                _eventWaitHandle?.Close();
                _eventWaitHandle?.Dispose();
            }
            catch { /* ignore */ }

            base.OnExit(e);
        }

        /// <summary>
        /// Waits for other instances to signal activation, and brings the main window to front.
        /// </summary>
        private void StartActivationListener()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (true)
                {
                    try
                    {
                        // Wait indefinitely for another instance to call Set()
                        _eventWaitHandle.WaitOne();

                        // Invoke UI thread to bring the window to front
                        if (_main != null)
                        {
                            _main.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    _main.BringToFront();
                                }
                                catch { /* ignore any UI errors */ }
                            }));
                        }
                    }
                    catch
                    {
                        // If wait fails, break the loop (perhaps app is shutting down)
                        break;
                    }
                }
            });
        }
    }
}