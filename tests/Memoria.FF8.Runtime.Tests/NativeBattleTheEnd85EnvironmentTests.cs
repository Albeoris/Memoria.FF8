using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85EnvironmentTests
{
    [Fact]
    public void Stage_fades_clear_only_RGB_and_restore_original_flag_bit4()
    {
        NativeBattleTheEnd85EnvironmentInput input = Input(0x787E20, 0);
        input = input with { Stage = input.Stage with { Groups = input.Stage.Groups.Select(group => group with { Flags = 6, ColorFlags = 0xAA123456 }).ToImmutableArray() } };
        input = NativeBattleTheEnd85Environment.Advance(input, 0x808080).Input;
        Assert.All(input.Stage.Groups, group => Assert.Equal(0xAA000000u, group.ColorFlags));
        for (int tick = 0; tick < 16; tick++)
        {
            input = NativeBattleTheEnd85Environment.Advance(input, 0x808080).Input;
        }
        Assert.All(input.Stage.Groups, group =>
        {
            Assert.Equal((short)4096, group.ColorDepth);
            Assert.Equal((byte)4, group.Flags);
        });
        input = AtStage6(input);
        input = NativeBattleTheEnd85Environment.Advance(input, 0x808080).Input;
        Assert.All(input.Stage.Groups, group => Assert.Equal((byte)6, group.Flags));
        NativeBattleTheEnd85EnvironmentStep last = null!;
        for (int tick = 0; tick < 16; tick++)
        {
            last = NativeBattleTheEnd85Environment.Advance(input, 0x808080);
            input = last.Input;
        }
        Assert.True(last.Complete);
        Assert.All(input.Stage.Groups, group => Assert.Equal((short)0, group.ColorDepth));
    }

    [Fact]
    public void Party_fade_saves_only_loaded_party_rows_and_restores_hide_bit_without_touching_enemy()
    {
        NativeBattleTheEnd85EnvironmentInput input = Input(0x787FD0, 1);
        input = input with { Models = input.Models.SetItem(0, new(0, 2)).SetItem(1, new(1, 6)).SetItem(2, new(2, 0)).SetItem(3, new(3, 2) { Color40 = 0x12345678 }) };
        for (int tick = 0; tick < 8; tick++)
        {
            input = NativeBattleTheEnd85Environment.Advance(input, 0x806040).Input;
        }
        Assert.Equal(0x02000000u, input.Models[0].Color40);
        Assert.Equal((ushort)0x806, input.Models[0].Flags0);
        Assert.Equal((ushort)0, input.Models[2].Flags0);
        Assert.Equal(0x12345678u, input.Models[3].Color40);
        Assert.Equal(0x802u, BinaryPrimitives.ReadUInt32LittleEndian(input.Context.AsSpan().Slice(32)));
        Assert.Equal(0x806u, BinaryPrimitives.ReadUInt32LittleEndian(input.Context.AsSpan().Slice(36)));
        input = NativeBattleTheEnd85Environment.Advance(AtStage6(input), 0x806040).Input;
        Assert.Equal((ushort)0x802, input.Models[0].Flags0);
        Assert.Equal((ushort)0x806, input.Models[1].Flags0);
        for (int tick = 0; tick < 16; tick++)
        {
            input = NativeBattleTheEnd85Environment.Advance(input, 0x806040).Input;
        }
        Assert.Equal(0x806040u, input.Models[0].Color40);
        Assert.Equal((byte)0, input.Models[0].Alpha7);
    }

    [Fact]
    public void Music_gain_clamps_to48_then127_and_waits_for_stage6()
    {
        NativeBattleTheEnd85EnvironmentInput input = Input(0x7882E0, 0);
        input = NativeBattleTheEnd85Environment.Advance(input, 0).Input;
        NativeBattleTheEnd85EnvironmentStep current = null!;
        for (int tick = 0; tick < 10; tick++)
        {
            current = NativeBattleTheEnd85Environment.Advance(input, 0);
            input = current.Input;
        }
        Assert.Equal((short)48, current.MusicGain);
        Assert.Null(NativeBattleTheEnd85Environment.Advance(input, 0).MusicGain);
        input = NativeBattleTheEnd85Environment.Advance(AtStage6(input), 0).Input;
        for (int tick = 0; tick < 10; tick++)
        {
            current = NativeBattleTheEnd85Environment.Advance(input, 0);
            input = current.Input;
        }
        Assert.Equal((short)127, current.MusicGain);
        Assert.True(current.Complete);
    }

    private static NativeBattleTheEnd85EnvironmentInput Input(uint callback, byte phase) => new(new NativeBattleModulePoolTask(1, 0, null, callback, new byte[84].ToImmutableArray()).WriteByte(41, phase), new byte[84].ToImmutableArray(), NativeBattleStageState.FromColdProcess(), Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToImmutableArray());
    private static NativeBattleTheEnd85EnvironmentInput AtStage6(NativeBattleTheEnd85EnvironmentInput input)
    {
        byte[] context = input.Context.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(context.AsSpan(64), 6);
        return input with { Context = context.ToImmutableArray() };
    }
}
