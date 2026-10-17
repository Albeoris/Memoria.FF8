using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public enum NativeBattleFieldReloadStage
{
    ModelCachesReset = 1
}

public sealed record NativeBattleFieldReloadState([property: JsonRequired] int Version, [property: JsonRequired] string ExecutableHash, [property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] long BattleEntryFrame, [property: JsonRequired] byte NativeResult, [property: JsonRequired] string OneHash, [property: JsonRequired] NativeBattleFieldReloadStage Stage)
{
    public void Validate()
    {
        if (Version != 1 || ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || FieldId is < 72 or > ushort.MaxValue || ProgramHash is not { Length: 64 } program || !program.All(Uri.IsHexDigit) || OneHash is not { Length: 64 } source || !source.All(Uri.IsHexDigit) || BattleEntryFrame < 1 || NativeResult is not (2 or 4) || Stage != NativeBattleFieldReloadStage.ModelCachesReset)
        {
            throw new InvalidDataException("Native kind3 pending loader lacks its actual reset stage and field/resource provenance.");
        }
    }
}

public sealed partial class EventEngine
{
    public NativeBattleFieldReloadState ExecuteNativeBattleFieldCacheReset(NativeFieldRegistrationResources resources, byte nativeResult)
    {
        NativeBattleCompletion battle = PendingInteractiveBattle ?? throw new NotSupportedException("Native5310F0 battle return requires its suspended scripted field battle.");
        if (nativeResult is not (2 or 4))
        {
            throw new NotSupportedException("Native result1/3/5 requires its actual saved183/fresh-field/game-over loader branch.");
        }
        resources.Validate();
        if (NativeRuntimeProfile is not { IsVerified: true } profile || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } initialized || !initialized.IsFrom(profile, FieldId, programHash) || resources.FieldId != FieldId || resources.ProgramHash != programHash || Scene.NativeActorLayoutVersion != 1 || Scene.NativeBattleFieldReload is not null)
        {
            throw new InvalidDataException("Native5310F0 requires the same retained, initialized field/program and no already-completed reload.");
        }
        if (Scene.NativeBattleFieldReloadState is { } pending)
        {
            pending.Validate();
            if (pending.FieldId != FieldId || pending.ProgramHash != programHash || pending.BattleEntryFrame != battle.Frame || pending.NativeResult != nativeResult || pending.OneHash != resources.One.Sha256)
            {
                throw new InvalidDataException("Pending kind3 resource registration no longer matches its original source.");
            }
            throw new NotSupportedException("Native kind3 reached532A40 resource/texture registration after actual5310F0; shared staging, skin/texture remap and instance-rebind producers remain required.");
        }
        NativeFieldModelCacheResetState reset = NativeFieldModelCacheResetState.Execute5310F0(profile);
        NativeBattleFieldReloadState state = new(1, profile.ExecutableHash, FieldId, programHash, battle.Frame, nativeResult, resources.One.Sha256, NativeBattleFieldReloadStage.ModelCachesReset);
        // Only actual5310F0 registration caches are changed. Paused actor records, clips, stacks and PCs remain retained.
        Scene.ModelParts = new([], [], []) { CacheReset = reset };
        Scene.NativeBattleFieldReloadState = state;
        return state;
    }
}
