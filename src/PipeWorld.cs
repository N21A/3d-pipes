using System;
using System.Collections.Generic;
using System.Drawing;

namespace ThreeDPipesScreensaver
{
    internal sealed class PipeWorld
    {
        private readonly ScreensaverSettings settings;
        private readonly Random random;
        private readonly HashSet<GridPoint> occupied;
        private readonly GridPoint[] directions;
        private readonly List<PipeSegment> segments;
        private readonly List<PipeElbow> elbows;
        private readonly List<PipeCap> caps;
        private readonly List<PipeRunner> runners;
        private double resetTimer;
        private double fadeTimer;
        private int lastWidth;
        private int lastHeight;

        public const float PipeRadius = 0.215f;
        public const float ElbowCentreRadius = 0.43f;

        public PipeWorld(ScreensaverSettings loadedSettings)
        {
            settings = loadedSettings;
            int seed = unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode();
            random = new Random(seed);
            occupied = new HashSet<GridPoint>();
            segments = new List<PipeSegment>(settings.MaxSegments + 16);
            elbows = new List<PipeElbow>(settings.MaxSegments / 3);
            caps = new List<PipeCap>(settings.PipeCount * 8);
            runners = new List<PipeRunner>(settings.PipeCount);
            directions = new[]
            {
                new GridPoint(1, 0, 0),
                new GridPoint(-1, 0, 0),
                new GridPoint(0, 1, 0),
                new GridPoint(0, -1, 0),
                new GridPoint(0, 0, 1),
                new GridPoint(0, 0, -1)
            };

            Colours = new[]
            {
                Color.FromArgb(105, 225, 220),
                Color.FromArgb(238, 42, 30),
                Color.FromArgb(202, 205, 210),
                Color.FromArgb(70, 190, 95),
                Color.FromArgb(232, 194, 56)
            };

            HalfY = 10;
            HalfX = 18;
            HalfZ = 12;
            FixedYawDegrees = 16.0f;
            FixedPitchDegrees = -7.0f;
            ResetScene();
        }

        public List<PipeSegment> Segments { get { return segments; } }
        public List<PipeElbow> Elbows { get { return elbows; } }
        public List<PipeCap> Caps { get { return caps; } }
        public List<PipeRunner> Runners { get { return runners; } }
        public Color[] Colours { get; private set; }
        public int HalfX { get; private set; }
        public int HalfY { get; private set; }
        public int HalfZ { get; private set; }
        public float FixedYawDegrees { get; private set; }
        public float FixedPitchDegrees { get; private set; }
        public float FadeAlpha { get; private set; }

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            double aspect = width / (double)height;
            int desiredHalfX = Math.Max(13, (int)Math.Ceiling(HalfY * aspect * 1.36));
            bool meaningfulChange = Math.Abs(desiredHalfX - HalfX) >= 2;

            HalfX = desiredHalfX;
            HalfZ = 12;
            lastWidth = width;
            lastHeight = height;

            if (meaningfulChange && segments.Count > 0)
            {
                ResetScene();
            }
        }

        public void Update(double deltaSeconds)
        {
            if (deltaSeconds <= 0.0)
            {
                return;
            }

            if (resetTimer > 0.0)
            {
                resetTimer -= deltaSeconds;
                fadeTimer += deltaSeconds;
                FadeAlpha = (float)Math.Max(0.0, Math.Min(1.0, fadeTimer / 0.75));
                if (resetTimer <= 0.0)
                {
                    ResetScene();
                }
                return;
            }

            double advance = deltaSeconds * 1000.0 / Math.Max(1, settings.GrowthDurationMs);
            for (int i = runners.Count - 1; i >= 0; i--)
            {
                PipeRunner runner = runners[i];
                runner.Progress += advance;

                int safety = 0;
                while (runner.Progress >= 1.0 && safety++ < 8)
                {
                    runner.Progress -= 1.0;
                    CompleteCurrentSegment(runner);

                    if (segments.Count >= settings.MaxSegments || !BeginNextSegment(runner))
                    {
                        caps.Add(new PipeCap(runner.Position, runner.ColourIndex));
                        runners.RemoveAt(i);
                        break;
                    }
                }
            }

            while (runners.Count < settings.PipeCount && segments.Count < settings.MaxSegments)
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
                resetTimer = 1.65;
                fadeTimer = 0.0;
                FadeAlpha = 0.0f;
            }
        }

        private void CompleteCurrentSegment(PipeRunner runner)
        {
            PipeSegment segment = new PipeSegment(
                runner.Position,
                runner.Destination,
                runner.ColourIndex,
                runner.PendingStartTrim,
                0.0f);

            segments.Add(segment);
            runner.PreviousSegmentIndex = segments.Count - 1;
            runner.Position = runner.Destination;
            runner.Direction = runner.PendingDirection;
            runner.HasDirection = true;
            runner.PendingStartTrim = 0.0f;
        }

        private bool BeginNextSegment(PipeRunner runner)
        {
            List<int> valid = new List<int>(6);
            int straightIndex = -1;

            for (int i = 0; i < directions.Length; i++)
            {
                GridPoint direction = directions[i];
                if (runner.HasDirection && IsOpposite(direction, runner.Direction))
                {
                    continue;
                }

                GridPoint destination = runner.Position + direction;
                if (!Inside(destination) || occupied.Contains(destination))
                {
                    continue;
                }

                valid.Add(i);
                if (runner.HasDirection && direction.Equals(runner.Direction))
                {
                    straightIndex = i;
                }
            }

            if (valid.Count == 0)
            {
                return false;
            }

            int chosenIndex;
            if (straightIndex >= 0 && random.NextDouble() < runner.StraightChance)
            {
                chosenIndex = straightIndex;
            }
            else
            {
                List<int> turnChoices = new List<int>(valid.Count);
                for (int i = 0; i < valid.Count; i++)
                {
                    if (valid[i] != straightIndex)
                    {
                        turnChoices.Add(valid[i]);
                    }
                }

                chosenIndex = turnChoices.Count > 0
                    ? turnChoices[random.Next(turnChoices.Count)]
                    : valid[random.Next(valid.Count)];
            }

            GridPoint chosenDirection = directions[chosenIndex];
            bool changedDirection = runner.HasDirection && !chosenDirection.Equals(runner.Direction);
            if (changedDirection)
            {
                if (runner.PreviousSegmentIndex >= 0 && runner.PreviousSegmentIndex < segments.Count)
                {
                    segments[runner.PreviousSegmentIndex].EndTrim = ElbowCentreRadius;
                }

                elbows.Add(new PipeElbow(
                    runner.Position,
                    runner.Direction,
                    chosenDirection,
                    runner.ColourIndex));
                runner.PendingStartTrim = ElbowCentreRadius;
            }
            else
            {
                runner.PendingStartTrim = 0.0f;
            }

            runner.PendingDirection = chosenDirection;
            runner.Destination = runner.Position + chosenDirection;
            runner.Progress = 0.0;
            occupied.Add(runner.Destination);
            return true;
        }

        private PipeRunner CreateRunner()
        {
            for (int attempt = 0; attempt < 240; attempt++)
            {
                GridPoint start = new GridPoint(
                    random.Next(-HalfX, HalfX + 1),
                    random.Next(-HalfY, HalfY + 1),
                    random.Next(-HalfZ, HalfZ + 1));

                if (occupied.Contains(start))
                {
                    continue;
                }

                PipeRunner runner = new PipeRunner(
                    start,
                    random.Next(Colours.Length),
                    0.72 + random.NextDouble() * 0.16);

                occupied.Add(start);
                caps.Add(new PipeCap(start, runner.ColourIndex));
                if (BeginNextSegment(runner))
                {
                    return runner;
                }

                occupied.Remove(start);
                caps.RemoveAt(caps.Count - 1);
            }

            return null;
        }

        private void ResetScene()
        {
            segments.Clear();
            elbows.Clear();
            caps.Clear();
            runners.Clear();
            occupied.Clear();
            resetTimer = 0.0;
            fadeTimer = 0.0;
            FadeAlpha = 0.0f;

            // The viewpoint changes only between scenes; it never rotates while a scene is running.
            FixedYawDegrees = (float)(12.0 + random.NextDouble() * 10.0) * (random.Next(2) == 0 ? -1.0f : 1.0f);
            FixedPitchDegrees = (float)(-5.0 - random.NextDouble() * 5.0);

            for (int i = 0; i < settings.PipeCount; i++)
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

        private static bool IsOpposite(GridPoint a, GridPoint b)
        {
            return a.X == -b.X && a.Y == -b.Y && a.Z == -b.Z;
        }
    }

    internal sealed class PipeRunner
    {
        public PipeRunner(GridPoint position, int colourIndex, double straightChance)
        {
            Position = position;
            Destination = position;
            Direction = GridPoint.Zero;
            PendingDirection = GridPoint.Zero;
            ColourIndex = colourIndex;
            StraightChance = straightChance;
            PreviousSegmentIndex = -1;
        }

        public GridPoint Position;
        public GridPoint Destination;
        public GridPoint Direction;
        public GridPoint PendingDirection;
        public int ColourIndex;
        public bool HasDirection;
        public double StraightChance;
        public double Progress;
        public float PendingStartTrim;
        public int PreviousSegmentIndex;
    }

    internal sealed class PipeSegment
    {
        public PipeSegment(GridPoint start, GridPoint end, int colourIndex, float startTrim, float endTrim)
        {
            Start = start;
            End = end;
            ColourIndex = colourIndex;
            StartTrim = startTrim;
            EndTrim = endTrim;
        }

        public GridPoint Start;
        public GridPoint End;
        public int ColourIndex;
        public float StartTrim;
        public float EndTrim;
    }

    internal sealed class PipeElbow
    {
        public PipeElbow(GridPoint centre, GridPoint incoming, GridPoint outgoing, int colourIndex)
        {
            Centre = centre;
            Incoming = incoming;
            Outgoing = outgoing;
            ColourIndex = colourIndex;
        }

        public GridPoint Centre;
        public GridPoint Incoming;
        public GridPoint Outgoing;
        public int ColourIndex;
    }

    internal sealed class PipeCap
    {
        public PipeCap(GridPoint position, int colourIndex)
        {
            Position = position;
            ColourIndex = colourIndex;
        }

        public GridPoint Position;
        public int ColourIndex;
    }

    internal struct GridPoint : IEquatable<GridPoint>
    {
        public static readonly GridPoint Zero = new GridPoint(0, 0, 0);

        public GridPoint(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly int X;
        public readonly int Y;
        public readonly int Z;

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
}
