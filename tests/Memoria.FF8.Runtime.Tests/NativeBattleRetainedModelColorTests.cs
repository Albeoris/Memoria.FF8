using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRetainedModelColorTests
{
    [Fact]
    public void Unallocated_nonzero_colors_survive_capture_JSON_and_the_next_bank_clear()
    {
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess();
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank(retained.ModelColors40).ToImmutableArray();
        models = models.SetItem(0, models[0] with { Flags0 = 3, Color40 = 0x00123456 }).SetItem(6, models[6] with { Flags0 = 0, Color40 = 0x00ABCDEF });
        BattleSnapshot snapshot = Snapshot(retained) with { NativeGraphics = new(models, ["party", null, null, "enemy", null, null, null], 0, 0) };
        NativeBattleRetainedMemory captured = NativeBattleRetainedMemory.Capture(snapshot, retained.ColdExecutableSha256);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(captured))!;
        NativeBattleGraphicsModel[] next = NativeBattleGraphicsModels.ClearBank(restored.ModelColors40);
        Assert.Equal(0x00123456u, next[0].Color40);
        Assert.Equal(0x00ABCDEFu, next[6].Color40);
        Assert.All(next, model => Assert.Equal((ushort)0, model.Flags0));
        Assert.Null(next[6].ModelId4);
        Assert.Null(next[6].Flags2);
        Assert.Null(next[6].ScaleVectorAddress48);
        Assert.Equal(0x00ABCDEFu, models[6].Color40);
    }

    [Fact]
    public void Legacy_missing_owner_is_not_hydrated_from_later_graphics_or_a_new_clear()
    {
        NativeBattleRetainedMemory legacy = NativeBattleRetainedMemory.FromColdProcess() with { ModelColors40 = null };
        string json = JsonSerializer.Serialize(legacy);
        Assert.DoesNotContain("ModelColors40", json);
        legacy = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(json)!;
        Assert.Null(legacy.ModelColors40);
        Assert.All(NativeBattleGraphicsModels.ClearBank(legacy.ModelColors40), model => Assert.Null(model.Color40));
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank([1u, 2u, 3u, 4u, 5u, 6u, 7u]).ToImmutableArray();
        BattleSnapshot snapshot = Snapshot(legacy) with { NativeGraphics = new(models, ["party", null, null, "enemy", null, null, null], 0, 0) };
        NativeBattleRetainedMemory captured = NativeBattleRetainedMemory.Capture(snapshot, null);
        Assert.Null(captured.ModelColors40);
    }

    [Fact]
    public void Individual_unknown_color_stays_unknown_while_other_rows_remain_known()
    {
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess() with { ModelColors40 = [1u, 2u, null, 4u, 5u, 6u, 0x00FEDCBAu] };
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearBank(retained.ModelColors40);
        Assert.Null(models[2].Color40);
        Assert.Equal(0x00FEDCBAu, models[6].Color40);
        NativeBattleRetainedMemory captured = NativeBattleRetainedMemory.Capture(Snapshot(retained) with { NativeGraphics = new(models.ToImmutableArray(), ["party", null, null, "enemy", null, null, null], 0, 0) }, null);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(captured))!;
        ImmutableArray<uint?> colors = restored.ModelColors40 ?? throw new InvalidDataException("The retained model-color owner was lost.");
        Assert.Null(colors[2]);
        Assert.Equal(0x00FEDCBAu, colors[6]);
    }

    private static BattleSnapshot Snapshot(NativeBattleRetainedMemory retained) => new BattleSession(new BattleDefinition
    {
        NativeRetainedMemory = retained,
        Combatants = [new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }]
    }, BattleAtbMode.Normal).Capture();
}
