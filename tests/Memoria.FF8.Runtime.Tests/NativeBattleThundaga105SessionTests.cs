using static Memoria.FF8.Tests.NativeBattleTypedFixture;
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
        NativeBattleHostedTestFixture.BindProcess(session);
        NativeBattleGraphicsModel model = new(0) { Transform = new(new(10, 20, 30), new(123, -1800, -789), -600, ground, 4096) };
        NativeBattleGraphicsState graphics = OriginalGraphics([(3, OriginalOwner(3), model)]);
        MethodInfo read = typeof(BattlePresentationProcess).GetMethod("ReadNativeThundaga105Anchor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ImmutableArray<byte> anchor = (ImmutableArray<byte>)read.Invoke(session.Presentation!, [graphics, OriginalOwner(3)])!;
        Assert.Equal(8, anchor.Length);
        Assert.Equal((short)123, BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan()));
        Assert.Equal(ground, BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan()[2..]));
        Assert.Equal((short)-789, BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan()[4..]));
        Assert.Equal(ground, BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan()[6..]));
    }
}
