using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleStageSource(short RotationSpeed, ImmutableArray<NativeBattlePoseSource?> Groups, ImmutableArray<byte> TextureAnimations, NativeBattleEffectHeapAllocation Texture)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStagePhysicalSource? PhysicalSource { get; init; }
    public NativeBattleStageCallback? Callback { get; init; }
    public NativeBattleStageColors? ModelColors { get; init; }
    public uint? SpecializedColor { get; init; }
}
public sealed record NativeBattleStageColors(uint Color40, uint Color44);
public sealed record NativeBattleStageCallback(uint Address, bool? UpdateTexture0);
public sealed record NativeBattleStageTask(NativeBattleStageCallback Callback, short Phase);
public sealed record NativeBattleStageGroup([property: JsonRequired] byte Flags, [property: JsonRequired] short ColorDepth, [property: JsonRequired] uint ColorFlags, [property: JsonRequired] byte Animation, [property: JsonRequired] byte PoseFlags, [property: JsonRequired] byte Counter, [property: JsonRequired] byte FrameCount, [property: JsonRequired] NativeBattlePoseSource? Source, [property: JsonRequired] ImmutableArray<NativeBattleActionMatrix> Matrices);

/// <summary>506C50/500EA0/500FD0's four physical44-byte stage rows and rotating group3 DWORD.</summary>
public sealed partial record NativeBattleStageState([property: JsonRequired] uint Flags, [property: JsonRequired] int Rotation, [property: JsonRequired] short RotationSpeed, [property: JsonRequired] ImmutableArray<NativeBattleStageGroup> Groups)
{
    public NativeBattleStageTask? Task { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFinalStage142State? FinalStage142 { get; init; }
    public NativeBattleStageSpecializedState? Specialized { get; init; }
    public NativeBattleStageExtendedState? Extended { get; init; }
    public ImmutableArray<NativeBattleRawMesh>? MutableGeometry { get; init; }
    public NativeBattleStagePaletteAllocation? PaletteAllocation { get; init; }
    public ImmutableArray<byte>? TextureAnimations { get; init; }
    public NativeBattleStageColors? ModelColors { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? SourceAddress1D9A820 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? ReloadColorSeed1D9A824 { get; init; }
    public static NativeBattleStageState FromColdProcess() => new(0, 0, 0, Enumerable.Range(0, 4).Select(_ => new NativeBattleStageGroup(0, 0, 0, 0, 0, 0, 0, null, [])).ToImmutableArray()) { ModelColors = new(0x00808080, 0x32808080), FinalStage142 = NativeBattleFinalStage142State.FromColdProcess(), Specialized = NativeBattleStageSpecializedState.FromColdProcess(), Extended = NativeBattleStageExtendedState.FromColdProcess(), SourceAddress1D9A820 = 0, ReloadColorSeed1D9A824 = 0 };

    public void Validate()
    {
        if (SourceAddress1D9A820 is uint sourceAddress && (sourceAddress & 3) != 0)
        {
            throw new InvalidDataException("Native stage source pointer must preserve its actual DWORD alignment.");
        }
        if (TextureAnimations is { IsDefault: true } || MutableGeometry is { IsDefault: true } || Task is { } task && (task.Callback is null || task.Callback.Address == 0 || task.Callback.UpdateTexture0 is null && !HasSpecializedService(task.Callback.Address)) || Groups.IsDefault || Groups.Length != 4 || Groups.Any(group => group is null || group.Matrices.IsDefault || group.Source is null && !group.Matrices.IsEmpty || group.Source is not null && group.Matrices.Length != group.Source.BoneCount || group.Counter > group.FrameCount))
        {
            throw new InvalidDataException("Native stage requires its four physical group rows, decoded counters and bone matrix banks.");
        }
        FinalStage142?.Validate();
        Specialized?.Validate();
        Extended?.Validate();
        if (MutableGeometry is { } geometry)
        {
            foreach (NativeBattleRawMesh mesh in geometry)
            {
                mesh.Validate();
            }
        }
        foreach (NativeBattleStageGroup group in Groups)
        {
            group.Source?.Validate();
            foreach (NativeBattleActionMatrix matrix in group.Matrices)
            {
                matrix.Validate();
            }
        }
    }

    private bool HasSpecializedService(uint address)
    {
        return address switch
        {
            NativeBattleFinalStage142.CallbackAddress => FinalStage142 is not null,
            0x518F50 or 0x50DFF0 or 0x511EF0 or 0x511EB0 => Extended is not null,
            _ => NativeBattleStageSpecialized.Supports(address) && Specialized is not null
        };
    }

    public NativeBattleStageState Reset506C50()
    {
        Validate();
        return this with { Flags = 0, Task = null, Specialized = Specialized is { } specialized ? specialized with { PublishedColor = 0 } : null, Groups = Groups.Select(group => group with { Flags = (byte)(group.Flags & 0xF8), ColorDepth = 0, PoseFlags = 0 }).ToImmutableArray() };
    }

    public NativeBattleStageState Initialize500EA0(NativeBattleStageSource source)
    {
        Validate();
        if (source.Groups.IsDefault || source.Groups.Length != 4)
        {
            throw new InvalidDataException("Native500EA0 requires its original four group sections.");
        }
        NativeBattleStageColors colors = source.ModelColors ?? throw new NotSupportedException("Native500EA0 requires its original initializer color producers; process-global color input is unknown.");
        ImmutableArray<NativeBattleStageGroup>.Builder groups = Groups.ToBuilder();
        for (int index = 0; index < 4; index++)
        {
            NativeBattleStageGroup group = groups[index];
            groups[index] = source.Groups[index] is { } pose ? Select509440(group with { Flags = (byte)(group.Flags | 7), Source = pose }) : group with { Flags = (byte)(group.Flags & 0xF8) };
        }
        NativeBattleStageState result = this with { Rotation = 0, RotationSpeed = source.RotationSpeed, Groups = groups.ToImmutable(), TextureAnimations = source.TextureAnimations.IsEmpty ? null : source.TextureAnimations, ModelColors = colors, MutableGeometry = source.Callback?.Address == 0x50DFF0 ? source.Geometry : null, SourceAddress1D9A820 = source.PhysicalSource is { } physical ? checked(physical.Address + (uint)physical.DirectoryOffset) : SourceAddress1D9A820 };
        result.Validate();
        return result;
    }

    public NativeBattleStageState Step500FD0(NativeBattleActionMatrix camera)
    {
        Validate();
        camera.Validate();
        ImmutableArray<NativeBattleStageGroup>.Builder groups = Groups.ToBuilder();
        for (int index = 0; index < 4; index++)
        {
            NativeBattleStageGroup group = groups[index];
            if (group.Source is null)
            {
                throw new NotSupportedException("Native500FD0 reached a stage group without its retained original skeleton and animation owner.");
            }
            if ((Flags & 4) == 0)
            {
                group = group.Counter >= group.FrameCount ? Select509440(group) : Advance508F90(group);
            }
            short angle = index == 3 ? unchecked((short)((short)Rotation >> 3)) : (short)0;
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(group.Matrices, camera, angle) };
            groups[index] = group;
        }
        return this with { Rotation = (Flags & 1) == 0 ? unchecked(Rotation + RotationSpeed) : Rotation, Groups = groups.ToImmutable() };
    }

    private static NativeBattleStageGroup Select509440(NativeBattleStageGroup group)
    {
        NativeBattlePoseSource source = group.Source ?? throw new InvalidDataException("Native509440 lacks its original stage animation resource.");
        byte frames = NativeBattlePoseDecoder.ReadFrameCounts(source.Animations.AsSpan())[0];
        byte limit = (group.PoseFlags & 1) != 0 ? unchecked((byte)(frames * 2 - 1)) : frames;
        return Advance508F90(group with { Animation = 0, PoseFlags = (byte)(group.PoseFlags & 0xF3), Counter = 0, FrameCount = limit });
    }

    private static NativeBattleStageGroup Advance508F90(NativeBattleStageGroup group)
    {
        NativeBattlePoseSource source = group.Source ?? throw new InvalidDataException("Native508F90 lacks its original stage resource.");
        byte counter = checked((byte)Math.Min(group.FrameCount, group.Counter + (((group.PoseFlags >> 1) & 1) + 1)));
        byte flags = (byte)((group.PoseFlags & 0xF3) | ((((group.PoseFlags >> 2) + 1) & 1) << 2));
        NativeBattleDecodedPose pose = source.Sample(group.Animation, counter, group.PoseFlags);
        return group with { Counter = counter, PoseFlags = flags, Matrices = NativeBattlePoseMatrices.StageBoneMatrices(source, pose) };
    }
}
