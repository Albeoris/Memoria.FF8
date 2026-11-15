using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeConstantFadeTests
{
    public static void Run()
    {
        Ff7FadeProcess start = Ff7NativeFade.Start(new(8, 240, 240, 200, 8), null);
        Check(start.Position == 0 && start.FieldColorArgb == 0x80FFFFFF && start.QuadColorArgb is null, "Constant tint was published before its native updater.");
        for (int callback = 1; callback <= 32; callback++)
        {
            Ff7FadeProcess cold = JsonSerializer.Deserialize<Ff7FadeProcess>(JsonSerializer.Serialize(start))!;
            start = Ff7NativeFade.Advance(start, movieActive: true);
            Check(start == Ff7NativeFade.Advance(cold, movieActive: true), "Constant fade diverged after cold restoration.");
            Check(start.Type == 8 && start.Position == Math.Min(255, callback * 8) && start.Completed == (callback == 32), "Type8 timer or movie independence changed.");
            Check(start.FieldColorArgb == 0x80F0F0C8 && start.QuadColorArgb is null, "Type8 must publish a constant field tint without a quad.");
        }
        Check(Ff7NativeFade.Advance(start).Position == 255, "Type8 terminal position did not persist.");
        Ff7FadeProcess zeroSpeed = Ff7NativeFade.Advance(Ff7NativeFade.Start(new(8, 1, 2, 3, 0), null));
        Check(!zeroSpeed.Completed && zeroSpeed.Position == 0 && zeroSpeed.FieldColorArgb == 0x80010203, "Zero speed invented timer progress or suppressed constant tint.");
        Ff7FadeProcess reverse = Ff7NativeFade.Start(new(7, 240, 240, 200, 8, 15), null);
        reverse = Ff7NativeFade.Advance(reverse);
        Check(reverse.Type == 7 && reverse.Position == 8 && reverse.FieldColorArgb == 0x80F0F0C8, "Type7 did not subtract its native timer.");
        reverse = Ff7NativeFade.Advance(reverse);
        Check(reverse.Type == 0 && reverse.Completed && reverse.FieldColorArgb == 0x80F0F0C8, "Type7 terminal callback reset tint too early.");
        Check(Ff7NativeFade.Advance(reverse).FieldColorArgb == 0x80FFFFFF, "Following type0 callback did not reset tint.");
        Ff7FadeProcess movie = Ff7NativeFade.Advance(Ff7NativeFade.Start(new(7, 240, 240, 200, 8, 255), null), movieActive: true);
        Check(movie.Type == 0 && movie.Position == 0 && movie.FieldColorArgb == 0x80F0F0C8, "Type7 movie completion lost its final tint callback.");
        Ff7FadeProcess packed = Ff7NativeFade.Advance(Ff7NativeFade.Start(new(8, 0, 0, 0, 1), null));
        Check(packed.FieldColorArgb == 0x81010100, "Native inverse256 packing was incorrectly clamped to bytes.");
        Ff7FadeProcess independent = Ff7NativeFade.Start(new(8, 240, 240, 200, 8), new(0, 0, 0, 0, 0, 0, true));
        Check(independent.UsesFieldColor && independent.HasConstantFieldColor, "The constant selector overwrote the independent ordinary tint selector.");
        Ff7FadeProcess replacement = Ff7NativeFade.Start(new(5, 255, 255, 255, 2, 255), independent);
        replacement = JsonSerializer.Deserialize<Ff7FadeProcess>(JsonSerializer.Serialize(replacement))!;
        Check(replacement.Type == 5 && replacement.HasConstantFieldColor && replacement.FieldColorArgb == 0x80FFFFFF, "Command replacement or cold restore lost the sticky selector or emitted early tint.");
        Check(Ff7NativeFade.Advance(replacement).QuadColorArgb == 0x80FDFDFD, "The sticky constant selector redirected type5's separate consumer.");
        Ff7FadeProcess reset = Ff7NativeFade.Advance(Ff7NativeFade.Start(new(0, 0, 0, 0, 0), replacement));
        Check(!reset.HasConstantFieldColor && !reset.UsesFieldColor, "Type0 did not clear both independent selectors.");
        Ff7FadeProcess legacy = JsonSerializer.Deserialize<Ff7FadeProcess>("{\"Type\":8,\"Position\":0,\"Speed\":8,\"Red\":240,\"Green\":240,\"Blue\":200,\"UsesFieldColor\":false}")!;
        Check(legacy.HasConstantFieldColor && Ff7NativeFade.Advance(legacy).FieldColorArgb == 0x80F0F0C8, "An absent presentation selector could not be derived from its recorded fade type.");
        CheckOriginal();
    }

    private static void CheckOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CONSTANT_FADE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original constant fade: configure resources and an external standalone pending fade fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session first = Ff7Session.Restore(installation, saved);
        using Ff7Session second = Ff7Session.Restore(installation, saved);
        Ff7SessionSave restored = first.Capture();
        Check(Equal(restored.Vm, saved.Vm) && Equal(restored.Progress, saved.Progress) && Equal(restored.TickContinuation, saved.TickContinuation) && Equal(restored.Process.FadeState, saved.Process.FadeState), "Cold restore changed pending native execution or fade facts.");
        Check(Equal(restored, second.Capture()), "Independent cold restorations differ.");
        first.SetInput(0, 0);
        second.SetInput(0, 0);
        first.AdvanceTick(advanceClock: false);
        second.AdvanceTick(advanceClock: false);
        Check(JsonSerializer.Serialize(first.Capture()) == JsonSerializer.Serialize(second.Capture()), "Independent pending fade continuations differ.");
        Check(first.Host.Process.FadeState is { Type: 5, FieldColorArgb: 0x80FFFFFF }, "Same-cycle replacement emitted intermediate type8 tint or lost type5.");
        Check(first.Host.Process.FadeState!.HasConstantFieldColor, "The actual same-cycle command replacement lost the constant selector.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Constant fade continuation changed its external source.");
        Console.WriteLine("PASS original pending fade and same-cycle replacement");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool Equal<T>(T first, T second)
    {
        return JsonNode.DeepEquals(JsonSerializer.SerializeToNode(first), JsonSerializer.SerializeToNode(second));
    }
}
