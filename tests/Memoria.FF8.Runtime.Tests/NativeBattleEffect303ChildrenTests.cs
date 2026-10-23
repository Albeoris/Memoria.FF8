using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect303Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect303Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect303ChildrenTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(0x100u)]
    [InlineData(0x200u)]
    [InlineData(0x40000u)]
    public void Node_uses_caller32_scale_gate_and_never_reads_unknown_scale_when_disabled(uint header)
    {
        var resources = NativeBattleEffect303StateTests.Resources();
        var caller = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(0, Point(100, 200, 300, 91).Read(0, 8)).Write(20, new byte[4]).Write(24, BitConverter.GetBytes(-128)).Write(28, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
        int calls = 0;
        var actual = DrawProgrammeNode66AE20(Frame(), resources.MainProgramme, Node(header), caller, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEffect303DrawMesh7043B0>(operation);
            var context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(-128, I(context, 24));
            Assert.Equal(0x2030u, U(context, 28));
            var matrix = current.Memory.GeometryRegisters!;
            Assert.Equal(new int?[] { 110, 220, 2330 }, matrix.Translation);
            calls++;
            return current;
        }));
        Assert.Equal(1, calls);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Invisible_node_does_not_read_caller_suffix_or_allocate()
    {
        var frame = Frame();
        var resources = NativeBattleEffect303StateTests.Resources();
        var node = Node(0) with { Data = Node(0).Data.SetItem(24, 0).SetItem(25, 0).SetItem(26, 0).SetItem(27, 0).SetItem(28, 0).SetItem(29, 0) };
        var actual = DrawProgrammeNode66AE20(frame, resources.MainProgramme, node, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), resources, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Same(frame, actual);
    }

    [Theory]
    [InlineData(false, 11, 2)]
    [InlineData(true, 9, 2)]
    [InlineData(true, 8, 0)]
    public void Sprite_draws_old_phase_and_moves_only_live_particle_rows(bool moving, int phase, int result)
    {
        var frame = Frame();
        uint descriptor = Pools[moving ? 3 : 4].Descriptor;
        (frame, uint address) = Append(frame, descriptor, moving ? 0x66C100u : 0x66C080u);
        var task = Read(frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 300).WriteWord(18, (ushort)phase);
        if (moving)
        {
            task = task.WriteWord(20, 5).WriteWord(22, unchecked((ushort)-6)).WriteWord(24, 7).WriteWord(28, 8).WriteWord(30, 9).WriteWord(32, unchecked((ushort)-10));
        }
        frame = Write(frame, address, task);
        int calls = 0;
        var inputs = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEffect303DrawSprite180>(operation);
            var context = Context(current, draw.TemporaryAddress, 180);
            Assert.Equal((ushort)phase, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(4, 2)));
            Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(36, 2)));
            Assert.Equal(moving ? ParticleSpriteAddress : SpriteAddress, draw.SourceAddress);
            Assert.Equal((short)phase, BinaryPrimitives.ReadInt16LittleEndian((current.Memory.ScriptedColorRegisters!.NormalInput ?? throw new InvalidOperationException()).AsSpan().Slice(6, 2)));
            calls++;
            return current;
        });
        var resources = NativeBattleEffect303StateTests.Resources();
        var step = moving ? AdvanceSprite66C100(frame, address, resources, inputs) : AdvanceSprite66C080(frame, address, resources, inputs);
        Assert.Equal(result, step.Result);
        var after = Read(step.Frame, address);
        Assert.Equal(phase + 1, after.ReadWord(18));
        Assert.Equal(moving && result == 0 ? (ushort)105 : (ushort)100, after.ReadWord(12));
        if (moving && result == 0)
        {
            Assert.Equal((ushort)13, after.ReadWord(20));
            Assert.Equal((ushort)3, after.ReadWord(22));
            Assert.Equal(unchecked((ushort)-3), after.ReadWord(24));
        }
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Child_static_phase_reads_model_rotation14_and_copies_packed_birth_fields()
    {
        var frame = Frame();
        var resources = NativeBattleEffect303StateTests.Resources();
        var model = new NativeBattleGraphicsModel(3, 0) { Rotation14 = 1024, Transform = new(new(0, 0, 0), new(100, 200, 300), 77, 2048, 4096) };
        var inputs = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEffect303DrawStaticMesh88>(operation);
            var expected = NativeBattleGfMatrices.Compose56C2F0(Identity(2000).ReadMatrix(), NativeBattlePandemona291Restoration.Rotation701270(-1024) with { X = 100, Y = 200, Z = 300 });
            Assert.Equal(expected.Rotation, current.Memory.GeometryRegisters!.Rotation20.Take(18).Select(value => value!.Value).Chunk(2).Select(bytes => BinaryPrimitives.ReadInt16LittleEndian(bytes)).ToImmutableArray());
            Assert.Equal(0u, U(Context(current, draw.TemporaryAddress, 88), 28));
            return current;
        }) with { ReadModel = (_, _) => model };
        frame = SpawnProgramme66C1C0(frame, Point(1, -2, 3, 5), MeshAddresses[0], 5000, resources, inputs);
        uint address = Pools[2].Bank;
        Assert.Equal((ushort)5000, Read(frame, address).ReadWord(22));
        var step = AdvanceProgramme66C220(frame, address, resources, inputs);
        Assert.Equal(0, step.Result);
        Assert.Equal((ushort)101, Read(step.Frame, address).ReadWord(16));
        Assert.Equal((ushort)8, Read(step.Frame, address).ReadWord(26));
        Assert.Equal((ushort)4, Read(step.Frame, address).ReadWord(30));
    }

    [Fact]
    public void Muzzle_restores_caster_then_reads_actual_current_bone_and_retains_output_W()
    {
        int order = 0;
        var inputs = Inputs((current, operation) =>
        {
            var restore = Assert.IsType<NativeBattleEffect303RestorePose508C90>(operation);
            Assert.Equal(0x1D97494u, restore.ModelAddress);
            Assert.Equal(0, order++);
            return current;
        }) with
        {
            ReadCurrentBoneMatrix = (_, model, bone) =>
            {
                Assert.Equal(1, order++);
                Assert.Equal(39, bone);
                Assert.Equal(0x1D97494u, model);
                return NativeBattlePackedMatrix32.FromMatrix(Identity().ReadMatrix() with { X = 10, Y = 20, Z = 30 }, null);
            },
            ReadModelWorld = (_, _) => NativeBattlePackedMatrix32.FromMatrix(Identity().ReadMatrix() with { X = 100, Y = 200, Z = 300 }, null)
        };
        var step = ReadMuzzle66C000(Frame(), Point(1, 2, 3, 39), Point(0, 0, 0, 0x1234), inputs);
        Assert.Equal(new short[] { 111, 222, 333, 0x1234 }, Enumerable.Range(0, 4).Select(index => W(step.Point8, index * 2)));
        Assert.Equal(2, order);
    }

    internal static NativeBattleEffect303Frame Frame()
    {
        var identity = Identity();
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity.ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = identity };
        var effect = new NativeBattleOriginalEffectState(303, 302, new(303, Entry, Initializer), null, 0) { Pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray() };
        return new(memory, effect, new(123), NativeBattleEffect303State.FromColdProcess().WriteDword(ArenaGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset).WriteDword(CasterGlobal, 0x1D97494) with { RootBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    internal static NativeBattleEffect303Inputs Inputs(Func<NativeBattleEffect303Frame, NativeBattleEffect303Operation, NativeBattleEffect303Frame> execute) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Identity(2000), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => Identity(), (_, _, _) => throw new InvalidOperationException(), _ => NativeBattleActionPacketsState.Fresh(), execute, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
    internal static NativeBattlePackedMatrix32 Identity(int z = 0) => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), null);
    internal static NativeBattleEffectArena Point(short x, short y, short z, short w) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z)).Write(6, BitConverter.GetBytes(w));
    internal static short W(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset, 2));
    internal static uint U(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(view.Read(offset, 4));
    internal static int I(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt32LittleEndian(view.Read(offset, 4));
    internal static NativeBattleGfAnimationNode Node(uint header)
    {
        byte[] data = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), header);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8 + axis * 2), (short)((axis + 1) * 10));
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, data.ToImmutableArray());
    }
}
