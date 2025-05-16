using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AutoClicker
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private CancellationTokenSource leftClickCts;
        private CancellationTokenSource rightClickCts;
        private int _clickInterval;
        private bool isPickingLocation = false;
        private int clickX, clickY;
        private bool isSettingLeftHotkey = false;
        private bool isSettingRightHotkey = false;
        private bool _useXButton1ForLeft = false;
        private bool _useXButton2ForRight = false;

        // Hotkey tracking
        private KeyConverter keyConverter = new KeyConverter();
        private Key _leftHotkeyKey;
        private Key _rightHotkeyKey;
        private int leftHotkeyId = 1;
        private int rightHotkeyId = 2;
        private int xButton1HotkeyId = 3;
        private int xButton2HotkeyId = 4;

        public event PropertyChangedEventHandler PropertyChanged;

        public int ClickInterval
        {
            get => _clickInterval;
            set
            {
                if (_clickInterval != value)
                {
                    _clickInterval = value;
                    Properties.Settings.Default.clickInterval = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged(nameof(ClickInterval));
                }
            }
        }

        public Key LeftHotkeyKey
        {
            get => _leftHotkeyKey;
            set
            {
                if (_leftHotkeyKey != value)
                {
                    _leftHotkeyKey = value;
                    Properties.Settings.Default.leftHotkeyKey = (int)value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged(nameof(LeftHotkeyKey));
                }
            }
        }

        public Key RightHotkeyKey
        {
            get => _rightHotkeyKey;
            set
            {
                if (_rightHotkeyKey != value)
                {
                    _rightHotkeyKey = value;
                    Properties.Settings.Default.rightHotkeyKey = (int)value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged(nameof(RightHotkeyKey));
                }
            }
        }

        public bool UseXButton1ForLeft
        {
            get => _useXButton1ForLeft;
            set
            {
                if (_useXButton1ForLeft != value)
                {
                    _useXButton1ForLeft = value;
                    Properties.Settings.Default.useXButton1ForLeft = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged(nameof(UseXButton1ForLeft));
                }
            }
        }

        public bool UseXButton2ForRight
        {
            get => _useXButton2ForRight;
            set
            {
                if (_useXButton2ForRight != value)
                {
                    _useXButton2ForRight = value;
                    Properties.Settings.Default.useXButton2ForRight = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged(nameof(UseXButton2ForRight));
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
        private const uint MOUSEEVENTF_XDOWN = 0x0080;
        private const uint MOUSEEVENTF_XUP = 0x0100;
        private const uint XBUTTON1 = 0x0001;
        private const uint XBUTTON2 = 0x0002;

        // For global hotkeys
        private const int WM_HOTKEY = 0x0312;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;
        private const int MOD_NOREPEAT = 0x4000;
        private const uint HIWORD_MASK = 0xFFFF0000;

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

            // Load all saved settings
            LoadSettings();

            // Update UI text
            txtLeftHotkey.Text = keyConverter.ConvertToString(LeftHotkeyKey);
            txtRightHotkey.Text = keyConverter.ConvertToString(RightHotkeyKey);
        }

        private void LoadSettings()
        {
            // Load click interval
            ClickInterval = Properties.Settings.Default.clickInterval;

            // Load hotkeys with default values if not set
            if (Properties.Settings.Default.leftHotkeyKey == 0)
                LeftHotkeyKey = Key.F5;
            else
                LeftHotkeyKey = (Key)Properties.Settings.Default.leftHotkeyKey;

            if (Properties.Settings.Default.rightHotkeyKey == 0)
                RightHotkeyKey = Key.F6;
            else
                RightHotkeyKey = (Key)Properties.Settings.Default.rightHotkeyKey;

            // Load mouse button settings
            UseXButton1ForLeft = Properties.Settings.Default.useXButton1ForLeft;
            UseXButton2ForRight = Properties.Settings.Default.useXButton2ForRight;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void PickLocation_Checked(object sender, RoutedEventArgs e)
        {
            isPickingLocation = true;
            this.MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
            txtStatus.Text = "Status: Click anywhere to select coordinates";
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
                txtStatus.Text = "Status: Location set";
            }
        }


        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(hWnd);
            source.AddHook(WndProc);

            // Register hotkeys based on loaded settings
            RegisterHotKeys(hWnd);
        }

        private void RegisterHotKeys(IntPtr hWnd)
        {
            // Register keyboard hotkeys
            RegisterHotKey(hWnd, leftHotkeyId, MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(LeftHotkeyKey));
            RegisterHotKey(hWnd, rightHotkeyId, MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(RightHotkeyKey));

            // For mouse buttons, we just use the flag variables as they're handled in WndProc
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            UnregisterAllHotkeys(hWnd);
        }

        private void UnregisterAllHotkeys(IntPtr hWnd)
        {
            // Unregister all hotkeys
            UnregisterHotKey(hWnd, leftHotkeyId);
            UnregisterHotKey(hWnd, rightHotkeyId);
            UnregisterHotKey(hWnd, xButton1HotkeyId);
            UnregisterHotKey(hWnd, xButton2HotkeyId);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_HOTKEY:
                    int id = wParam.ToInt32();
                    if (id == leftHotkeyId)
                    {
                        ToggleLeftClicker();
                        handled = true;
                    }
                    else if (id == rightHotkeyId)
                    {
                        ToggleRightClicker();
                        handled = true;
                    }
                    break;

                case WM_XBUTTONDOWN:
                    int xButton = (int)(((long)lParam & HIWORD_MASK) >> 16);
                    if (xButton == XBUTTON1 && UseXButton1ForLeft)
                    {
                        ToggleLeftClicker();
                        handled = true;
                    }
                    else if (xButton == XBUTTON2 && UseXButton2ForRight)
                    {
                        ToggleRightClicker();
                        handled = true;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        private void ChangeHotkey(bool isLeftButton, Key newKey)
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;

            if (isLeftButton)
            {
                // Unregister the old hotkey
                UnregisterHotKey(hWnd, leftHotkeyId);

                // Update key reference and save to settings
                LeftHotkeyKey = newKey;

                // Register new hotkey
                RegisterHotKey(hWnd, leftHotkeyId, MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(newKey));

                // Update UI
                txtLeftHotkey.Text = keyConverter.ConvertToString(newKey);
            }
            else
            {
                // Unregister the old hotkey
                UnregisterHotKey(hWnd, rightHotkeyId);

                // Update key reference and save to settings
                RightHotkeyKey = newKey;

                // Register new hotkey
                RegisterHotKey(hWnd, rightHotkeyId, MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(newKey));

                // Update UI
                txtRightHotkey.Text = keyConverter.ConvertToString(newKey);
            }

            txtStatus.Text = "Status: Hotkey set";
        }

        private void btnSetLeftHotkey_Click(object sender, RoutedEventArgs e)
        {
            isSettingLeftHotkey = true;
            isSettingRightHotkey = false;
            txtStatus.Text = "Status: Press a key for left click...";
            txtLeftHotkey.Focus();
        }

        private void btnSetRightHotkey_Click(object sender, RoutedEventArgs e)
        {
            isSettingLeftHotkey = false;
            isSettingRightHotkey = true;
            txtStatus.Text = "Status: Press a key for right click...";
            txtRightHotkey.Focus();
        }

        private void txtLeftHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (isSettingLeftHotkey)
            {
                Key newKey = e.Key == Key.System ? e.SystemKey : e.Key;

                if (newKey != Key.Escape && newKey != Key.Tab)
                {
                    ChangeHotkey(true, newKey);
                    isSettingLeftHotkey = false;
                }
                else if (newKey == Key.Escape)
                {
                    isSettingLeftHotkey = false;
                    txtStatus.Text = "Status: Hotkey setting canceled";
                }

                e.Handled = true;
            }
        }

        private void txtRightHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (isSettingRightHotkey)
            {
                Key newKey = e.Key == Key.System ? e.SystemKey : e.Key;

                if (newKey != Key.Escape && newKey != Key.Tab)
                {
                    ChangeHotkey(false, newKey);
                    isSettingRightHotkey = false;
                }
                else if (newKey == Key.Escape)
                {
                    isSettingRightHotkey = false;
                    txtStatus.Text = "Status: Hotkey setting canceled";
                }

                e.Handled = true;
            }
        }

        private void chkXButton1ForLeft_Checked(object sender, RoutedEventArgs e)
        {
            UseXButton1ForLeft = true;
            txtStatus.Text = "Status: Side Button 1 enabled for left click";
        }

        private void chkXButton1ForLeft_Unchecked(object sender, RoutedEventArgs e)
        {
            UseXButton1ForLeft = false;
            txtStatus.Text = "Status: Side Button 1 disabled";
        }

        private void chkXButton2ForRight_Checked(object sender, RoutedEventArgs e)
        {
            UseXButton2ForRight = true;
            txtStatus.Text = "Status: Side Button 2 enabled for right click";
        }

        private void chkXButton2ForRight_Unchecked(object sender, RoutedEventArgs e)
        {
            UseXButton2ForRight = false;
            txtStatus.Text = "Status: Side Button 2 disabled";
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

            // Capture UI values on UI thread before starting the task
            bool useFixedLocation = rbPickLocation.IsChecked == true;
            int x = clickX;
            int y = clickY;

            leftClickCts = new CancellationTokenSource();
            Task.Run(() => AutoClick(MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, leftClickCts.Token, useFixedLocation, x, y));

            txtStatus.Text = "Status: Left clicking active";
        }

        private void StopLeftClicker()
        {
            if (leftClickCts != null)
            {
                leftClickCts.Cancel();
                leftClickCts.Dispose();
                leftClickCts = null;
                txtStatus.Text = "Status: Left clicking stopped";
            }
        }

        private void StartRightClicker()
        {
            ClickInterval = GetInterval();
            if (rightClickCts != null) return;

            // Capture UI values on UI thread before starting the task
            bool useFixedLocation = rbPickLocation.IsChecked == true;
            int x = clickX;
            int y = clickY;

            rightClickCts = new CancellationTokenSource();
            Task.Run(() => AutoClick(MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, rightClickCts.Token, useFixedLocation, x, y));

            txtStatus.Text = "Status: Right clicking active";
        }

        private void StopRightClicker()
        {
            if (rightClickCts != null)
            {
                rightClickCts.Cancel();
                rightClickCts.Dispose();
                rightClickCts = null;
                txtStatus.Text = "Status: Right clicking stopped";
            }
        }

        private async Task AutoClick(uint downEvent, uint upEvent, CancellationToken token, bool useFixedLocation, int x, int y)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Use the captured value instead of accessing UI control directly
                    if (useFixedLocation)
                    {
                        SetCursorPos(x, y);
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