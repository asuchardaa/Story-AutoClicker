using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace AutoClicker
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource leftClickCts;
        private CancellationTokenSource rightClickCts;
        private int clickInterval = 100;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const uint MOUSEEVENTF_RIGHTUP = 0x10;

        // Pro globální klávesové zkratky
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_NOREPEAT = 0x4000;
        private const int HOTKEY_ID_F5 = 1;
        private const int HOTKEY_ID_F6 = 2;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += Window_SourceInitialized;
            Closing += Window_Closing;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(hWnd);
            source.AddHook(WndProc);

            RegisterHotKey(hWnd, HOTKEY_ID_F5, MOD_NOREPEAT, 0x74); // F5
            RegisterHotKey(hWnd, HOTKEY_ID_F6, MOD_NOREPEAT, 0x75); // F6
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hWnd, HOTKEY_ID_F5);
            UnregisterHotKey(hWnd, HOTKEY_ID_F6);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_F5)
                {
                    ToggleLeftClicker();
                    handled = true;
                }
                else if (id == HOTKEY_ID_F6)
                {
                    ToggleRightClicker();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void ToggleLeftClicker()
        {
            if (leftClickCts == null)
                StartLeftClicker();
            else
                StopLeftClicker();
        }

        private void ToggleRightClicker()
        {
            if (rightClickCts == null)
                StartRightClicker();
            else
                StopRightClicker();
        }

        private void StartLeftClicker()
        {
            clickInterval = 100;
            if (leftClickCts != null) return;
            leftClickCts = new CancellationTokenSource();
            Task.Run(() => AutoClick(MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, leftClickCts.Token));
        }

        private void StopLeftClicker()
        {
            if (leftClickCts != null)
            {
                leftClickCts.Cancel();
                leftClickCts.Dispose();
                leftClickCts = null;
            }
        }

        private void StartRightClicker()
        {
            clickInterval = 100;
            if (rightClickCts != null) return;
            rightClickCts = new CancellationTokenSource();
            Task.Run(() => AutoClick(MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, rightClickCts.Token));
        }

        private void StopRightClicker()
        {
            if (rightClickCts != null)
            {
                rightClickCts.Cancel();
                rightClickCts.Dispose();
                rightClickCts = null;
            }
        }

        private async Task AutoClick(uint downEvent, uint upEvent, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    mouse_event(downEvent, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(upEvent, 0, 0, 0, UIntPtr.Zero);
                    await Task.Delay(clickInterval, token);
                }
            }
            catch (TaskCanceledException) { }
        }
    }
}
