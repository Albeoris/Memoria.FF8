using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleThundaga105SessionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-321)]
    [InlineData(456)]
    public void Primary_anchor_preserves_model_XZ_and_replaces_elevated_Y_with_ground_WORD36(short ground)
    {
        BattleSession session = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        NativeBattleGraphicsModel model = new(3, 0) { Transform = new(new(10, 20, 30), new(123, -1800, -789), -600, ground, 4096) };
        NativeBattleGraphicsState graphics = new(Enumerable.Range(0, 7).Select(index => index == 3 ? model : new NativeBattleGraphicsModel(index, 0)).ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0);
        MethodInfo read = typeof(BattleSession).GetMethod("ReadNativeThundaga105Anchor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ImmutableArray<byte> anchor = (ImmutableArray<byte>)read.Invoke(session, [graphics, 3])!;
        Assert.Equal(8, anchor.Length);
        Assert.Equal((short)123, BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan()));
        Assert.Equal(ground, BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan()[2..]));
        Assert.Equal((short)-789, BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan()[4..]));
        Assert.Equal(ground, BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan()[6..]));
    }
}
