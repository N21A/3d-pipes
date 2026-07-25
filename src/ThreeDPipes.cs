using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("3D Pipes Screensaver")]
[assembly: AssemblyDescription("Procedurally generated 3D pipes screensaver for Windows")]
[assembly: AssemblyCompany("3D Pipes")]
[assembly: AssemblyProduct("3D Pipes Screensaver")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

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
            LaunchMode result = new LaunchMode();
            result.Kind = LaunchKind.FullScreen;
            result.PreviewParent = IntPtr.Zero;

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
                return result;
            }

            return result;
        }
    }

    internal sealed class ScreensaverSettings
    {
        private const string RegistryPath = @"Software\ThreeDPipesScreensaver";

        public int GrowthDelayMs = 82;
        public int PipeCount = 8;
        public int MaxSegments = 620;
        public int RotationPercent = 8;
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

                    settings.GrowthDelayMs = ReadInt(key, "GrowthDelayMs", settings.GrowthDelayMs, 25, 300);
                    settings.PipeCount = ReadInt(key, "PipeCount", settings.PipeCount, 2, 20);
                    settings.MaxSegments = ReadInt(key, "MaxSegments", settings.MaxSegments, 150, 1800);
                    settings.RotationPercent = ReadInt(key, "RotationPercent", settings.RotationPercent, 0, 25);
                    settings.KeepAwake = ReadInt(key, "KeepAwake", settings.KeepAwake ? 1 : 0, 0, 1) == 1;
                }
            }
            catch
            {
                // Defaults remain valid if registry access is unavailable.
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

                key.SetValue("GrowthDelayMs", GrowthDelayMs, RegistryValueKind.DWord);
                key.SetValue("PipeCount", PipeCount, RegistryValueKind.DWord);
                key.SetValue("MaxSegments", MaxSegments, RegistryValueKind.DWord);
                key.SetValue("RotationPercent", RotationPercent, RegistryValueKind.DWord);
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
        private readonly TrackBar rotationTrack;
        private readonly Label rotationValue;
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
            ClientSize = new Size(520, 410);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            Label title = new Label();
            title.Text = "Procedural 3D Pipes";
            title.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(24, 20);
            Controls.Add(title);

            Label description = new Label();
            description.Text = "Every run creates a new pipe layout, colour palette and camera path.";
            description.AutoSize = true;
            description.ForeColor = SystemColors.GrayText;
            description.Location = new Point(27, 58);
            Controls.Add(description);

            Label speedLabel = MakeLabel("Growth interval", 28, 103);
            Controls.Add(speedLabel);

            speedTrack = new TrackBar();
            speedTrack.Minimum = 25;
            speedTrack.Maximum = 300;
            speedTrack.TickFrequency = 25;
            speedTrack.Value = settings.GrowthDelayMs;
            speedTrack.Location = new Point(171, 92);
            speedTrack.Size = new Size(264, 45);
            speedTrack.ValueChanged += delegate { UpdateLabels(); };
            Controls.Add(speedTrack);

            speedValue = MakeLabel(string.Empty, 440, 103);
            speedValue.Size = new Size(58, 25);
            Controls.Add(speedValue);

            Label pipesLabel = MakeLabel("Simultaneous pipes", 28, 154);
            Controls.Add(pipesLabel);

            pipeCount = new NumericUpDown();
            pipeCount.Minimum = 2;
            pipeCount.Maximum = 20;
            pipeCount.Value = settings.PipeCount;
            pipeCount.Location = new Point(174, 151);
            pipeCount.Size = new Size(88, 26);
            Controls.Add(pipeCount);

            Label densityLabel = MakeLabel("Scene density", 28, 203);
            Controls.Add(densityLabel);

            segmentCount = new NumericUpDown();
            segmentCount.Minimum = 150;
            segmentCount.Maximum = 1800;
            segmentCount.Increment = 50;
            segmentCount.Value = settings.MaxSegments;
            segmentCount.Location = new Point(174, 200);
            segmentCount.Size = new Size(88, 26);
            Controls.Add(segmentCount);

            Label rotationLabel = MakeLabel("Camera rotation", 28, 252);
            Controls.Add(rotationLabel);

            rotationTrack = new TrackBar();
            rotationTrack.Minimum = 0;
            rotationTrack.Maximum = 25;
            rotationTrack.TickFrequency = 5;
            rotationTrack.Value = settings.RotationPercent;
            rotationTrack.Location = new Point(171, 239);
            rotationTrack.Size = new Size(264, 45);
            rotationTrack.ValueChanged += delegate { UpdateLabels(); };
            Controls.Add(rotationTrack);

            rotationValue = MakeLabel(string.Empty, 440, 252);
            rotationValue.Size = new Size(58, 25);
            Controls.Add(rotationValue);

            keepAwake = new CheckBox();
            keepAwake.Text = "Keep the computer and display awake while 3D Pipes is running";
            keepAwake.Checked = settings.KeepAwake;
            keepAwake.AutoSize = true;
            keepAwake.Location = new Point(28, 302);
            Controls.Add(keepAwake);

            Label sleepNote = new Label();
            sleepNote.Text = "This is useful while the laptop is acting as a server or uploading files.";
            sleepNote.AutoSize = true;
            sleepNote.ForeColor = SystemColors.GrayText;
            sleepNote.Location = new Point(49, 329);
            Controls.Add(sleepNote);

            Button ok = new Button();
            ok.Text = "Save";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(332, 365);
            ok.Size = new Size(78, 30);
            ok.Click += SaveClicked;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(420, 365);
            cancel.Size = new Size(78, 30);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            UpdateLabels();
        }

        private Label MakeLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            return label;
        }

        private void UpdateLabels()
        {
            speedValue.Text = speedTrack.Value.ToString(CultureInfo.InvariantCulture) + " ms";
            rotationValue.Text = rotationTrack.Value == 0
                ? "Off"
                : rotationTrack.Value.ToString(CultureInfo.InvariantCulture) + "%";
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            settings.GrowthDelayMs = speedTrack.Value;
            settings.PipeCount = (int)pipeCount.Value;
            settings.MaxSegments = (int)segmentCount.Value;
            settings.RotationPercent = rotationTrack.Value;
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
        private long previousMilliseconds;
        private double growthAccumulator;
        private double cameraYaw;
        private double cameraPitch;
        private bool cursorHidden;
        private long lastExecutionStateRefresh;

        public PipesForm(LaunchMode launchMode, ScreensaverSettings loadedSettings)
        {
            mode = launchMode;
            settings = loadedSettings;
            world = new PipeWorld(settings);
            stopwatch = Stopwatch.StartNew();
            previousMilliseconds = 0;
            growthAccumulator = 0.0;
            cameraYaw = world.InitialYaw;
            cameraPitch = world.InitialPitch;
            initialMouse = Cursor.Position;

            Text = "3D Pipes Screensaver";
            BackColor = Color.Black;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

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

            frameTimer = new Timer();
            frameTimer.Interval = 30;
            frameTimer.Tick += FrameTick;

            KeyDown += ExitOnKey;
            MouseDown += ExitOnMouseDown;
            MouseMove += ExitOnMouseMove;
            FormClosed += FormWasClosed;
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

            if (settings.KeepAwake && mode.Kind != LaunchKind.Preview)
            {
                SleepInhibitor.Enable();
                lastExecutionStateRefresh = stopwatch.ElapsedMilliseconds;
            }

            frameTimer.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // The renderer paints the entire surface, avoiding a second clear and flicker.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            world.Render(e.Graphics, ClientRectangle, cameraYaw, cameraPitch, stopwatch.Elapsed.TotalSeconds);
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
            double delta = Math.Min(0.1, Math.Max(0.0, (now - previousMilliseconds) / 1000.0));
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

            growthAccumulator += delta * 1000.0;
            while (growthAccumulator >= settings.GrowthDelayMs)
            {
                world.Advance(stopwatch.Elapsed.TotalSeconds);
                growthAccumulator -= settings.GrowthDelayMs;
            }

            double rotationRate = settings.RotationPercent / 100.0;
            cameraYaw += delta * rotationRate;
            cameraPitch = world.InitialPitch + Math.Sin(stopwatch.Elapsed.TotalSeconds * 0.11) * 0.10;

            if (settings.KeepAwake && mode.Kind != LaunchKind.Preview && now - lastExecutionStateRefresh >= 30000)
            {
                SleepInhibitor.Enable();
                lastExecutionStateRefresh = now;
            }

            Invalidate();
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
            if (mode.Kind == LaunchKind.FullScreen)
            {
                Close();
            }
            else if (mode.Kind == LaunchKind.Windowed && e.KeyCode == Keys.Escape)
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

    internal sealed class PipeWorld
    {
        private readonly ScreensaverSettings settings;
        private readonly Random random;
        private readonly List<PipeSegment> segments;
        private readonly List<PipeJoint> joints;
        private readonly List<PipeRunner> runners;
        private readonly HashSet<GridPoint> occupied;
        private readonly Color[] palette;
        private readonly GridPoint[] directions;
        private double resetAt;
        private double fadeStartedAt;
        private int sceneNumber;

        private const int HalfX = 6;
        private const int HalfY = 5;
        private const int HalfZ = 5;

        public double InitialYaw { get; private set; }
        public double InitialPitch { get; private set; }

        public PipeWorld(ScreensaverSettings loadedSettings)
        {
            settings = loadedSettings;
            int seed = unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode();
            random = new Random(seed);
            segments = new List<PipeSegment>();
            joints = new List<PipeJoint>();
            runners = new List<PipeRunner>();
            occupied = new HashSet<GridPoint>();
            palette = new Color[]
            {
                Color.FromArgb(30, 210, 255),
                Color.FromArgb(255, 62, 154),
                Color.FromArgb(255, 183, 38),
                Color.FromArgb(108, 255, 119),
                Color.FromArgb(164, 95, 255),
                Color.FromArgb(255, 78, 62),
                Color.FromArgb(46, 245, 208),
                Color.FromArgb(240, 240, 245),
                Color.FromArgb(85, 140, 255),
                Color.FromArgb(255, 112, 235)
            };
            directions = new GridPoint[]
            {
                new GridPoint(1, 0, 0),
                new GridPoint(-1, 0, 0),
                new GridPoint(0, 1, 0),
                new GridPoint(0, -1, 0),
                new GridPoint(0, 0, 1),
                new GridPoint(0, 0, -1)
            };

            ResetScene();
        }

        public void Advance(double elapsedSeconds)
        {
            if (resetAt > 0.0)
            {
                if (elapsedSeconds >= resetAt)
                {
                    ResetScene();
                }
                return;
            }

            for (int i = runners.Count - 1; i >= 0; i--)
            {
                PipeRunner runner = runners[i];
                if (!AdvanceRunner(runner))
                {
                    joints.Add(new PipeJoint(runner.Position, runner.Colour, 1.08));
                    runners.RemoveAt(i);
                }
            }

            while (runners.Count < settings.PipeCount)
            {
                PipeRunner replacement = CreateRunner();
                if (replacement == null)
                {
                    break;
                }
                runners.Add(replacement);
            }

            if (segments.Count >= settings.MaxSegments || runners.Count == 0)
            {
                fadeStartedAt = elapsedSeconds;
                resetAt = elapsedSeconds + 2.1;
            }
        }

        public void Render(Graphics graphics, Rectangle bounds, double yaw, double pitch, double elapsedSeconds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (LinearGradientBrush background = new LinearGradientBrush(
                bounds,
                Color.FromArgb(3, 5, 12),
                Color.Black,
                LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(background, bounds);
            }

            DrawSubtleStars(graphics, bounds, sceneNumber);

            Projection projection = new Projection(bounds, yaw, pitch);
            List<ProjectedItem> projectedItems = new List<ProjectedItem>(segments.Count + joints.Count + runners.Count);
            int i;
            for (i = 0; i < segments.Count; i++)
            {
                ProjectedSegment projected = projection.Project(segments[i]);
                if (projected != null)
                {
                    projectedItems.Add(new ProjectedItem(projected));
                }
            }
            for (i = 0; i < joints.Count; i++)
            {
                ProjectedJoint joint = projection.Project(joints[i]);
                if (joint != null)
                {
                    projectedItems.Add(new ProjectedItem(joint));
                }
            }
            for (i = 0; i < runners.Count; i++)
            {
                ProjectedJoint head = projection.Project(new PipeJoint(runners[i].Position, runners[i].Colour, 1.04));
                if (head != null)
                {
                    projectedItems.Add(new ProjectedItem(head));
                }
            }

            projectedItems.Sort(delegate(ProjectedItem a, ProjectedItem b)
            {
                return b.Depth.CompareTo(a.Depth);
            });

            for (i = 0; i < projectedItems.Count; i++)
            {
                ProjectedItem item = projectedItems[i];
                if (item.Segment != null)
                {
                    DrawPipeSegment(graphics, item.Segment, projection);
                }
                else
                {
                    DrawJoint(graphics, item.Joint, projection);
                }
            }

            if (resetAt > 0.0)
            {
                double duration = Math.Max(0.001, resetAt - fadeStartedAt);
                double progress = Math.Max(0.0, Math.Min(1.0, (elapsedSeconds - fadeStartedAt) / duration));
                int alpha = (int)(255.0 * progress * progress);
                using (SolidBrush fade = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                {
                    graphics.FillRectangle(fade, bounds);
                }
            }
        }

        private bool AdvanceRunner(PipeRunner runner)
        {
            List<int> choices = new List<int>(5);
            int i;
            for (i = 0; i < directions.Length; i++)
            {
                if (runner.HasDirection && IsOpposite(i, runner.DirectionIndex))
                {
                    continue;
                }

                GridPoint next = runner.Position + directions[i];
                if (Inside(next) && !occupied.Contains(next))
                {
                    choices.Add(i);
                }
            }

            if (choices.Count == 0)
            {
                return false;
            }

            int chosen;
            if (runner.HasDirection && choices.Contains(runner.DirectionIndex) && random.NextDouble() < 0.48)
            {
                chosen = runner.DirectionIndex;
            }
            else
            {
                chosen = choices[random.Next(choices.Count)];
            }

            GridPoint destination = runner.Position + directions[chosen];
            bool changedDirection = runner.HasDirection && chosen != runner.DirectionIndex;
            if (changedDirection)
            {
                joints.Add(new PipeJoint(runner.Position, runner.Colour, 1.12));
            }
            else if (runner.HasDirection && random.NextDouble() < 0.025)
            {
                joints.Add(new PipeJoint(runner.Position, runner.Colour, 1.48));
            }

            segments.Add(new PipeSegment(runner.Position, destination, runner.Colour));
            occupied.Add(destination);
            runner.Position = destination;
            runner.DirectionIndex = chosen;
            runner.HasDirection = true;
            return true;
        }

        private PipeRunner CreateRunner()
        {
            int attempts;
            for (attempts = 0; attempts < 160; attempts++)
            {
                GridPoint point = new GridPoint(
                    random.Next(-HalfX, HalfX + 1),
                    random.Next(-HalfY, HalfY + 1),
                    random.Next(-HalfZ, HalfZ + 1));

                if (occupied.Contains(point))
                {
                    continue;
                }

                occupied.Add(point);
                Color colour = palette[random.Next(palette.Length)];
                joints.Add(new PipeJoint(point, colour, 1.12));
                return new PipeRunner(point, colour);
            }
            return null;
        }

        private void ResetScene()
        {
            segments.Clear();
            joints.Clear();
            runners.Clear();
            occupied.Clear();
            resetAt = 0.0;
            fadeStartedAt = 0.0;
            sceneNumber = random.Next();
            InitialYaw = random.NextDouble() * Math.PI * 2.0;
            InitialPitch = -0.35 + random.NextDouble() * 0.35;

            int i;
            for (i = 0; i < settings.PipeCount; i++)
            {
                PipeRunner runner = CreateRunner();
                if (runner != null)
                {
                    runners.Add(runner);
                }
            }
        }

        private bool Inside(GridPoint point)
        {
            return point.X >= -HalfX && point.X <= HalfX &&
                   point.Y >= -HalfY && point.Y <= HalfY &&
                   point.Z >= -HalfZ && point.Z <= HalfZ;
        }

        private bool IsOpposite(int a, int b)
        {
            return (a == 0 && b == 1) || (a == 1 && b == 0) ||
                   (a == 2 && b == 3) || (a == 3 && b == 2) ||
                   (a == 4 && b == 5) || (a == 5 && b == 4);
        }

        private void DrawPipeSegment(Graphics graphics, ProjectedSegment segment, Projection projection)
        {
            float width = segment.Width;
            Color baseColour = projection.Fog(segment.Segment.Colour, segment.Depth);
            Color shadowColour = Darken(baseColour, 0.33);
            Color highlightColour = Lighten(baseColour, 0.72);

            using (Pen shadow = new Pen(Color.FromArgb(190, shadowColour), width + Math.Max(2F, width * 0.19F)))
            {
                shadow.StartCap = LineCap.Round;
                shadow.EndCap = LineCap.Round;
                graphics.DrawLine(shadow, segment.A, segment.B);
            }

            using (Pen body = new Pen(baseColour, width))
            {
                body.StartCap = LineCap.Round;
                body.EndCap = LineCap.Round;
                graphics.DrawLine(body, segment.A, segment.B);
            }

            float offset = Math.Max(1F, width * 0.18F);
            PointF highlightA = new PointF(segment.A.X - offset, segment.A.Y - offset);
            PointF highlightB = new PointF(segment.B.X - offset, segment.B.Y - offset);
            using (Pen highlight = new Pen(Color.FromArgb(175, highlightColour), Math.Max(1.4F, width * 0.18F)))
            {
                highlight.StartCap = LineCap.Round;
                highlight.EndCap = LineCap.Round;
                graphics.DrawLine(highlight, highlightA, highlightB);
            }
        }

        private void DrawJoint(Graphics graphics, ProjectedJoint joint, Projection projection)
        {
            float radius = joint.Radius;
            RectangleF ellipse = new RectangleF(
                joint.Center.X - radius,
                joint.Center.Y - radius,
                radius * 2F,
                radius * 2F);

            Color baseColour = projection.Fog(joint.Joint.Colour, joint.Depth);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(135, 0, 0, 0)))
            {
                graphics.FillEllipse(shadow, ellipse.X + radius * 0.20F, ellipse.Y + radius * 0.22F, ellipse.Width, ellipse.Height);
            }

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(ellipse);
                using (PathGradientBrush gradient = new PathGradientBrush(path))
                {
                    gradient.CenterPoint = new PointF(
                        joint.Center.X - radius * 0.32F,
                        joint.Center.Y - radius * 0.32F);
                    gradient.CenterColor = Lighten(baseColour, 0.92);
                    gradient.SurroundColors = new Color[] { Darken(baseColour, 0.46) };
                    graphics.FillPath(gradient, path);
                }
            }

            using (Pen rim = new Pen(Color.FromArgb(120, Lighten(baseColour, 0.35)), Math.Max(1F, radius * 0.08F)))
            {
                graphics.DrawEllipse(rim, ellipse);
            }
        }

        private void DrawSubtleStars(Graphics graphics, Rectangle bounds, int seed)
        {
            Random stars = new Random(seed);
            int count = Math.Max(18, Math.Min(90, (bounds.Width * bounds.Height) / 35000));
            using (SolidBrush starBrush = new SolidBrush(Color.FromArgb(52, 190, 210, 255)))
            {
                int i;
                for (i = 0; i < count; i++)
                {
                    float x = (float)(stars.NextDouble() * bounds.Width);
                    float y = (float)(stars.NextDouble() * bounds.Height);
                    float size = stars.NextDouble() < 0.14 ? 2F : 1F;
                    graphics.FillEllipse(starBrush, x, y, size, size);
                }
            }
        }

        private static Color Lighten(Color colour, double amount)
        {
            amount = Math.Max(0.0, Math.Min(1.0, amount));
            return Color.FromArgb(
                colour.A,
                colour.R + (int)((255 - colour.R) * amount),
                colour.G + (int)((255 - colour.G) * amount),
                colour.B + (int)((255 - colour.B) * amount));
        }

        private static Color Darken(Color colour, double factor)
        {
            factor = Math.Max(0.0, Math.Min(1.0, factor));
            return Color.FromArgb(
                colour.A,
                (int)(colour.R * factor),
                (int)(colour.G * factor),
                (int)(colour.B * factor));
        }
    }

    internal sealed class Projection
    {
        private readonly Rectangle bounds;
        private readonly double yawCos;
        private readonly double yawSin;
        private readonly double pitchCos;
        private readonly double pitchSin;
        private readonly double focal;
        private readonly double cameraDistance;
        private readonly double worldScale;
        private readonly double baseRadius;

        public Projection(Rectangle renderBounds, double yaw, double pitch)
        {
            bounds = renderBounds;
            yawCos = Math.Cos(yaw);
            yawSin = Math.Sin(yaw);
            pitchCos = Math.Cos(pitch);
            pitchSin = Math.Sin(pitch);
            focal = Math.Min(bounds.Width, bounds.Height) * 0.94;
            cameraDistance = 27.0;
            worldScale = 1.35;
            baseRadius = Math.Max(5.0, Math.Min(bounds.Width, bounds.Height) * 0.0205);
        }

        public ProjectedSegment Project(PipeSegment segment)
        {
            ProjectedPoint a = ProjectPoint(segment.A);
            ProjectedPoint b = ProjectPoint(segment.B);
            if (!a.Visible || !b.Visible)
            {
                return null;
            }

            double depth = (a.Depth + b.Depth) * 0.5;
            float width = (float)Math.Max(2.2, baseRadius * cameraDistance / depth);
            return new ProjectedSegment(segment, a.Screen, b.Screen, depth, width);
        }

        public ProjectedJoint Project(PipeJoint joint)
        {
            ProjectedPoint point = ProjectPoint(joint.Position);
            if (!point.Visible)
            {
                return null;
            }

            float radius = (float)Math.Max(2.2, baseRadius * 0.58 * cameraDistance / point.Depth * joint.Scale);
            return new ProjectedJoint(joint, point.Screen, point.Depth, radius);
        }

        public Color Fog(Color colour, double depth)
        {
            double normalized = (depth - 17.0) / 25.0;
            double brightness = 1.0 - Math.Max(0.0, Math.Min(0.55, normalized * 0.55));
            return Color.FromArgb(
                colour.A,
                Math.Max(0, Math.Min(255, (int)(colour.R * brightness))),
                Math.Max(0, Math.Min(255, (int)(colour.G * brightness))),
                Math.Max(0, Math.Min(255, (int)(colour.B * brightness))));
        }

        private ProjectedPoint ProjectPoint(GridPoint point)
        {
            double x = point.X * worldScale;
            double y = point.Y * worldScale;
            double z = point.Z * worldScale;

            double xYaw = yawCos * x + yawSin * z;
            double zYaw = -yawSin * x + yawCos * z;
            double yPitch = pitchCos * y - pitchSin * zYaw;
            double zPitch = pitchSin * y + pitchCos * zYaw;
            double depth = zPitch + cameraDistance;

            if (depth <= 1.0)
            {
                return new ProjectedPoint(false, PointF.Empty, depth);
            }

            double scale = focal / depth;
            PointF screen = new PointF(
                (float)(bounds.Left + bounds.Width * 0.5 + xYaw * scale),
                (float)(bounds.Top + bounds.Height * 0.5 - yPitch * scale));
            return new ProjectedPoint(true, screen, depth);
        }
    }

    internal struct GridPoint : IEquatable<GridPoint>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public GridPoint(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static GridPoint operator +(GridPoint a, GridPoint b)
        {
            return new GridPoint(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public bool Equals(GridPoint other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPoint && Equals((GridPoint)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Z;
                return hash;
            }
        }
    }

    internal sealed class PipeRunner
    {
        public GridPoint Position;
        public Color Colour;
        public int DirectionIndex;
        public bool HasDirection;

        public PipeRunner(GridPoint position, Color colour)
        {
            Position = position;
            Colour = colour;
            DirectionIndex = 0;
            HasDirection = false;
        }
    }

    internal sealed class PipeSegment
    {
        public readonly GridPoint A;
        public readonly GridPoint B;
        public readonly Color Colour;

        public PipeSegment(GridPoint a, GridPoint b, Color colour)
        {
            A = a;
            B = b;
            Colour = colour;
        }
    }

    internal sealed class PipeJoint
    {
        public readonly GridPoint Position;
        public readonly Color Colour;
        public readonly double Scale;

        public PipeJoint(GridPoint position, Color colour, double scale)
        {
            Position = position;
            Colour = colour;
            Scale = scale;
        }
    }

    internal sealed class ProjectedItem
    {
        public readonly ProjectedSegment Segment;
        public readonly ProjectedJoint Joint;
        public readonly double Depth;

        public ProjectedItem(ProjectedSegment segment)
        {
            Segment = segment;
            Joint = null;
            Depth = segment.Depth;
        }

        public ProjectedItem(ProjectedJoint joint)
        {
            Segment = null;
            Joint = joint;
            Depth = joint.Depth;
        }
    }

    internal struct ProjectedPoint
    {
        public readonly bool Visible;
        public readonly PointF Screen;
        public readonly double Depth;

        public ProjectedPoint(bool visible, PointF screen, double depth)
        {
            Visible = visible;
            Screen = screen;
            Depth = depth;
        }
    }

    internal sealed class ProjectedSegment
    {
        public readonly PipeSegment Segment;
        public readonly PointF A;
        public readonly PointF B;
        public readonly double Depth;
        public readonly float Width;

        public ProjectedSegment(PipeSegment segment, PointF a, PointF b, double depth, float width)
        {
            Segment = segment;
            A = a;
            B = b;
            Depth = depth;
            Width = width;
        }
    }

    internal sealed class ProjectedJoint
    {
        public readonly PipeJoint Joint;
        public readonly PointF Center;
        public readonly double Depth;
        public readonly float Radius;

        public ProjectedJoint(PipeJoint joint, PointF center, double depth, float radius)
        {
            Joint = joint;
            Center = center;
            Depth = depth;
            Radius = radius;
        }
    }

    internal static class SleepInhibitor
    {
        private const uint SystemRequired = 0x00000001;
        private const uint DisplayRequired = 0x00000002;
        private const uint Continuous = 0x80000000;

        public static void Enable()
        {
            NativeMethods.SetThreadExecutionState(Continuous | SystemRequired | DisplayRequired);
        }

        public static void Disable()
        {
            NativeMethods.SetThreadExecutionState(Continuous);
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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr window);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SetThreadExecutionState(uint executionState);
    }
}
