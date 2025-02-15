using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;
using System.Linq;

namespace QuinAutoRun
{
    public partial class MainWindow : Window
    {
        private const int HotkeyId = 9000;
        private bool isRunning = false;
        private bool isListening = true;
        private Stopwatch toggleStopwatch = new Stopwatch();
        private LowLevelMouseProc _mouseProc;
        private LowLevelKeyboardProc _keyboardProc;
        private IntPtr _mouseHookID = IntPtr.Zero;
        private IntPtr _keyboardHookID = IntPtr.Zero;

        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_F11 = 0x7A;
        private const int VK_SHIFT = 0x10;
        private const int VK_W = 0x57;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private Dictionary<string, string> keybinds = new Dictionary<string, string>();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            _mouseProc = MouseHookCallback;
            _keyboardProc = KeyboardHookCallback;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _mouseHookID = SetMouseHook(_mouseProc);
            _keyboardHookID = SetKeyboardHook(_keyboardProc);
            toggleStopwatch.Start();
            LoadKeybinds();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UnhookWindowsHookEx(_mouseHookID);
            UnhookWindowsHookEx(_keyboardHookID);
        }

        private IntPtr SetMouseHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr SetKeyboardHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_MBUTTONDOWN && isListening)
            {
                if (toggleStopwatch.ElapsedMilliseconds >= 500)
                {
                    ToggleKeyHolding();
                    toggleStopwatch.Restart();
                }
            }
            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);
                string keyString = key.ToString();
                if (Keyboard.Modifiers != ModifierKeys.None)
                {
                    keyString = $"{Keyboard.Modifiers} + {keyString}";
                }

                if (vkCode == VK_F11)
                {
                    isListening = !isListening;
                    UpdateStatus(isListening ? "LISTENING" : "NOT LISTENING", isListening ? Colors.Green : Colors.Gray);
                }
                else if (isListening && keybinds.ContainsValue(keyString))
                {
                    // Simulate the corresponding key press
                    string keybindName = keybinds.FirstOrDefault(x => x.Value == keyString).Key;
                    byte targetKey = GetTargetKey(keybindName);
                    SimulateKeyPress(targetKey);

                    // Suppress the original key combination
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
        }

        private void SimulateKeyPress(byte key)
        {
            IntPtr hwnd = FindWindow(null, "TheQuinfall");
            if (hwnd == IntPtr.Zero)
            {
                MessageBox.Show("Game not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Bring the game window to the foreground
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);

            keybd_event(key, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(50); // Add a small delay to simulate human key press duration
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private byte GetTargetKey(string keybindName)
        {
            switch (keybindName)
            {
                case "KeybindF1": return 0x70; // F1
                case "KeybindF2": return 0x71; // F2
                case "KeybindF3": return 0x72; // F3
                case "KeybindF4": return 0x73; // F4
                case "KeybindF5": return 0x74; // F5
                case "KeybindF6": return 0x75; // F6
                case "KeybindF7": return 0x76; // F7
                case "KeybindF8": return 0x77; // F8
                case "KeybindF9": return 0x78; // F9
                case "KeybindF10": return 0x79; // F10
                case "Keybind6": return 0x36; // 6
                case "Keybind7": return 0x37; // 7
                case "Keybind8": return 0x38; // 8
                case "Keybind9": return 0x39; // 9
                case "Keybind0": return 0x30; // 0
                default: return 0;
            }
        }

        private void KeybindTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                if (e.Key == Key.Escape)
                {
                    textBox.Text = string.Empty;
                    keybinds.Remove(textBox.Name);
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    Keyboard.ClearFocus();
                    e.Handled = true;
                    return;
                }

                string key = e.Key.ToString();
                if (Keyboard.Modifiers != ModifierKeys.None)
                {
                    key = $"{Keyboard.Modifiers} + {key}";
                }

                textBox.Text = key;
                keybinds[textBox.Name] = key; // Store the key with modifiers in the dictionary
                e.Handled = true;
            }
        }

        private void ToggleKeyHolding()
        {
            if (isRunning)
            {
                StopKeyHolding();
            }
            else
            {
                StartKeyHolding();
            }
        }

        private void StartKeyHolding()
        {
            IntPtr hwnd = FindWindow(null, "TheQuinfall");
            if (hwnd == IntPtr.Zero)
            {
                MessageBox.Show("Game not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            isRunning = true;
            UpdateStatus("RUNNING", Colors.Green);

            // Simulate key press
            keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
            keybd_event(VK_W, 0, 0, UIntPtr.Zero);
        }

        private void StopKeyHolding()
        {
            isRunning = false;
            UpdateStatus("STOPPED", Colors.Red);

            // Simulate key release
            keybd_event(VK_W, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void UpdateStatus(string status, Color color)
        {
            StatusTextBlock.Text = status;
            StatusTextBlock.Background = new SolidColorBrush(color);
            StatusBorder.Background = new SolidColorBrush(color);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveKeybinds();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            keybinds.Clear();
            foreach (var key in new List<string> { "KeybindF1", "KeybindF2", "KeybindF3", "KeybindF4", "KeybindF5", "KeybindF6", "KeybindF7", "KeybindF8", "KeybindF9", "KeybindF10", "Keybind6", "Keybind7", "Keybind8", "Keybind9", "Keybind0" })
            {
                var textBox = FindName(key) as TextBox;
                if (textBox != null)
                {
                    textBox.Text = string.Empty;
                }
            }
        }

        private void SaveKeybinds()
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keybindings.txt");
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (var keybind in keybinds)
                {
                    writer.WriteLine($"{keybind.Key}:{keybind.Value}");
                }
            }
            MessageBox.Show("Keybinds saved successfully.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadKeybinds()
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keybindings.txt");
            if (File.Exists(filePath))
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var parts = line.Split(':');
                        if (parts.Length == 2)
                        {
                            keybinds[parts[0]] = parts[1];
                            var textBox = FindName(parts[0]) as TextBox;
                            if (textBox != null)
                            {
                                textBox.Text = parts[1];
                            }
                        }
                    }
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    }
}




