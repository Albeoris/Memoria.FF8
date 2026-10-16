using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeSwap()
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || !HasCanonicalPartySlots(Scene) || services.CharacterVitals is null || !services.CanStoreNativeEventOwners)
        {
            return "Native SWAP requires its actual saved field/combat party bytes, backup owner and saved-character publication";
        }
        if (UnsupportedJunctionBackupAliases() is string aliases)
        {
            return aliases;
        }
        try
        {
            _ = PrepareNativeSwap();
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private (NativeCharacterVitals Vitals, byte[] FieldParty) PrepareNativeSwap()
    {
        NativeCharacterVitals current = services.CharacterVitals!;
        current.Validate();
        byte[] fieldParty = current.JunctionPartyBackup.Take(3).ToArray();
        if (fieldParty.Where(identity => identity != 255).Distinct().Count() != fieldParty.Count(identity => identity != 255))
        {
            throw new NotSupportedException("Native SWAP would restore duplicate field body-slot ownership.");
        }
        //51E8A0 exchanges the raw banks before495EF0 rebuilds saved combat stats.
        NativeCharacterVitals next = (current.DeepCopy() with
        {
            CombatParty = current.JunctionPartyBackup.Skip(3).ToArray(),
            JunctionPartyBackup = [.. Scene.Party.Select(identity => checked((byte)identity)), .. current.CombatParty]
        }).RebuildParty();
        return (next, fieldParty);
    }

    private void ExecuteNativeSwap(Fiber fiber)
    {
        (NativeCharacterVitals next, byte[] fieldParty) = PrepareNativeSwap();
        CommitSavedCharacters(fiber, next, Jsm.Opcode.SWAP);
        Scene.Party.Clear();
        Scene.Party.AddRange(fieldParty.Select(identity => (int)identity));
        //Native return2 consumes no stack/register value and does not refresh actor bindings.
    }
}
