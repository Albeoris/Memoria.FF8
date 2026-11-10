using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePartyTurnTests
{
    public static void Run()
    {
        Ff7Actor source = new() { Entity = 15, Slot = 0, Model = 0, Direction = 240 };
        Ff7Actor target = new() { Entity = 7, Slot = 1, Model = 1, PartyCharacter = 4, X4096 = 50 * 4096, Visible = false };
        Ff7FieldProcess state = new() { Actors = [source, target], BlinkStateInitialized = true };
        int[] party = [255, 255, 255];
        FieldHostRequest request = new(1, 15, 1, 0xA77, new FieldActorFaceParty(0, 8, 2));
        Ff7FieldHost missing = Build(state, party, null);
        Check(missing.Execute(request).Completed && missing.GetActor(15) == source, "An empty script-party slot must skip without changing the source turn.");
        missing = Build(state, [8, 255, 255], null);
        Check(missing.Execute(request).Completed && missing.GetActor(15) == source, "An unmapped party character must skip without substituting the controlled actor.");
        missing = Build(state with { Actors = [source, target with { EntityBound = false }] }, [4, 255, 255], null);
        Check(missing.Execute(request).Completed && missing.GetActor(15) == source, "An unbound character model must skip native party facing.");
        bool rejected = false;
        try
        {
            missing.Execute(request with { Id = 2, Command = new FieldActorFaceParty(3, 8, 2) });
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }
        Check(rejected && missing.GetActor(15) == source, "An unsupported party-slot byte was accepted or mutated its actor.");
        Ff7FieldHost ambiguous = Build(state with { Actors = [source, target, target with { Slot = 2, Entity = 8 }] }, [4, 255, 255], null);
        rejected = false;
        try
        {
            ambiguous.Execute(request);
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }
        Check(rejected && ambiguous.GetActor(15) == source, "An ambiguous native character mapping was silently replaced by an arbitrary actor.");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP native party turn interpolation: original executable tables are not configured.");
            return;
        }
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(Ff7Installation.Discover(root));
        for (byte slot = 0; slot < 3; slot++)
        {
            party = [255, 255, 255];
            party[slot] = 4;
            Ff7FieldHost original = Build(state, party, native);
            request = request with { Command = new FieldActorFaceParty(slot, 8, 2) };
            Check(!original.Execute(request).Completed && original.GetActor(15) is { TurnMode: 2, TurnSteps: 8 }, "PTURA must resolve its character binding and start the shared eased eight-step turn.");
            int end = original.GetActor(15).TurnEnd;
            original.AdvanceActors();
            Ff7FieldHost cold = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(original.Capture()))!, party, native);
            int callbacks = 1;
            while (!original.Execute(request).Completed && callbacks <= 10)
            {
                Check(!cold.Execute(request).Completed, "Cold party facing completed before its native owner.");
                original.AdvanceActors();
                cold.AdvanceActors();
                callbacks++;
                Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold party-facing interpolation diverged.");
            }
            Check(cold.Execute(request).Completed && callbacks == 9 && original.GetActor(15).Direction == (end & 255) && original.GetActor(15).TurnMode == 0, "PTURA changed shared sample0-through8 timing or completion clearing.");
            Check(original.GetActor(15).X4096 == source.X4096 && original.GetActor(7).X4096 == target.X4096, "Party facing moved a source or target actor.");
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root), "mds5_1");
        Check(field.Scripts.Data.Span.Slice(0xA77, 4).SequenceEqual(new byte[] { 0x35, 0, 8, 2 }), "The original dog interaction no longer contains the admitted native PTURA operands.");
        Console.WriteLine("PASS PTURA party-slot/character/entity resolution, missing targets, shared turn timing and cold continuation");
    }

    private static Ff7FieldHost Build(Ff7FieldProcess state, int[] party, Ff7PcExecutableData? native)
    {
        PcWalkmeshTriangle triangle = new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), -1, -1, -1);
        Ff7FieldResources field = new("party-turn-fixture", null!, null!, null!, [], [triangle], null!, null!, null!);
        return new(field, (_, _) => 1, state)
        {
            ReadScriptParty = () => party,
            Movement = native is null ? null : new([triangle], native.NativeSinCos, native.NativeArcAngles)
        };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
