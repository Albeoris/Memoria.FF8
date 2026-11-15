using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePartyPresentationTests
{
    public static void Run()
    {
        UsesNativeScriptBindingsAndAllocatedSlots();
        SkipsEmptyMissingAndOutOfRangeBindings();
        RetainsCurrentAllocationAndIndependentResults();
        ValidatesBounds();
        InstalledStandalone();
        Console.WriteLine("PASS native post-initialization party presentation publication");
    }

    private static void UsesNativeScriptBindingsAndAllocatedSlots()
    {
        FieldEntitySnapshot[] entities = [Entity(), Entity(4), Entity(0), Entity(6), Entity(3)];
        Ff7Actor[] actors = [Actor(6, 1, 0), Actor(2, 2, 4), Actor(4, 3, 1), Actor(0, 4, 6)];
        int[] party = [0, 4, 6];
        string before = JsonSerializer.Serialize(new { entities, actors, party });
        byte[] mask = Ff7NativePartyPresentation.Create(8, party, entities, actors);
        Check(mask.SequenceEqual(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0, 0 }), "Party filtering used actor character metadata, descriptor index, visibility or party order instead of native character/entity/allocation bindings.");
        Check(before == JsonSerializer.Serialize(new { entities, actors, party }), "Creating presentation masks changed source bindings, logical actors or script party.");
        Check(actors.All(actor => !actor.Visible), "The fixture must distinguish presentation allocation from logical visibility.");
    }

    private static void SkipsEmptyMissingAndOutOfRangeBindings()
    {
        FieldEntitySnapshot[] entities = [Entity(), Entity(0, false), Entity(4), Entity(6), Entity(3), Entity(7)];
        Ff7Actor[] actors = [Actor(1, 1, 0), Actor(9, 3, 6), Actor(255, 4, 3), Actor(2, 5, 7) with { EntityBound = false }];
        foreach (int[] party in new int[][] { [255, 255, 255], [0, 4, 6], [3, 7, 8] })
        {
            Check(Ff7NativePartyPresentation.Create(8, party, entities, actors).All(value => value == 0), "An empty character, unbound entity, absent actor, detached allocation or slot outside the native range enabled a mask.");
        }
        Check(Ff7NativePartyPresentation.Create(0, [0, 255, 255], [Entity(0)], [Actor(0, 0, -1)]).SequenceEqual(new byte[] { 1 }), "The inclusive first allocated-slot bound was lost.");
        Check(Ff7NativePartyPresentation.Create(8, [6, 255, 255], entities, [Actor(8, 3, -1)]).Last() == 1, "The inclusive last allocated-slot bound was lost.");
    }

    private static void RetainsCurrentAllocationAndIndependentResults()
    {
        FieldEntitySnapshot[] entities = [Entity(), Entity(0)];
        Ff7Actor[] actors = [Actor(2, 1, 0) with { EntityBound = false }, Actor(7, 1, -1)];
        byte[] first = Ff7NativePartyPresentation.Create(8, [0, 255, 255], entities, actors);
        byte[] second = Ff7NativePartyPresentation.Create(8, [0, 255, 255], entities, actors);
        Check(first[7] == 1 && first[2] == 0 && first.Sum(value => value) == 1, "Native presentation selected a historical detached model instead of the current entity allocation.");
        first[7] = 0;
        Check(second[7] == 1 && !ReferenceEquals(first, second), "Separate field publications shared a mutable mask array.");
    }

    private static void ValidatesBounds()
    {
        foreach (int bound in new[] { -1, 256, int.MaxValue })
        {
            bool rejected = false;
            try
            {
                Ff7NativePartyPresentation.Create(bound, [255, 255, 255], [], []);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Check(rejected, "Party presentation accepted an allocated-slot bound outside native byte allocation identities.");
        }
        byte[] widest = Ff7NativePartyPresentation.Create(255, [255, 255, 255], [], []);
        Check(widest.Length == 256 && widest.All(value => value == 0), "The generic byte-slot helper incorrectly imposed one field's smaller resource rule.");
    }

    private static void InstalledStandalone()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PARTY_PRESENTATION_SAVE");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed party presentation fixture; select MEMORIA_FF7_PARTY_PRESENTATION_SAVE and MEMORIA_FF7_GAME_DIR.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream stream = new(original, false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("A party presentation fixture requires an FFVII session.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        Ff7SessionSave restored = session.Capture();
        Check(session.MapId == 502 && restored.Vm.Tick == 0, "The selected fixture must be the native post-initialization party presentation boundary.");
        Check(JsonSerializer.Serialize(saved.Vm) == JsonSerializer.Serialize(restored.Vm) && JsonSerializer.Serialize(saved.Progress) == JsonSerializer.Serialize(restored.Progress), "Legacy presentation reconstruction replayed initialization or changed VM/progress facts.");
        byte[] mask = restored.Process.NativePartyPresentationMask ?? throw new InvalidOperationException("Restoration omitted the native party mask publication.");
        Check(mask.SequenceEqual(new byte[] { 1, 0, 0, 0, 1, 0, 0, 1, 0 }), "The reached original script party did not publish allocated slots0/4/7.");
        Ff7NativeModelPresentation[] presentation = restored.Process.Actors.Select(actor => session.GetModelPresentation(actor.Slot)).ToArray();
        foreach (Ff7Actor actor in restored.Process.Actors)
        {
            Check(session.GetModelPresentation(actor.Slot).Mask == (actor.Slot <= 8 ? mask[actor.Slot] : 1), "The dynamic party filter changed static masks outside its allocated-slot range.");
        }
        string beforeRead = JsonSerializer.Serialize(session.Capture());
        foreach (Ff7Actor actor in restored.Process.Actors)
        {
            session.PrepareActorPresentation(actor);
        }
        Check(beforeRead == JsonSerializer.Serialize(session.Capture()), "Pure model presentation reads mutated the native publication or actor state.");
        Ff7SessionSave roundTrip = JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(restored))!;
        using Ff7Session cold = Ff7Session.Restore(installation, roundTrip);
        Check(JsonSerializer.Serialize(restored) == JsonSerializer.Serialize(cold.Capture()), "The recorded mask publication changed on JSON cold restoration.");
        session.Machine.SynchronizePartyFromMain([3, 255, 255]);
        Check(session.Capture().Process.NativePartyPresentationMask!.SequenceEqual(mask), "A later native script-party change recomputed the field-entry mask.");
        Check(presentation.SequenceEqual(restored.Process.Actors.Select(actor => session.GetModelPresentation(actor.Slot))), "Model reads recomputed party membership after field-entry publication.");
        using Ff7Session changedCold = Ff7Session.Restore(installation, session.Capture());
        Check(changedCold.Capture().Process.NativePartyPresentationMask!.SequenceEqual(mask), "Cold restoration recomputed an already recorded mask from later party membership.");
        Check(original.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The presentation fixture changed its original standalone source.");
    }

    private static FieldEntitySnapshot Entity(int? character = null, bool bound = true)
    {
        return new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray(), bound) { PartyCharacter = character };
    }

    private static Ff7Actor Actor(int slot, int entity, int character)
    {
        return new() { Slot = slot, Entity = entity, Model = 100 + slot, PartyCharacter = character, Visible = false };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
