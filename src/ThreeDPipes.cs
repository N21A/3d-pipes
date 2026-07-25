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
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

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

        public int GrowthDurationMs = 145;
        public int PipeCount = 3;
        public int MaxSegments = 420;
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

                    settings.GrowthDurationMs = ReadInt(key, "GrowthDurationMs", settings.GrowthDurationMs, 55, 500);
                    settings.PipeCount = ReadInt(key, "ClassicPipeCount", settings.PipeCount, 1, 8);
                    settings.MaxSegments = ReadInt(key, "ClassicMaxSegments", settings.MaxSegments, 120, 1000);
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
        private readonly ScreensaverSettings settings;
        private readonly TrackBar speedTrack;
        private readonly Label speedValue;
        private readonly NumericUpDown pipeCount;
        private readonly NumericUpDown segmentCount;
        private readonly CheckBox keepAwake;

        public ConfigForm()
        {
            settings = ScreensaverSettings.Load();

            Text = "3D Pipes Screensaver Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(535, 395);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            Label title = MakeLabel("Classic 3D Pipes", 24, 20);
            title.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
            Controls.Add(title);

            Label description = MakeLabel(
                "Fixed camera, smooth pipe growth and a screen-filling procedural layout.",
                27,
                58);
            description.ForeColor = SystemColors.GrayText;
            Controls.Add(description);

            Controls.Add(MakeLabel("Time per grid section", 28, 105));
            speedTrack = new TrackBar
            {
                Minimum = 55,
                Maximum = 500,
                TickFrequency = 45,
                Value = settings.GrowthDurationMs,
                Location = new Point(178, 92),
                Size = new Size(267, 45)
            };
            speedTrack.ValueChanged += delegate { UpdateLabels(); };
            Controls.Add(speedTrack);

            speedValue = MakeLabel(string.Empty, 451, 105);
            speedValue.Size = new Size(65, 25);
            Controls.Add(speedValue);

            Controls.Add(MakeLabel("Simultaneous pipes", 28, 158));
            pipeCount = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 8,
                Value = settings.PipeCount,
                Location = new Point(181, 155),
                Size = new Size(88, 26)
            };
            Controls.Add(pipeCount);

            Controls.Add(MakeLabel("Scene density", 28, 210));
            segmentCount = new NumericUpDown
            {
                Minimum = 120,
                Maximum = 1000,
                Increment = 40,
                Value = settings.MaxSegments,
                Location = new Point(181, 207),
                Size = new Size(88, 26)
            };
            Controls.Add(segmentCount);

            Label camera = MakeLabel("Camera", 28, 261);
            Controls.Add(camera);
            Label cameraValue = MakeLabel("Fixed (classic mode)", 181, 261);
            cameraValue.ForeColor = SystemColors.GrayText;
            Controls.Add(cameraValue);

            keepAwake = new CheckBox
            {
                Text = "Keep the computer and display awake while 3D Pipes is running",
                Checked = settings.KeepAwake,
                AutoSize = true,
                Location = new Point(28, 307)
            };
            Controls.Add(keepAwake);

            Label sleepNote = MakeLabel(
                "Useful while the laptop is acting as a server or uploading files.",
                49,
                333);
            sleepNote.ForeColor = SystemColors.GrayText;
            Controls.Add(sleepNote);

            Button ok = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Location = new Point(347, 356),
                Size = new Size(78, 30)
            };
            ok.Click += SaveClicked;
            Controls.Add(ok);

            Button cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(435, 356),
                Size = new Size(78, 30)
            };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            UpdateLabels();
        }

        private static Label MakeLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Location = new Point(x, y)
            };
        }

        private void UpdateLabels()
        {
            speedValue.Text = speedTrack.Value.ToString(CultureInfo.InvariantCulture) + " ms";
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            settings.GrowthDurationMs = speedTrack.Value;
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
