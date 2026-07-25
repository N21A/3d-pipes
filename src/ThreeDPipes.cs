using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("3D Pipes Screensaver")]
[assembly: AssemblyDescription("Procedurally generated classic 3D pipes screensaver for Windows")]
[assembly: AssemblyCompany("3D Pipes")]
[assembly: AssemblyProduct("3D Pipes Screensaver")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]

namespace ThreeDPipesScreensaver
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                LaunchMode mode = CommandLine.Parse(args);
                if (mode.Kind == LaunchKind.Configure)
                {
                    using (ConfigForm config = new ConfigForm())
                    {
                        config.ShowDialog();
                    }
                    return;
                }

                ScreensaverSettings settings = ScreensaverSettings.Load();
                using (PipesForm form = new PipesForm(mode, settings))
                {
                    Application.Run(form);
                }
            }
            catch (Exception ex)
            {
                bool silent = args != null && args.Length > 0 &&
                              args[0].StartsWith("/s", StringComparison.OrdinalIgnoreCase);
                if (!silent)
                {
                    MessageBox.Show(
                        "3D Pipes could not start.\n\n" + ex.Message,
                        "3D Pipes Screensaver",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }
    }

    internal enum LaunchKind
    {
        FullScreen,
        Preview,
        Configure,
        Windowed
    }

    internal sealed class LaunchMode
    {
        public LaunchKind Kind;
        public IntPtr PreviewParent;
    }

    internal static class CommandLine
    {
        public static LaunchMode Parse(string[] args)
        {
            LaunchMode result = new LaunchMode
            {
                Kind = LaunchKind.FullScreen,
                PreviewParent = IntPtr.Zero
            };

            if (args == null || args.Length == 0)
            {
                return result;
            }

            string first = (args[0] ?? string.Empty).Trim().ToLowerInvariant();
            if (first.StartsWith("/c") || first.StartsWith("-c"))
            {
                result.Kind = LaunchKind.Configure;
                return result;
            }

            if (first.StartsWith("/w") || first.StartsWith("-w"))
            {
                result.Kind = LaunchKind.Windowed;
                return result;
            }

            if (first.StartsWith("/p") || first.StartsWith("-p"))
            {
                result.Kind = LaunchKind.Preview;
                string handleText = string.Empty;
                int colon = first.IndexOf(':');
                if (colon >= 0 && colon + 1 < first.Length)
                {
                    handleText = first.Substring(colon + 1);
                }
                else if (args.Length > 1)
                {
                    handleText = args[1];
                }

                long handleValue;
                if (long.TryParse(handleText, NumberStyles.Integer, CultureInfo.InvariantCulture, out handleValue))
                {
                    result.PreviewParent = new IntPtr(handleValue);
                }
            }

            return result;
        }
    }

    internal sealed class ScreensaverSettings
    {
        private const string RegistryPath = @"Software\ThreeDPipesScreensaver";

        public int GrowthDurationMs = 120;
        public int PipeCount = 8;
        public int MaxSegments = 500;
        public bool KeepAwake = true;

        public static ScreensaverSettings Load()
        {
            ScreensaverSettings settings = new ScreensaverSettings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    if (key == null)
                    {
                        return settings;
                    }

                    settings.GrowthDurationMs = ReadInt(key, "GrowthDurationMs", settings.GrowthDurationMs, 50, 300);
                    settings.PipeCount = ReadInt(key, "ClassicPipeCount", settings.PipeCount, 3, 16);
                    settings.MaxSegments = ReadInt(key, "ClassicMaxSegments", settings.MaxSegments, 120, 1200);
                    settings.KeepAwake = ReadInt(key, "KeepAwake", settings.KeepAwake ? 1 : 0, 0, 1) == 1;
                }
            }
            catch
            {
                // Keep defaults if registry access is unavailable.
            }
            return settings;
        }

        public void Save()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null)
                {
                    return;
                }

                key.SetValue("GrowthDurationMs", GrowthDurationMs, RegistryValueKind.DWord);
                key.SetValue("ClassicPipeCount", PipeCount, RegistryValueKind.DWord);
                key.SetValue("ClassicMaxSegments", MaxSegments, RegistryValueKind.DWord);
                key.SetValue("KeepAwake", KeepAwake ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        private static int ReadInt(RegistryKey key, string name, int fallback, int minimum, int maximum)
        {
            object value = key.GetValue(name);
            int parsed;
            if (value == null || !int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed))
            {
                return fallback;
            }
            return Math.Max(minimum, Math.Min(maximum, parsed));
        }
    }

    internal sealed class ConfigForm : Form
    {
        private const int MinimumDurationMs = 50;
        private const int MaximumDurationMs = 300;
        private const int SliderTotal = MinimumDurationMs + MaximumDurationMs;
        private const double EffectiveGrowthMultiplier = 2.05;

        private readonly ScreensaverSettings settings;
        private readonly TrackBar speedTrack;
        private readonly Label speedValue;
        private readonly Label speedDetail;
        private readonly NumericUpDown pipeCount;
        private readonly NumericUpDown segmentCount;
        private readonly CheckBox keepAwake;

        public ConfigForm()
        {
            settings = ScreensaverSettings.Load();

            Text = "3D Pipes Screensaver Settings";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScroll = true;
            ClientSize = new Size(720, 520);
            MinimumSize = new Size(650, 500);
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            Padding = new Padding(24);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 8,
                Padding = new Padding(4)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            Label title = new Label
            {
                Text = "Classic 3D Pipes",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 17F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(title, 0, 0);

            Label description = new Label
            {
                Text = "Fixed camera, perspective depth, smooth growth and a screen-filling procedural layout.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 20)
            };
            root.Controls.Add(description, 0, 1);

            TableLayoutPanel speedPanel = CreateSettingPanel();
            Label speedLabel = CreateSettingLabel("Pipe speed");
            speedPanel.Controls.Add(speedLabel, 0, 0);

            TableLayoutPanel speedControl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0)
            };
            speedControl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            speedControl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            speedControl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Label slower = new Label
            {
                Text = "Slower",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 8, 8, 0)
            };
            speedControl.Controls.Add(slower, 0, 0);

            speedTrack = new TrackBar
            {
                Minimum = MinimumDurationMs,
                Maximum = MaximumDurationMs,
                TickFrequency = 25,
                SmallChange = 5,
                LargeChange = 20,
                Value = DurationToSlider(settings.GrowthDurationMs),
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0)
            };
            speedTrack.ValueChanged += delegate { UpdateSpeedLabels(); };
            speedControl.Controls.Add(speedTrack, 1, 0);

            Label faster = new Label
            {
                Text = "Faster",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(8, 8, 0, 0)
            };
            speedControl.Controls.Add(faster, 2, 0);

            speedDetail = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 0)
            };
            speedControl.Controls.Add(speedDetail, 1, 1);

            speedValue = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Font = new Font(Font.FontFamily, 10F, FontStyle.Bold),
                Margin = new Padding(12, 8, 0, 0)
            };
            speedPanel.Controls.Add(speedControl, 1, 0);
            speedPanel.Controls.Add(speedValue, 2, 0);
            root.Controls.Add(speedPanel, 0, 2);

            TableLayoutPanel pipePanel = CreateSettingPanel();
            pipePanel.Controls.Add(CreateSettingLabel("Minimum pipes per scene"), 0, 0);
            pipeCount = new NumericUpDown
            {
                Minimum = 3,
                Maximum = 16,
                Value = Math.Max(3, Math.Min(16, settings.PipeCount)),
                Width = 100,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 0, 4)
            };
            pipePanel.Controls.Add(pipeCount, 1, 0);
            Label pipeHelp = new Label
            {
                Text = "Scenes begin with 2–5 pipes; more are added every five seconds.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(12, 7, 0, 0)
            };
            pipePanel.Controls.Add(pipeHelp, 2, 0);
            root.Controls.Add(pipePanel, 0, 3);

            TableLayoutPanel densityPanel = CreateSettingPanel();
            densityPanel.Controls.Add(CreateSettingLabel("Scene density"), 0, 0);
            segmentCount = new NumericUpDown
            {
                Minimum = 120,
                Maximum = 1200,
                Increment = 40,
                Value = Math.Max(120, Math.Min(1200, settings.MaxSegments)),
                Width = 100,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 0, 4)
            };
            densityPanel.Controls.Add(segmentCount, 1, 0);
            Label densityHelp = new Label
            {
                Text = "Maximum occupied grid positions before the scene dissolves.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(12, 7, 0, 0)
            };
            densityPanel.Controls.Add(densityHelp, 2, 0);
            root.Controls.Add(densityPanel, 0, 4);

            TableLayoutPanel cameraPanel = CreateSettingPanel();
            cameraPanel.Controls.Add(CreateSettingLabel("Camera"), 0, 0);
            Label cameraValue = new Label
            {
                Text = "Fixed with perspective depth",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 7, 0, 0)
            };
            cameraPanel.Controls.Add(cameraValue, 1, 0);
            root.Controls.Add(cameraPanel, 0, 5);

            Panel awakePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 0)
            };
            keepAwake = new CheckBox
            {
                Text = "Keep the computer and display awake while 3D Pipes is running",
                Checked = settings.KeepAwake,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            awakePanel.Controls.Add(keepAwake);
            Label sleepNote = new Label
            {
                Text = "Useful while the laptop is acting as a server or uploading files.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(24, 29)
            };
            awakePanel.Controls.Add(sleepNote);
            awakePanel.MinimumSize = new Size(0, 58);
            root.Controls.Add(awakePanel, 0, 6);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 18, 0, 0)
            };

            Button cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(92, 34),
                Margin = new Padding(8, 0, 0, 0)
            };
            buttons.Controls.Add(cancel);

            Button save = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                MinimumSize = new Size(92, 34),
                Margin = new Padding(8, 0, 0, 0)
            };
            save.Click += SaveClicked;
            buttons.Controls.Add(save);
            root.Controls.Add(buttons, 0, 7);

            AcceptButton = save;
            CancelButton = cancel;
            UpdateSpeedLabels();
        }

        private static TableLayoutPanel CreateSettingPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 12)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            return panel;
        }

        private static Label CreateSettingLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 7, 12, 0)
            };
        }

        private static int DurationToSlider(int durationMs)
        {
            int clamped = Math.Max(MinimumDurationMs, Math.Min(MaximumDurationMs, durationMs));
            return SliderTotal - clamped;
        }

        private static int SliderToDuration(int sliderValue)
        {
            return SliderTotal - sliderValue;
        }

        private void UpdateSpeedLabels()
        {
            int durationMs = SliderToDuration(speedTrack.Value);
            double sectionsPerSecond = 1000.0 / durationMs * EffectiveGrowthMultiplier;
            speedValue.Text = sectionsPerSecond.ToString("0.0", CultureInfo.InvariantCulture) + " sections/s";
            speedDetail.Text = durationMs.ToString(CultureInfo.InvariantCulture) + " ms per grid section";
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            settings.GrowthDurationMs = SliderToDuration(speedTrack.Value);
            settings.PipeCount = (int)pipeCount.Value;
            settings.MaxSegments = (int)segmentCount.Value;
            settings.KeepAwake = keepAwake.Checked;
            settings.Save();
            Close();
        }
    }

    internal sealed class PipesForm : Form
    {
        private readonly LaunchMode mode;
        private readonly ScreensaverSettings settings;
        private readonly PipeWorld world;
        private readonly Timer frameTimer;
        private readonly Stopwatch stopwatch;
        private readonly Point initialMouse;
        private OpenGlRenderer renderer;
        private long previousMilliseconds;
        private long lastExecutionStateRefresh;
        private bool cursorHidden;

        public PipesForm(LaunchMode launchMode, ScreensaverSettings loadedSettings)
        {
            mode = launchMode;
            settings = loadedSettings;
            world = new PipeWorld(settings);
            stopwatch = Stopwatch.StartNew();
            initialMouse = Cursor.Position;

            Text = "3D Pipes Screensaver";
            BackColor = Color.Black;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.Opaque, true);

            if (mode.Kind == LaunchKind.Windowed)
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(1280, 720);
                MinimumSize = new Size(640, 360);
                ShowInTaskbar = true;
            }
            else if (mode.Kind == LaunchKind.Preview)
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                Bounds = SystemInformation.VirtualScreen;
                TopMost = true;
                ShowInTaskbar = false;
            }

            frameTimer = new Timer { Interval = 16 };
            frameTimer.Tick += FrameTick;

            KeyDown += ExitOnKey;
            MouseDown += ExitOnMouseDown;
            MouseMove += ExitOnMouseMove;
            FormClosed += FormWasClosed;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            renderer = new OpenGlRenderer(Handle);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (renderer != null)
            {
                renderer.Dispose();
                renderer = null;
            }
            base.OnHandleDestroyed(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (mode.Kind == LaunchKind.Preview)
            {
                AttachToPreviewParent();
            }
            else if (mode.Kind == LaunchKind.FullScreen)
            {
                Cursor.Hide();
                cursorHidden = true;
                Activate();
            }

            world.Resize(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));

            if (settings.KeepAwake && mode.Kind != LaunchKind.Preview)
            {
                SleepInhibitor.Enable();
                lastExecutionStateRefresh = stopwatch.ElapsedMilliseconds;
            }

            previousMilliseconds = stopwatch.ElapsedMilliseconds;
            frameTimer.Start();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (ClientSize.Width > 0 && ClientSize.Height > 0)
            {
                world.Resize(ClientSize.Width, ClientSize.Height);
                if (renderer != null)
                {
                    renderer.Resize(ClientSize.Width, ClientSize.Height);
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // OpenGL clears the full back buffer.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (renderer != null && ClientSize.Width > 0 && ClientSize.Height > 0)
            {
                renderer.Render(world, ClientSize.Width, ClientSize.Height);
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_SCREENSAVE = 0xF140;
            const int SC_MONITORPOWER = 0xF170;

            if (m.Msg == WM_SYSCOMMAND)
            {
                int command = m.WParam.ToInt32() & 0xFFF0;
                if (command == SC_SCREENSAVE || command == SC_MONITORPOWER)
                {
                    return;
                }
            }

            base.WndProc(ref m);
        }

        private void FrameTick(object sender, EventArgs e)
        {
            long now = stopwatch.ElapsedMilliseconds;
            double deltaSeconds = Math.Min(0.10, Math.Max(0.0, (now - previousMilliseconds) / 1000.0));
            previousMilliseconds = now;

            if (mode.Kind == LaunchKind.Preview)
            {
                if (mode.PreviewParent == IntPtr.Zero || !NativeMethods.IsWindow(mode.PreviewParent))
                {
                    Close();
                    return;
                }
                ResizeToPreviewParent();
            }

            world.Update(deltaSeconds);

            if (settings.KeepAwake && mode.Kind != LaunchKind.Preview &&
                now - lastExecutionStateRefresh >= 30000)
            {
                SleepInhibitor.Enable();
                lastExecutionStateRefresh = now;
            }

            Invalidate(false);
        }

        private void AttachToPreviewParent()
        {
            if (mode.PreviewParent == IntPtr.Zero)
            {
                Close();
                return;
            }

            NativeMethods.SetParent(Handle, mode.PreviewParent);
            int style = NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_STYLE);
            style = (style | NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE) & ~NativeMethods.WS_POPUP;
            NativeMethods.SetWindowLong(Handle, NativeMethods.GWL_STYLE, style);
            ResizeToPreviewParent();
        }

        private void ResizeToPreviewParent()
        {
            NativeMethods.RECT rectangle;
            if (NativeMethods.GetClientRect(mode.PreviewParent, out rectangle))
            {
                NativeMethods.MoveWindow(
                    Handle,
                    0,
                    0,
                    Math.Max(1, rectangle.Right - rectangle.Left),
                    Math.Max(1, rectangle.Bottom - rectangle.Top),
                    true);
            }
        }

        private void ExitOnKey(object sender, KeyEventArgs e)
        {
            if (mode.Kind == LaunchKind.FullScreen ||
                (mode.Kind == LaunchKind.Windowed && e.KeyCode == Keys.Escape))
            {
                Close();
            }
        }

        private void ExitOnMouseDown(object sender, MouseEventArgs e)
        {
            if (mode.Kind == LaunchKind.FullScreen)
            {
                Close();
            }
        }

        private void ExitOnMouseMove(object sender, MouseEventArgs e)
        {
            if (mode.Kind != LaunchKind.FullScreen || stopwatch.ElapsedMilliseconds < 700)
            {
                return;
            }

            Point current = Cursor.Position;
            int distance = Math.Abs(current.X - initialMouse.X) + Math.Abs(current.Y - initialMouse.Y);
            if (distance > 8)
            {
                Close();
            }
        }

        private void FormWasClosed(object sender, FormClosedEventArgs e)
        {
            frameTimer.Stop();
            if (settings.KeepAwake && mode.Kind != LaunchKind.Preview)
            {
                SleepInhibitor.Disable();
            }
            if (cursorHidden)
            {
                Cursor.Show();
                cursorHidden = false;
            }
        }
    }

    internal static class SleepInhibitor
    {
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        public static void Enable()
        {
            NativeMethods.SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
        }

        public static void Disable()
        {
            NativeMethods.SetThreadExecutionState(ES_CONTINUOUS);
        }
    }

    internal static class NativeMethods
    {
        public const int GWL_STYLE = -16;
        public const int WS_CHILD = 0x40000000;
        public const int WS_VISIBLE = 0x10000000;
        public const int WS_POPUP = unchecked((int)0x80000000);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr window, int index, int newLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr window, out RECT rectangle);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr window);

        [DllImport("kernel32.dll")]
        public static extern uint SetThreadExecutionState(uint executionState);
    }
}
