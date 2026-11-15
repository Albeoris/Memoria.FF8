using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDialogueMaskContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        session.SetEventMemory(session.EventMemory.Write(210, 2, NativeScalarValue.Exact(0xFFAB)));
        byte[] content = SyntheticObjects([1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.POPM_B, 211), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0),
             (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0),
             (Jsm.Opcode.PSHM_B, 211), (Jsm.Opcode.POPM_B, 512),
             (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WINCLOSE, 0),
             (Jsm.Opcode.PSHM_B, 211), (Jsm.Opcode.POPM_B, 513),
             (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.WINCLOSE, 0),
             (Jsm.Opcode.PSHM_B, 211), (Jsm.Opcode.POPM_B, 514), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]
        ]);
        EventEngine engine = new(session);
        engine.LoadField(600, content, ["Persistent"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(engine.EventMemory.Read(210, 2).RequireExact() == 0x00AB, "Native field loading did not clear the old dialogue mask while preserving its neighbor.");
        for (int callback = 0; callback < 40 && !session.State.Variables.ContainsKey(514); callback++)
        {
            engine.Step();
        }
        Require(session.State.Variables.ContainsKey(514) && engine.EventMemory.Read(512, 1).RequireExact() == 0xC4 && engine.EventMemory.Read(513, 1).RequireExact() == 0xC0 && engine.EventMemory.Read(514, 1).RequireExact() == 0x80, "Script reads lost persistent channel occupancy or channel-specific closure.");
        Require(engine.EventMemory.Read(210, 2).RequireExact() == 0x80AB && engine.CurrentDialogue is null, "Native dialogue mask writes changed adjacent bytes or retained a closed window.");
        Console.WriteLine("Native dialogue mask: loader reset, persistent channels2/6, script BYTE211 reads, per-channel close and neighboring-byte preservation passed.");
    }
}
