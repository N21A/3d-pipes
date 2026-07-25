using System;
using System.Collections.Generic;
using System.Drawing;

namespace ThreeDPipesScreensaver
{
    internal sealed class PipeWorld
    {
        private const int MaximumRunLength = 7;
        private const double DissolveDurationSeconds = 1.55;

        private readonly ScreensaverSettings settings;
        private readonly Random random;
        private readonly HashSet<GridPoint> occupied;
        private readonly GridPoint[] directions;
        private readonly List<PipeSegment> segments;
        private readonly List<PipeElbow> elbows;
        private readonly List<PipeCap> caps;
        private readonly List<PipeRunner> runners;

        private bool dissolving;
        private double dissolveElapsed;
        private double sceneAge;
        private double sceneLifetime;

        public const float PipeRadius = 0.30f;
        public const float ElbowCentreRadius = 0.56f;

        public PipeWorld(ScreensaverSettings loadedSettings)
        {
            settings = loadedSettings;
            int seed = unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode();
            random = new Random(seed);
            occupied = new HashSet<GridPoint>();
            segments = new List<PipeSegment>(settings.MaxSegments / 2 + 32);
            elbows = new List<PipeElbow>(settings.MaxSegments / 4 + 16);
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
                Color.FromArgb(72, 184, 178),
                Color.FromArgb(205, 55, 43),
                Color.FromArgb(190, 194, 201),
                Color.FromArgb(47, 157, 91),
                Color.FromArgb(58, 108, 188),
                Color.FromArgb(213, 169, 52)
            };

            HalfY = 8;
            HalfX = 17;
            HalfZ = 8;
            CameraDistance = 24.0f;
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
        public float CameraDistance { get; private set; }
        public float FixedYawDegrees { get; private set; }
        public float FixedPitchDegrees { get; private set; }
        public float DissolveProgress { get; private set; }
        public int DissolveSeed { get; private set; }

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            double aspect = width / (double)height;
            int desiredHalfX = Math.Max(12, (int)Math.Ceiling(HalfY * aspect * 1.22));
            bool meaningfulChange = Math.Abs(desiredHalfX - HalfX) >= 2;

            HalfX = desiredHalfX;
            HalfZ = 8;
            CameraDistance = 24.0f;

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

            if (dissolving)
            {
                dissolveElapsed += deltaSeconds;
                DissolveProgress = (float)Math.Max(
                    0.0,
                    Math.Min(1.0, dissolveElapsed / DissolveDurationSeconds));

                if (DissolveProgress >= 1.0f)
                {
                    ResetScene();
                }
                return;
            }

            sceneAge += deltaSeconds;
            double distanceAdvance = deltaSeconds * 1000.0 / Math.Max(45, settings.GrowthDurationMs);

            for (int i = runners.Count - 1; i >= 0; i--)
            {
                PipeRunner runner = runners[i];
                runner.Progress += distanceAdvance;

                int safety = 0;
                while (runner.Progress >= runner.RunLength && safety++ < 8)
                {
                    double carryDistance = runner.Progress - runner.RunLength;
                    CompleteCurrentRun(runner);

                    if (occupied.Count >= settings.MaxSegments || !BeginNextRun(runner))
                    {
                        caps.Add(new PipeCap(
                            runner.Position,
                            runner.Direction,
                            runner.ColourIndex));
                        runners.RemoveAt(i);
                        break;
                    }

                    runner.Progress = carryDistance;
                }
            }

            while (runners.Count < settings.PipeCount && occupied.Count < settings.MaxSegments)
            {
                PipeRunner replacement = CreateRunner();
                if (replacement == null)
                {
                    break;
                }
                runners.Add(replacement);
            }

            if (occupied.Count >= settings.MaxSegments ||
                runners.Count == 0 ||
                sceneAge >= sceneLifetime)
            {
                StartDissolve();
            }
        }

        private void CompleteCurrentRun(PipeRunner runner)
        {
            bool continuesStraight = runner.HasDirection &&
                                     runner.Direction.Equals(runner.PendingDirection) &&
                                     runner.PreviousSegmentIndex >= 0 &&
                                     runner.PreviousSegmentIndex < segments.Count;

            if (continuesStraight)
            {
                PipeSegment previous = segments[runner.PreviousSegmentIndex];
                if (previous.End.Equals(runner.Position))
                {
                    previous.End = runner.Destination;
                    previous.EndTrim = 0.0f;
                }
                else
                {
                    continuesStraight = false;
                }
            }

            if (!continuesStraight)
            {
                segments.Add(new PipeSegment(
                    runner.Position,
                    runner.Destination,
                    runner.ColourIndex,
                    runner.PendingStartTrim,
                    0.0f));
                runner.PreviousSegmentIndex = segments.Count - 1;
            }

            runner.Position = runner.Destination;
            runner.Direction = runner.PendingDirection;
            runner.HasDirection = true;
            runner.PendingStartTrim = 0.0f;
        }

        private bool BeginNextRun(PipeRunner runner)
        {
            List<DirectionOption> options = new List<DirectionOption>(6);
            DirectionOption straightOption = null;

            for (int i = 0; i < directions.Length; i++)
            {
                GridPoint direction = directions[i];
                if (runner.HasDirection && IsOpposite(direction, runner.Direction))
                {
                    continue;
                }

                int maximumLength = GetMaximumFreeRun(runner.Position, direction);
                if (maximumLength <= 0)
                {
                    continue;
                }

                DirectionOption option = new DirectionOption(direction, maximumLength);
                options.Add(option);
                if (runner.HasDirection && direction.Equals(runner.Direction))
                {
                    straightOption = option;
                }
            }

            if (options.Count == 0)
            {
                return false;
            }

            DirectionOption chosen;
            if (straightOption != null && random.NextDouble() < runner.StraightChance)
            {
                chosen = straightOption;
            }
            else
            {
                chosen = ChooseTurnOption(options, straightOption);
            }

            bool changedDirection = runner.HasDirection &&
                                    !chosen.Direction.Equals(runner.Direction);
            if (changedDirection)
            {
                if (runner.PreviousSegmentIndex >= 0 &&
                    runner.PreviousSegmentIndex < segments.Count)
                {
                    segments[runner.PreviousSegmentIndex].EndTrim = ElbowCentreRadius;
                }

                elbows.Add(new PipeElbow(
                    runner.Position,
                    runner.Direction,
                    chosen.Direction,
                    runner.ColourIndex));
                runner.PendingStartTrim = ElbowCentreRadius;
            }
            else
            {
                runner.PendingStartTrim = 0.0f;
            }

            int runLength = ChooseRunLength(chosen.MaximumLength, !changedDirection);
            runner.PendingDirection = chosen.Direction;
            runner.RunLength = runLength;
            runner.Destination = runner.Position + chosen.Direction * runLength;
            runner.Progress = 0.0;

            for (int step = 1; step <= runLength; step++)
            {
                occupied.Add(runner.Position + chosen.Direction * step);
            }
            return true;
        }

        private DirectionOption ChooseTurnOption(
            List<DirectionOption> options,
            DirectionOption straightOption)
        {
            double totalWeight = 0.0;
            for (int i = 0; i < options.Count; i++)
            {
                DirectionOption option = options[i];
                if (ReferenceEquals(option, straightOption) && options.Count > 1)
                {
                    option.Weight = 0.12;
                }
                else
                {
                    double planeWeight = option.Direction.Z == 0 ? 1.0 : 0.62;
                    double spaceWeight = 0.70 + Math.Min(1.0, option.MaximumLength / 5.0);
                    option.Weight = planeWeight * spaceWeight;
                }
                totalWeight += option.Weight;
            }

            double selection = random.NextDouble() * totalWeight;
            for (int i = 0; i < options.Count; i++)
            {
                selection -= options[i].Weight;
                if (selection <= 0.0)
                {
                    return options[i];
                }
            }
            return options[options.Count - 1];
        }

        private int ChooseRunLength(int maximumLength, bool continuingStraight)
        {
            double roll = random.NextDouble();
            int desired;
            if (roll < 0.07)
            {
                desired = 1;
            }
            else if (roll < 0.24)
            {
                desired = 2;
            }
            else if (roll < 0.53)
            {
                desired = 3;
            }
            else if (roll < 0.78)
            {
                desired = 4;
            }
            else if (roll < 0.93)
            {
                desired = 5;
            }
            else
            {
                desired = 6;
            }

            if (continuingStraight && desired < maximumLength && random.NextDouble() < 0.35)
            {
                desired++;
            }
            return Math.Max(1, Math.Min(maximumLength, desired));
        }

        private int GetMaximumFreeRun(GridPoint origin, GridPoint direction)
        {
            int maximumLength = 0;
            for (int length = 1; length <= MaximumRunLength; length++)
            {
                GridPoint point = origin + direction * length;
                if (!Inside(point) || occupied.Contains(point))
                {
                    break;
                }
                maximumLength = length;
            }
            return maximumLength;
        }

        private PipeRunner CreateRunner()
        {
            for (int attempt = 0; attempt < 260; attempt++)
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
                    0.66 + random.NextDouble() * 0.16);

                occupied.Add(start);
                if (BeginNextRun(runner))
                {
                    caps.Add(new PipeCap(
                        start,
                        -runner.PendingDirection,
                        runner.ColourIndex));
                    return runner;
                }

                occupied.Remove(start);
            }
            return null;
        }

        private void StartDissolve()
        {
            if (dissolving)
            {
                return;
            }

            dissolving = true;
            dissolveElapsed = 0.0;
            DissolveProgress = 0.0f;
            DissolveSeed = random.Next();
        }

        private void ResetScene()
        {
            segments.Clear();
            elbows.Clear();
            caps.Clear();
            runners.Clear();
            occupied.Clear();

            dissolving = false;
            dissolveElapsed = 0.0;
            DissolveProgress = 0.0f;
            DissolveSeed = random.Next();
            sceneAge = 0.0;
            sceneLifetime = 20.0 + random.NextDouble() * 8.0;

            // Mostly head-on, with a mild fixed angle for depth. The camera never rotates mid-scene.
            if (random.NextDouble() < 0.72)
            {
                FixedYawDegrees = (float)(-5.0 + random.NextDouble() * 10.0);
                FixedPitchDegrees = (float)(-4.5 + random.NextDouble() * 4.0);
            }
            else
            {
                FixedYawDegrees = (float)(10.0 + random.NextDouble() * 6.0) *
                                  (random.Next(2) == 0 ? -1.0f : 1.0f);
                FixedPitchDegrees = (float)(-5.0 - random.NextDouble() * 3.0);
            }

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

        private sealed class DirectionOption
        {
            public DirectionOption(GridPoint direction, int maximumLength)
            {
                Direction = direction;
                MaximumLength = maximumLength;
            }

            public readonly GridPoint Direction;
            public readonly int MaximumLength;
            public double Weight;
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
            RunLength = 1;
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
        public int RunLength;
    }

    internal sealed class PipeSegment
    {
        public PipeSegment(
            GridPoint start,
            GridPoint end,
            int colourIndex,
            float startTrim,
            float endTrim)
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
        public PipeElbow(
            GridPoint centre,
            GridPoint incoming,
            GridPoint outgoing,
            int colourIndex)
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
        public PipeCap(GridPoint position, GridPoint outwardDirection, int colourIndex)
        {
            Position = position;
            OutwardDirection = outwardDirection;
            ColourIndex = colourIndex;
        }

        public GridPoint Position;
        public GridPoint OutwardDirection;
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

        public static GridPoint operator -(GridPoint value)
        {
            return new GridPoint(-value.X, -value.Y, -value.Z);
        }

        public static GridPoint operator *(GridPoint value, int scalar)
        {
            return new GridPoint(value.X * scalar, value.Y * scalar, value.Z * scalar);
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
