using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using MahApps.Metro.Controls;
using Binding = System.Windows.Data.Binding;
using CharacterCasing = System.Windows.Controls.CharacterCasing;
using MenuItem = System.Windows.Controls.MenuItem;

namespace GCodeCorrector.Infrastructure
{
    public class PerMonitorDpiWindow : MetroWindow
    {
        private static bool perMonitorEnabled;

        private List<MenuItem> additionalMenu = new List<MenuItem>();

        private double additionalScaleFactor;

        private Point currentDpi = new Point(96.0, 96.0);

        private IntPtr handle;

        protected HwndSource Source;

        private bool additionalMenuNeedToBeAdded;

        private bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            if (IsWindowsVersionAtLeast(10, 0, 17763))
            {
                var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (IsWindowsVersionAtLeast(10, 0, 18985))
                {
                    attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }

            return false;
        }


        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private static bool invertMode;

        public static void SetInvertColorMode(bool newValue)
        {
            invertMode = newValue;
        }

        private bool IsWindowsVersionAtLeast(uint major, uint minor, uint buildNumber)
        {
            RtlGetVersion(out var result);
            return result.MajorVersion >= major && result.MinorVersion >= minor && result.BuildNumber >= buildNumber;
        }

        public static bool IsLightTheme()
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
        }

        public PerMonitorDpiWindow()
        {
            TitleCharacterCasing = CharacterCasing.Normal;
            var frameworkElementFactory = new FrameworkElementFactory(typeof(Image));
            frameworkElementFactory.SetValue(Image.WidthProperty, 24.0);
            frameworkElementFactory.SetValue(Image.HeightProperty, 24.0);
            frameworkElementFactory.SetValue(Image.MarginProperty, new Thickness(3));
            frameworkElementFactory.SetBinding(Image.SourceProperty, new Binding());
            IconTemplate = new DataTemplate()
            {
                VisualTree = frameworkElementFactory
            };
            if (!perMonitorEnabled) perMonitorEnabled = SetAwareness();
            Loaded += OnLoaded;
            Closing += OnClosing;
            additionalScaleFactor = 1.0;
        }

        public double ScaleFactor { get; private set; }

        public string ScaleResource { get; set; } = "scaleTransf";

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool InsertMenu(IntPtr hMenu, int wPosition, int wFlags, int wIDNewItem,
            string lpNewItem);

        [DllImport("SHCore.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwareness(ProcessDpiAwareness awareness);

        [DllImport("ntdll.dll", SetLastError = true)]
        internal static extern uint RtlGetVersion(out OsVersionInfo versionInformation);

        private bool HighWindows()
        {
            RtlGetVersion(out var result);
            return result.MajorVersion > 6 || (result.MajorVersion == 6 && result.MinorVersion > 3);
        }

        public void AddAdditionalMenu(List<MenuItem> menuItems)
        {
            additionalMenu = menuItems.ToList();
            if (handle == IntPtr.Zero)
            {
                additionalMenuNeedToBeAdded = true;
                return;
            }
            if (menuItems.Any())
            {
                var systemMenuHandle = GetSystemMenu(handle, false);
                for (var index = 0; index < menuItems.Count; index++)
                {
                    var item = menuItems[index];
                    var wFlag = item.Command == null ? 0x400 | 0x800 : 0x400;
                    InsertMenu(systemMenuHandle, (int)item.Tag, wFlag, 1000 + index, (string)item.Header);
                }
            }
        }

        private bool SetAwareness()
        {
            return !HighWindows() || SetProcessDpiAwareness(ProcessDpiAwareness.ProcessPerMonitorDpiAware);
        }

        private void OnClosing(object sender, EventArgs e)
        {
            Closing -= OnClosing;
            Source = (HwndSource)PresentationSource.FromVisual(this);
            Source?.RemoveHook(WindowProcedureHook);
        }

        private IntPtr WindowProcedureHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x02E0)
            {
                var oldDpi = currentDpi;

                currentDpi = new Point
                {
                    X = wParam.ToInt32() >> 16,
                    Y = wParam.ToInt32() & 0x0000FFFF
                };

                OnDPIChanged();

                handled = true;
                return IntPtr.Zero;
            }

            if (msg == 0x112)
            {
                var commandIndex = wParam.ToInt32();
                if (commandIndex >= 1000 && commandIndex <= additionalMenu.Count + 1000)
                {
                    var item = additionalMenu[commandIndex - 1000];
                    if (item != null && item.Command != null)
                        if (item.Command.CanExecute(item.CommandParameter))
                            item.Command.Execute(item.CommandParameter);
                }
            }

            return IntPtr.Zero;
        }

        private void OnDPIChanged()
        {
            Task.Run(() =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    Resources.Remove(ScaleResource);
                    var defaultDpi = 96.0;
                    if (anyQuadroDpiExists)
                    {
                        defaultDpi = 192;
                    }
                    Resources[ScaleResource] = new ScaleTransform(additionalScaleFactor * currentDpi.X / defaultDpi,
                        additionalScaleFactor * currentDpi.Y / defaultDpi);
                    var transform = new ScaleTransform(currentDpi.X / defaultDpi * additionalScaleFactor,
                        currentDpi.Y / defaultDpi * additionalScaleFactor);
                    GetVisualChild(0)?.SetValue(LayoutTransformProperty, transform);
                    InvalidateMeasure();
                    ScaleFactor = currentDpi.X / defaultDpi;
                    DpiChanged?.Invoke(this, ScaleFactor);
                });
            });
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            Source = (HwndSource)PresentationSource.FromVisual(this);
            Source?.AddHook(WindowProcedureHook);
            if (Source != null)
            {
                var set = UseImmersiveDarkMode(Source.Handle, invertMode);
            }

            handle = new WindowInteropHelper(this).Handle;
            if (additionalMenuNeedToBeAdded)
            {
                additionalMenuNeedToBeAdded = false;
                AddAdditionalMenu(additionalMenu);
            }
            if (HighWindows())
            {
                var screen = Screen.FromHandle(handle);
                var screens = Screen.AllScreens;
                var anyQuadroDpiExistsTemp = false;
                foreach (var scr in screens)
                {
                    screen.GetDpi(DpiType.Effective, out var xtemp, out var ytemp);
                    var dpiScaleTemp = xtemp / 96.0;
                    if (dpiScaleTemp >= 2.0)
                    {
                        anyQuadroDpiExistsTemp = true;
                        break;
                    }
                }

                anyQuadroDpiExists = anyQuadroDpiExistsTemp;
                if (anyQuadroDpiExists)
                {
                    currentDpi = new Point(192, 192);
                }

                screen.GetDpi(DpiType.Effective, out var x, out var y);
                currentDpi = new Point(x, y);

                OnDPIChanged();
                if (WindowStartupLocation == WindowStartupLocation.CenterScreen)
                {
                    var defaultDpi = 96.0;
                    if (anyQuadroDpiExists)
                    {
                        defaultDpi = 192;
                    }
                    var scale = currentDpi.X / defaultDpi;
                    var height = ActualHeight;
                    var width = ActualWidth;
                    var left = Left;
                    var top = Top;
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    var taskBarHeight = screen.Bounds.Height - screen.WorkingArea.Height;
                    Left = left - (width / 2.0) * (scale - 1.0) * scale;
                    Top = top - (height / 2.0) * (scale - 1.0) * scale - taskBarHeight / 2.0;
                    if (Top < screen.Bounds.Top)
                    {
                        Top = screen.Bounds.Top;
                    }
                }
            }
        }

        private bool anyQuadroDpiExists = false;

        public void SetAdditionalScaleFactor(double scaleFactor)
        {
            if (Math.Abs(additionalScaleFactor - scaleFactor) > double.Epsilon)
            {
                additionalScaleFactor = scaleFactor;
                OnDPIChanged();
            }
        }

        public event Action<PerMonitorDpiWindow, double> DpiChanged;

        [StructLayout(LayoutKind.Sequential)]
        internal struct OsVersionInfo
        {
            private readonly uint OsVersionInfoSize;

            internal readonly uint MajorVersion;
            internal readonly uint MinorVersion;

            internal readonly uint BuildNumber;

            private readonly uint PlatformId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            private readonly string CSDVersion;
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private enum ProcessDpiAwareness
        {
            ProcessDpiUnaware = 0,
            ProcessSystemDpiAware = 1,
            ProcessPerMonitorDpiAware = 2
        }
    }

    public static class ScreenExtensions
    {
        public static void GetDpi(this Screen screen, DpiType dpiType, out uint dpiX, out uint dpiY)
        {
            var pnt = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
            var mon = MonitorFromPoint(pnt, 2);
            GetDpiForMonitor(mon, dpiType, out dpiX, out dpiY);
        }

        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromPoint([In] System.Drawing.Point pt, [In] uint dwFlags);

        [DllImport("Shcore.dll")]
        private static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX,
            [Out] out uint dpiY);
    }

    public enum DpiType
    {
        Effective = 0,
        Angular = 1,
        Raw = 2
    }
}