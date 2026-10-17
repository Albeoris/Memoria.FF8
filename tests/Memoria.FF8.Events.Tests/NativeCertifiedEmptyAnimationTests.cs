using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCertifiedEmptyAnimation(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        (Jsm.Opcode, int)[] operations = [(Jsm.Opcode.ANIMESYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000)];
        EventEngine Create(ushort terminal)
        {
            EventEngine engine = CreateNativeAnimationBackupFixture(vitals, economy, operations, ushort.MaxValue);
            engine.ReadNativeEmptyAnimationTerminals = models =>
            {
                Require(models.SequenceEqual([-1]), "Empty animation resource query changed actual retained physical model order.");
                return [terminal];
            };
            return engine;
        }
        EventEngine live = Create(257);
        live.Step();
        Require(live.Actors[0].NativeAnimationHeader is { BoundModelId: -1, EmptyModelTerminal: 257, Terminal: 257, Frame: 0, Flags: 0x2000 }, "Certified empty WORD12 was not copied by the genuine allocation binding.");
        for (int callback = 0; callback < 8; callback++)
        {
            live.Step();
        }
        EventEngine restored = Create(257);
        restored.Restore(live.Capture());
        for (int callback = 9; callback <= 17; callback++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Certified empty animation lost its physical phase or immutable model terminal after cold restore.");
            if (callback < 17)
            {
                Require(!CapturedVariables(live.Capture()).TryGetProperty("5000", out _), "ANIMESYNC completed before the native 16-unit phase reached physical WORD12.");
            }
        }
        Require(CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 77 && live.Actors[0].NativeAnimationHeader is { Frame: 0, Flags: 0x2800, EmptyModelTerminal: 257 }, "Certified empty animation failed to release its actual ANIMESYNC owner at the native repeating boundary.");
        EventEngine signed = Create(32769);
        signed.Step();
        for (int callback = 0; callback < 32; callback++)
        {
            signed.Step();
        }
        Require(signed.Actors[0].NativeAnimationHeader is { Frame: 0, Terminal: 32769, Flags: 0x2800 } && CapturedVariables(signed.Capture()).GetProperty("5000").GetInt32() == 77, "Native callback lost the original signed WORD terminal comparison.");
    }
}
