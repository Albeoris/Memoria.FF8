using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectMemoryLayoutTests
{
    private static NativeBattleEffectMemory? escaped;

    [Fact]
    public void Common_with_copies_remain_small_when_the_result_escapes()
    {
        NativeBattleEffectMemory source = Memory() with { CureContextF4 = 3, CuraContextF4 = 7 };
        for (int index = 0; index < 64; index++)
        {
            Volatile.Write(ref escaped, source with { Cursor = index });
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1024; index++)
        {
            Volatile.Write(ref escaped, source with { Cursor = index });
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.InRange(allocated, 1, 1024 * 1024 - 1);
        Assert.Equal(1023, Volatile.Read(ref escaped)!.Cursor);
        Assert.Equal((short)3, Volatile.Read(ref escaped)!.CureContextF4);
        Assert.Equal((short)7, source.CuraContextF4);
        Volatile.Write(ref escaped, null);
    }

    [Fact]
    public void Effect_initializers_preserve_value_equality_independent_order_and_with_isolation()
    {
        NativeBattleEffectMemory source = Memory();
        NativeBattleEffectMemory first = source with { CureContextF4 = 3, CuraContextF4 = 7 };
        NativeBattleEffectMemory reordered = source with { CuraContextF4 = 7, CureContextF4 = 3 };
        NativeBattleEffectMemory changed = first with { CureContextF4 = 9 };

        Assert.Equal(first, reordered);
        Assert.Equal(first.GetHashCode(), reordered.GetHashCode());
        Assert.NotEqual(first, changed);
        Assert.Equal((short)3, first.CureContextF4);
        Assert.Equal((short)3, reordered.CureContextF4);
        Assert.Equal((short)9, changed.CureContextF4);
        Assert.Equal((short)7, changed.CuraContextF4);
        Assert.Null(source.CureContextF4);
        Assert.Null(source.CuraContextF4);
        Assert.Equal(first, changed with { CureContextF4 = 3 });
        Assert.Equal(first.ToString(), reordered.ToString());
    }

    [Fact]
    public void Json_keeps_flat_effect_properties_required_constructor_fields_and_roundtrip()
    {
        NativeBattleEffectMemory source = Memory() with { Cursor = 17, CureContextF4 = 3, CuraContextF4 = 7 };
        string json = JsonSerializer.Serialize(source);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal(17, root.GetProperty(nameof(NativeBattleEffectMemory.Cursor)).GetInt32());
        Assert.Equal(3, root.GetProperty(nameof(NativeBattleEffectMemory.CureContextF4)).GetInt32());
        Assert.Equal(7, root.GetProperty(nameof(NativeBattleEffectMemory.CuraContextF4)).GetInt32());
        Assert.Equal(JsonValueKind.Array, root.GetProperty(nameof(NativeBattleEffectMemory.Allocations)).ValueKind);
        Assert.DoesNotContain("effectState", json, StringComparison.OrdinalIgnoreCase);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(json)!;
        Assert.Equal(source.Cursor, restored.Cursor);
        Assert.Equal(source.CureContextF4, restored.CureContextF4);
        Assert.Equal(source.CuraContextF4, restored.CuraContextF4);
        Assert.Equal(source.Allocations.ToArray(), restored.Allocations.ToArray());
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        string missingRequired = json.Replace("\"Cursor\":17,", "", StringComparison.Ordinal);
        Assert.NotEqual(json, missingRequired);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleEffectMemory>(missingRequired));
    }

    [Fact]
    public void Capture_and_restore_traverse_grouped_effect_storage_without_sharing_working_owners()
    {
        NativeBattleEffect65State state = NativeBattleEffect65State.FromUnknown().WriteWord(NativeBattleEffect65Profile.LiteralBase, 123);
        NativeBattleEffectMemory source = Memory() with { Effect65 = state };
        NativeBattleEffectMemory captured = NativeBattleStorageSnapshots.Capture(source);
        NativeBattleEffectMemory first = NativeBattleStorageSnapshots.Restore(captured);
        NativeBattleEffectMemory second = NativeBattleStorageSnapshots.Restore(captured);
        first.Effect65!.WriteWord(NativeBattleEffect65Profile.LiteralBase, 456);

        Assert.Equal((ushort)123, source.Effect65!.ReadWord(NativeBattleEffect65Profile.LiteralBase));
        Assert.Equal((ushort)123, captured.Effect65!.ReadWord(NativeBattleEffect65Profile.LiteralBase));
        Assert.Equal((ushort)123, second.Effect65!.ReadWord(NativeBattleEffect65Profile.LiteralBase));
        Assert.Equal((ushort)456, first.Effect65.ReadWord(NativeBattleEffect65Profile.LiteralBase));
        Assert.NotSame(source.Effect65.Storage.Working, first.Effect65.Storage.Working);
    }

    [Fact]
    public void Native65_geometry_publication_keeps_grouped_effect_state_and_prior_registers_independent()
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65LowerFixture.Frame();
        frame = frame with { Memory = frame.Memory with { Effect65 = frame.State, CureContextF4 = 3 } };
        uint? originalIr0 = frame.Memory.ScriptedColorRegisters!.Ir0;
        NativeBattleEffect65Frame result = NativeBattleEffect65Camera.Interpolate6FC250(frame, new(100, -100, 50), new(300, 100, -50), new(300, 100, -50), new(100, -100, 50), -10, 10, 1, 3, NativeBattleEffect65LowerFixture.Inputs());

        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(originalIr0, frame.Memory.ScriptedColorRegisters.Ir0);
        Assert.Same(frame.Memory.Effect65, result.Memory.Effect65);
        Assert.Equal((short)3, result.Memory.CureContextF4);
        Assert.Same(frame.State, result.State);
    }

    private static NativeBattleEffectMemory Memory() => new(null, 0, 0, [], 0, 0, 0, 0);
}
