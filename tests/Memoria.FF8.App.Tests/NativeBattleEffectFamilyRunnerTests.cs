using System.Text.Json;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffectFamilyRunnerTests
{
    [Fact]
    public void Binary_identity_collection_uses_the_actual_executing_assemblies()
    {
        Dictionary<string, string> hashes = NativeBattleEffectFamilyRunner.CollectBinaryHashes();
        Assert.Equal(5, hashes.Count);
        Assert.Contains(typeof(BattleScenePresentation).Assembly.GetName().Name!, hashes.Keys);
        Assert.Contains(typeof(Memoria.FF8.Runtime.BattleSession).Assembly.GetName().Name!, hashes.Keys);
        Assert.Contains(typeof(Memoria.FF8.Assets.GameAssets).Assembly.GetName().Name!, hashes.Keys);
        Assert.Contains(typeof(Memoria.FF8.Rendering.ITriangleRenderer).Assembly.GetName().Name!, hashes.Keys);
        Assert.Contains(typeof(NativeBattleEffectFamilyRunnerTests).Assembly.GetName().Name!, hashes.Keys);
        Assert.All(hashes.Values, value => Assert.Equal(32, Convert.FromHexString(value).Length));
    }

    [Fact]
    public void External_case_runs_in_its_own_process()
    {
        Execute("run");
    }

    [Fact]
    public void External_case_cold_loads_and_checks_its_next_callback_in_its_own_process()
    {
        Execute("cold");
    }

    [Fact]
    public void Case_requires_a_native_bound_and_complete_independent_fixture_names()
    {
        NativeEffectFamilyCase valid = Case();
        NativeBattleEffectFamilyRunner.Validate(valid);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.Validate(valid with { MaximumCallbacks = 0 }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.Validate(valid with { ProcessTimeoutSeconds = 0 }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.Validate(valid with { TerminationBasis = "" }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.Validate(valid with { ColdSnapshot = "" }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.Validate(valid with { ExpectedNextSnapshot = "" }));
        NativeBattleEffectFamilyRunner.Validate(valid with { ExpectedContactTransitions = 0 });
        NativeBattleEffectFamilyRunner.Validate(valid with { ExpectedContactTransitions = 3 });
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.Validate(valid with { ExpectedContactTransitions = -1 }));
    }

    [Fact]
    public void Missing_owner_observable_is_rejected_without_reconstruction()
    {
        JsonElement source = JsonSerializer.SerializeToElement(new { NativeEffectMemory = (object?)null });
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.ReadPointer(source, "/NativeEffectMemory/Owner"));
        Assert.Equal("{\"NativeEffectMemory\":null}", source.GetRawText());
    }

    [Fact]
    public void Stale_completed_effect_cannot_admit_an_unrelated_queued_action()
    {
        NativeEffectFamilyCase item = Case();
        NativeBattleOriginalEffectState stale = new(item.Effect, 0, new(item.Effect, item.Entry, 0), null, 0);
        Assert.False(NativeBattleEffectFamilyRunner.IsInitialized(stale, item));
        byte[] header = new byte[20];
        header[6] = 2;
        NativeBattleActionPacketsState packets = new(1, 0, [new(header.ToImmutableArray(), 0, null, null)], [], null);
        byte[] descriptor = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(4), 0x1D280C4);
        NativeBattleDescriptorPacket queued = new(descriptor.ToImmutableArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.ValidateActionIdentity(queued, packets, null, item));
        header[6] = 1;
        packets = packets with { Headers = [new(header.ToImmutableArray(), 0, null, null)] };
        NativeBattleEffectFamilyRunner.ValidateActionIdentity(queued, packets, null, item);
        NativeBattleActionGraphicsState otherHeader = new(1, 0, 0, 0, 0, null, null, 0, null, 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.ValidateActionIdentity(queued, packets, otherHeader, item));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(4), 0x1D280C5);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.ValidateActionIdentity(new(descriptor.ToImmutableArray()), packets, null, item));
    }

    [Fact]
    public void Snapshot_pointer_selects_arrays_and_escaped_property_names()
    {
        using JsonDocument source = JsonDocument.Parse("{\"a/b\":{\"~owner\":[12,34]}}");
        Assert.Equal(34, NativeBattleEffectFamilyRunner.ReadPointer(source.RootElement, "/a~1b/~0owner/1").GetInt32());
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFamilyRunner.ReadPointer(source.RootElement, "/a~1b/~0owner/2"));
    }

    private static NativeEffectFamilyCase Case()
    {
        return new("case-a", 1, 1, "initial.json", "cold.json", "next.json", 30, "Supplied native callback observation", "/NativePartyPresentation/Rows/0/HitPoints", 1, [new("/NativeEffectMemory/Active", JsonSerializer.SerializeToElement(0))]) { ProcessTimeoutSeconds = 60 };
    }

    private static void Execute(string phase)
    {
        string? manifestPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_FAMILY_MANIFEST");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifestPath), "An external family manifest is required.");
        string requestedPhase = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_FAMILY_PHASE") ?? throw new InvalidDataException("A single isolated phase is required.");
        Assert.SkipUnless(requestedPhase == phase, "This process executes the other isolated family phase.");
        string id = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_FAMILY_CASE") ?? throw new InvalidDataException("A single case identity is required; unproved in-process resets are forbidden.");
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        NativeEffectFamilyManifest manifest = JsonSerializer.Deserialize<NativeEffectFamilyManifest>(File.ReadAllBytes(manifestPath!)) ?? throw new InvalidDataException("The manifest is null.");
        NativeEffectFamilyCase item = manifest.Cases.Single(candidate => candidate.Id == id);
        NativeEffectFamilyResult result = NativeBattleEffectFamilyRunner.Execute(item, phase, Path.GetDirectoryName(Path.GetFullPath(manifestPath!))!, gameDirectory);
        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_FAMILY_RESULT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            File.WriteAllText(output, json);
        }
        Console.WriteLine(json);
        Assert.True(result.FirstBlocker is null, result.FirstBlocker);
    }
}
