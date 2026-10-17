using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Three party-only493840 prior-status mirrors; seven entries would overlap E30 and E38 in native memory.</summary>
public sealed record NativeBattlePartyStatusMirrors([property: JsonRequired] ImmutableArray<ushort?> Saved, [property: JsonRequired] ImmutableArray<uint?> Battle)
{
    public static NativeBattlePartyStatusMirrors Unknown() => new([null, null, null], [null, null, null]);

    public void Validate()
    {
        if (Saved.IsDefault || Saved.Length != 3 || Battle.IsDefault || Battle.Length != 3)
        {
            throw new InvalidDataException("Native493840 requires exactly three party E30/E38 status mirrors.");
        }
    }

    public NativeBattlePartyStatusMirrors Observe(int index, ushort saved, uint battle)
    {
        Validate();
        if (index is < 0 or >= 3)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return this with { Saved = Saved.SetItem(index, saved), Battle = Battle.SetItem(index, battle) };
    }
}

/// <summary>Physical process memory retained across battle entry; a new battle is not a new executable image.</summary>
public sealed record NativeBattleRetainedMemory([property: JsonRequired] NativeBattleActionPacketsState ActionPackets, [property: JsonRequired] NativeBattleAllocationControl? AllocationControl, [property: JsonRequired] NativeBattlePartyStatusMirrors PartyStatusMirrors, [property: JsonRequired] NativeBattleActionScratch ActionScratch, [property: JsonRequired] int? EffectIoResult, [property: JsonRequired] string? ColdExecutableSha256)
{
    [JsonRequired]
    public NativeBattleEffectMemory? EffectMemory { get; init; }
    public NativeBattleStageState? Stage { get; init; }
    public NativeBattleModelTextureCache? ModelTextureCache { get; init; }
    public NativeBattleModelLoader? ModelLoader { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFileStaging? FileStaging { get; init; }
    public NativeBattleModelCpuResources? ModelCpuResources { get; init; }
    public NativeBattleCameraState? Camera { get; init; }
    public NativeBattleFileIoState? FileIo { get; init; }
    public NativeVibrationProcessState? VibrationProcess { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMusicProcessState? MusicProcess { get; init; }
    [JsonRequired]
    public NativeBattleActionMemory ActionMemory { get; init; } = NativeBattleActionMemory.Unknown();

    [JsonRequired]
    public ImmutableArray<byte?> ElementAttack { get; init; } = [null, null, null, null, null, null, null];

    public static NativeBattleRetainedMemory FromColdProcess()
    {
        //Authenticated FF8_EN .data zero-fill: packet arenas, DF9/DFD/E01 and the three party E30/E38 mirrors.
        NativeBattleActionScratch scratch = new() { DeferredEffectsE00 = false, GuardianFormulaE1E = false, GuardianBoostPercent = 0, FinisherPendingE03 = false, LastPublishedTargetCountE05 = 0, FinisherActionIndexE2E = 0, ActorIndex = 0, Command = 0, Action = 0, EventActorB0F = 0, EventLane = 0 };
        return new(NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]), new(0, false, false), new([0, 0, 0], [0u, 0u, 0u]), scratch, 0, NativeFieldRuntimeProfile.FF8EnExecutableHash) { ElementAttack = [0, 0, 0, 0, 0, 0, 0], EffectMemory = NativeBattleEffectMemory.FromColdProcess(), Stage = NativeBattleStageState.FromColdProcess(), ModelTextureCache = NativeBattleModelTextureCache.FromColdProcess(), ModelCpuResources = NativeBattleModelCpuResources.FromColdProcess(), Camera = NativeBattleCameraState.FromColdProcess(), FileIo = NativeBattleFileIoState.FromColdProcess(), ActionMemory = NativeBattleActionMemory.FromLoaderBytes(new byte[56]) };
    }

    public static NativeBattleRetainedMemory Capture(BattleSnapshot snapshot, string? coldExecutableSha256)
    {
        NativeBattleActionScratch scratch = snapshot.NativeActionScratch ?? throw new InvalidDataException("Native retained memory requires its actual action scratch owner.");
        NativeBattleRetainedMemory result = new(snapshot.NativeActionPackets ?? throw new InvalidDataException("Native retained memory requires the actual action packet owner."), snapshot.NativeScriptState.AllocationControl, snapshot.NativePartyStatusMirrors ?? throw new InvalidDataException("Native retained memory requires its actual party status mirrors."), scratch with { ComponentCompletion = null }, snapshot.NativeEffectIoResult, coldExecutableSha256) { ElementAttack = snapshot.NativeElementAttack, EffectMemory = snapshot.NativeEffectMemory, Stage = snapshot.NativeStage, ModelTextureCache = snapshot.NativeModelTextureCache, ModelLoader = snapshot.NativeModelLoader, FileStaging = snapshot.NativeFileStaging, ModelCpuResources = snapshot.NativeModelCpuResources, Camera = snapshot.NativeCamera, FileIo = snapshot.NativeFileIo, VibrationProcess = snapshot.NativeVibrationProcess, MusicProcess = snapshot.NativeMusicProcess, ActionMemory = snapshot.NativeActionMemory ?? throw new InvalidDataException("Native retained action memory is missing.") };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (ActionPackets is null || PartyStatusMirrors is null || ActionScratch is null || ElementAttack.IsDefault || ElementAttack.Length != 7 || ActionScratch.ComponentCompletion is not null || ColdExecutableSha256 is not null && (ColdExecutableSha256.Length != 64 || !ColdExecutableSha256.All(Uri.IsHexDigit)))
        {
            throw new InvalidDataException("Native retained battle memory lacks its physical owners or valid cold-image provenance.");
        }
        ActionPackets.Validate();
        PartyStatusMirrors.Validate();
        ActionScratch.Validate();
        (ActionMemory ?? throw new InvalidDataException("Native retained action memory is missing.")).Validate();
        EffectMemory?.Validate();
        Stage?.Validate();
        ModelTextureCache?.Validate();
        ModelLoader?.Validate();
        FileStaging?.Validate();
        if (ModelLoader?.Task is { Phase: > 0 } && FileStaging is null)
        {
            throw new InvalidDataException("Native retained active model load requires its actual shared199B018 staging owner.");
        }
        ModelCpuResources?.Validate();
        Camera?.Validate();
        FileIo?.Validate();
        VibrationProcess?.Validate();
        MusicProcess?.Validate();
    }
}
