using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa88ModelOperationsTests
{
    [Fact]
    public void Visibility_saves_the_exact_bank_and_restores_only_bit4_of_still_loaded_models()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToImmutableArray();
        models = models.SetItem(0, models[0] with { Flags0 = 0x12 }).SetItem(3, models[3] with { Flags0 = 0x22 });
        NativeBattleRinoa88SceneMemory scene = new(NativeBattleEffectArena.Clear());
        NativeBattleRinoa88VisibilityStep hidden = NativeBattleRinoa88ModelOperations.ApplyVisibility(scene, models, 0x769CB0);
        Assert.Equal((ushort)0x26, hidden.Models[3].Flags0);
        Assert.Equal((ushort)0x22, hidden.Scene.Word(44));
        Assert.Equal((ushort)0x12, hidden.Models[0].Flags0);
        hidden = hidden with { Models = hidden.Models.SetItem(3, hidden.Models[3] with { Flags0 = 0xA6 }) };
        NativeBattleRinoa88VisibilityStep restored = NativeBattleRinoa88ModelOperations.ApplyVisibility(hidden.Scene, hidden.Models, 0x769F40);
        Assert.Equal((ushort)0xA2, restored.Models[3].Flags0);
    }

    [Fact]
    public void Party_midpoint_uses_active_party_extrema_and_signed_truncation()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToImmutableArray();
        models = models.SetItem(0, models[0] with { Flags0 = 2, Transform = new(default, new(-101, 123, -100), 0, 0, 4096) });
        models = models.SetItem(2, models[2] with { Flags0 = 2, Transform = new(default, new(100, -999, 99), 0, 0, 4096) });
        models = models.SetItem(3, models[3] with { Flags0 = 2, Transform = new(default, new(10000, 0, 10000), 0, 0, 4096) });
        ImmutableArray<byte> point = NativeBattleRinoa88ModelOperations.PartyMidpoint769A90(models);
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan()));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan().Slice(2)));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan().Slice(4)));
    }

    [Theory]
    [InlineData(0, 0x00091A2B)]
    [InlineData(2048, 0xCC091A2B)]
    public void Deformation_uses_native_color_store_widths(int amount, uint expected)
    {
        NativeBattleModulePoolTask task = new(0, 0, null, 0x771DB0, new byte[340].ToImmutableArray());
        task = task.WriteDword(88, 0xCCFFFFFF).WriteByte(55, 99);
        uint source = amount == 0 ? 0x00091A2Bu : 0x00123456u;
        task = NativeBattleRinoa88ModelOperations.Deform773420(task, source, (short)amount, 0);
        Assert.Equal(expected, task.ReadDword(88));
        Assert.Equal(amount == 0 ? (byte)0 : (byte)99, task.ReadByte(55));
    }

    [Theory]
    [InlineData(7, 5, 7)]
    [InlineData(3, 1, 1)]
    public void Stage_visibility_copies_native_backup_bit_instead_of_reconstructing_flags(int original, int hidden, int restored)
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        stage = stage with { Groups = stage.Groups.Select(group => group with { Flags = (byte)original }).ToImmutableArray() };
        stage = NativeBattleRinoa88Stage.Visibility(stage, 0x769DB0);
        Assert.All(stage.Groups, group => Assert.Equal((byte)hidden, group.Flags));
        stage = NativeBattleRinoa88Stage.Visibility(stage, 0x769E60);
        Assert.All(stage.Groups, group => Assert.Equal((byte)restored, group.Flags));
    }
}
