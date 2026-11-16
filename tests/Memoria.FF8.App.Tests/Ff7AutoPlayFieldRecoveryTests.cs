using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7AutoPlayFieldRecoveryTests
{
    [Fact]
    public void OriginalPartyRecoversThroughNativeMenuCommandsBeforeWalking()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_FIELD_RECOVERY_SAVE");
        Assert.SkipUnless(ff7 is not null && ff8 is not null && path is not null, "Select original resources and an external field-recovery continuation.");
        byte[] bytes = File.ReadAllBytes(path!);
        using MemoryStream input = new(bytes, writable: false);
        CombinedSessionSave source = CombinedSessionCodec.Read(input);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using Ff7Session session = Ff7Session.Restore(installation, source.Ff7!);
        using CrossoverGuestFieldVitals guests = new(ff8!, () => source, _ => throw new InvalidOperationException("Native field recovery changed the guest owner."));
        session.GuestFieldVitals = guests;
        Ff7SessionSave before = session.Capture();
        int[] knockedOut = before.Progress.Party.Where(id => id is >= 0 and <= 8 && !guests.StoryCharacters.Contains(id) && before.Progress.Characters.Single(character => character.Id == id).HitPoints == 0).ToArray();
        Assert.NotEmpty(knockedOut);
        int commands = 0;
        while (commands < 32 && Ff7Scene.TryApplyAutoPlayFieldRecovery(session))
        {
            commands++;
        }
        Assert.InRange(commands, 1, 31);
        Assert.All(knockedOut, id => Assert.True(session.Progress.Characters.Single(character => character.Id == id).HitPoints > 0));
        Assert.False(Ff7Scene.TryApplyAutoPlayFieldRecovery(session));
        Assert.Equal(JsonSerializer.Serialize(before.Vm), JsonSerializer.Serialize(session.Capture().Vm));
        Assert.Equal(before.Progress.Gil, session.Progress.Gil);
        Assert.Equal(before.Progress.CompletedBattles, session.Progress.CompletedBattles);
        Assert.Equal(before.Progress.Inventory.Where(slot => slot.ItemId == 7).Sum(slot => slot.Quantity) - knockedOut.Length, session.Progress.Inventory.Where(slot => slot.ItemId == 7).Sum(slot => slot.Quantity));
        Assert.All(guests.StoryCharacters, id => Assert.Equal(before.Progress.Characters.Single(character => character.Id == id), session.Progress.Characters.Single(character => character.Id == id)));
        using MemoryStream snapshot = new();
        CombinedSessionCodec.Write(snapshot, source with { Ff7 = session.Capture() });
        snapshot.Position = 0;
        using Ff7Session cold = Ff7Session.Restore(installation, CombinedSessionCodec.Read(snapshot).Ff7!);
        cold.GuestFieldVitals = guests;
        Assert.Equal(JsonSerializer.Serialize(session.Progress), JsonSerializer.Serialize(cold.Progress));
        Assert.False(Ff7Scene.TryApplyAutoPlayFieldRecovery(cold));
        Assert.Equal(bytes, File.ReadAllBytes(path!));
    }
}
