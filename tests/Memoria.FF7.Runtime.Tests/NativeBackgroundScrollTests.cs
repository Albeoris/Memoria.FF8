using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBackgroundScrollTests
{
    public static void Run()
    {
        Check(new Ff7BackgroundScroll(16, -16).Advance(1024, 1024) == new Ff7BackgroundScroll(16, -16, 16, -16), "BGSCR rate16 must advance exactly one signed sixteenth-pixel unit group per field callback.");
        Check(new Ff7BackgroundScroll(16, -16, 32760, -32760).Advance(1024, 1024) == new Ff7BackgroundScroll(16, -16, -16376, 16376), "BGSCR must wrap signed16 addition before the native signed remainder.");
        Check(new Ff7BackgroundScroll(16, -16, 24, -24).Advance(2, 2) == new Ff7BackgroundScroll(16, -16, 8, -8), "BGSCR must retain the sign of the remainder and use dimension<<4.");
        Check(new Ff7BackgroundScroll(0, 0, 16, -16).Advance(1024, 1024) == new Ff7BackgroundScroll(0, 0, 16, -16), "Stopping BGSCR must preserve its accumulated phase.");
        Check(new Memoria.FF7.Formats.PcFieldTriggers("manual", 0, 0, 0, 0, 0, 0, [], [], [], []).LayerCameraFactors.Count == 0, "Authored manual trigger resources must retain the empty optional camera-factor table.");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original BGSCR host: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "ujunon2");
        Check(field.Scripts.Data.Span.Slice(0x050F, 7).SequenceEqual(new byte[] { 0x2D, 0, 3, 16, 0, 0xF0, 0xFF }), "The selected original beach does not match the reached BGSCR operands.");
        Check(field.Triggers.LayerDimensions.SequenceEqual(new short[] { 1024, 1024, 1024, 1024 }) && field.Triggers.InitialLayerScrollOffsets.SequenceEqual(new short[4]), "The selected original beach dimensions/initial phases changed.");
        Check(field.Triggers.LayerCameraFactors.SequenceEqual(new short[] { 0, 0, 256, 256 }), "The original beach layer2 camera-fixed and layer3 unity parallax factors changed.");
        Ff7FieldHost host = new(field, (_, _) => 1);
        FieldHostRequest start = new(1, 1, 7, 0x050F, new FieldBackgroundCommand(FieldBackgroundOperation.Scroll, 3, 16, -16));
        Check(host.Execute(start).Completed && host.Process.BackgroundLayer3Scroll == new Ff7BackgroundScroll(16, -16), "BGSCR must publish rates immediately without advancing phase during dispatch.");
        host.AdvanceActors();
        Check(host.Process.BackgroundLayer3Scroll == new Ff7BackgroundScroll(16, -16, 16, -16) && host.Process.BackgroundLayer2Scroll == new Ff7BackgroundScroll(), "The existing post-VM field phase must advance only the selected layer once.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        Check(cold.Execute(start).Completed, "A completed BGSCR receipt must survive JSON restoration.");
        host.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Live/cold BGSCR phase changed after the next field callback.");
        Check(host.Process.BackgroundLayer3Scroll == new Ff7BackgroundScroll(16, -16, 32, -32), "Cold receipt polling reset or duplicated accumulated phase.");
        Check(host.Execute(new(2, 1, 7, 0x0519, new FieldBackgroundCommand(FieldBackgroundOperation.Scroll, 3, 0, 0))).Completed, "Stopping BGSCR failed.");
        host.AdvanceActors();
        Check(host.Process.BackgroundLayer3Scroll == new Ff7BackgroundScroll(0, 0, 32, -32), "Zero rates must keep accumulated offset.");
        Ff7FieldResources shifted = field with { Triggers = field.Triggers with { InitialLayerScrollOffsets = new short[] { 7, -8, 9, -10 } } };
        JsonObject legacy = JsonSerializer.SerializeToNode(host.Capture())!.AsObject();
        legacy.Remove(nameof(Ff7FieldProcess.BackgroundLayer2Scroll));
        legacy.Remove(nameof(Ff7FieldProcess.BackgroundLayer3Scroll));
        Ff7FieldHost older = new(shifted, (_, _) => 1, legacy.Deserialize<Ff7FieldProcess>()!);
        Check(older.Process.BackgroundLayer2Scroll == new Ff7BackgroundScroll(0, 0, 7, -8) && older.Process.BackgroundLayer3Scroll == new Ff7BackgroundScroll(0, 0, 9, -10), "Absent old-save scroll state must recover selected-resource offsets without replaying a script.");
        Ff7FieldHost next = new(shifted, (_, _) => 1, host.CreateNextFieldProcess(428));
        Check(next.Process.BackgroundLayer2Scroll == new Ff7BackgroundScroll(0, 0, 7, -8) && next.Process.BackgroundLayer3Scroll == new Ff7BackgroundScroll(0, 0, 9, -10), "A new field must reset rates and select its own initial phases.");
        Console.WriteLine("PASS original beach BGSCR rate/phase timing, signed wrap, stop, cold receipt, resource defaults and field reset");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
