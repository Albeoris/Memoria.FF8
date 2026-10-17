using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed record NativeEffectFamilyManifest(NativeEffectFamilyCase[] Cases);
internal sealed record NativeEffectFamilyValue(string Pointer, JsonElement Value);
internal sealed record NativeEffectFamilyCase(string Id, ushort Effect, uint Entry, string InitialSnapshot, string ColdSnapshot, string ExpectedNextSnapshot, int MaximumCallbacks, string TerminationBasis, string ContactPointer, int ExpectedContactTransitions, NativeEffectFamilyValue[] FinalValues)
{
    public int ProcessTimeoutSeconds { get; init; }
}
internal sealed record NativeEffectFamilyMetrics(double ElapsedSeconds, double CallbackSeconds, Dictionary<string, string> InputSha256, Dictionary<string, string> BinarySha256, long ArtifactBytes);
internal sealed record NativeEffectFamilyResult(string Id, string Phase, string Status, string? FirstBlocker, string[] Passed, int Callbacks, int ObservableTransitions, int DescriptorCompletions, long? Frame, long? NativeCallback)
{
    public string[] Pending { get; init; } = ["Native comparison", "Native contact count (only observable transitions counted)", "Significant graphics boundaries", "Camera", "Sound routing", "Audibility"];
    public string[] SavedFixtures { get; init; } = [];
    public NativeEffectFamilyMetrics? Metrics { get; init; }
}

// Exercises the same presentation entry point as the parameterized effect tests.
// Each invocation handles one case/phase; the process launcher owns isolation.
internal static class NativeBattleEffectFamilyRunner
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    internal static Dictionary<string, string> CollectBinaryHashes()
    {
        Assembly[] assemblies = [typeof(BattleScenePresentation).Assembly, typeof(BattleSession).Assembly, typeof(GameAssets).Assembly, typeof(Memoria.FF8.Rendering.ITriangleRenderer).Assembly, typeof(NativeBattleEffectFamilyRunner).Assembly];
        Dictionary<string, string> hashes = [];
        foreach (Assembly assembly in assemblies)
        {
            string name = assembly.GetName().Name ?? throw new InvalidDataException("An executing binary has no assembly identity.");
            hashes.Add(name, Hash(assembly.Location));
        }
        return hashes;
    }

    internal static void Validate(NativeEffectFamilyCase item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || item.Entry == 0 || item.MaximumCallbacks <= 0 || item.ProcessTimeoutSeconds <= 0 || string.IsNullOrWhiteSpace(item.TerminationBasis))
        {
            throw new InvalidDataException("A case requires an identity, entry and positive callback bound with its native termination basis.");
        }
        if (string.IsNullOrWhiteSpace(item.InitialSnapshot) || string.IsNullOrWhiteSpace(item.ColdSnapshot) || string.IsNullOrWhiteSpace(item.ExpectedNextSnapshot))
        {
            throw new InvalidDataException("Initial, independent cold and expected-next standalone fixtures are required.");
        }
        if (string.IsNullOrWhiteSpace(item.ContactPointer) || item.ExpectedContactTransitions < 0 || item.FinalValues is not { Length: > 0 })
        {
            throw new InvalidDataException("An explicit nonnegative observable transition count and final owner values are required.");
        }
    }

    internal static void ValidateActionIdentity(NativeBattleDescriptorPacket descriptor, NativeBattleActionPacketsState? packets, NativeBattleActionGraphicsState? action, NativeEffectFamilyCase item)
    {
        Require(packets is not null, "The standalone action packet owner is absent.");
        uint offset = unchecked(descriptor.Argument - 0x1D280C4u);
        Require(offset % 20 == 0 && offset / 20 < NativeBattleActionPacketsState.HeaderCapacity, "The action104 descriptor does not address a physical20-byte action header.");
        int index = (int)(offset / 20);
        Require(NativeBattleActionGraphics.Word(packets!.Headers[index], 6) == item.Effect, "The queued action104 header selects a different effect from the manifest.");
        Require(action is null || action.HeaderIndex == index, "The started graphics action does not belong to the queued action104 header.");
    }

    internal static bool IsInitialized(NativeBattleOriginalEffectState? effect, NativeEffectFamilyCase item)
    {
        return effect is { ReturnedDescriptor: > 0 } && effect.Binding.Effect == item.Effect && effect.Binding.Entry == item.Entry;
    }

    internal static JsonElement ReadPointer(JsonElement root, string pointer)
    {
        if (!pointer.StartsWith('/'))
        {
            throw new InvalidDataException("Snapshot observables require an absolute JSON pointer.");
        }
        JsonElement value = root;
        foreach (string encoded in pointer[1..].Split('/'))
        {
            string segment = encoded.Replace("~1", "/").Replace("~0", "~");
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(segment, out JsonElement property))
            {
                value = property;
            }
            else if (value.ValueKind == JsonValueKind.Array && int.TryParse(segment, out int index) && index >= 0 && index < value.GetArrayLength())
            {
                value = value[index];
            }
            else
            {
                throw new InvalidDataException($"Required snapshot observable '{pointer}' is absent; owners cannot be reconstructed.");
            }
        }
        return value;
    }

    internal static BattleSnapshot LoadSnapshot(string path)
    {
        return JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("The standalone snapshot is null.");
    }

    internal static NativeEffectFamilyResult Execute(NativeEffectFamilyCase item, string phase, string fixtureDirectory, string gameDirectory)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan callbackElapsed = TimeSpan.Zero;
        Dictionary<string, string> inputHashes = [];
        Dictionary<string, string> binaryHashes = [];
        long artifactBytes = 0;
        List<string> passed = [];
        string stage = "manifest";
        int callbacks = 0;
        int contacts = 0;
        int completions = 0;
        BattleSession? battle = null;
        List<string> saved = [];
        string? outputDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_FAMILY_FIXTURE_OUTPUT");
        try
        {
            Validate(item);
            Require(phase is "run" or "cold", "Unknown phase.");
            binaryHashes = CollectBinaryHashes();
            passed.Add(stage);
            stage = "resources";
            using GameAssets game = new(gameDirectory);
            using BattleAssets assets = new(gameDirectory);
            NativeBattleEffectBinding original = game.ReadNativeEffectCatalogForEffects([item.Effect]).Single(binding => binding.Effect == item.Effect);
            Require(original.Entry == item.Entry, "The installed original entry differs from the manifest.");
            passed.Add(stage);
            stage = "admission";
            string sourcePath = Path.Combine(fixtureDirectory, phase == "run" ? item.InitialSnapshot : item.ColdSnapshot);
            inputHashes[phase == "run" ? "InitialSnapshot" : "ColdSnapshot"] = Hash(sourcePath);
            BattleSnapshot source = LoadSnapshot(sourcePath);
            NativeBattleEffectBinding supplied = source.Definition.NativeEffectCatalog.Single(binding => binding.Effect == item.Effect);
            Require(JsonSerializer.Serialize(original) == JsonSerializer.Serialize(supplied), "The supplied fixture does not contain the installed original effect binding and resources.");
            Require(source.NativeGraphics is not null && source.NativeEffectMemory is not null, "The standalone fixture lacks its native graphics or effect memory owner.");
            battle = BattleSession.Restore(source);
            Require(JsonSerializer.Serialize(source) == JsonSerializer.Serialize(battle.Capture()), "Restore changed the supplied standalone state.");
            NativeBattleDescriptorState descriptors = battle.NativeGraphics!.Descriptors;
            Require(descriptors.Queue.Count(slot => descriptors.Packets[slot].Command == 104) == 1, "The fixture requires exactly one live action104 descriptor.");
            NativeBattleDescriptorPacket descriptor = descriptors.Packets[descriptors.Queue.Single(slot => descriptors.Packets[slot].Command == 104)];
            ValidateActionIdentity(descriptor, source.NativeActionPackets, source.NativeGraphics!.ActionGraphics, item);
            passed.Add(stage);
            BattleScenePresentation scene = new(assets, source.Definition, true);
            if (phase == "cold")
            {
                stage = "cold-next-callback";
                Require(IsInitialized(battle.NativeGraphics?.OriginalEffect, item), "The independent cold fixture does not contain the expected live initialized effect.");
                string expectedPath = Path.Combine(fixtureDirectory, item.ExpectedNextSnapshot);
                inputHashes["ExpectedNextSnapshot"] = Hash(expectedPath);
                BattleSnapshot expected = LoadSnapshot(expectedPath);
                // Load expected state independently; never derive it from this execution.
                BattleSession reference = BattleSession.Restore(expected);
                Require(JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(reference.Capture()), "Expected-next fixture changes during restore.");
                Advance();
                callbacks++;
                Require(battle.NativeGraphics!.Callback == source.NativeGraphics!.Callback + 1, "The cold fixture did not advance exactly one native callback.");
                Require(JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(battle.Capture()), "Cold next callback differs from the supplied expected-next fixture.");
                Require(scene.Render(battle, 320, 224).SequenceEqual(new BattleScenePresentation(assets, expected.Definition, true).Render(reference, 320, 224)), "Cold next-callback pixels differ.");
                passed.Add(stage);
            }
            else
            {
                stage = "initialization";
                bool initialized = IsInitialized(battle.NativeGraphics?.OriginalEffect, item);
                Save("initial", source);
                bool midpointSaved = false;
                bool nextSaved = false;
                string contact = ReadPointer(JsonSerializer.SerializeToElement(battle.Capture()), item.ContactPointer).GetRawText();
                while (HasDescriptor(battle) && callbacks < item.MaximumCallbacks)
                {
                    if (initialized && !midpointSaved && battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor is > 0)
                    {
                        Save("cold", battle.Capture());
                        midpointSaved = true;
                    }
                    Advance();
                    callbacks++;
                    if (midpointSaved && !nextSaved)
                    {
                        Save("next", battle.Capture());
                        nextSaved = true;
                    }
                    initialized |= IsInitialized(battle.NativeGraphics?.OriginalEffect, item);
                    if (initialized && !passed.Contains("initialization"))
                    {
                        passed.Add("initialization");
                    }
                    stage = initialized ? "completion" : "initialization";
                    JsonElement current = JsonSerializer.SerializeToElement(battle.Capture());
                    string nextContact = ReadPointer(current, item.ContactPointer).GetRawText();
                    if (contact != nextContact)
                    {
                        contacts++;
                    }
                    contact = nextContact;
                    if (!HasDescriptor(battle))
                    {
                        completions++;
                    }
                }
                Require(initialized, "The expected effect initializer was never observed.");
                Require(!HasDescriptor(battle), $"The action remains live at its explicit {item.MaximumCallbacks}-callback bound ({item.TerminationBasis}).");
                Require(completions == 1, "Action completion was not observed exactly once.");
                Save("after", battle.Capture());
                passed.Add("completion");
                stage = "task-owner-release";
                Require(battle.NativeGraphics!.Descriptors.Tasks.All(task => task.Kind != NativeBattleDescriptorTaskKind.Action104 || (task.Flags & 1) == 0), "An action104 task remains live.");
                NativeBattleOriginalEffectState completedEffect = battle.NativeGraphics.OriginalEffect ?? throw new InvalidDataException("The completed effect owner is absent.");
                Require(completedEffect.Binding.Effect == item.Effect && completedEffect.Binding.Entry == item.Entry, "The completed effect owner differs from the manifest identity.");
                Require(completedEffect.ReturnedDescriptor == 0, "The effect returned task is not released.");
                Require(completedEffect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0)), "An original effect child task remains live.");
                Require(battle.NativeEffectMemory is { Active: 0 }, "The effect memory remains active.");
                JsonElement final = JsonSerializer.SerializeToElement(battle.Capture());
                foreach (NativeEffectFamilyValue expected in item.FinalValues)
                {
                    Require(ReadPointer(final, expected.Pointer).GetRawText() == expected.Value.GetRawText(), $"Final owner observable '{expected.Pointer}' differs.");
                }
                passed.Add(stage);
                stage = "once-only-contact";
                Require(contacts == item.ExpectedContactTransitions, $"Expected {item.ExpectedContactTransitions} observable transitions, observed {contacts}.");
                Advance();
                callbacks++;
                Require(!HasDescriptor(battle), "The completed action descriptor reappeared on its next callback.");
                Require(ReadPointer(JsonSerializer.SerializeToElement(battle.Capture()), item.ContactPointer).GetRawText() == contact, "The contact observable changed again after completion.");
                passed.Add(stage);
            }
            return Result("checked", null);
        }
        catch (Exception error)
        {
            return Result("blocked", stage + ": " + error.GetType().Name + ": " + error.Message);
        }

        NativeEffectFamilyResult Result(string status, string? blocker)
        {
            return new(item.Id, phase, status, blocker, passed.ToArray(), callbacks, contacts, completions, battle?.Frame, battle?.NativeGraphics?.Callback)
            {
                SavedFixtures = saved.ToArray(),
                Metrics = new(elapsed.Elapsed.TotalSeconds, callbackElapsed.TotalSeconds, inputHashes, binaryHashes, artifactBytes)
            };
        }

        void Advance()
        {
            long started = Stopwatch.GetTimestamp();
            try
            {
                long beforeCallback = battle!.NativeGraphics!.Callback;
                battle!.AdvanceNativeGraphicsPresentation(Callback);
                Require(battle.NativeGraphics!.Callback == beforeCallback + 1, "Presentation did not advance exactly one native callback.");
            }
            finally
            {
                callbackElapsed += Stopwatch.GetElapsedTime(started);
            }
        }

        void Save(string name, BattleSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return;
            }
            Directory.CreateDirectory(outputDirectory);
            string path = Path.Combine(outputDirectory, name + ".battle.json");
            string json = JsonSerializer.Serialize(snapshot);
            using (FileStream stream = new(path, FileMode.CreateNew))
            using (StreamWriter writer = new(stream))
            {
                writer.Write(json);
            }
            artifactBytes += new FileInfo(path).Length;
            BattleSession independent = BattleSession.Restore(LoadSnapshot(path));
            Require(json == JsonSerializer.Serialize(independent.Capture()), "An emitted standalone fixture changes on immediate roundtrip.");
            saved.Add(path);
        }
    }

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool HasDescriptor(BattleSession battle)
    {
        NativeBattleDescriptorState state = battle.NativeGraphics?.Descriptors ?? throw new InvalidDataException("The native graphics owner is absent.");
        return state.Queue.Any(slot => state.Packets[slot].Command == 104);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
