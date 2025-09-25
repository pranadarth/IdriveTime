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
        private static Mutex _mutex;
        private MainWindow _main;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "CodesignIn_Mutex";
            bool createdNew;
            _mutex = new Mutex(true, mutexName, out createdNew);
            if (!createdNew)
            {
                Shutdown();
                return;
            }

            // base.OnStartup(e);
            _main = new MainWindow();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            base.OnExit(e);
        }
    }
}
