using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceStandingIdentityTests
{
    [Theory]
    [InlineData(0, 77, "A2", 2)]
    [InlineData(1, 77, "A2", 2)]
    [InlineData(0, 77, "AC00", 2)]
    [InlineData(3, 0, "A2", 16)]
    [InlineData(0, 0, "A2", 16)]
    public void Standing_uses_model_identity_while_orientation_keeps_physical_slot(int index, byte modelId, string opcode, byte expectedSequence)
    {
        byte[] bytes = new byte[48];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 34);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 38);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 40);
        Convert.FromHexString(opcode).CopyTo(bytes, 34);
        bytes[38] = 0;
        bytes[40] = 16;
        NativeBattleGraphicsResource resource = new(bytes.ToImmutableArray()) { AnimationFrames = modelId >= 16 ? [4, 5] : Enumerable.Repeat((byte)4, 17).ToImmutableArray() };
        NativeBattleGraphicsModel model = new(index, 3)
        {
            ModelId4 = modelId,
            Flags2 = 0,
            Flags8 = 0x1000000,
            Sequence = 1,
            ResourceFlags44 = 1,
            SequenceState = new() { Default2 = 2, Pose = 0, PoseFrame114 = 1, PoseLimit115 = 4 }
        };
        NativeBattleSequenceInputs inputs = new(null, null) { OriginalResource = resource };
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, inputs);
        Assert.Equal(expectedSequence, step.Model.Sequence);
        Assert.Equal(expectedSequence == 2 ? (byte)0 : (byte)16, step.Model.SequenceState!.Pose);
        Assert.Equal(index >= 3 ? (short)2048 : (short)0, step.Model.Rotation14);
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(step.Model))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleSequenceMachine.Advance(step.Model, resource, inputs)), JsonSerializer.Serialize(NativeBattleSequenceMachine.Advance(restored, resource, inputs)));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceMachine.Advance(model with { ModelId4 = null }, resource, inputs));
    }
}
