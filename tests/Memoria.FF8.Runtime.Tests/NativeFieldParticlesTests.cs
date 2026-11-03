using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldParticlesTests
{
    [Fact]
    public void SpawnMovesBeforeDrawingAndOffDrainsWithoutRewindingParticles()
    {
        byte[] definition = Definition();
        Write16(definition, 0, 32);
        Write16(definition, 20, 32);
        NativeFieldParticlesState initial = NativeFieldParticlesState.Create(definition).WithEmitter(0, 1);
        NativeFieldParticlesFrame first = initial.Step(new byte[256]);
        NativeFieldParticleSprite sprite = Assert.Single(first.Sprites);
        Assert.Equal((short)102, sprite.X);
        Assert.Equal((byte)104, first.State.RandomPhase);
        Assert.Equal((byte)1, first.State.Memory[10042]);
        Assert.Equal((byte)0, initial.Memory[10043]);
        NativeFieldParticlesFrame second = first.State.WithEmitter(0, 0).Step(new byte[256]);
        Assert.Equal((short)104, Assert.Single(second.Sprites).X);
        Assert.Equal((byte)0, second.State.Memory[10043]);
        Assert.Equal((short)0, Read16(second.State.Memory, 348));
        Assert.Empty(second.State.Step(new byte[256]).Sprites);
    }

    [Fact]
    public void SpawnNarrowsScaledSpreadBeforeSignedMultiplyAndWrapsPhase()
    {
        byte[] definition = Definition();
        Write16(definition, 354, 1024);
        Write16(definition, 356, short.MaxValue);
        Write16(definition, 364, 1024);
        byte[] table = Enumerable.Repeat((byte)255, 256).ToArray();
        NativeFieldParticlesState state = NativeFieldParticlesState.Create(definition).WithEmitter(0, 1) with { RandomPhase = 250 };
        NativeFieldParticlesFrame frame = state.Step(table);
        byte[] memory = frame.State.Memory;
        Assert.Equal(unchecked((short)(-16384 + (-32768 * 255 >> 8) + 16 * 32767)), Read16(memory, 10034));
        Assert.Equal(unchecked((short)((-2 * 255 >> 8) - short.MaxValue)), Read16(memory, 10038));
        Assert.Equal(unchecked((short)((-32768 * 255 >> 8) - 16384)), Read16(memory, 10028));
        Assert.Equal((byte)98, frame.State.RandomPhase);
    }

    [Fact]
    public void PerTypeLimitAndSharedPoolConsumeRandomOnlyForSuccessfulSpawns()
    {
        byte[] definition = Definition();
        definition[5952 + 128] = 7;
        Write16(definition, 346, 2);
        NativeFieldParticlesFrame first = NativeFieldParticlesState.Create(definition).WithEmitter(0, 1).Step(new byte[256]);
        Assert.Equal(2, first.Sprites.Count);
        Assert.Equal((byte)208, first.State.RandomPhase);
        NativeFieldParticlesFrame second = first.State.Step(new byte[256]);
        Assert.Equal((byte)208, second.State.RandomPhase);
        NativeFieldParticlesState full = NativeFieldParticlesState.Create(definition).WithEmitter(0, 1);
        for (int index = 0; index < 128; index++)
        {
            full.Memory[10016 + index * 32 + 27] = 1;
            full.Memory[10016 + index * 32 + 24] = 1;
        }
        full.Memory[372 + 14] = 2;
        Write16(full.Memory, 372 + 348, 128);
        Assert.Equal((byte)0, full.Step(new byte[256]).State.RandomPhase);
    }

    [Fact]
    public void StageInterpolationAndSerializationPreservePhysicalCounters()
    {
        byte[] definition = Definition();
        definition[8] = 20;
        definition[28] = 40;
        definition[16] = 100;
        definition[36] = 200;
        definition[34] = 1;
        NativeFieldParticlesFrame first = NativeFieldParticlesState.Create(definition).WithEmitter(0, 1).Step(new byte[256]);
        NativeFieldParticlesState saved = JsonSerializer.Deserialize<NativeFieldParticlesState>(JsonSerializer.Serialize(first.State))!;
        NativeFieldParticlesFrame second = saved.WithEmitter(0, 0).Step(new byte[256]);
        NativeFieldParticleSprite sprite = Assert.Single(second.Sprites);
        Assert.Equal((short)30, sprite.Width);
        Assert.Equal((byte)150, sprite.Red);
        Assert.Equal((byte)1, second.State.Memory[10041]);
        Assert.Equal((byte)0, second.State.Memory[10042]);
        Assert.Equal((byte)1, second.State.Memory[10043]);
        Assert.Equal((byte)0, second.State.Step(new byte[256]).State.Memory[10043]);
    }

    [Fact]
    public void InvalidEmitterDoesNotMutateCallerAndWarmUpRestoresControlBytes()
    {
        byte[] definition = Definition();
        Write16(definition, 5952 + 252, 9);
        NativeFieldParticlesState invalid = NativeFieldParticlesState.Create(definition).WithEmitter(0, 1);
        byte[] original = (byte[])invalid.Memory.Clone();
        Assert.Throws<NotSupportedException>(() => invalid.Step(new byte[256]));
        Assert.Equal(original, invalid.Memory);
        definition = Definition();
        Write16(definition, 5952 + 248, 1);
        NativeFieldParticlesState initial = NativeFieldParticlesState.Create(definition).WithEmitter(0, 1);
        NativeFieldParticlesState warmed = initial.WarmUp(new byte[256]);
        Assert.Equal(initial.Emitters, warmed.Emitters);
        Assert.Equal((byte)1, warmed.Memory[10042]);
        Assert.Equal((byte)104, warmed.RandomPhase);
    }

    [Fact]
    public void RestoreRejectsDefinitionAndDrawingTamperingWithoutChangingState()
    {
        byte[] definition = Definition();
        NativeFieldParticlesFrame frame = NativeFieldParticlesState.Create(definition).WithEmitter(0, 1).Step(new byte[256]);
        frame.State.ValidateDefinition(definition);
        frame.State.ValidateDrawings(frame.Sprites);
        byte[] original = (byte[])frame.State.Memory.Clone();
        NativeFieldParticleSprite sprite = Assert.Single(frame.Sprites);
        Assert.Throws<InvalidDataException>(() => frame.State.ValidateDrawings([sprite with { U = 1 }]));
        Assert.Throws<InvalidDataException>(() => frame.State.ValidateDrawings([sprite, sprite]));
        definition[0] = 1;
        Assert.Throws<InvalidDataException>(() => frame.State.ValidateDefinition(definition));
        Assert.Equal(original, frame.State.Memory);
    }

    private static byte[] Definition()
    {
        byte[] definition = new byte[10016];
        for (int emitter = 0; emitter < 16; emitter++)
        {
            for (int lane = 0; lane < 4; lane++)
            {
                Write16(definition, 5952 + emitter * 254 + 144 + lane * 18, -1);
            }
        }
        definition[14] = 2;
        definition[5952 + 128] = 1;
        definition[5952 + 208] = 10;
        Write16(definition, 5952, 100);
        Write16(definition, 5952 + 144, 0);
        Write16(definition, 5952 + 252, 1);
        Write16(definition, 346, 1);
        return definition;
    }

    private static void Write16(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, 2), unchecked((short)value));
    private static short Read16(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, 2));
}
