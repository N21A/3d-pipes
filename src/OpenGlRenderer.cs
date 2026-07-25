using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ThreeDPipesScreensaver
{
    internal sealed class OpenGlRenderer : IDisposable
    {
        private const uint GL_COLOR_BUFFER_BIT = 0x00004000;
        private const uint GL_DEPTH_BUFFER_BIT = 0x00000100;
        private const uint GL_QUADS = 0x0007;
        private const uint GL_QUAD_STRIP = 0x0008;
        private const uint GL_MODELVIEW = 0x1700;
        private const uint GL_PROJECTION = 0x1701;
        private const uint GL_DEPTH_TEST = 0x0B71;
        private const uint GL_LEQUAL = 0x0203;
        private const uint GL_LIGHTING = 0x0B50;
        private const uint GL_LIGHT0 = 0x4000;
        private const uint GL_LIGHT1 = 0x4001;
        private const uint GL_NORMALIZE = 0x0BA1;
        private const uint GL_AMBIENT = 0x1200;
        private const uint GL_DIFFUSE = 0x1201;
        private const uint GL_SPECULAR = 0x1202;
        private const uint GL_POSITION = 0x1203;
        private const uint GL_SHININESS = 0x1601;
        private const uint GL_FRONT_AND_BACK = 0x0408;
        private const uint GL_SMOOTH = 0x1D01;

        private readonly IntPtr windowHandle;
        private IntPtr deviceContext;
        private IntPtr renderingContext;
        private int viewportWidth;
        private int viewportHeight;
        private bool disposed;

        private readonly float[] ambient = new float[4];
        private readonly float[] diffuse = new float[4];
        private readonly float[] specular = new float[4];

        private static readonly float[] Light0Position = { -0.65f, 0.90f, 0.70f, 0.0f };
        private static readonly float[] Light0Ambient = { 0.10f, 0.10f, 0.10f, 1.0f };
        private static readonly float[] Light0Diffuse = { 1.00f, 0.98f, 0.95f, 1.0f };
        private static readonly float[] Light0Specular = { 1.00f, 1.00f, 1.00f, 1.0f };
        private static readonly float[] Light1Position = { 0.80f, -0.20f, 0.40f, 0.0f };
        private static readonly float[] Light1Ambient = { 0.02f, 0.02f, 0.02f, 1.0f };
        private static readonly float[] Light1Diffuse = { 0.24f, 0.28f, 0.34f, 1.0f };
        private static readonly float[] Light1Specular = { 0.18f, 0.20f, 0.24f, 1.0f };

        public OpenGlRenderer(IntPtr handle)
        {
            windowHandle = handle;
            CreateContext();
            Initialise();
        }

        public void Resize(int width, int height)
        {
            if (disposed || width <= 0 || height <= 0)
            {
                return;
            }

            MakeCurrent();
            viewportWidth = width;
            viewportHeight = height;
            Gl.glViewport(0, 0, width, height);
        }

        public void Render(PipeWorld world, int width, int height)
        {
            if (disposed || width <= 0 || height <= 0)
            {
                return;
            }

            MakeCurrent();
            if (width != viewportWidth || height != viewportHeight)
            {
                Resize(width, height);
            }

            Gl.glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

            Gl.glMatrixMode(GL_PROJECTION);
            Gl.glLoadIdentity();
            Gl.gluPerspective(58.0, width / (double)Math.Max(1, height), 0.35, 100.0);

            Gl.glMatrixMode(GL_MODELVIEW);
            Gl.glLoadIdentity();
            Gl.gluLookAt(
                0.0,
                0.0,
                world.CameraDistance,
                0.0,
                0.0,
                0.0,
                0.0,
                1.0,
                0.0);
            Gl.glRotatef(world.FixedPitchDegrees, 1.0f, 0.0f, 0.0f);
            Gl.glRotatef(world.FixedYawDegrees, 0.0f, 1.0f, 0.0f);

            for (int colour = 0; colour < world.Colours.Length; colour++)
            {
                SetMaterial(world.Colours[colour]);

                for (int i = 0; i < world.Segments.Count; i++)
                {
                    PipeSegment segment = world.Segments[i];
                    if (segment.ColourIndex == colour)
                    {
                        DrawSegment(segment);
                    }
                }

                for (int i = 0; i < world.Elbows.Count; i++)
                {
                    PipeElbow joint = world.Elbows[i];
                    if (joint.ColourIndex == colour)
                    {
                        DrawSphere(
                            joint.Centre.X,
                            joint.Centre.Y,
                            joint.Centre.Z,
                            PipeWorld.JointRadius);
                    }
                }

                for (int i = 0; i < world.Caps.Count; i++)
                {
                    PipeCap cap = world.Caps[i];
                    if (cap.ColourIndex == colour)
                    {
                        DrawHemisphere(
                            cap.Position.X,
                            cap.Position.Y,
                            cap.Position.Z,
                            cap.OutwardDirection.X,
                            cap.OutwardDirection.Y,
                            cap.OutwardDirection.Z,
                            PipeWorld.PipeRadius);
                    }
                }

                for (int i = 0; i < world.Runners.Count; i++)
                {
                    PipeRunner runner = world.Runners[i];
                    if (runner.ColourIndex == colour)
                    {
                        DrawRunner(runner);
                    }
                }
            }

            if (world.DissolveProgress > 0.0f)
            {
                DrawDissolveOverlay(
                    width,
                    height,
                    world.DissolveProgress,
                    world.DissolveSeed);
            }

            Gl.SwapBuffers(deviceContext);
        }

        private void CreateContext()
        {
            deviceContext = Gl.GetDC(windowHandle);
            if (deviceContext == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not obtain the window device context.");
            }

            Gl.PIXELFORMATDESCRIPTOR descriptor = new Gl.PIXELFORMATDESCRIPTOR();
            descriptor.nSize = (ushort)Marshal.SizeOf(typeof(Gl.PIXELFORMATDESCRIPTOR));
            descriptor.nVersion = 1;
            descriptor.dwFlags = Gl.PFD_DRAW_TO_WINDOW |
                                 Gl.PFD_SUPPORT_OPENGL |
                                 Gl.PFD_DOUBLEBUFFER;
            descriptor.iPixelType = Gl.PFD_TYPE_RGBA;
            descriptor.cColorBits = 32;
            descriptor.cAlphaBits = 8;
            descriptor.cDepthBits = 24;
            descriptor.iLayerType = Gl.PFD_MAIN_PLANE;

            int format = Gl.ChoosePixelFormat(deviceContext, ref descriptor);
            if (format == 0 || !Gl.SetPixelFormat(deviceContext, format, ref descriptor))
            {
                throw new InvalidOperationException("Windows could not initialise an OpenGL pixel format.");
            }

            renderingContext = Gl.wglCreateContext(deviceContext);
            if (renderingContext == IntPtr.Zero)
            {
                throw new InvalidOperationException("Windows could not create an OpenGL rendering context.");
            }
            MakeCurrent();
        }

        private void Initialise()
        {
            Gl.glClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            Gl.glClearDepth(1.0);
            Gl.glEnable(GL_DEPTH_TEST);
            Gl.glDepthFunc(GL_LEQUAL);
            Gl.glEnable(GL_LIGHTING);
            Gl.glEnable(GL_LIGHT0);
            Gl.glEnable(GL_LIGHT1);
            Gl.glEnable(GL_NORMALIZE);
            Gl.glShadeModel(GL_SMOOTH);

            Gl.glLightfv(GL_LIGHT0, GL_POSITION, Light0Position);
            Gl.glLightfv(GL_LIGHT0, GL_AMBIENT, Light0Ambient);
            Gl.glLightfv(GL_LIGHT0, GL_DIFFUSE, Light0Diffuse);
            Gl.glLightfv(GL_LIGHT0, GL_SPECULAR, Light0Specular);
            Gl.glLightfv(GL_LIGHT1, GL_POSITION, Light1Position);
            Gl.glLightfv(GL_LIGHT1, GL_AMBIENT, Light1Ambient);
            Gl.glLightfv(GL_LIGHT1, GL_DIFFUSE, Light1Diffuse);
            Gl.glLightfv(GL_LIGHT1, GL_SPECULAR, Light1Specular);
        }

        private void SetMaterial(Color colour)
        {
            float r = colour.R / 255.0f;
            float g = colour.G / 255.0f;
            float b = colour.B / 255.0f;

            ambient[0] = r * 0.20f;
            ambient[1] = g * 0.20f;
            ambient[2] = b * 0.20f;
            ambient[3] = 1.0f;

            diffuse[0] = r;
            diffuse[1] = g;
            diffuse[2] = b;
            diffuse[3] = 1.0f;

            specular[0] = 0.92f;
            specular[1] = 0.92f;
            specular[2] = 0.92f;
            specular[3] = 1.0f;

            Gl.glMaterialfv(GL_FRONT_AND_BACK, GL_AMBIENT, ambient);
            Gl.glMaterialfv(GL_FRONT_AND_BACK, GL_DIFFUSE, diffuse);
            Gl.glMaterialfv(GL_FRONT_AND_BACK, GL_SPECULAR, specular);
            Gl.glMaterialf(GL_FRONT_AND_BACK, GL_SHININESS, 96.0f);
        }

        private static void DrawSegment(PipeSegment segment)
        {
            DrawCylinder(
                segment.Start.X,
                segment.Start.Y,
                segment.Start.Z,
                segment.End.X,
                segment.End.Y,
                segment.End.Z,
                PipeWorld.PipeRadius);
        }

        private static void DrawRunner(PipeRunner runner)
        {
            float visibleDistance = (float)Math.Min(runner.RunLength, runner.Progress);
            float ex = runner.Position.X + runner.PendingDirection.X * visibleDistance;
            float ey = runner.Position.Y + runner.PendingDirection.Y * visibleDistance;
            float ez = runner.Position.Z + runner.PendingDirection.Z * visibleDistance;

            if (visibleDistance > 0.002f)
            {
                DrawCylinder(
                    runner.Position.X,
                    runner.Position.Y,
                    runner.Position.Z,
                    ex,
                    ey,
                    ez,
                    PipeWorld.PipeRadius);
            }

            DrawHemisphere(
                ex,
                ey,
                ez,
                runner.PendingDirection.X,
                runner.PendingDirection.Y,
                runner.PendingDirection.Z,
                PipeWorld.PipeRadius);
        }

        private static void DrawCylinder(
            float sx,
            float sy,
            float sz,
            float ex,
            float ey,
            float ez,
            float radius)
        {
            float dx = ex - sx;
            float dy = ey - sy;
            float dz = ez - sz;
            float length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length <= 0.001f)
            {
                return;
            }

            Gl.glPushMatrix();
            Gl.glTranslatef(sx, sy, sz);
            RotatePositiveZToVector(dx, dy, dz);
            DrawUnitCylinder(radius, length);
            Gl.glPopMatrix();
        }

        private static void RotatePositiveZToVector(float dx, float dy, float dz)
        {
            float length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length <= 0.0001f)
            {
                return;
            }

            float cosine = Math.Max(-1.0f, Math.Min(1.0f, dz / length));
            float angle = (float)(Math.Acos(cosine) * 180.0 / Math.PI);
            float axisX = -dy;
            float axisY = dx;
            float axisLength = (float)Math.Sqrt(axisX * axisX + axisY * axisY);

            if (axisLength > 0.0001f)
            {
                Gl.glRotatef(angle, axisX / axisLength, axisY / axisLength, 0.0f);
            }
            else if (dz < 0.0f)
            {
                Gl.glRotatef(180.0f, 1.0f, 0.0f, 0.0f);
            }
        }

        private static void DrawUnitCylinder(float radius, float length)
        {
            const int sides = 24;
            Gl.glBegin(GL_QUAD_STRIP);
            for (int i = 0; i <= sides; i++)
            {
                double angle = i * Math.PI * 2.0 / sides;
                float x = (float)Math.Cos(angle);
                float y = (float)Math.Sin(angle);
                Gl.glNormal3f(x, y, 0.0f);
                Gl.glVertex3f(x * radius, y * radius, 0.0f);
                Gl.glVertex3f(x * radius, y * radius, length);
            }
            Gl.glEnd();
        }

        private static void DrawSphere(float x, float y, float z, float radius)
        {
            const int stacks = 12;
            const int slices = 24;

            Gl.glPushMatrix();
            Gl.glTranslatef(x, y, z);

            for (int stack = 0; stack < stacks; stack++)
            {
                double latitude0 = -Math.PI * 0.5 + stack * Math.PI / stacks;
                double latitude1 = -Math.PI * 0.5 + (stack + 1) * Math.PI / stacks;
                float z0 = (float)Math.Sin(latitude0);
                float r0 = (float)Math.Cos(latitude0);
                float z1 = (float)Math.Sin(latitude1);
                float r1 = (float)Math.Cos(latitude1);

                Gl.glBegin(GL_QUAD_STRIP);
                for (int slice = 0; slice <= slices; slice++)
                {
                    double longitude = slice * Math.PI * 2.0 / slices;
                    float cx = (float)Math.Cos(longitude);
                    float cy = (float)Math.Sin(longitude);

                    Gl.glNormal3f(cx * r0, cy * r0, z0);
                    Gl.glVertex3f(cx * r0 * radius, cy * r0 * radius, z0 * radius);
                    Gl.glNormal3f(cx * r1, cy * r1, z1);
                    Gl.glVertex3f(cx * r1 * radius, cy * r1 * radius, z1 * radius);
                }
                Gl.glEnd();
            }

            Gl.glPopMatrix();
        }

        private static void DrawHemisphere(
            float x,
            float y,
            float z,
            float directionX,
            float directionY,
            float directionZ,
            float radius)
        {
            const int latitudeSteps = 8;
            const int longitudeSteps = 24;

            Gl.glPushMatrix();
            Gl.glTranslatef(x, y, z);
            RotatePositiveZToVector(directionX, directionY, directionZ);

            for (int latitude = 0; latitude < latitudeSteps; latitude++)
            {
                double phi0 = latitude * Math.PI * 0.5 / latitudeSteps;
                double phi1 = (latitude + 1) * Math.PI * 0.5 / latitudeSteps;
                float ring0 = (float)Math.Cos(phi0);
                float ring1 = (float)Math.Cos(phi1);
                float z0 = (float)Math.Sin(phi0);
                float z1 = (float)Math.Sin(phi1);

                Gl.glBegin(GL_QUAD_STRIP);
                for (int longitude = 0; longitude <= longitudeSteps; longitude++)
                {
                    double theta = longitude * Math.PI * 2.0 / longitudeSteps;
                    float cx = (float)Math.Cos(theta);
                    float cy = (float)Math.Sin(theta);

                    Gl.glNormal3f(cx * ring0, cy * ring0, z0);
                    Gl.glVertex3f(cx * ring0 * radius, cy * ring0 * radius, z0 * radius);
                    Gl.glNormal3f(cx * ring1, cy * ring1, z1);
                    Gl.glVertex3f(cx * ring1 * radius, cy * ring1 * radius, z1 * radius);
                }
                Gl.glEnd();
            }

            Gl.glPopMatrix();
        }

        private static void DrawDissolveOverlay(
            int width,
            int height,
            float progress,
            int seed)
        {
            const int tileSize = 20;
            int columns = Math.Max(1, (width + tileSize - 1) / tileSize);
            int rows = Math.Max(1, (height + tileSize - 1) / tileSize);
            float softenedProgress = Math.Min(1.0f, progress * 1.08f);

            Gl.glDisable(GL_LIGHTING);
            Gl.glDisable(GL_DEPTH_TEST);

            Gl.glMatrixMode(GL_PROJECTION);
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            Gl.glOrtho(0.0, width, height, 0.0, -1.0, 1.0);

            Gl.glMatrixMode(GL_MODELVIEW);
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            Gl.glColor3f(0.0f, 0.0f, 0.0f);

            Gl.glBegin(GL_QUADS);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (TileThreshold(column, row, seed) > softenedProgress)
                    {
                        continue;
                    }

                    float x0 = column * tileSize;
                    float y0 = row * tileSize;
                    float x1 = Math.Min(width, x0 + tileSize + 1);
                    float y1 = Math.Min(height, y0 + tileSize + 1);

                    Gl.glVertex3f(x0, y0, 0.0f);
                    Gl.glVertex3f(x1, y0, 0.0f);
                    Gl.glVertex3f(x1, y1, 0.0f);
                    Gl.glVertex3f(x0, y1, 0.0f);
                }
            }
            Gl.glEnd();

            Gl.glPopMatrix();
            Gl.glMatrixMode(GL_PROJECTION);
            Gl.glPopMatrix();
            Gl.glMatrixMode(GL_MODELVIEW);
            Gl.glEnable(GL_DEPTH_TEST);
            Gl.glEnable(GL_LIGHTING);
        }

        private static float TileThreshold(int x, int y, int seed)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)x * 0x9E3779B9u;
                value ^= (uint)y * 0x85EBCA6Bu;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (value & 0x00FFFFFFu) / 16777215.0f;
            }
        }

        private void MakeCurrent()
        {
            if (!Gl.wglMakeCurrent(deviceContext, renderingContext))
            {
                throw new InvalidOperationException("OpenGL could not activate its rendering context.");
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            if (renderingContext != IntPtr.Zero)
            {
                Gl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                Gl.wglDeleteContext(renderingContext);
                renderingContext = IntPtr.Zero;
            }

            if (deviceContext != IntPtr.Zero)
            {
                Gl.ReleaseDC(windowHandle, deviceContext);
                deviceContext = IntPtr.Zero;
            }
        }
    }

    internal static class Gl
    {
        public const uint PFD_DOUBLEBUFFER = 0x00000001;
        public const uint PFD_DRAW_TO_WINDOW = 0x00000004;
        public const uint PFD_SUPPORT_OPENGL = 0x00000020;
        public const byte PFD_TYPE_RGBA = 0;
        public const sbyte PFD_MAIN_PLANE = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct PIXELFORMATDESCRIPTOR
        {
            public ushort nSize;
            public ushort nVersion;
            public uint dwFlags;
            public byte iPixelType;
            public byte cColorBits;
            public byte cRedBits;
            public byte cRedShift;
            public byte cGreenBits;
            public byte cGreenShift;
            public byte cBlueBits;
            public byte cBlueShift;
            public byte cAlphaBits;
            public byte cAlphaShift;
            public byte cAccumBits;
            public byte cAccumRedBits;
            public byte cAccumGreenBits;
            public byte cAccumBlueBits;
            public byte cAccumAlphaBits;
            public byte cDepthBits;
            public byte cStencilBits;
            public byte cAuxBuffers;
            public sbyte iLayerType;
            public byte bReserved;
            public uint dwLayerMask;
            public uint dwVisibleMask;
            public uint dwDamageMask;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);
        [DllImport("gdi32.dll")]
        public static extern int ChoosePixelFormat(IntPtr deviceContext, ref PIXELFORMATDESCRIPTOR descriptor);
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetPixelFormat(IntPtr deviceContext, int format, ref PIXELFORMATDESCRIPTOR descriptor);
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SwapBuffers(IntPtr deviceContext);
        [DllImport("opengl32.dll")]
        public static extern IntPtr wglCreateContext(IntPtr deviceContext);
        [DllImport("opengl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool wglDeleteContext(IntPtr renderingContext);
        [DllImport("opengl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool wglMakeCurrent(IntPtr deviceContext, IntPtr renderingContext);
        [DllImport("opengl32.dll")]
        public static extern void glClearColor(float red, float green, float blue, float alpha);
        [DllImport("opengl32.dll")]
        public static extern void glClearDepth(double depth);
        [DllImport("opengl32.dll")]
        public static extern void glClear(uint mask);
        [DllImport("opengl32.dll")]
        public static extern void glEnable(uint capability);
        [DllImport("opengl32.dll")]
        public static extern void glDisable(uint capability);
        [DllImport("opengl32.dll")]
        public static extern void glDepthFunc(uint function);
        [DllImport("opengl32.dll")]
        public static extern void glShadeModel(uint mode);
        [DllImport("opengl32.dll")]
        public static extern void glViewport(int x, int y, int width, int height);
        [DllImport("opengl32.dll")]
        public static extern void glMatrixMode(uint mode);
        [DllImport("opengl32.dll")]
        public static extern void glLoadIdentity();
        [DllImport("opengl32.dll")]
        public static extern void glRotatef(float angle, float x, float y, float z);
        [DllImport("opengl32.dll")]
        public static extern void glTranslatef(float x, float y, float z);
        [DllImport("opengl32.dll")]
        public static extern void glPushMatrix();
        [DllImport("opengl32.dll")]
        public static extern void glPopMatrix();
        [DllImport("opengl32.dll")]
        public static extern void glBegin(uint mode);
        [DllImport("opengl32.dll")]
        public static extern void glEnd();
        [DllImport("opengl32.dll")]
        public static extern void glNormal3f(float x, float y, float z);
        [DllImport("opengl32.dll")]
        public static extern void glVertex3f(float x, float y, float z);
        [DllImport("opengl32.dll")]
        public static extern void glMaterialfv(uint face, uint parameterName, float[] parameters);
        [DllImport("opengl32.dll")]
        public static extern void glMaterialf(uint face, uint parameterName, float parameter);
        [DllImport("opengl32.dll")]
        public static extern void glLightfv(uint light, uint parameterName, float[] parameters);
        [DllImport("opengl32.dll")]
        public static extern void glColor3f(float red, float green, float blue);
        [DllImport("opengl32.dll")]
        public static extern void glOrtho(double left, double right, double bottom, double top, double nearPlane, double farPlane);
        [DllImport("glu32.dll")]
        public static extern void gluPerspective(double fieldOfViewY, double aspect, double nearPlane, double farPlane);
        [DllImport("glu32.dll")]
        public static extern void gluLookAt(double eyeX, double eyeY, double eyeZ, double centreX, double centreY, double centreZ, double upX, double upY, double upZ);
    }
}
