using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBlinkTests
{
    public static void Run()
    {
        byte[] random = Enumerable.Range(0, 256).Select(index => (byte)index).ToArray();
        Ff7Actor Actor(int slot) => new() { Slot = slot, Entity = slot, Model = slot, Visible = true };
        Ff7FieldProcess initial = new() { BlinkStateInitialized = true, BlinkRandomIndex = 255, Actors = [Actor(2), Actor(0), Actor(3) with { AutomaticBlinkDisabled = true }, Actor(1)] };
        Ff7FieldProcess first = Ff7NativeBlink.Advance(initial, random, _ => true);
        Check(first.BlinkRandomIndex == 2, "Blink RNG must wrap in original model-slot order.");
        Check(first.Actors.Single(actor => actor.Slot == 0).BlinkCountdown == 95 && first.Actors.Single(actor => actor.Slot == 1).BlinkCountdown == 64 && first.Actors.Single(actor => actor.Slot == 2).BlinkCountdown == 65, "Native intervals or slot order differ from the original byte table.");
        Check(first.Actors.Single(actor => actor.Slot == 3) == initial.Actors.Single(actor => actor.Slot == 3), "Disabled model consumed blink state.");
        Ff7FieldProcess held = Ff7NativeBlink.Advance(first, random, _ => false);
        Check(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(held), "Rejected presentation caught up its blink clock or RNG.");
        Ff7FieldProcess next = Ff7NativeBlink.Advance(first, random, _ => true);
        Check(next.BlinkRandomIndex == 2 && next.Actors.Single(actor => actor.Slot == 0) is { NativeEyeLeft: 1, NativeEyeRight: 1, BlinkCountdown: 94 }, "Closed eyes must last one eligible update.");
        Ff7FieldProcess cold = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(next))!;
        for (int tick = 0; tick < 180; tick++)
        {
            next = Ff7NativeBlink.Advance(next, random, actor => tick % 3 != 0 || actor.Slot == 0);
            cold = Ff7NativeBlink.Advance(cold, random, actor => tick % 3 != 0 || actor.Slot == 0);
            Check(JsonSerializer.Serialize(next) == JsonSerializer.Serialize(cold), "Cold blink ownership diverged.");
            cold = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(cold))!;
        }
        Ff7Actor fractional = Actor(0) with { X4096 = -1, Y4096 = 2 * 4096 + 2048, Z4096 = 7 * 4096 + 4095, OffsetX = 2, OffsetY = 3, OffsetZ = 4 };
        string untouched = JsonSerializer.Serialize(fractional);
        Check(Ff7NativeModelPosition.PresentationPoint(fractional, new(22, -34, 1)) == new Vector3(1, 27, -33), "Native visual endpoints must use arithmetic Q12 shift, original offsets and the same per-field presentation corrections.");
        Check(untouched == JsonSerializer.Serialize(fractional), "Native visual preparation changed the logical actor.");
        Ff7Actor prepared = Ff7NativeModelPosition.PrepareActorPresentation(fractional, new(22, -34, 0));
        Check(!prepared.Visible && prepared.OffsetX == 0 && prepared.OffsetY == 0 && prepared.OffsetZ == 0 && prepared.X4096 == 4096 && prepared.Y4096 == 27 * 4096 && prepared.Z4096 == -33 * 4096, "Native model mask and quantized endpoint must affect only the presentation copy.");
        Check(untouched == JsonSerializer.Serialize(fractional), "Native mask preparation changed logical visibility or coordinates.");
        Check(Ff7NativeModelPosition.PresentationPoint(Actor(0) with { X4096 = 32767 * 4096, OffsetX = 1 }, new(0, 0, 1)).X == -32768, "Native visual endpoint must preserve signed16 narrowing.");
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 1000), 0, 1000);
        Ff7Actor actor = Actor(0) with { Z4096 = 10 * 4096 };
        bool Gate(Ff7Actor selected, Vector2 offset = default, bool bypass = false, Ff7NativeModelPresentation? config = null) => Ff7NativeBlink.Eligible(selected, camera, config ?? new(0, 0, 1), offset, bypass);
        Check(Gate(actor) && !Gate(actor with { Visible = false }) && !Gate(actor, config: new(0, 0, 0)), "Native visibility/mask gate differs.");
        Check(!Gate(actor with { X4096 = -200 * 4096 }) && Gate(actor with { X4096 = -199 * 4096 }) && Gate(actor with { X4096 = 239 * 4096 }) && !Gate(actor with { X4096 = 240 * 4096 }), "Native strict horizontal margins differ.");
        Check(!Gate(actor with { Y4096 = -232 * 4096 }) && Gate(actor with { Y4096 = -231 * 4096 }) && Gate(actor with { Y4096 = 347 * 4096 }) && !Gate(actor with { Y4096 = 348 * 4096 }), "Native strict vertical margins differ.");
        Check(Gate(actor with { X4096 = -200 * 4096 }, new(1, 0)) && Gate(actor with { X4096 = 1000 * 4096 }, bypass: true), "Native scroll/shake or scripted camera override was omitted.");
        Check(!Gate(actor, bypass: true, config: new(0, 14360, 1)) && !Gate(actor, config: new(2000, 0, 1)), "Native extra offsets or strict depth bound were omitted.");
        Check(Ff7NativeBlink.Eligible(actor, camera with { Translation = new(0, 0, -1000) }, new(0, 0, 1), Vector2.Zero, true), "The native predicate must not invent a positive-depth requirement.");
        Check(!Ff7NativeBlink.Eligible(actor, null, new(0, 0, 1), Vector2.Zero, true), "A suppressed movie camera cannot use a field fallback.");
        bool undefined = false;
        try
        {
            Ff7NativeBlink.Eligible(actor, camera with { Translation = Vector3.Zero }, new(0, 0, 1), Vector2.Zero, true);
        }
        catch (NotSupportedException)
        {
            undefined = true;
        }
        Check(undefined, "Zero-depth native screen coordinates must remain explicit.");
        Installed();
    }

    private static void Installed()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP native blink installed metadata: optional resources are unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Check(executable.NativePresentation.Model(139, 3) == new Ff7NativeModelPresentation(0, 0, 1), "Reached Biggs native configuration differs.");
        Check(executable.NativePresentation.Model(466, 17) == new Ff7NativeModelPresentation(31, -16, 1), "Native all-slot literal configuration differs.");
        Check(executable.NativePresentation.Model(141, 1).ExtraY == 22 && executable.NativePresentation.Model(141, 0).ExtraY == 0, "Native selected-slot configuration differs.");
        Check(executable.NativePresentation.Model(704, 1).Mask == 0 && executable.NativePresentation.Model(704, 0).Mask == 1, "Native literal model-mask exception differs.");
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "tin_1");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        using LgpArchive fieldArchive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, field.Models.Models[3], executable, fieldArchive);
        Check(model.Eyes is { Role: 9, HeadPart: 2, FirstGroup: 1, ClosedLeft: not null, ClosedRight: not null }, $"Native Biggs alternate eye resources/groups differ: role={model.Eyes?.Role} part={model.Eyes?.HeadPart} group={model.Eyes?.FirstGroup} left={model.Eyes?.ClosedLeft is not null} right={model.Eyes?.ClosedRight is not null}.");
        Ff7FieldHost host = new(field, (_, _) => 1, new() { BlinkStateInitialized = true, BlinkRandomIndex = 107 });
        Check(host.CreateNextFieldProcess().BlinkRandomIndex == 107, "Global blink index was reset at a field transition.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
