using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDiscJumpContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeSteamDiscMount mount = NativeSteamDiscMountReader.Read(gameDirectory, 2);
        Require(mount.SelectedDisc == 2 && mount.MarkerLength > 0 && File.Exists(Path.Combine(gameDirectory, "Data", "disk", "DISK1")), "Steam marker selection inferred the first physical marker instead of the requested second disc.");
        Require(NativeSteamDiscMountReader.Read(gameDirectory, 3).SelectedDisc == 3, "Steam marker selection retained the earlier mounted number.");
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.DISC, 0), (Jsm.Opcode.PSHN_L, 66184), (Jsm.Opcode.PSHN_L, 65537), (Jsm.Opcode.PSHN_L, 65534), (Jsm.Opcode.PSHN_L, 65533), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.DISCJUMP, 65578), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.RET, 8)]]);
        (GameSession Session, EventEngine Engine) Create(Func<byte, NativeSteamDiscMount>? provider)
        {
            GameSession target = GameSession.CreateColdProcess();
            target.StartNewGame();
            target.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            target.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            target.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
            target.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            target.RequestField(99, 0);
            EventEngine result = new(target) { ReadSteamDiscMount = provider };
            result.LoadField(99, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (target, result);
        }
        var issuing = Create(_ => mount);
        issuing.Engine.Step();
        issuing.Engine.Step();
        FieldTransition arrival = issuing.Session.State.FieldArrival ?? throw new InvalidOperationException("DISCJUMP did not issue a destination packet.");
        Require(arrival.FieldId == 648 && arrival.X == 1 && arrival.Y == -2 && arrival.NativeExtraWord == -3 && arrival.Direction == 255 && arrival.Triangle == 42, "DISCJUMP changed original low-WORD operand truncation or facing-byte arrival projection.");
        Require(arrival.DiscJump is { NativeMode: 6, NativeTransitionKind: 9, NativeFieldEntryKind: 0, NativeRequest4900: 1, NativeReturn: 1, PhysicalSlot: 0, NativeFacingWord: 511, Mount.SelectedDisc: 2 } && !issuing.Session.State.Variables.ContainsKey(500) && issuing.Engine.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "DISCJUMP lost its mode6/kind9 discriminator or executed the outgoing script past the polling request.");
        JsonElement emitted = JsonSerializer.SerializeToElement(arrival);
        Require(JsonSerializer.Deserialize<FieldTransition>(emitted)?.DiscJump == arrival.DiscJump, "DISCJUMP's native entry/mount discriminator did not serialize independently.");
        JsonElement source = issuing.Engine.Capture();
        JsonElement owner = source.GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean() && !fiber.GetProperty("Complete").GetBoolean());
        int jump = JsmProgram.Read(content).Operations.Single(operation => operation.Opcode == Jsm.Opcode.DISCJUMP).Index;
        Require(owner.GetProperty("Position").GetInt32() == jump && owner.GetProperty("Stack").GetArrayLength() == 0, "DISCJUMP did not consume five operands and retain its native return1 instruction pointer.");
        long frame = issuing.Engine.Frame;
        issuing.Engine.Step();
        Require(issuing.Engine.Frame == frame && !issuing.Session.State.Variables.ContainsKey(500), "DISCJUMP's departing engine resumed after a transition request.");

        foreach (Func<byte, NativeSteamDiscMount>? provider in new Func<byte, NativeSteamDiscMount>?[] { null, _ => mount with { SelectedDisc = 1 }, _ => throw new IOException("Selected marker unavailable") })
        {
            var refused = Create(provider);
            refused.Engine.Step();
            try
            {
                refused.Engine.Step();
                throw new InvalidOperationException("DISCJUMP admitted an absent or wrong selected marker.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement frozen = refused.Engine.Capture();
            JsonElement failing = frozen.GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean() && !fiber.GetProperty("Complete").GetBoolean());
            Require(refused.Engine.LastFault?.Opcode == "DISCJUMP" && failing.GetProperty("Position").GetInt32() == jump && failing.GetProperty("Stack").GetArrayLength() == 5 && refused.Session.State.FieldArrival is null && refused.Session.State.FieldId == 99, "Refused Steam DISCJUMP mutated the issuing PC, stack or destination.");
        }
        Console.WriteLine("DISC JUMP: selected Steam markers, original operand widths, physical slot, mode6/kind9/entry0, return1 boundary and atomic missing/wrong-marker refusal passed.");
    }
}
