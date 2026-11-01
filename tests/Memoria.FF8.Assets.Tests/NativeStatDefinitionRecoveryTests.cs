using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeStatDefinitionRecoveryTests
{
    [Fact]
    public void GameStateRestoresEverySnapshotWithoutReplacingItsSavedProgress()
    {
        NativeCharacterVitals source = Source();
        NativeCharacterVitals root = Missing(source).DeepCopy();
        NativeCharacterVitals active = Missing(source).DeepCopy();
        NativeCharacterVitals departing = Missing(source).DeepCopy();
        root.Records[0] = root.Records[0] with { Experience = 100 };
        active.Records[0] = active.Records[0] with { Experience = 200, HitPoints = new(8, 9) };
        departing.Records[0] = departing.Records[0] with { Experience = 300, HitPoints = new(10, 11) };
        GameState saved = new() { CharacterVitals = root, EventState = Snapshot(active, 47), DepartingFieldState = Snapshot(departing, 31) };
        string before = JsonSerializer.Serialize(saved);

        GameState restored = NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(saved, source);
        NativeCharacterVitals activeRestored = restored.EventState!.Value.GetProperty("CharacterVitals").Deserialize<NativeCharacterVitals>()!;
        NativeCharacterVitals departingRestored = restored.DepartingFieldState!.Value.GetProperty("CharacterVitals").Deserialize<NativeCharacterVitals>()!;

        Assert.Equal(JsonSerializer.Serialize(root), JsonSerializer.Serialize(Missing(restored.CharacterVitals!)));
        Assert.Equal(JsonSerializer.Serialize(active), JsonSerializer.Serialize(Missing(activeRestored)));
        Assert.Equal(JsonSerializer.Serialize(departing), JsonSerializer.Serialize(Missing(departingRestored)));
        Assert.Equal(source.Definitions.CharacterStatusPassiveFlags, activeRestored.Definitions.CharacterStatusPassiveFlags);
        Assert.Equal(source.Definitions.CharacterStatusPassiveFlags, departingRestored.Definitions.CharacterStatusPassiveFlags);
        AssertSnapshotContentPreserved(saved.EventState!.Value, restored.EventState.Value);
        AssertSnapshotContentPreserved(saved.DepartingFieldState!.Value, restored.DepartingFieldState.Value);
        Assert.Equal(before, JsonSerializer.Serialize(saved));
    }

    [Fact]
    public void GameStateWithCurrentDefinitionsRetainsIdentity()
    {
        NativeCharacterVitals source = Source();
        GameState saved = new() { CharacterVitals = source, EventState = Snapshot(source, 47), DepartingFieldState = Snapshot(source, 31) };
        Assert.Same(saved, NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(saved, Missing(source)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncompatibleNestedKernelRejectsWithoutMutatingAnySnapshot(bool departing)
    {
        NativeCharacterVitals source = Source();
        NativeCharacterVitals root = Missing(source);
        NativeCharacterVitals incompatible = root with { KernelSha256 = new string('0', 64) };
        GameState saved = new()
        {
            CharacterVitals = root,
            EventState = Snapshot(departing ? root : incompatible, 47),
            DepartingFieldState = Snapshot(departing ? incompatible : root, 31)
        };
        string before = JsonSerializer.Serialize(saved);

        Assert.Throws<NotSupportedException>(() => NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(saved, source));

        Assert.Equal(before, JsonSerializer.Serialize(saved));
        Assert.True(saved.CharacterVitals.Definitions.CharacterStatusPassiveFlags.IsDefault);
    }

    private static JsonElement Snapshot(NativeCharacterVitals vitals, int frame)
    {
        return JsonSerializer.SerializeToElement(new { CharacterVitals = vitals, Frame = frame, Unrelated = new { Values = new[] { 7, 3, 5 }, Text = "retained", Optional = (string?)null } });
    }

    private static void AssertSnapshotContentPreserved(JsonElement before, JsonElement after)
    {
        JsonObject original = JsonNode.Parse(before.GetRawText())!.AsObject();
        JsonObject restored = JsonNode.Parse(after.GetRawText())!.AsObject();
        original.Remove("CharacterVitals");
        restored.Remove("CharacterVitals");
        Assert.True(JsonNode.DeepEquals(original, restored));
    }

    [Fact]
    public void MissingFlagsRestoreOnlyTheImmutableFlagTable()
    {
        NativeCharacterVitals source = Source();
        Assert.Equal(1u, source.Definitions.CharacterStatusPassiveFlags[58 - 58]);
        Assert.Equal(0x100u, source.Definitions.CharacterStatusPassiveFlags[66 - 58]);
        Assert.Equal(0x20000u, source.Definitions.CharacterStatusPassiveFlags[64 - 58]);
        NativeCharacterVitals saved = Missing(source) with { DreamEnabled = true, KernelSha256 = source.KernelSha256.ToLowerInvariant() };
        saved.Records[0] = saved.Records[0] with { Experience = 1234, HitPoints = new(7, 9), KillCount = 12, Junction = saved.Records[0].Junction with { Abilities = 66 } };
        string before = JsonSerializer.Serialize(saved);

        NativeCharacterVitals restored = NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(saved, source);

        restored.Validate();
        Assert.Equal(source.Definitions.CharacterStatusPassiveFlags, restored.Definitions.CharacterStatusPassiveFlags);
        Assert.Equal(before, JsonSerializer.Serialize(Missing(restored)));
        Assert.Equal(before, JsonSerializer.Serialize(saved));
        Assert.Same(saved.Records, restored.Records);
        Assert.Same(saved.GuardianForces, restored.GuardianForces);
        Assert.Same(saved.MenuDefinitions, restored.MenuDefinitions);
    }

    [Fact]
    public void DifferentKernelCannotSupplyMissingFlags()
    {
        NativeCharacterVitals source = Source();
        NativeCharacterVitals saved = Missing(source) with { KernelSha256 = new string('0', 64) };
        Assert.Throws<NotSupportedException>(() => NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(saved, source));
        Assert.True(saved.Definitions.CharacterStatusPassiveFlags.IsDefault);
    }

    [Fact]
    public void SourceWithoutFlagsCannotSupplyMissingFlags()
    {
        NativeCharacterVitals saved = Missing(Source());
        Assert.Throws<NotSupportedException>(() => NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(saved, saved));
    }

    [Fact]
    public void ExistingFlagsRemainAuthoritativeWithoutConsultingSource()
    {
        NativeCharacterVitals saved = Source();
        NativeCharacterVitals unrelated = Missing(saved) with { KernelSha256 = new string('0', 64) };
        Assert.Same(saved, NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(saved, unrelated));
    }

    [Fact]
    public void InvalidSavedFlagTableIsRejected()
    {
        NativeCharacterVitals source = Source();
        NativeCharacterVitals saved = source with { Definitions = source.Definitions with { CharacterStatusPassiveFlags = ImmutableArray.Create(1u) } };
        Assert.Throws<InvalidDataException>(() => NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(saved, source));
    }

    [Theory]
    [InlineData(66, 20)]
    [InlineData(64, 21)]
    public void RestoredFlagsDistinguishStrBonusFromMoveHpUp(int ability, int expectedHitPoints)
    {
        NativeCharacterVitals source = Source();
        NativeCharacterVitals saved = Missing(source);
        saved.Records[0] = saved.Records[0] with { Junction = saved.Records[0].Junction with { Abilities = (uint)ability } };
        NativeEventMemory memory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [4] = 0, [12] = 7, [244] = 0 });
        Assert.Throws<NotSupportedException>(() => NativeMovementProgress.Sample(memory, saved, null, null, null, 1));

        NativeCharacterVitals restored = NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(saved, source);
        NativeCharacterVitals cold = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(restored))!;
        cold.Validate();
        NativeMovementProgressResult result = NativeMovementProgress.Sample(memory, cold, null, null, null, 1);

        Assert.Equal(new NativeHpWord((ushort)expectedHitPoints, (ushort)expectedHitPoints), result.Vitals!.Records[0].HitPoints);
        Assert.Equal(0, result.Memory.Read(12, 4).RequireExact());
        Assert.Equal(new NativeHpWord(20, 20), saved.Records[0].HitPoints);
        Assert.Equal(restored.Definitions.CharacterStatusPassiveFlags, cold.Definitions.CharacterStatusPassiveFlags);
        Assert.Same(cold, NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(cold, source));
    }

    private static NativeCharacterVitals Missing(NativeCharacterVitals source)
    {
        return source with { Definitions = source.Definitions with { CharacterStatusPassiveFlags = default } };
    }

    private static NativeCharacterVitals Source()
    {
        int[] lengths = new int[56];
        lengths[1] = 60 * 57;
        lengths[2] = 132 * 16;
        lengths[6] = 36 * 11;
        int[] counts = [20, 19, 19, 20, 5, 9, 24];
        for (int index = 0; index < counts.Length; index++)
        {
            lengths[11 + index] = counts[index] * 8;
        }
        byte[] kernel = new byte[228 + lengths.Sum()];
        BinaryPrimitives.WriteUInt32LittleEndian(kernel, 56);
        int offset = 228;
        int[] offsets = new int[56];
        for (int index = 0; index < lengths.Length; index++)
        {
            offsets[index] = offset;
            BinaryPrimitives.WriteUInt32LittleEndian(kernel.AsSpan(4 + index * 4), (uint)offset);
            offset += lengths[index];
        }
        BinaryPrimitives.WriteUInt32LittleEndian(kernel.AsSpan(4 + 56 * 4), (uint)offset);
        for (int id = 0; id < 11; id++)
        {
            kernel[offsets[6] + id * 36 + 6] = 100;
            kernel[offsets[6] + id * 36 + 9] = 255;
            kernel[offsets[6] + id * 36 + 10] = 100;
        }
        for (int id = 0; id < 16; id++)
        {
            kernel[offsets[2] + id * 132 + 21] = 255;
        }
        uint[] flags = [1, 2, 4, 8, 16, 65536, 131072, 128, 256, 512, 1024, 2048, 16384, 8192, 4096, 32768, 262144, 32, 64, 524288];
        for (int index = 0; index < flags.Length; index++)
        {
            kernel[offsets[14] + index * 8 + 5] = (byte)flags[index];
            kernel[offsets[14] + index * 8 + 6] = (byte)(flags[index] >> 8);
            kernel[offsets[14] + index * 8 + 7] = (byte)(flags[index] >> 16);
        }
        byte[] initial = new byte[2812];
        for (int id = 0; id < 8; id++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(initial.AsSpan(1088 + id * 152), 20);
            initial[1088 + id * 152 + 8] = (byte)id;
            initial[1088 + id * 152 + 148] = 1;
        }
        initial[2725] = 255;
        initial[2726] = 255;
        return NativeCharacterInitialization.Read(initial, kernel);
    }
}
