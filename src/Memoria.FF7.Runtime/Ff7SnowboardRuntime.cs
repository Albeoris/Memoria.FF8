using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed class Ff7SnowboardRuntime
{
    internal const int NativeSubstepsPerTick = 2;
    private enum Stage
    {
        IntroFade,
        IntroAction,
        Playing,
        Ending,
        FinishWhite,
        FinishBlack,
        Finished
    }

    private const float AngleUnit = 0.0015339808305725455f;
    private readonly Ff7SnowboardNativeData native;
    private readonly Ff7SnowboardObjects objects;
    private readonly Ff7SnowboardPose pose;
    private readonly byte[][] tiles;
    private Stage stage;
    private int phaseClock;
    private int finishFade;
    private Vector3 nativeBase;
    private Vector3 cameraAngles = new(0, (float)Math.PI, 0);
    private Matrix4x4 view;
    private readonly Matrix4x4 projection = Ff7SnowboardCamera.CreateProjection(640, 480);
    private uint? overlay;
    private ushort held;
    private ushort pressed;
    private bool nativeFinished;

    internal Ff7SnowboardRuntime(Ff7Installation installation, FieldHostRequest request, uint seed)
    {
        if (request.Command is not FieldMinigameCommand { Game: 2 })
        {
            throw new InvalidDataException("The snowboard module requires its actual native Game 2 request.");
        }
        Request = request;
        Resources = Ff7SnowboardResources.LoadStory(installation);
        native = Ff7PcExecutableData.LoadSnowboardData(installation);
        if (native.InitialCameraParameters.Count != 8 || native.FinishVertices.Count != 6 || native.TerrainMarkers.Count != 6 || native.CameraActionClasses.Count != 32)
        {
            throw new InvalidDataException("The snowboard module is missing its original camera and scene tables.");
        }
        Simulation = new(native, seed);
        objects = new(Ff7PcExecutableData.LoadSnowboardObjectData(installation), native);
        pose = new(Ff7PcExecutableData.LoadSnowboardPresentationData(installation));
        Simulation.Rider.PoseReference += pose.SetReference;
        Simulation.Rider.PoseBlend += pose.Blend;
        Simulation.Sound += sound => Sound?.Invoke(sound.Channel, sound.Sound, sound.Volume);
        objects.Sound += sound => Sound?.Invoke(sound.Channel, sound.Sample, sound.Volume);
        tiles = new byte[76][];
        for (int tile = 0; tile < tiles.Length; tile++)
        {
            tiles[tile] = native.TileGeometry.Skip(tile * Ff7SnowboardTerrain.TileSize).Take(Ff7SnowboardTerrain.TileSize).ToArray();
        }
        nativeBase = CoursePosition;
        overlay = 0xFF000000;
        UpdateIntroCamera(256);
        Frame = BuildFrame();
    }

    public FieldHostRequest Request { get; }
    public Ff7SnowboardAssets Resources { get; }
    public Ff7SnowboardFrame Frame { get; private set; }
    public long Tick { get; private set; }
    public long NativeTicks { get; private set; }
    public bool Paused { get; private set; }
    public bool Finished => nativeFinished;
    public uint RandomSeed => Simulation.Rider.RandomSeed;
    public short Score => objects.ScoreCollected;
    public int Course => Simulation.Navigation.CurrentCourse;
    public int Segment => Simulation.Navigation.CurrentSegment;
    public string NativeProgress => $"{stage}:{phaseClock}:{finishFade}:{Simulation.Navigation.CurrentCourse}:{Simulation.Navigation.CurrentSegment}:{Simulation.Navigation.CurrentTriangle}:{Simulation.Rider.Motion.Position}:{Simulation.Rider.Script.ActionWord}:{Simulation.Rider.Script.ProgramCounter}:{Simulation.Rider.Script.Remaining}:{Score}";
    public byte NativeResult => Finished ? native.Results[Course] : throw new InvalidOperationException("The snowboard result is not available before native retirement.");
    internal Ff7SnowboardSimulation Simulation { get; }
    internal Ff7SnowboardNativeData NativeData => native;
    internal event Action<int, int, int>? Sound;

    internal void Advance(ushort held, ushort pressed)
    {
        if (Finished)
        {
            return;
        }
        this.held = held;
        this.pressed = pressed;
        for (int substep = 0; substep < NativeSubstepsPerTick; substep++)
        {
            bool sample = substep == NativeSubstepsPerTick - 1;
            if (sample)
            {
                overlay = null;
            }
            AdvanceNative(sample);
            NativeTicks++;
        }
        if (nativeFinished)
        {
            stage = Stage.Finished;
        }
        if (stage == Stage.Playing && (pressed & 0x800) != 0)
        {
            Paused = !Paused;
        }
        Tick++;
        Frame = BuildFrame();
    }

    private void AdvanceNative(bool sample)
    {
        switch (stage)
        {
            case Stage.IntroFade:
                if (sample)
                {
                    overlay = Gray(phaseClock);
                }
                phaseClock += 4;
                if (phaseClock >= 256)
                {
                    phaseClock = 0;
                    stage = Stage.IntroAction;
                }
                break;
            case Stage.IntroAction:
                if ((pressed & 0x800) != 0)
                {
                    Simulation.Rider.SignalLanding();
                    phaseClock = 255;
                }
                phaseClock++;
                if (sample && phaseClock < 128)
                {
                    overlay = White(256 - phaseClock * 2);
                }
                Simulation.PrepareIntro();
                nativeBase = CoursePosition;
                UpdateIntroCamera(phaseClock);
                objects.Advance(Simulation, Tick, 1, 2, presentationSample: sample);
                ApplyPose(Simulation.AdvanceIntroRider(held, pressed, Tick));
                if ((Simulation.Rider.Script.ActionWord & 255) != 16 && phaseClock > 255)
                {
                    stage = Stage.Playing;
                }
                break;
            case Stage.Playing:
                if (Paused)
                {
                    Simulation.PreparePaused();
                }
                else
                {
                    Simulation.AdvanceMotion(ending: false);
                    nativeBase = CoursePosition;
                    UpdateNormalCamera();
                }
                objects.Advance(Simulation, Tick, 1, CourseData.Auxiliary[Segment] + 1, advance: !Paused, presentationSample: sample);
                if (!Paused)
                {
                    ApplyPose(Simulation.AdvanceRider(held, pressed, Tick));
                }
                if ((CourseData.Attributes[Segment] & 0x80) != 0)
                {
                    nativeBase = ReadEndBase();
                    stage = Stage.Ending;
                }
                break;
            case Stage.Ending:
                AdvanceEnding(sample, fading: false);
                if (Simulation.LogicalDistance == CourseData.EndDistance)
                {
                    int action = Simulation.Rider.Script.ActionWord & 255;
                    if (Simulation.Navigation.CurrentTriangle >= 60 && (action == 1 || action is 17 or 18))
                    {
                        Simulation.Rider.SetAction(3);
                    }
                    if (Simulation.CourseEnded)
                    {
                        phaseClock = 0;
                        finishFade = 0;
                        stage = Stage.FinishWhite;
                    }
                }
                break;
            case Stage.FinishWhite:
                Simulation.Rider.Motion.Velocity -= new Vector3(0, 3072, 0);
                AdvanceEnding(sample, fading: true);
                phaseClock++;
                if (phaseClock > 60)
                {
                    finishFade += 2;
                    if (sample)
                    {
                        overlay = White(finishFade);
                    }
                    if (finishFade >= 252)
                    {
                        stage = Stage.FinishBlack;
                    }
                }
                break;
            case Stage.FinishBlack:
                finishFade -= 4;
                if (sample)
                {
                    overlay = Gray(finishFade);
                }
                if (finishFade <= 0)
                {
                    nativeFinished = true;
                }
                break;
        }
    }

    private void AdvanceEnding(bool sample, bool fading)
    {
        Simulation.AdvanceMotion(ending: true);
        Vector3 target = Simulation.CameraPositionQ12 * (1f / 4096) - nativeBase;
        float length = (float)Math.Sqrt((double)target.X * target.X + (double)target.Z * target.Z);
        view = Ff7SnowboardCamera.CreateRotation(new((float)Math.Atan2(target.Y, length), -(float)Math.Atan2(target.X, target.Z), 0));
        view.Translation = new(0, 96, 0);
        objects.Advance(Simulation, Tick, 7 - (CourseData.EndDistance - Simulation.LogicalDistance), 8, presentationSample: sample);
        int action = Simulation.Rider.Script.ActionWord & 255;
        ushort nativeHeld = (ushort)(fading && action is >= 3 and <= 6 ? 0x40 : 0);
        ApplyPose(Simulation.AdvanceRider(nativeHeld, 0, Tick));
    }

    private void UpdateIntroCamera(int clock)
    {
        short pitch;
        short yaw;
        short roll;
        Vector3 offset;
        if (clock < 256)
        {
            pitch = unchecked((short)((clock * 96 >> 8) - 32));
            yaw = unchecked((short)(4096 - (clock << 11 >> 8)));
            roll = 0;
            offset = new(0, (clock << 6 >> 8) + 64, (clock * 224 >> 8) + 96);
        }
        else
        {
            pitch = native.InitialCameraParameters[0];
            yaw = native.InitialCameraParameters[1];
            roll = native.InitialCameraParameters[2];
            offset = new(native.InitialCameraParameters[4], native.InitialCameraParameters[5], native.InitialCameraParameters[6]);
        }
        cameraAngles = new(pitch * AngleUnit, Ff7SnowboardCamera.WrapYaw(yaw * AngleUnit), roll * AngleUnit);
        view = Ff7SnowboardCamera.CreateView(Simulation.Rider.Motion.Position, nativeBase, offset, pitch, yaw, roll, 0);
    }

    private void UpdateNormalCamera()
    {
        Ff7SnowboardRiderMotionState rider = Simulation.Rider.Motion;
        byte[] tile = CurrentTile;
        int action = Simulation.Rider.Script.ActionWord & 255;
        if ((uint)action >= native.CameraActionClasses.Count)
        {
            throw new NotSupportedException("The snowboard camera has no native rider-action classification.");
        }
        Vector3 target = Ff7SnowboardCamera.ReadNormalTarget(tile, CourseData.Positions[Segment * 3 + 1], nativeBase, Simulation.CameraPositionQ12, Simulation.Navigation.LocalZ, Simulation.Surface.TurnAngle);
        cameraAngles = Ff7SnowboardCamera.AdvanceNormalAngles(cameraAngles, Simulation.Surface.CourseDirection, rider.Speed, rider.Velocity, native.CameraActionClasses[action], CourseData.Attributes[Segment], Simulation.CameraAirFrames, Word(tile, 2));
        view = Ff7SnowboardCamera.CreateNormalView(target, cameraAngles);
        Vector3 relative = Simulation.CameraPositionQ12 * (1f / 4096) - nativeBase;
        Vector4 screen = Vector4.Transform(new Vector4(relative, 1), view * projection);
        float y = (1 - screen.Y / screen.W) * 240;
        view = Ff7SnowboardCamera.AdjustNormalScreenY(view, y, 480);
    }

    private void ApplyPose(IReadOnlyList<Ff7SnowboardActionEffect> effects)
    {
        foreach (Ff7SnowboardActionFrame frame in effects.OfType<Ff7SnowboardActionFrame>())
        {
            pose.Begin(frame);
        }
        pose.Advance();
    }

    private Ff7SnowboardFrame BuildFrame()
    {
        List<Ff7SnowboardModelInstance> instances = [];
        foreach (Ff7SnowboardVisibleSegment visible in objects.VisibleSegments)
        {
            Ff7SnowboardCourseData course = native.Courses[visible.Course];
            Vector3 position = new(course.Positions[visible.Segment * 3], course.Positions[visible.Segment * 3 + 1], course.Positions[visible.Segment * 3 + 2]);
            Matrix4x4 transform = Ff7SnowboardObjects.BuildTerrainTransform(course.Angles[visible.Segment], position, nativeBase);
            uint models = unchecked((uint)course.Parameters[visible.Segment]);
            if (models == 0)
            {
                byte[] tile = tiles[course.Tiles[visible.Segment]];
                foreach (short marker in native.TerrainMarkers)
                {
                    Vector3 local = new(Word(tile, 8 + marker * 8), Word(tile, 10 + marker * 8), Word(tile, 12 + marker * 8));
                    Matrix4x4 placement = transform;
                    placement.Translation = Vector3.Transform(local, transform);
                    instances.Add(new(0xE7, placement) { NativePrimitiveKind = 2 });
                }
            }
            else
            {
                instances.Add(new((byte)models, transform));
                for (uint remaining = models >> 8; remaining != 0; remaining >>= 8)
                {
                    instances.Add(new((byte)remaining, transform));
                }
            }
        }
        foreach (Ff7SnowboardObjectFrame item in objects.Frames)
        {
            instances.Add(new(item.ModelIndex, Ff7SnowboardObjects.BuildObjectTransform(item, nativeBase)) { NativePrimitiveKind = item.NativePrimitiveKind, DrawArgument = item.DrawArgument });
        }
        Ff7SnowboardRiderController rider = Simulation.Rider;
        Vector3 relativePosition = rider.Motion.Position - nativeBase * 4096;
        Matrix4x4 actor = Ff7SnowboardPose.BuildActorPlacement(relativePosition, rider.Heading, rider.VisualAngles);
        instances.AddRange(pose.Sample(actor));
        Ff7SnowboardPhase phase = stage switch
        {
            Stage.IntroFade or Stage.IntroAction => Ff7SnowboardPhase.Intro,
            Stage.Playing => Ff7SnowboardPhase.Playing,
            Stage.Ending => Ff7SnowboardPhase.Finishing,
            Stage.FinishWhite or Stage.FinishBlack => Ff7SnowboardPhase.FadeOut,
            _ => Ff7SnowboardPhase.Finished
        };
        return new(Tick, phase, rider.Motion.Position, view, projection, instances.AsReadOnly(), 0, Course, Segment, rider.Motion.Speed) { OverlayArgb = overlay };
    }

    private Vector3 ReadEndBase()
    {
        int segment = CourseData.EndDistance - CourseData.StartDistance;
        byte[] tile = tiles[CourseData.Tiles[segment]];
        int vertex = native.FinishVertices[(Simulation.Navigation.CurrentTriangle / 2) % 6];
        short x = Word(tile, 8 + vertex * 8);
        short y = Word(tile, 10 + vertex * 8);
        short z = unchecked((short)(Word(tile, 12 + vertex * 8) - 128));
        (x, z) = Ff7SnowboardCourseNavigation.Rotate(x, z, CourseData.Angles[Segment]);
        return new(CourseData.Positions[segment * 3] + x, CourseData.Positions[segment * 3 + 1] + y, CourseData.Positions[segment * 3 + 2] + z);
    }

    private Ff7SnowboardCourseData CourseData => native.Courses[Course];
    private byte[] CurrentTile => tiles[CourseData.Tiles[Segment]];
    private Vector3 CoursePosition => new(CourseData.Positions[Segment * 3], CourseData.Positions[Segment * 3 + 1], CourseData.Positions[Segment * 3 + 2]);
    private static short Word(byte[] source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(offset));
    private static uint White(int alpha) => unchecked((uint)(byte)alpha << 24) | 0x00FFFFFF;
    private static uint Gray(int value) => 0xFF000000 | (uint)(byte)value * 0x00010101;
}
