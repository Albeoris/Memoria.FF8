using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Controller;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBladeShot12ChildrenTests
{
    private static readonly NativeBattleBladeShot12Resources Resources = new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, 0, 0);

    [Theory]
    [InlineData(0x6E9B50u)]
    [InlineData(0x6E9C10u)]
    public void Expiring_sprite_draws_before_retirement_and_skips_physics_even_with_graphics_pause(uint callback)
    {
        var frame = Frame(callback, 2);
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, NativeBattleTemporaryMemory.Address + 8, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0xCAFEBABEu)), 4) };
        int draws = 0;
        var actual = AdvanceSprite(frame, ChildBBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBladeShot12DrawSprite180>(operation);
            var context = Context(current, draw.TemporaryAddress, 180);
            Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(4, 2)));
            Assert.Equal(callback == 0x6E9C10 ? (ushort)1 : (ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(0x24, 2)));
            if (callback == 0x6E9B50)
            {
                Assert.Equal(0xCAFEBABEu, BitConverter.ToUInt32(context.Read(8, 4)));
            }
            draws++;
            return current;
        }) with { GraphicsFlags = 0x201 });
        Assert.Equal(1, draws);
        Assert.Equal(2, actual.Result);
        var task = Read(actual.Frame, ChildBBank);
        Assert.Equal((ushort)3, task.ReadWord(12));
        Assert.Equal((ushort)10, task.ReadWord(0x14));
        Assert.Equal((ushort)7, task.ReadWord(0x1C));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Live_sprite_wraps_velocity_before_position_and_preserves_unwritten_suffix()
    {
        var frame = Frame(0x6E9B50, 0);
        var task = Read(frame, ChildBBank).WriteWord(0x1C, 0x7FFF).WriteWord(0x24, 2).WriteWord(0x32, 0xBEEF);
        frame = Write(frame, ChildBBank, task);
        var actual = AdvanceSprite(frame, ChildBBank, Resources, Inputs());
        Assert.Equal(0, actual.Result);
        task = Read(actual.Frame, ChildBBank);
        Assert.Equal((ushort)0x8001, task.ReadWord(0x1C));
        Assert.Equal((ushort)0x800B, task.ReadWord(0x14));
        Assert.Equal((ushort)0xBEEF, task.ReadWord(0x32));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(20)]
    public void Transient_outside_unsigned_lifetime_touches_no_owner(int age)
    {
        Assert.Null(DrawTransient6EA3E0(null!, 0, age, Resources, null!));
    }

    [Theory]
    [InlineData(6, 0)]
    [InlineData(7, 2)]
    public void Custom_mesh_draws_eight_ages_with_C0_context_and_three_byte_FAR(int age, int result)
    {
        var frame = Frame(0x6E9D60, (ushort)age, true);
        var actual = AdvanceMesh(frame, ChildABank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBladeShot12DrawCustomMesh88>(operation);
            var context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(0xC0u, BitConverter.ToUInt32(context.Read(28, 4)));
            Assert.Equal(0x1364F14u, draw.SourceAddress);
            Assert.Throws<NotSupportedException>(() => context.Read(11, 1));
            return current;
        }) with { GraphicsFlags = 0x201 });
        Assert.Equal(result, actual.Result);
        Assert.Equal((ushort)(age + 1), Read(actual.Frame, ChildABank).ReadWord(12));
    }

    private static NativeBattleBladeShot12Frame Frame(uint callback, ushort age, bool mesh = false)
    {
        var pool = NativeBattleEffectTaskPool.Initialize(mesh ? ChildADescriptor : ChildBDescriptor, mesh ? ChildABank : ChildBBank, mesh ? (ushort)32 : (ushort)52, 1);
        (pool, _) = pool.Append(callback);
        NativeBattleOriginalEffectState effect = new(12, 11, new(12, Entry, Initializer), null, RootDescriptor) { Pools = [pool] };
        var memory = NativeBattleBladeShot12MeshContext88Tests.Memory(2000) with { ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        var frame = new NativeBattleBladeShot12Frame(memory, effect, new(123), NativeBattleBladeShot12State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        var task = Read(frame, pool.BankAddress).WriteWord(12, age).WriteDword(16, 0x13627E4).WriteWord(0x14, 10).WriteWord(0x16, 20).WriteWord(0x18, 30).WriteWord(0x1A, 3).WriteWord(0x1C, 7);
        if (!mesh)
        {
            task = task.WriteWord(0x22, 2048).WriteWord(0x24, 2).WriteWord(0x2C, 1).WriteWord(0x2E, 2).WriteWord(0x30, 3);
        }
        return Write(frame, pool.BankAddress, task);
    }
    private static NativeBattleBladeShot12Inputs Inputs(Func<NativeBattleBladeShot12Frame, NativeBattleBladeShot12Operation, NativeBattleBladeShot12Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute ?? ((current, _) => current));
}
