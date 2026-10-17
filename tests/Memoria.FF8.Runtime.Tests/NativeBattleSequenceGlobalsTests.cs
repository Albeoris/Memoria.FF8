using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceGlobalsTests
{
    [Theory]
    [InlineData(0, 0, 4096)]
    [InlineData(512, 2896, 2896)]
    [InlineData(1024, 4096, 0)]
    [InlineData(-1024, -4096, 0)]
    [InlineData(2048, 0, -4096)]
    [InlineData(int.MaxValue, 369, 4079)]
    [InlineData(int.MinValue, -375, 4079)]
    public void Writes_apply_original_full_dword_trigonometry_and_read_signed_words(int angle, short sine, short cosine)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().WriteSequenceGlobal5048E0(18, angle).WriteSequenceGlobal5048E0(19, angle);
        Assert.Equal(sine, memory.ReadSequenceGlobal5044B0(18));
        Assert.Equal(cosine, memory.ReadSequenceGlobal5044B0(19));
    }

    [Fact]
    public void Legacy_missing_globals_remain_unknown_until_each_original_write()
    {
        JsonObject saved = JsonSerializer.SerializeToNode(NativeBattleEffectMemory.FromColdProcess())!.AsObject();
        saved.Remove("SequenceSine");
        saved.Remove("SequenceCosine");
        NativeBattleEffectMemory memory = saved.Deserialize<NativeBattleEffectMemory>()!;
        Assert.Throws<NotSupportedException>(() => memory.ReadSequenceGlobal5044B0(18));
        Assert.Throws<NotSupportedException>(() => memory.ReadSequenceGlobal5044B0(19));
        memory = memory.WriteSequenceGlobal5048E0(18, -1024);
        Assert.Equal((short)-4096, memory.ReadSequenceGlobal5044B0(18));
        Assert.Throws<NotSupportedException>(() => memory.ReadSequenceGlobal5044B0(19));
        Assert.Throws<NotSupportedException>(() => memory.WriteSequenceGlobal5048E0(20, 0));
    }

    [Fact]
    public void Process_words_survive_allocator_load_and_json_restore()
    {
        NativeBattleEffectMemory cold = NativeBattleEffectMemory.FromColdProcess();
        Assert.Equal((short)0, cold.ReadSequenceGlobal5044B0(18));
        Assert.Equal((short)0, cold.ReadSequenceGlobal5044B0(19));
        NativeBattleEffectMemory memory = cold.WriteSequenceGlobal5048E0(18, -1024).WriteSequenceGlobal5048E0(19, 2048).BeginLoad(1);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Equal((short)-4096, restored.ReadSequenceGlobal5044B0(18));
        Assert.Equal((short)-4096, restored.ReadSequenceGlobal5044B0(19));
    }

    [Fact]
    public void Sequence_dispatch_preserves_expression_dword_and_publishes_shared_words_immediately()
    {
        byte[] program = Convert.FromHexString("C0FF7FCCFF7FE512C312E500C00004E513C313E501A1");
        byte[] section = new byte[4 + program.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(section, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(2), 4);
        program.CopyTo(section, 4);
        NativeBattleGraphicsResource resource = new(section.ToImmutableArray()) { AnimationFrames = [1] };
        NativeBattleGraphicsModel model = new(3, 3) { Flags2 = 4, Flags8 = 0, Sequence = 1, ResourceFlags44 = 1, SequenceState = new() { Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 } };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleSequenceInputs inputs = new(null, null)
        {
            ReadExternal = variable => memory.ReadSequenceGlobal5044B0(variable),
            WriteExternal = (variable, value) => memory = memory.WriteSequenceGlobal5048E0(variable, value)
        };
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, inputs);
        Assert.Equal(unchecked((short)NativeBattleCameraMath.Sine(32767 * 32767)), step.Model.SequenceState!.Locals[0]);
        Assert.NotEqual(unchecked((short)NativeBattleCameraMath.Sine(1)), step.Model.SequenceState.Locals[0]);
        Assert.Equal((short)0, step.Model.SequenceState.Locals[1]);
    }
}
