using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AutoClicker
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource leftClickCts;
        private CancellationTokenSource rightClickCts;
        private int _clickInterval;
        private bool isPickingLocation = false;
        private int clickX, clickY;

        public event PropertyChangedEventHandler PropertyChanged;

        public int ClickInterval 
        {
            get => _clickInterval;
            set {
                if (_clickInterval != value) 
                { 
                    _clickInterval = value;
                    Properties.Settings.Default.clickInterval = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged(nameof(ClickInterval));
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

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

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += Window_SourceInitialized;
            Closing += Window_Closing;
            DataContext = this;
            ClickInterval = Properties.Settings.Default.clickInterval;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void PickLocation_Checked(object sender, RoutedEventArgs e)
        {
            isPickingLocation = true;
            this.MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
            MessageBox.Show("Klikněte kamkoliv na obrazovce pro výběr souřadnic.");
        }

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isPickingLocation)
            {
                if (GetCursorPos(out POINT point))
                {
                    clickX = point.X;
                    clickY = point.Y;
                    txtX.Text = point.X.ToString();
                    txtY.Text = point.Y.ToString();
                }
                isPickingLocation = false;
                this.MouseLeftButtonDown -= MainWindow_MouseLeftButtonDown;
            }
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

        private void BtnStartLeft_Click(object sender, RoutedEventArgs e) => StartLeftClicker();
        private void BtnStopLeft_Click(object sender, RoutedEventArgs e) => StopLeftClicker();
        private void BtnStartRight_Click(object sender, RoutedEventArgs e) => StartRightClicker();
        private void BtnStopRight_Click(object sender, RoutedEventArgs e) => StopRightClicker();

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
            ClickInterval = GetInterval();
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
            ClickInterval = GetInterval();
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
                    if (isPickingLocation == true) {
                        SetCursorPos(clickX, clickY);
                    }
                    mouse_event(downEvent, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(upEvent, 0, 0, 0, UIntPtr.Zero);
                    await Task.Delay(ClickInterval, token);
                }
            }
            catch (TaskCanceledException) { }
        }

        private int GetInterval()
        {
            int.TryParse(txtHours.Text, out int hours);
            int.TryParse(txtMinutes.Text, out int minutes);
            int.TryParse(txtSeconds.Text, out int seconds);
            int.TryParse(txtMilliseconds.Text, out int milliseconds);

            return (hours * 3600000) + (minutes * 60000) + (seconds * 1000) + milliseconds;
        }
    }
}
