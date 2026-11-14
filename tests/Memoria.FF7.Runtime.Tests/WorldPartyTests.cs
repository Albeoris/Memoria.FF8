using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldPartyTests
{
    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP original world party menu return: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        byte[] persistent = new byte[1280];
        persistent[0] = 152;
        persistent[29] = 255;
        persistent[0x109] = 0;
        persistent[0x10A] = 1;
        persistent[0x10B] = 2;
        using Ff7WorldRuntime world = new(installation, persistent, 1, new byte[] { 0, 1, 2 });
        for (int callback = 0; callback < 700 && !world.CanSave; callback++)
        {
            if (world.Process.Dialogue is { AwaitingInput: true, Completed: false } dialogue)
            {
                world.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
            }
            world.Advance();
        }
        Ff7WorldEntity before = world.Process.Entities.Single(entity => entity.Model == world.ControlledModel);
        world.SynchronizeMenuParty([6, 2, 1]);
        Check(world.Process is { ControlledModel: 1, PlayerModel: 1, PartyChange: { Joined0: 0x26, Joined1: 0, Left0: 0x20, Left1: 0 } } && ScriptParty(world).SequenceEqual(new[] { 6, 1, 2 }), "Native menu return must preserve script-party slots, publish character model deltas and select Tifa when Cloud leaves.");
        Ff7WorldEntity after = world.Process.Entities.Single(entity => entity.Model == 1);
        Check(after.X == before.X && after.Y == before.Y && after.Z == before.Z && after.AnimationFrame == before.AnimationFrame && !world.Process.Vm.Models.Any(model => model.Model == 0), "Changing the foot leader must preserve the current native object and script state without replaying on_load.");
        Check(world.ReadSpecial(17, 1) == 0x26 && world.ReadSpecial(18, 1) == 0 && world.ReadSpecial(19, 1) == 0x20 && world.ReadSpecial(20, 1) == 0 && world.ReadSpecial(14, 1) == 1, "World party selectors must expose their native joined, left and leader owners.");
        using Ff7WorldRuntime cold = new(installation, world.Machine.CopyPersistent(), 1, new byte[] { 6, 1, 2 });
        cold.Restore(Cold(world.Process));
        world.Advance();
        cold.Advance();
        Check(JsonSerializer.Serialize(world.Process) == JsonSerializer.Serialize(cold.Process), "World menu party changes must preserve exact cold continuation and the renamed script owner.");
        world.SynchronizeMenuParty([1, 6, 2]);
        Check(world.Process.PartyChange is null && ScriptParty(world).SequenceEqual(new[] { 6, 1, 2 }), "Reordering the main party must clear native menu deltas while preserving script-party order.");
        world.SynchronizeMenuParty([0, 4, 8]);
        Check(world.Process is { PlayerModel: 0, ControlledModel: 0, PartyChange: { Joined0: 0x20, Joined1: 0x24, Left0: 0x26, Left1: 0x21 } }, "Native party deltas must prioritize Cait Sith and cap each direction at two model slots.");
        world.SynchronizeMenuParty([6, 1, 8]);
        Check(world.Process is { PlayerModel: 2, ControlledModel: 2, PartyChange: { Joined0: 0x26, Joined1: 0x21, Left0: 0x20, Left1: 0x24 } }, "A party containing Cid and neither Cloud nor Tifa must choose the native Cid model and retain Cait Sith priority.");
        CheckStandaloneMenu(installation);
        Console.WriteLine("PASS native world PHS script-party synchronization, character deltas, leader identity and cold continuation");
    }

    private static void CheckStandaloneMenu(Ff7Installation installation)
    {
        string? checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(checkpoint))
        {
            Console.WriteLine("SKIP standalone world PHS: configure MEMORIA_FF7_WORLD_CHECKPOINT.");
            return;
        }
        byte[] source = File.ReadAllBytes(checkpoint);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave save = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("The selected world checkpoint has no FFVII state.");
        using Ff7Session sourceSession = Ff7Session.Restore(installation, save);
        Ff7SessionSave fixture = save;
        if (!sourceSession.CanUseMenuPhs)
        {
            Check(!sourceSession.TryReplaceMenuPartyMember(1, 4), "An original world checkpoint without native PHS permission must reject a party exchange.");
            byte[] variables = save.Vm.PersistentVariables.ToArray();
            variables[29] |= 1;
            variables[31] &= 0xFE;
            fixture = save with { Vm = save.Vm with { PersistentVariables = variables } };
            Console.WriteLine("PASS original standalone PHS lock; testing menu return with an isolated permission-operand fixture.");
        }
        using Ff7Session session = Ff7Session.Restore(installation, fixture);
        Check(session.CanUseMenuPhs, "The isolated menu fixture must permit native PHS.");
        int active = session.Progress.Party.First(character => character != 255 && (session.Progress.LockedCharacters & (1 << character)) == 0);
        int reserve = Enumerable.Range(0, 9).First(character => !session.Progress.Party.Contains(character) && (session.Progress.AvailableCharacters & (1 << character)) != 0 && (session.Progress.LockedCharacters & (1 << character)) == 0);
        int[] oldScript = ScriptParty(session.World!);
        Check(session.TryReplaceMenuPartyMember(active, reserve), "The native PHS transaction must accept its legal available, unlocked exchange.");
        Check(ScriptParty(session.World!).SequenceEqual(oldScript), "An open PHS menu must preserve script-party state until its native return.");
        Ff7SessionSave pending = session.Capture();
        Check(pending.WorldPartyMenuPending == true && pending.Progress.Party.Contains(reserve), "A save made inside PHS must retain its pending menu synchronization and recorded party changes.");
        using Ff7Session restored = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(pending))!);
        using Ff7Session legacy = Ff7Session.Restore(installation, pending with { WorldPartyMenuPending = null });
        session.CompleteWorldPlayerMenu();
        session.AdvanceTick(advanceClock: false);
        restored.AdvanceTick(advanceClock: false);
        legacy.AdvanceTick(advanceClock: false);
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(restored.Capture()), "A cold save from an open menu must perform the same native return as an explicit close, without replaying completed world scripts.");
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(legacy.Capture()), "An older save without menu-process metadata must recover pending synchronization from its recorded party facts.");
        Check(session.World!.ReadSpecial(17, session.World.ControlledModel) == reserve + 0x20 && session.World.ReadSpecial(19, session.World.ControlledModel) == active + 0x20, "Actual standalone PHS must publish its original joined and left character models.");
        for (int callback = 0; callback < 16; callback++)
        {
            session.AdvanceTick(advanceClock: false);
            restored.AdvanceTick(advanceClock: false);
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Native PHS return scripts must continue identically after cold restoration.");
        }
        Check(File.ReadAllBytes(checkpoint).AsSpan().SequenceEqual(source), "Original standalone PHS validation must leave its supplied save unchanged.");
    }

    private static int[] ScriptParty(Ff7WorldRuntime world)
    {
        return Enumerable.Range(0, 3).Select(index => (int)world.ReadPersistentByte(0x109 + index)).ToArray();
    }

    private static Ff7WorldProcess Cold(Ff7WorldProcess state)
    {
        return JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(state))!;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
