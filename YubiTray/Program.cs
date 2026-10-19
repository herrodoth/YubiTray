using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Management;
using System.Threading;
using System.Windows.Forms;

namespace YubiTray2
{
    static class Program
    {
        // Single instance check using Mutex
        private static Mutex _singleInstanceMutex;
        
        [STAThread]
        static void Main()
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "YubiTray2Mutex", out createdNew);
            
            if (!createdNew)
            {
                MessageBox.Show("YubiTray2 este deja rulat.", "YubiTray2", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
            
            _singleInstanceMutex.ReleaseMutex();
        }
    }

    class TrayApp : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private System.Windows.Forms.Timer _timer;
        private bool? _lastState; // null so the first Refresh always sets an icon
        private Icon _iconConnected;
        private Icon _iconDisconnected;

        // Yubico USB Vendor ID (all YubiKey variants)
        private const string YUBICO_VID = "VID_1050";
        private const string APP_NAME = "YubiTray2";
        private int _refreshInterval = 1500;

        public TrayApp()
        {
            _iconConnected = LoadIconResource("YubiTray2.ico.imageres_106.ico");
            _iconDisconnected = LoadIconResource("YubiTray2.ico.imageres_107.ico");

            // Verify icons loaded
            if (_iconConnected == null || _iconDisconnected == null)
            {
                MessageBox.Show("Iconurile nu au fost incarcate corect!", "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _trayIcon = new NotifyIcon
            {
                Visible = true,
                ContextMenu = BuildMenu()
            };

            _timer = new System.Windows.Forms.Timer { Interval = _refreshInterval };
            _timer.Tick += (s, e) => Refresh();
            _timer.Start();

            Refresh();
        }

        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();
            
            var startupItem = new MenuItem("Pornire la startup", (s, e) => ToggleStartup((MenuItem)s));
            startupItem.Checked = IsStartupEnabled();
            menu.MenuItems.Add(startupItem);
            
            menu.MenuItems.Add(new MenuItem("-")); // Separator
            
            // Refresh Interval Menu
            var refreshMenu = new MenuItem("- Refresh Interval -");
            refreshMenu.Enabled = false;
            menu.MenuItems.Add(refreshMenu);
            
            var fastItem = menu.MenuItems.Add("Fast (0.5s)", (s, e) => SetRefreshInterval(500));
            var normalItem = menu.MenuItems.Add("Normal (1.5s)", (s, e) => SetRefreshInterval(1500));
            var slowItem = menu.MenuItems.Add("Slow (5s)", (s, e) => SetRefreshInterval(5000));
            
            // Mark default as checked
            normalItem.Checked = true;
            
            // Store references for later updates
            _refreshMenuItems = new[] { fastItem, normalItem, slowItem };
            
            menu.MenuItems.Add(new MenuItem("-")); // Separator
            
            menu.MenuItems.Add("Despre", (s, e) => ShowAbout());
            menu.MenuItems.Add(new MenuItem("-")); // Separator
            
            menu.MenuItems.Add("Exit", (s, e) =>
            {
                _timer.Stop();
                _trayIcon.Visible = false;
                Application.Exit();
            });
            return menu;
        }

        private MenuItem[] _refreshMenuItems;

        private void SetRefreshInterval(int ms)
        {
            _refreshInterval = ms;
            _timer.Interval = ms;
            
            // Update checkmarks on menu items
            if (_refreshMenuItems != null)
            {
                _refreshMenuItems[0].Checked = (ms == 500);
                _refreshMenuItems[1].Checked = (ms == 1500);
                _refreshMenuItems[2].Checked = (ms == 5000);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key?.GetValue(APP_NAME) != null;
                }
            }
            catch { return false; }
        }

        private void ToggleStartup(MenuItem item)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (IsStartupEnabled())
                        key.DeleteValue(APP_NAME, false);
                    else
                        key.SetValue(APP_NAME, Application.ExecutablePath);
                }
            }
            catch { }
            
            item.Checked = IsStartupEnabled();
        }

        private void ShowAbout()
        {
            MessageBox.Show($"{APP_NAME}\nVersiunea 1.0\n\nMonitorizează starea YubiKey în timp real.", "Despre YubiTray2", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Refresh()
        {
            try
            {
                int count = GetYubiKeyCount();
                bool connected = count > 0;

                if (connected != _lastState)
                {
                    _lastState = connected;
                    UpdateIcon(connected);
                }

                // Fixed, short text: NotifyIcon.Text throws if it reaches 64 characters
                _trayIcon.Text = !connected ? "YubiKey: NECONECTAT"
                    : count == 1 ? "YubiKey: CONECTAT"
                    : $"YubiKey: CONECTAT ({count})";
            }
            catch { }
        }

        // Counts physical keys: each key appears as one USB parent device plus
        // several interface (&MI_xx) and HID children, so only the parent is counted.
        private int GetYubiKeyCount()
        {
            int count = 0;
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE '%{YUBICO_VID}%'"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            var id = obj["DeviceID"]?.ToString() ?? "";
                            if (id.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase) && id.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) < 0)
                                count++;
                        }
                    }
                }
            }
            catch { }
            return count;
        }

        private Icon LoadIconResource(string iconFile)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            // Resource names are prefixed with the root namespace at build time
            // (e.g. YubiTray2.ico.imageres_106.ico), so match by file name suffix.
            string resource = null;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(iconFile, StringComparison.OrdinalIgnoreCase))
                {
                    resource = name;
                    break;
                }
            }

            using (var stream = resource != null ? assembly.GetManifestResourceStream(resource) : null)
            {
                if (stream == null)
                {
                    return (Icon)SystemIcons.Information.Clone();
                }
                return new Icon(stream);
            }
        }

        private void UpdateIcon(bool connected)
        {
            _trayIcon.Icon = connected 
                ? _iconConnected 
                : _iconDisconnected;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
                _timer = null;
                
                _trayIcon?.Dispose();
                _trayIcon = null;
                
                _iconConnected?.Dispose();
                _iconConnected = null;
                
                _iconDisconnected?.Dispose();
                _iconDisconnected = null;
            }
            base.Dispose(disposing);
        }
    }
}
