namespace Memoria.FF8.Runtime;

public enum NativeBattleSummonFloorMode
{
    BelowTargetPlane,
    TargetPlane
}

public enum NativeBattleTargetBoundsMode
{
    AllContacts,
    FirstContact
}

public enum NativeBattleMirroredSurfaceMode
{
    InitialCapture,
    ContinuousCapture
}

/// <summary>Spatial presentation traits supplied by the scene that owns the battle.</summary>
public sealed record NativeBattleEffectSceneContext(NativeBattleSummonFloorMode FloorMode, NativeBattleTargetBoundsMode TargetBoundsMode)
{
    public NativeBattleMirroredSurfaceMode? MirroredSurfaceMode { get; init; }

    public static NativeBattleEffectSceneContext FromNativeEncounter(ushort encounterId, byte? stageResourceIndex = null)
    {
        return new(encounterId == 0x3F ? NativeBattleSummonFloorMode.TargetPlane : NativeBattleSummonFloorMode.BelowTargetPlane, encounterId == 796 ? NativeBattleTargetBoundsMode.FirstContact : NativeBattleTargetBoundsMode.AllContacts)
        {
            MirroredSurfaceMode = stageResourceIndex is { } stage ? FromNativeStage(stage) : null
        };
    }

    public NativeBattleMirroredSurfaceMode RequireMirroredSurfaceMode(byte? nativeStageResourceIndex)
    {
        Validate();
        return MirroredSurfaceMode ?? (nativeStageResourceIndex is { } stage ? FromNativeStage(stage) : throw new NotSupportedException("The mirrored surface requires its scene capture policy."));
    }

    public void Validate()
    {
        if (!Enum.IsDefined(FloorMode) || !Enum.IsDefined(TargetBoundsMode) || MirroredSurfaceMode is { } mirror && !Enum.IsDefined(mirror))
        {
            throw new InvalidDataException("The battle scene has an unsupported spatial presentation trait.");
        }
    }

    private static NativeBattleMirroredSurfaceMode FromNativeStage(byte stageResourceIndex)
    {
        return stageResourceIndex is 0x2C or 0x2D or 0x2E ? NativeBattleMirroredSurfaceMode.ContinuousCapture : NativeBattleMirroredSurfaceMode.InitialCapture;
    }
}
