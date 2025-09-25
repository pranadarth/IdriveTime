using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ButtonAnimationPoc
{

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        bool _isAnimating;
        Storyboard loadingAnimation;

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            _isAnimating = true;

            loadingAnimation = FindResource("LoadAnime") as Storyboard;
            Storyboard moveAnimation = FindResource("MoveAnime") as Storyboard;

            LoginText.Visibility = Visibility.Collapsed;
            AnimeGrid.Visibility = Visibility.Visible;
            moveAnimation.Begin(this, true);
           // loadingAnimation.Completed += Animation_Completed;
            loadingAnimation.Begin(this, true);

            await Task.Delay(2000); // Simulates login function

            //_isAnimating = false;
            moveAnimation.Stop();
            loadingAnimation.Stop();
            LoginText.Visibility = Visibility.Visible;
            AnimeGrid.Visibility = Visibility.Collapsed;

        }


        private void Animation_Completed(object sender, EventArgs e)
        {
            if (_isAnimating)
            {
                loadingAnimation.Begin(this, true);
            }
            else
            {
                loadingAnimation.Completed -= Animation_Completed;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
               this.DragMove();
        }

        private void ManualBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var point = e.GetPosition(this);
            var screenPoint = PointToScreen(point);
            ShowSystemMenu(hwnd, (int)screenPoint.X, (int)screenPoint.Y);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint WM_SYSCOMMAND = 0x0112;

        private void ShowSystemMenu(IntPtr hwnd, int x, int y)
        {
            IntPtr hMenu = GetSystemMenu(hwnd, false);
            IntPtr cmd = TrackPopupMenu(hMenu, TPM_LEFTALIGN | TPM_RETURNCMD, x, y, 0, hwnd, IntPtr.Zero);

            if (cmd != IntPtr.Zero)
            {
                SendMessage(hwnd, WM_SYSCOMMAND, cmd, IntPtr.Zero);
            }
        }
    }
}
