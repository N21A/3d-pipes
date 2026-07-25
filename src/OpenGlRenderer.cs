using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ThreeDPipesScreensaver
{
    internal sealed class OpenGlRenderer : IDisposable
    {
        private const uint GL_COLOR_BUFFER_BIT = 0x00004000;
        private const uint GL_DEPTH_BUFFER_BIT = 0x00000100;
        private const uint GL_QUAD_STRIP = 0x0008;
        private const uint GL_MODELVIEW = 0x1700;
        private const uint GL_PROJECTION = 0x1701;
        private const uint GL_DEPTH_TEST = 0x0B71;
        private const uint GL_LEQUAL = 0x0203;
        private const uint GL_LIGHTING = 0x0B50;
        private const uint GL_LIGHT0 = 0x4000;
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
        private readonly float[] specular = { 0.78f, 0.78f, 0.78f, 1.0f };
        private static readonly float[] LightPosition = { -0.45f, 0.75f, 0.65f, 0.0f };
        private static readonly float[] LightAmbient = { 0.13f, 0.13f, 0.13f, 1.0f };
        private static readonly float[] LightDiffuse = { 0.95f, 0.95f, 0.95f, 1.0f };
        private static readonly float[] LightSpecular = { 1.0f, 1.0f, 1.0f, 1.0f };

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
            Gl.gluPerspective(48.0, width / (double)Math.Max(1, height), 1.0, 180.0);

            Gl.glMatrixMode(GL_MODELVIEW);
            Gl.glLoadIdentity();
            Gl.gluLookAt(0.0, 0.25, 42.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0);
            Gl.glRotatef(world.FixedPitchDegrees, 1.0f, 0.0f, 0.0f);
            Gl.glRotatef(world.FixedYawDegrees, 0.0f, 1.0f, 0.0f);

            float brightness = 1.0f - world.FadeAlpha;
            for (int colour = 0; colour < world.Colours.Length; colour++)
            {
                SetMaterial(world.Colours[colour], brightness);

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
                    PipeElbow elbow = world.Elbows[i];
                    if (elbow.ColourIndex == colour)
                    {
                        DrawElbow(elbow);
                    }
                }

                for (int i = 0; i < world.Caps.Count; i++)
                {
                    PipeCap cap = world.Caps[i];
                    if (cap.ColourIndex == colour)
                    {
                        DrawSphere(cap.Position.X, cap.Position.Y, cap.Position.Z, PipeWorld.PipeRadius * 1.04f);
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
            descriptor.dwFlags = Gl.PFD_DRAW_TO_WINDOW | Gl.PFD_SUPPORT_OPENGL | Gl.PFD_DOUBLEBUFFER;
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
            Gl.glEnable(GL_NORMALIZE);
            Gl.glShadeModel(GL_SMOOTH);
            Gl.glLightfv(GL_LIGHT0, GL_POSITION, LightPosition);
            Gl.glLightfv(GL_LIGHT0, GL_AMBIENT, LightAmbient);
            Gl.glLightfv(GL_LIGHT0, GL_DIFFUSE, LightDiffuse);
            Gl.glLightfv(GL_LIGHT0, GL_SPECULAR, LightSpecular);
        }

        private void SetMaterial(Color colour, float brightness)
        {
            float r = colour.R / 255.0f * brightness;
            float g = colour.G / 255.0f * brightness;
            float b = colour.B / 255.0f * brightness;

            ambient[0] = r * 0.24f;
            ambient[1] = g * 0.24f;
            ambient[2] = b * 0.24f;
            ambient[3] = 1.0f;
            diffuse[0] = r;
            diffuse[1] = g;
            diffuse[2] = b;
            diffuse[3] = 1.0f;

            Gl.glMaterialfv(GL_FRONT_AND_BACK, GL_AMBIENT, ambient);
            Gl.glMaterialfv(GL_FRONT_AND_BACK, GL_DIFFUSE, diffuse);
            Gl.glMaterialfv(GL_FRONT_AND_BACK, GL_SPECULAR, specular);
            Gl.glMaterialf(GL_FRONT_AND_BACK, GL_SHININESS, 72.0f);
        }

        private static void DrawSegment(PipeSegment segment)
        {
            float dx = segment.End.X - segment.Start.X;
            float dy = segment.End.Y - segment.Start.Y;
            float dz = segment.End.Z - segment.Start.Z;
            DrawCylinder(
                segment.Start.X + dx * segment.StartTrim,
                segment.Start.Y + dy * segment.StartTrim,
                segment.Start.Z + dz * segment.StartTrim,
                segment.End.X - dx * segment.EndTrim,
                segment.End.Y - dy * segment.EndTrim,
                segment.End.Z - dz * segment.EndTrim,
                PipeWorld.PipeRadius);
        }

        private static void DrawRunner(PipeRunner runner)
        {
            float progress = (float)Math.Max(runner.PendingStartTrim, Math.Min(1.0, runner.Progress));
            float sx = runner.Position.X + runner.PendingDirection.X * runner.PendingStartTrim;
            float sy = runner.Position.Y + runner.PendingDirection.Y * runner.PendingStartTrim;
            float sz = runner.Position.Z + runner.PendingDirection.Z * runner.PendingStartTrim;
            float ex = runner.Position.X + runner.PendingDirection.X * progress;
            float ey = runner.Position.Y + runner.PendingDirection.Y * progress;
            float ez = runner.Position.Z + runner.PendingDirection.Z * progress;

            if (progress > runner.PendingStartTrim + 0.002f)
            {
                DrawCylinder(sx, sy, sz, ex, ey, ez, PipeWorld.PipeRadius);
            }
            DrawSphere(ex, ey, ez, PipeWorld.PipeRadius * 1.035f);
        }

        private static void DrawCylinder(float sx, float sy, float sz, float ex, float ey, float ez, float radius)
        {
            float dx = ex - sx;
            float dy = ey - sy;
            float dz = ez - sz;
            float length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length <= 0.001f)
            {
                return;
            }

            float angle = (float)(Math.Acos(Math.Max(-1.0f, Math.Min(1.0f, dz / length))) * 180.0 / Math.PI);
            float axisX = -dy;
            float axisY = dx;
            float axisLength = (float)Math.Sqrt(axisX * axisX + axisY * axisY);

            Gl.glPushMatrix();
            Gl.glTranslatef(sx, sy, sz);
            if (axisLength > 0.0001f)
            {
                Gl.glRotatef(angle, axisX / axisLength, axisY / axisLength, 0.0f);
            }
            else if (dz < 0.0f)
            {
                Gl.glRotatef(180.0f, 1.0f, 0.0f, 0.0f);
            }
            DrawUnitCylinder(radius, length);
            Gl.glPopMatrix();
        }

        private static void DrawUnitCylinder(float radius, float length)
        {
            const int sides = 18;
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
            const int stacks = 10;
            const int slices = 16;
            Gl.glPushMatrix();
            Gl.glTranslatef(x, y, z);

            for (int stack = 0; stack < stacks; stack++)
            {
                double lat0 = -Math.PI * 0.5 + stack * Math.PI / stacks;
                double lat1 = -Math.PI * 0.5 + (stack + 1) * Math.PI / stacks;
                float z0 = (float)Math.Sin(lat0);
                float r0 = (float)Math.Cos(lat0);
                float z1 = (float)Math.Sin(lat1);
                float r1 = (float)Math.Cos(lat1);

                Gl.glBegin(GL_QUAD_STRIP);
                for (int slice = 0; slice <= slices; slice++)
                {
                    double lon = slice * Math.PI * 2.0 / slices;
                    float cx = (float)Math.Cos(lon);
                    float cy = (float)Math.Sin(lon);
                    Gl.glNormal3f(cx * r0, cy * r0, z0);
                    Gl.glVertex3f(cx * r0 * radius, cy * r0 * radius, z0 * radius);
                    Gl.glNormal3f(cx * r1, cy * r1, z1);
                    Gl.glVertex3f(cx * r1 * radius, cy * r1 * radius, z1 * radius);
                }
                Gl.glEnd();
            }
            Gl.glPopMatrix();
        }

        private static void DrawElbow(PipeElbow elbow)
        {
            const int arcSteps = 9;
            Vector3 incoming = new Vector3(elbow.Incoming.X, elbow.Incoming.Y, elbow.Incoming.Z);
            Vector3 outgoing = new Vector3(elbow.Outgoing.X, elbow.Outgoing.Y, elbow.Outgoing.Z);
            Vector3 centre = new Vector3(elbow.Centre.X, elbow.Centre.Y, elbow.Centre.Z);
            Vector3 origin = centre - incoming * PipeWorld.ElbowCentreRadius + outgoing * PipeWorld.ElbowCentreRadius;

            Vector3 previous = origin - outgoing * PipeWorld.ElbowCentreRadius;
            for (int step = 1; step <= arcSteps; step++)
            {
                float theta = (float)(step * Math.PI * 0.5 / arcSteps);
                Vector3 radial = outgoing * -(float)Math.Cos(theta) + incoming * (float)Math.Sin(theta);
                Vector3 current = origin + radial * PipeWorld.ElbowCentreRadius;
                DrawCylinder(previous.X, previous.Y, previous.Z, current.X, current.Y, current.Z, PipeWorld.PipeRadius);
                previous = current;
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

    internal struct Vector3
    {
        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public static Vector3 operator +(Vector3 a, Vector3 b)
        {
            return new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vector3 operator -(Vector3 a, Vector3 b)
        {
            return new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vector3 operator *(Vector3 value, float scalar)
        {
            return new Vector3(value.X * scalar, value.Y * scalar, value.Z * scalar);
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
        [DllImport("glu32.dll")]
        public static extern void gluPerspective(double fieldOfViewY, double aspect, double nearPlane, double farPlane);
        [DllImport("glu32.dll")]
        public static extern void gluLookAt(double eyeX, double eyeY, double eyeZ, double centreX, double centreY, double centreZ, double upX, double upY, double upZ);
    }
}
