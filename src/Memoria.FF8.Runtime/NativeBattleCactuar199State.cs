using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCactuar199State([property: JsonRequired] NativeBattleCactuarRawModelState RawModel, [property: JsonRequired] NativeBattleActionMatrix? BaseMatrix, [property: JsonRequired] NativeBattleActionMatrix? BaseView, [property: JsonRequired] uint? LookupCounter, [property: JsonRequired] int? VibrationHandle, [property: JsonRequired] uint TerminalFlags, [property: JsonRequired] NativeBattleGuardianCameraTask? CameraTask, [property: JsonRequired] int? FirstTargetActor)
{
    public static NativeBattleCactuar199State FromColdProcess() => new(NativeBattleCactuarRawModelState.FromColdProcess(), null, null, null, null, 0, null, null);
    public void Validate()
    {
        if (RawModel is null || RawModel.Model.IsDefault || RawModel.Model.Length != 0x90 || RawModel.Control.IsDefault || RawModel.Control.Length != 0x34 || FirstTargetActor is < 0 or >= 7)
        {
            throw new InvalidDataException("Native Cactuar requires its actual raw model/control and first-target owners.");
        }
        BaseMatrix?.Validate();
        BaseView?.Validate();
    }
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCactuar199State? Cactuar199
    {
        get => effectState.Cactuar199;
        init => effectState = effectState with { Cactuar199 = value };
    }
}
public sealed record NativeBattleCactuar199Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random)
{
    [JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public abstract record NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199Sound(uint Descriptor) : NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199StreamStart(uint Descriptor) : NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199StreamPlay(int Pan, int Log, int Volume) : NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199Flash(int Value) : NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199FullscreenFlash(byte A, byte B, byte C, byte D) : NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199Contact(int Contact) : NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199HitReaction(int Actor, int Mode) : NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199CommonTextureLoad(int DestinationArenaOffset, uint CompletionAddress) : NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199StopVibration(int Handle) : NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199CameraPublish(NativeBattleGuardianCameraStep Step) : NativeBattleCactuar199Operation;
public sealed record NativeBattleCactuar199ParticleStep(NativeBattleCactuar199Frame Frame, short FrameResult);
public sealed record NativeBattleCactuar199NeedleStep(NativeBattleCactuar199Frame Frame, bool BAccepted);
public sealed record NativeBattleCactuar199VibrationStep(NativeBattleCactuar199Frame Frame, int Handle);
public sealed record NativeBattleCactuar199Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<NativeBattleActionMatrix> ReadView, Func<NativeBattleCameraPose> ReadCameraPose, Func<NativeBattleGuardianCameraProcess> ReadCameraProcess, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<int, byte, short, ImmutableArray<byte>> ReadBone, Func<ushort, ImmutableArray<byte>> ReadCentroid, Func<NativeBattleCactuar199Frame, NativeBattleCactuar199Frame> DrawBody, Func<NativeBattleCactuar199Frame, NativeBattleModulePoolTask, NativeBattleCactuar199Frame> DrawMesh, Func<NativeBattleCactuar199Frame, ImmutableArray<byte>, NativeBattleCactuar199ParticleStep> DrawParticle, Func<NativeBattleCactuar199Frame, NativeBattleActionMatrix, NativeBattleVector, uint, NativeBattleCactuar199NeedleStep> DrawNeedle, Func<NativeBattleCactuar199Frame, NativeBattleCactuar199VibrationStep> StartVibration, Func<NativeBattleCactuar199Frame, NativeBattleCactuar199Operation, NativeBattleCactuar199Frame> Execute);
