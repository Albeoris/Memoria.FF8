using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeQuadFadeTests
{
    public static void Run()
    {
        Ff7FadeProcess baseline = new(0, 0, 0, 0, 0, 0, false, 0x80123456);
        Ff7FadeProcess fade = Ff7NativeFade.Start(new(1, 255, 255, 255, 16, 255), baseline);
        Check(fade.Position == 256 && !fade.EntryCurve && !fade.UsesFieldColor, "Ordinary white FADE must retain the linear native quad branch.");
        for (int callback = 1; callback <= 16; callback++)
        {
            Ff7FadeProcess cold = JsonSerializer.Deserialize<Ff7FadeProcess>(JsonSerializer.Serialize(fade))!;
            fade = Ff7NativeFade.Advance(fade);
            Check(fade == Ff7NativeFade.Advance(cold), "Fullscreen fade diverged on the next cold-restored callback.");
            uint expected = (uint)(256 - callback * 16) << 24 | 0x00010101;
            Check(fade.QuadColorArgb == expected && fade.QuadBlend == Ff7FadeQuadBlend.Alpha, "White FADE must complement native operands before packing RGB and advance alpha linearly.");
            Check(fade.FieldColorArgb == baseline.FieldColorArgb, "A fullscreen quad must preserve the independently owned field tint.");
            Check(fade.Completed == (callback == 16), "FADEW completion changed before the native endpoint.");
        }
        Check(fade.Type == 0 && fade.QuadColorArgb == 0x00010101, "The terminal callback must retain its transparent native quad.");
        fade = Ff7NativeFade.Advance(fade);
        Check(fade.QuadColorArgb is null && fade.FieldColorArgb == 0x80FFFFFF, "The following type0 callback must reset presentation.");

        foreach (int type in new[] { 1, 2 })
        {
            Ff7FadeProcess mixed = Ff7NativeFade.Start(new(type, 20, 220, 255, 16, type == 1 ? 255 : 0), baseline);
            mixed = Ff7NativeFade.Advance(mixed, movieActive: true);
            uint alpha = type == 1 ? 0u : 16u;
            Check(mixed.QuadColorArgb == (alpha << 24 | 0x00EC2401) && !mixed.UsesFieldColor, "Strict threshold endpoints and complemented RGB must use the ordinary quad contract.");
            Check(mixed.Position == (int)alpha, "Only type1 may complete early while a movie is active.");
        }
        Ff7FadeProcess sticky = Ff7NativeFade.Advance(Ff7NativeFade.Start(new(1, 255, 255, 255, 16, 255), baseline with { UsesFieldColor = true }));
        Check(sticky.UsesFieldColor && sticky.QuadColorArgb is null, "An existing sticky field-color selector must retain native tint ownership.");
        foreach (int component in new[] { 21, 219 })
        {
            Ff7FadeProcess tinted = Ff7NativeFade.Start(new(2, component, 0, 255, 16), baseline);
            Check(tinted.UsesFieldColor, "Strictly midrange operands must select the native sticky field-color branch.");
        }
        Ff7FadeProcess legacy = JsonSerializer.Deserialize<Ff7FadeProcess>("{\"Type\":1,\"Position\":256,\"Speed\":16,\"Red\":255,\"Green\":255,\"Blue\":255,\"UsesFieldColor\":false}")!;
        Check(Ff7NativeFade.Advance(legacy).QuadColorArgb == 0xF0010101, "Older continuations must restore absent presentation values without rejecting the save.");
        RunInstalledCommand();
    }

    private static void RunInstalledCommand()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed fullscreen fade command: original resources are unset.");
            return;
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root), "elminn_2");
        Check(field.Scripts.Data.Span.Slice(0x0751, 9).SequenceEqual(new byte[] { 0x6B, 0, 0, 255, 255, 255, 16, 1, 255 }), "The original reached fullscreen fade operands changed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
