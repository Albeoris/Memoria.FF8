using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleRinoa88State([property: JsonRequired] int SourceArenaOffset, [property: JsonRequired] int AuxiliaryCursor, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa88SceneMemory? Scene { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa88IoState? Io { get; init; }
    [JsonRequired]
    public ImmutableDictionary<uint, NativeBattleEffectOwnedModelState> OwnedModels { get; init; } = ImmutableDictionary<uint, NativeBattleEffectOwnedModelState>.Empty;
    [JsonRequired]
    public ImmutableArray<byte> BodyProjected { get; init; }
    [JsonRequired]
    public ImmutableDictionary<uint, ImmutableArray<NativeBattleActionMatrix>> BodyBones { get; init; } = ImmutableDictionary<uint, ImmutableArray<NativeBattleActionMatrix>>.Empty;
    public static NativeBattleRinoa88State FromColdProcess() => new(0, 0, null, null, null)
    {
        BodyProjected = new byte[2416].ToImmutableArray(),
        MorphScratch = new byte[1440].ToImmutableArray()
    };

    public int? EmitterOffset { get; init; }
    public int? GeometryOffset { get; init; }
    public int Counter25ACD34 { get; init; }
    public int Counter25A6BBC { get; init; }
    public ushort Word25ACCF0 { get; init; }
    public ushort Word25AC6D8 { get; init; }
    public ushort Word25A6BB8 { get; init; }
    public ushort Word25AC6E8 { get; init; }
    public ushort Word25AC3A4 { get; init; }
    public ushort Word25A4DA0 { get; init; }

    public void Validate()
    {
        if (SourceArenaOffset < 0 || SourceArenaOffset >= NativeBattleEffectArena.Length || AuxiliaryCursor < 0 || AuxiliaryCursor > NativeBattleEffectArena.Length || PrimaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || SecondaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || BodyProjected.IsDefault || BodyProjected.Length != 2416 || MorphScratch.IsDefault || MorphScratch.Length != 1440 || OwnedModels is null || BodyBones is null || Counter25ACD34 is not (0 or 1) || Counter25A6BBC is not (0 or 1) || EmitterOffset.HasValue != GeometryOffset.HasValue || EmitterOffset is < 0 || GeometryOffset is { } geometry && (geometry != EmitterOffset + 2160 || geometry > NativeBattleEffectArena.Length - 47040))
        {
            throw new InvalidDataException("Native Rinoa88 process storage contradicts its original physical owners.");
        }
        if (NodeData is { } data && (data.IsDefault || data.Length != NativeBattleRinoa88NodeResources.Length))
        {
            throw new InvalidDataException("Native Rinoa88 cached node bytes leave their actual file410 owner.");
        }
        Scene?.Validate();
        Io?.Validate();
        CopiedCamera?.Validate();
        foreach ((uint address, NativeBattleEffectOwnedModelState model) in OwnedModels)
        {
            if (address != model.TaskAddress || model.ResourceAddress is not (0x154F91C or 0x155563C))
            {
                throw new InvalidDataException("Native Rinoa88 owned model changed its original task or resource identity.");
            }
        }
        foreach (ImmutableArray<NativeBattleActionMatrix> bones in BodyBones.Values)
        {
            if (bones.IsDefault)
            {
                throw new InvalidDataException("Native Rinoa88 retained bone owner is missing.");
            }
            foreach (NativeBattleActionMatrix bone in bones)
            {
                bone.Validate();
            }
        }
    }

}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa88State? Rinoa88 { get; init; }
}
