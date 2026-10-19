using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112ParticlesTests
{
    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(32767, 32767, 0)]
    [InlineData(65535, 1, 0)]
    public void SpriteCounterWrapsAsSignedWordAfterDrawingTheOriginalPhase(ushort age, ushort lifetime, int expectedResult)
    {
        NativeBattleModulePoolTask task = Task(28).WriteWord(12, age).WriteWord(14, lifetime).WriteWord(16, 0x8000).WriteWord(18, 2).WriteWord(20, 0xFFFD).WriteWord(22, 0xFFFC).WriteDword(24, 0x12C5650);
        NativeBattleDuel112Sprite? drawn = null;
        NativeBattleDuel112ParticleStep result = NativeBattleDuel112Particles.Advance6CD5C0(task, sprite => drawn = sprite);
        Assert.Equal(age, drawn!.Phase);
        Assert.Equal(new NativeBattleVector(-32768, 2, -3), drawn.Position);
        Assert.Equal((ushort)0xFFFC, drawn.Scale);
        Assert.Equal(1, drawn.DepthOffset);
        Assert.Equal(expectedResult, result.Result);
        Assert.Equal(unchecked((ushort)(age + 1)), result.Task.ReadWord(12));
        Assert.Equal(task.Payload.AsSpan().Slice(2).ToArray(), result.Task.Payload.AsSpan().Slice(2).ToArray());
    }

    [Fact]
    public void MovingSpriteUsesSeparateSignedDepthWordAndAdvancesOnlyAfterDrawing()
    {
        NativeBattleModulePoolTask task = Task(36).WriteWord(12, 0).WriteWord(14, 1).WriteDword(16, 0x12C5794).WriteWord(20, 1).WriteWord(22, 0xFFFE).WriteWord(24, 3).WriteWord(26, 4096).WriteWord(28, 0xFFFF).WriteWord(30, 2).WriteWord(32, 0xFFFD).WriteWord(34, 0xFFF8);
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
        NativeBattleDuel112ParticleStep Run(NativeBattleModulePoolTask current) => NativeBattleDuel112Particles.Advance6CD750(current, sprite =>
        {
            Assert.Equal(new NativeBattleVector(1, -2, 3), sprite.Position);
            Assert.Equal(2, sprite.DepthOffset);
            Assert.Equal((ushort)0, sprite.Phase);
        });
        NativeBattleDuel112ParticleStep result = Run(task);
        Assert.Equal(0, result.Task.ReadWord(20));
        Assert.Equal(0, result.Task.ReadWord(22));
        Assert.Equal(0, result.Task.ReadWord(24));
        Assert.Equal(2, result.Result);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(restored)));
    }

    [Fact]
    public void DebrisDrawsBeforeGravityRotationMovementAndSignedGroundBounce()
    {
        NativeBattleModulePoolTask task = Task(48).WriteWord(12, 29).WriteWord(16, 32767).WriteWord(18, 0xFFFF).WriteWord(20, 0).WriteWord(24, 10).WriteWord(26, 0).WriteWord(28, 0xFFFF).WriteWord(32, 0xFFFF).WriteWord(34, 5).WriteWord(36, 0x7FFF).WriteWord(40, 2).WriteWord(42, 0xFFFF).WriteWord(44, 1);
        NativeBattleDuel112ParticleStep result = NativeBattleDuel112Particles.Advance6CD640(task, drawn => Assert.Same(task, drawn));
        Assert.Equal((ushort)32777, result.Task.ReadWord(16));
        Assert.Equal((ushort)0, result.Task.ReadWord(18));
        Assert.Equal((ushort)0xFFFF, result.Task.ReadWord(20));
        Assert.Equal((ushort)0xFFF8, result.Task.ReadWord(26));
        Assert.Equal((ushort)1, result.Task.ReadWord(32));
        Assert.Equal((ushort)4, result.Task.ReadWord(34));
        Assert.Equal((ushort)0x8000, result.Task.ReadWord(36));
        Assert.Equal(2, result.Result);
        Assert.Equal(task.ReadWord(46), result.Task.ReadWord(46));
    }

    private static NativeBattleModulePoolTask Task(ushort stride)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, stride, 1);
        (pool, int slot) = pool.Append(0x6CD5C0);
        return pool.Slots[slot] with { Payload = Enumerable.Repeat((byte)0x91, stride - 12).ToImmutableArray() };
    }
}
