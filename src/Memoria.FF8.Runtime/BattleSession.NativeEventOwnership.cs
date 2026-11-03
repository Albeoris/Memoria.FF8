namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void ValidateNativeEventOwner(string actorId, int? headerIndex, BattleNativeEventOwner? owner, byte? callbackStage, BattleNativeAction? native)
    {
        if (owner is null)
        {
            return;
        }
        if (!HasEncounterDamageRoutes || native is null || callbackStage != 4 && native.Command != 247 || string.IsNullOrWhiteSpace(owner.ActorId) || !byId.TryGetValue(owner.ActorId, out BattleCombatant? source) || source.NativeIndex is < 0 or >= 7 || owner.SourceHeaderIndex is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || headerIndex is int child && child <= owner.SourceHeaderIndex || headerIndex is null && native.Command != 247)
        {
            throw new InvalidDataException("Saved counter child lacks its accepted native source-event ownership.");
        }
        ValidateNativeHeaderReference(source, owner.SourceHeaderIndex);
        if (nativeActionScratch.EventActorB0F != source.NativeIndex)
        {
            throw new InvalidDataException("Saved counter source-event actor differs from the current native event.");
        }
        ValidateNativeHeaderReference(GetActor(actorId), headerIndex);
    }
}
