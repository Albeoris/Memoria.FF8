using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Controller;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGatlingGun150DrawingTests
{
    [Theory]
    [InlineData(0x60E6D0u, 1, 0xDC24E0u, 2048, 2)]
    [InlineData(0x60EBD0u, 8, 0xDC27E0u, 4096, 1)]
    [InlineData(0x60F1C0u, 4, 0xDC2AA8u, 3072, 1)]
    public void Mesh_uses_actual_partial_context_and_draws_before_pause(uint callback, ushort age, uint source, int cue, int count)
    {
        NativeBattleGatlingGun150Frame frame = Fixture(callback, age);
        int calls = 0;
        NativeBattleGatlingGun150Inputs inputs = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleGatlingGun150DrawStatic88>(operation);
            Assert.Equal(source, draw.SourceAddress);
            Assert.Equal(17, draw.TableBaseBucket);
            Assert.Equal(2, draw.DepthShift);
            NativeBattleEffectArena context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(0xF3u, Dword(context, 0x1C));
            Assert.Equal(unchecked((uint)cue), Dword(context, 12));
            Assert.Equal(0u, Dword(context, 8));
            Assert.Null(current.Memory.GeometryRegisters!.Rotation20[18]);
            if (callback == 0x60E6D0)
            {
                NativeBattleActionMatrix billboard = current.Memory.ModuleSpriteBillboard21DFED0!.ReadMatrix();
                Assert.Equal(new int?[] { billboard.X, billboard.Y, billboard.Z }, current.Memory.GeometryRegisters.Translation);
                Assert.NotEqual(1000, billboard.Z);
            }
            if (calls == 0)
            {
                Assert.Throws<NotSupportedException>(() => context.Read(4, 4));
            }
            else
            {
                Assert.Equal(0x12345678u, Dword(context, 4));
            }
            calls++;
            return PublishContext(current, draw.TemporaryAddress, context.Write(4, BitConverter.GetBytes(0x12345678u)), 88);
        }, 0x201);
        var actual = AdvanceMesh(frame, ChildBank, null!, inputs);
        Assert.Equal(count, calls);
        Assert.Equal(0, actual.Result);
        Assert.Equal(age, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)4096, Read(actual.Frame, ChildBank).ReadWord(0x1C));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(0x60EBD0u, 8, 2)]
    [InlineData(0x60F1C0u, 4, 2)]
    public void Mesh_negative_speed_uses_signed_truncating_division_and_native_completion(uint callback, ushort age, int done)
    {
        NativeBattleGatlingGun150Frame frame = Fixture(callback, age);
        var actual = AdvanceMesh(frame, ChildBank, null!, Inputs((current, _) => current));
        Assert.Equal(done, actual.Result);
        Assert.Equal((ushort)4087, Read(actual.Frame, ChildBank).ReadWord(0x1C));
        Assert.Equal((short)-8, Signed(Read(actual.Frame, ChildBank), 0x1E));
    }

    [Fact]
    public void Negative_mesh_age_retains_unused_cue_and_single_sprite_releases_before_completion()
    {
        NativeBattleGatlingGun150Frame frame = Fixture(0x60EBD0, ushort.MaxValue);
        var mesh = AdvanceMesh(frame, ChildBank, null!, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleGatlingGun150DrawStatic88>(operation);
            NativeBattleEffectArena context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(0x33u, Dword(context, 0x1C));
            Assert.Throws<NotSupportedException>(() => context.Read(12, 4));
            return current;
        }));
        Assert.Equal((ushort)0, Read(mesh.Frame, ChildBank).ReadWord(12));
        frame = Fixture(0x60ED10, 15);
        var sprite = AdvanceSprite(frame, ChildBank, null!, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleGatlingGun150DrawSprite180>(operation);
            Assert.Equal(0xDC1FA4u, draw.SourceAddress);
            Assert.Equal((ushort)15, BitConverter.ToUInt16(Context(current, draw.TemporaryAddress, 180).Read(4, 2)));
            Assert.Equal((ushort)0, BitConverter.ToUInt16(Context(current, draw.TemporaryAddress, 180).Read(0x24, 2)));
            Assert.False(current.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
            return current;
        }));
        Assert.Equal(2, sprite.Result);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(sprite.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Duplicate_first_mesh_passes_have_distinct_fixed_copy_ids_and_the_original_shared_task_owner()
    {
        NativeBattleGatlingGun150Frame frame = Fixture(0x60E6D0, 1);
        List<NativeBattleGatlingGun150DrawStatic88> draws = [];
        var result = AdvanceMesh(frame, ChildBank, null!, Inputs((current, operation) =>
        {
            draws.Add(Assert.IsType<NativeBattleGatlingGun150DrawStatic88>(operation));
            return current;
        }, 0x201));
        Assert.Equal(2, draws.Count);
        NativeBattlePilotVisualIdentity first = Assert.IsType<NativeBattlePilotVisualIdentity>(draws[0].PilotIdentity);
        NativeBattlePilotVisualIdentity second = Assert.IsType<NativeBattlePilotVisualIdentity>(draws[1].PilotIdentity);
        Assert.Equal(0, first.Primitive);
        Assert.Equal(1, second.Primitive);
        Assert.Equal(first with { Primitive = 1 }, second);
        Assert.Equal((ushort)150, first.Effect);
        Assert.Equal(0xDC24E0, first.ResourceGeneration);
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal((ushort)1, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Equal(0, result.Result);
    }

    private static NativeBattleGatlingGun150Frame Fixture(uint callback, ushort age)
    {
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, _) = children.Append(callback);
        NativeBattleOriginalEffectState effect = new(150, 150, new(150, Entry, Initializer), null, RootDescriptor) { Pools = [children] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleGatlingGun150Frame frame = new(memory, effect, new(1), NativeBattleGatlingGun150State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        return Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 1000).MarkPayloadUnknown(0x16, 2).WriteWord(0x18, 0).WriteWord(0x1A, 0).WriteWord(0x1C, 4096).WriteWord(0x1E, unchecked((ushort)-9)));
    }

    private static NativeBattleGatlingGun150Inputs Inputs(Func<NativeBattleGatlingGun150Frame, NativeBattleGatlingGun150Operation, NativeBattleGatlingGun150Frame> execute, uint flags = 0) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute);
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
