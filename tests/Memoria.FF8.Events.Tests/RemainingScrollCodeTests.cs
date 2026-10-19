using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void RemainingLayerScrollContracts()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        EventEngine Create(Jsm.Opcode opcode, int layer = 1, int selector = 0, int duration = 65539, bool certificate = true)
        {
            bool party = opcode != Jsm.Opcode.LSCROLLA2;
            bool continuous = opcode == Jsm.Opcode.DSCROLLP2;
            List<(Jsm.Opcode, int)> script = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETCAMERA, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, layer), (party ? Jsm.Opcode.PSHN_L : Jsm.Opcode.PSHAC, selector)];
            if (!continuous)
            {
                script.Add((Jsm.Opcode.PSHN_L, duration));
            }
            script.AddRange([(opcode, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000)]);
            if (!continuous)
            {
                script.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SCROLLSYNC2, 0), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_B, 5001)]);
            }
            script.AddRange([(Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)], script.ToArray()]);
            EventEngine engine = new(CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy())) { AnimationFrameCount = (_, _) => 1 };
            engine.ResolveNativeNormalBackgroundScrollCenter = () => ((short)40, (short)-80);
            engine.ResolveNativeLayerActorScrollCenter = (row, actor) => row == 1 && actor == 0 ? ((short)(engine.Frame * 2), (short)-engine.Frame) : null;
            engine.CanSetNativeLayerActorLinearBackgroundScroll = (row, actor, ticks) => certificate && row == 1 && actor == 0 && ticks >= 0;
            engine.CanSetNativeLayerActorCosineBackgroundScroll = (row, actor, ticks) => certificate && row == 1 && actor == 0 && ticks >= 0;
            engine.CanRestoreNativeActorScroll = (scene, _) => scene.ActorBackgroundScroll is { Layer: 1, NativeActorId: 0 } current && NativeActorScrollPhase.IsValid(current);
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.LSCROLLA2, Jsm.Opcode.DSCROLLP2, Jsm.Opcode.LSCROLLP2, Jsm.Opcode.CSCROLLP2 })
        {
            EventEngine live = Create(opcode);
            bool issue = false;
            live.CommandExecuted += command =>
            {
                if (command.Name == opcode.ToString())
                {
                    issue = true;
                    Require(live.Scene.ActorBackgroundScroll is { Layer: 1, NativeActorId: 0, Status: 0, X: 40, Y: -80 } current && (opcode == Jsm.Opcode.DSCROLLP2 || current.Duration == 3), "Layer scroll changed genuine current or failed lowWORD duration before the driver.");
                }
            };
            for (int callback = 0; callback < 8 && !issue; callback++)
            {
                live.Step();
            }
            bool continuous = opcode == Jsm.Opcode.DSCROLLP2;
            Require(issue && live.Scene.ActorBackgroundScroll is { Layer: 1, Status: 1, PreviousX: 40, PreviousY: -80 }, "Layer scroll did not issue or retain its explicit row history.");
            JsonElement saved = live.Capture();
            EventEngine restored = Create(opcode);
            restored.Restore(saved);
            for (int callback = 0; callback < 6; callback++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Layer actor/party camera changed after independent restore.");
            }
            Require(live.Scene.ActorBackgroundScroll!.Status == (continuous ? 1 : 2), "Layer interpolation/sync completion disagreed with the shared native phase.");
        }
        foreach (EventEngine declined in new[] { Create(Jsm.Opcode.LSCROLLA2, certificate: false), Create(Jsm.Opcode.LSCROLLP2, selector: 3), Create(Jsm.Opcode.CSCROLLP2, duration: -1), Create(Jsm.Opcode.DSCROLLP2, layer: 65537) })
        {
            try
            {
                for (int callback = 0; callback < 8; callback++)
                {
                    declined.Step();
                }
                throw new InvalidOperationException("Layer actor scroll accepted an unsupported selector, layer, duration or missing certificate.");
            }
            catch (EventScriptException)
            {
                Require(declined.Scene.ActorBackgroundScroll is null, "Refused layer scroll mutated its row owner.");
            }
        }
        Console.WriteLine("Layer actor/party scroll: sampled selectors, DWORD layer, WORD duration, real previous center, shared interpolation, completion and independent restore passed.");
    }
}
