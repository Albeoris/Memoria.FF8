using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRetainedModelColorTests
{
    [Fact]
    public void Unallocated_nonzero_colors_survive_capture_JSON_and_the_next_bank_clear()
    {
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess();
        NativeBattleRetainedMemory modelSeed = retained with { ModelColors40 = [0x00123456u, null, null, null, null, null, 0x00ABCDEFu] };
        BattleSnapshot snapshot = Snapshot(retained);
        NativeBattleGraphicsState graphics = GraphicsWithColors(snapshot, modelSeed);
        BattleModelOwner party = snapshot.ReadFixturePresentation().NativeActionPackets!.ModelAliases.DecodeLiteral(0);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(party, graphics.ActorModels.Get(party) with { Flags0 = 3 }) };
        snapshot = snapshot.WithFixturePresentation(facts => facts with { NativeGraphics = graphics });
        NativeBattleRetainedMemory captured = NativeBattleRetainedMemory.Capture(snapshot, retained.ColdExecutableSha256);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(captured))!;
        ImmutableArray<NativeBattleGraphicsModel> next = OriginalCells(restored);
        Assert.Equal(0x00123456u, next[0].Color40);
        Assert.Equal(0x00ABCDEFu, next[6].Color40);
        Assert.All(next, model => Assert.Equal((ushort)0, model.Flags0));
        Assert.Null(next[6].ModelId4);
        Assert.Null(next[6].Flags2);
        Assert.Null(next[6].ScaleVectorAddress48);
        Assert.Equal(0x00ABCDEFu, ReadOriginalModelCell(graphics, 6).Color40);
    }

    [Fact]
    public void Legacy_missing_owner_is_not_hydrated_from_later_graphics_or_a_new_clear()
    {
        NativeBattleRetainedMemory legacy = NativeBattleRetainedMemory.FromColdProcess() with { ModelColors40 = null };
        string json = JsonSerializer.Serialize(legacy);
        Assert.DoesNotContain("ModelColors40", json);
        legacy = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(json)!;
        Assert.Null(legacy.ModelColors40);
        Assert.All(OriginalCells(legacy), model => Assert.Null(model.Color40));
        NativeBattleRetainedMemory modelSeed = legacy with { ModelColors40 = [1u, 2u, 3u, 4u, 5u, 6u, 7u] };
        BattleSnapshot snapshot = Snapshot(legacy);
        snapshot = snapshot.WithFixturePresentation(facts => facts with { NativeGraphics = GraphicsWithColors(snapshot, modelSeed) });
        NativeBattleRetainedMemory captured = NativeBattleRetainedMemory.Capture(snapshot, null);
        Assert.Null(captured.ModelColors40);
    }

    [Fact]
    public void Individual_unknown_color_stays_unknown_while_other_rows_remain_known()
    {
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess() with { ModelColors40 = [1u, 2u, null, 4u, 5u, 6u, 0x00FEDCBAu] };
        ImmutableArray<NativeBattleGraphicsModel> models = OriginalCells(retained);
        Assert.Null(models[2].Color40);
        Assert.Equal(0x00FEDCBAu, models[6].Color40);
        BattleSnapshot snapshot = Snapshot(retained);
        NativeBattleRetainedMemory captured = NativeBattleRetainedMemory.Capture(snapshot.WithFixturePresentation(facts => facts with { NativeGraphics = GraphicsWithColors(snapshot, retained) }), null);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(captured))!;
        ImmutableArray<uint?> colors = restored.ModelColors40 ?? throw new InvalidDataException("The retained model-color owner was lost.");
        Assert.Null(colors[2]);
        Assert.Equal(0x00FEDCBAu, colors[6]);
    }

    private static NativeBattleGraphicsState GraphicsWithColors(BattleSnapshot snapshot, NativeBattleRetainedMemory modelSeed)
    {
        NativeBattleModelAliases aliases = snapshot.ReadFixturePresentation().NativeActionPackets!.ModelAliases;
        NativeBattleModelBoundary boundary = NativeBattleModelBoundary.FromRetainedMemory(modelSeed);
        BattleActorModels models = new([]);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(0), aliases.DecodeLiteral(0), models);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(3), aliases.DecodeLiteral(3), models);
        return new(models, boundary, 0, 0, OriginalEffects());
    }

    private static ImmutableArray<NativeBattleGraphicsModel> OriginalCells(NativeBattleRetainedMemory retained)
    {
        return NativeBattleModelBoundary.FromRetainedMemory(retained).CaptureOriginalCells(new BattleActorModels([]));
    }

    private static BattleSnapshot Snapshot(NativeBattleRetainedMemory retained) => new BattleSession(new BattleDefinition
    {
        NativeRetainedMemory = retained,
        Combatants = [new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }]
    }, BattleAtbMode.Normal).Capture();
}
