using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Emoji;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static Emoji.Wpf.EmojiData;
using System.Collections.ObjectModel;

namespace PracticeTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private void DisconnectMachine_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}


/*private const int WM_DPICHANGED = 0x02E0;

protected override void OnSourceInitialized(EventArgs e)
{
    base.OnSourceInitialized(e);

    IntPtr hwnd = new WindowInteropHelper(this).Handle;
    HwndSource source = HwndSource.FromHwnd(hwnd);
    if (source != null)
    {
        source.AddHook(WndProc);
    }
}

private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
{
    if (msg == WM_DPICHANGED)
    {
        int dpiX = wParam.ToInt32() & 0xFFFF;
        int dpiY = (wParam.ToInt32() >> 16) & 0xFFFF;
        OnDpiChanged(dpiX, dpiY);
        handled = true;
    }
    return IntPtr.Zero;
}

private void OnDpiChanged(int dpiX, int dpiY)
{
    MessageBox.Show($"DPI changed to {dpiX} x {dpiY}");

}*/
