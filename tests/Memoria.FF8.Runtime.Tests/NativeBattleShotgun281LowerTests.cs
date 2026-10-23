using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleShotgun281Controller;
using static Memoria.FF8.Runtime.NativeBattleShotgun281Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShotgun281LowerTests
{
    [Fact]
    public void Muzzle_consumes_packed_bone_selector_and_current_composed_matrix()
    {
        var resources = NativeBattleShotgun281StateTests.Resources();
        byte[] point = resources.MuzzlePoint8.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(point, 10);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(2), 20);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(4), 30);
        resources = resources with { MuzzlePoint8 = point.ToImmutableArray() };
        var frame = Frame();
        var matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 1024) with { X = 1, Y = 2, Z = 3 };
        int calls = 0;
        var actual = UpdateMuzzle67E500(frame, resources, Inputs() with { ReadCurrentBoneMatrix = (_, model, bone) =>
        {
            Assert.Equal(0x1D97494u, model);
            Assert.Equal(23, bone);
            calls++;
            return NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        } });
        Assert.Equal(1, calls);
        Assert.Equal(31u, actual.State.ReadDword(BasisAAddress + 20));
        Assert.Equal(22u, actual.State.ReadDword(BasisAAddress + 24));
        Assert.Equal(unchecked((uint)-7), actual.State.ReadDword(BasisAAddress + 28));
        Assert.Equal((ushort)23, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.True(actual.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.Null(actual.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Negative_parent_releases_twenty_without_current_matrix_reads_or_output_writes()
    {
        var frame = SeedTemporary(Frame());
        var point = NativeBattleEffectArena.Clear().MarkUnknown(0, 6).Write(6, BitConverter.GetBytes((ushort)0xBEEF));
        var actual = ReadAnchor67E550(frame, 0x1D972C0, 0, point, Inputs() with { ReadBoneParent = (_, _, _) => -1, ReadCurrentBoneMatrix = (_, _, _) => throw new InvalidOperationException(), ReadModelWorld = (_, _) => throw new InvalidOperationException() });
        Assert.Equal(1, actual.Result);
        Assert.Equal(JsonSerializer.Serialize(point), JsonSerializer.Serialize(actual.Point8));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        Assert.Equal(Context(frame, NativeBattleTemporaryMemory.Address, 20).Read(0, 20), Context(actual.Frame, NativeBattleTemporaryMemory.Address, 20).Read(0, 20));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Anchor_uses_current_translation_and_world_FPU_preserving_W_and_scratch_suffix(bool unknownW)
    {
        var frame = SeedTemporary(Frame());
        var point = NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xBEEF));
        if (unknownW)
        {
            point = point.MarkUnknown(6, 2);
        }
        int aliases = 0;
        var inputs = Inputs() with
        {
            ReadBoneAlias = (_, model, selector) =>
            {
                Assert.Equal(0x1D972C0u, model);
                Assert.Equal(0xF0, selector);
                aliases++;
                return 2;
            },
            ReadBoneParent = (_, _, bone) =>
            {
                Assert.Equal(2, bone);
                return 1;
            },
            ReadCurrentBoneMatrix = (_, _, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 11, Y = -12, Z = 13 }, null),
            ReadModelWorld = (_, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 }, null)
        };
        var actual = ReadAnchor67E550(frame, 0x1D972C0, 0xF0, point, inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal(1, aliases);
        Assert.Equal(new short[] { 111, 188, 313 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(actual.Point8.Read(axis * 2, 2))));
        Assert.Equal(!unknownW, actual.Point8.ReadSnapshot(6, 2).Known[0]);
        if (!unknownW)
        {
            Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(actual.Point8.Read(6, 2)));
        }
        var temporary = Context(actual.Frame, NativeBattleTemporaryMemory.Address, 20);
        Assert.Equal(new int[] { 11, -12, 13 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt32LittleEndian(temporary.Read(axis * 4, 4))));
        Assert.Equal(Enumerable.Repeat((byte)0xAA, 8), temporary.Read(12, 8));
        Assert.Equal(JsonSerializer.Serialize(frame.Memory.GeometryRegisters), JsonSerializer.Serialize(actual.Frame.Memory.GeometryRegisters));
        Assert.Equal(JsonSerializer.Serialize(frame.Memory.ScriptedColorRegisters), JsonSerializer.Serialize(actual.Frame.Memory.ScriptedColorRegisters));
    }

    [Theory]
    [InlineData(0u, false, false)]
    [InlineData(0x200u, true, false)]
    [InlineData(0x8000u, false, true)]
    [InlineData(0x8200u, true, true)]
    public void Header_position_and_rotation_branches_are_independent(uint header, bool direct, bool noCompose)
    {
        var resources = NativeBattleShotgun281StateTests.Resources();
        var frame = Frame();
        NativeBattleActionMatrix callerMatrix = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 1024) with { X = 1000, Y = 2000, Z = 3000 };
        var caller = NativeBattlePackedMatrix32.FromMatrix(callerMatrix, null);
        var node = Node(header, new(100, 200, 300));
        bool called = false;
        var actual = DrawProgrammeNode281(frame, resources.ProgrammeA, node, caller, -128, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleShotgun281DrawMesh7043B0>(operation);
            var loaded = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(current.Memory.GeometryRegisters!).ReadMatrix();
            Assert.Equal(direct ? 1100 : 1300, loaded.X);
            Assert.Equal(2200, loaded.Y);
            Assert.Equal(direct ? 3300 : 2900, loaded.Z);
            Assert.Equal(direct || noCompose ? NativeBattleCuragaMatrices.Identity8DD770().Rotation : callerMatrix.Rotation, loaded.Rotation);
            Assert.Equal(0x2030u, U(Context(current, draw.TemporaryAddress, 88), 28));
            Assert.Equal(unchecked((uint)-128), U(Context(current, draw.TemporaryAddress, 88), 24));
            called = true;
            return current;
        }));
        Assert.True(called);
        Assert.Equal(frame.Random, actual.Random);
        if (direct)
        {
            Assert.Equal(JsonSerializer.Serialize(frame.Memory.ScriptedColorRegisters), JsonSerializer.Serialize(actual.Memory.ScriptedColorRegisters));
        }
        else
        {
            Assert.False(actual.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4096, 1)]
    [InlineData(1024, -1)]
    public void Shape_endpoints_and_morph_keep_physical_scratch_W(int ratio, int selected)
    {
        var resources = TwoShapes();
        var frame = Frame();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(ScratchOffset, BitConverter.GetBytes((ushort)0x1234)).Write(ScratchOffset + 6, BitConverter.GetBytes((ushort)0xBEEF)) } };
        uint seen = 0;
        var actual = DrawProgrammeNode281(frame, resources.ProgrammeA, Node(0, default, ratio), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), -128, resources, Inputs((current, operation) =>
        {
            seen = Assert.IsType<NativeBattleShotgun281DrawMesh7043B0>(operation).VertexAddress;
            return current;
        }));
        Assert.Equal(selected < 0 ? ArenaAddress + ScratchOffset : ProgrammeAAddress + 76u + (uint)(selected * 8), seen);
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.Arena!.Read(ScratchOffset + 6, 2)));
        Assert.Equal(selected < 0 ? (ushort)150 : (ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.Arena.Read(ScratchOffset, 2)));
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(7, 10, 0)]
    public void Sprite_draw_uses_phase_W_then_old_velocity_and_signed_terminal(ushort phase, ushort lifetime, int expected)
    {
        var frame = Frame();
        var pool = frame.Effect.Pools[1];
        (pool, _) = pool.Append(0x67E5F0);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        var task = Read(frame, ArenaAddress).WriteDword(12, SpriteAAddress).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x16, phase).WriteWord(0x18, 10).WriteWord(0x1A, unchecked((ushort)-20)).WriteWord(0x1C, 30).WriteWord(0x1E, lifetime).WriteWord(0x20, unchecked((ushort)-1)).WriteWord(0x22, 2).WriteWord(0x24, unchecked((ushort)-3));
        frame = Write(frame, ArenaAddress, task);
        bool called = false;
        var actual = AdvanceSprite67E5F0(frame, ArenaAddress, NativeBattleShotgun281StateTests.Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleShotgun281DrawSprite180>(operation);
            Assert.Equal(phase, Read(current, ArenaAddress).ReadWord(0x16));
            Assert.Equal(phase, BinaryPrimitives.ReadUInt16LittleEndian(current.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
            Assert.Equal(phase, BinaryPrimitives.ReadUInt16LittleEndian(Context(current, draw.TemporaryAddress, 180).Read(4, 2)));
            called = true;
            return current;
        }));
        Assert.True(called);
        Assert.Equal(expected, actual.Result);
        var next = Read(actual.Frame, ArenaAddress);
        Assert.Equal(new ushort[] { 110, 180, 330 }, new[] { next.ReadWord(0x10), next.ReadWord(0x12), next.ReadWord(0x14) });
        Assert.Equal(new short[] { 9, -18, 27 }, new[] { Signed(next, 0x18), Signed(next, 0x1A), Signed(next, 0x1C) });
        Assert.Equal((ushort)(phase + 1), next.ReadWord(0x16));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Inactive_secondary_programme_publishes_basis_translation_and_returns_two()
    {
        var resources = NativeBattleShotgun281StateTests.Resources();
        byte[] bytes = resources.ProgrammeB.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x100), 0);
        resources = resources with { ProgrammeB = resources.ProgrammeB with { Data = bytes.ToImmutableArray() } };
        var frame = Frame();
        var pool = frame.Effect.Pools[2];
        (pool, _) = pool.Append(0x67E6B0);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, pool) } };
        uint address = ArenaAddress + 0x1400;
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[312].ToImmutableArray()).Initialize7016B0(resources.ProgrammeB, 312);
        byte[] cache = workspace.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(cache.AsSpan(4), 31);
        workspace = workspace with { Data = cache.ToImmutableArray() };
        var task = CopyTask(Read(frame, address).WriteWord(12, 100).WriteWord(14, unchecked((ushort)-200)).WriteWord(16, 300), 20, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), 312);
        frame = Write(frame, address, task);
        var actual = AdvanceProgramme67E6B0(frame, address, resources, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Equal(2, actual.Result);
        Assert.Equal(32u, Read(actual.Frame, address).ReadDword(24));
        Assert.Equal(100u, actual.Frame.State.ReadDword(BasisBAddress + 20));
        Assert.Equal(unchecked((uint)-200), actual.Frame.State.ReadDword(BasisBAddress + 24));
        Assert.Equal(300u, actual.Frame.State.ReadDword(BasisBAddress + 28));
        Assert.Equal(frame.Memory.ScriptedColorRegisters, actual.Frame.Memory.ScriptedColorRegisters);
    }

    private static NativeBattleShotgun281Resources TwoShapes()
    {
        var resources = NativeBattleShotgun281StateTests.Resources();
        byte[] bytes = resources.ProgrammeA.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(64), 28);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(72), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(76), 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(84), 300);
        return resources with { ProgrammeA = resources.ProgrammeA with { Data = bytes.ToImmutableArray() } };
    }
    private static NativeBattleGfAnimationNode Node(uint header, NativeBattleVector position, int ratio = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), (short)position.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), (short)position.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), (short)position.Z);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), (short)ratio);
        if (ratio != 0)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleShotgun281Frame SeedTemporary(NativeBattleShotgun281Frame frame) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, NativeBattleTemporaryMemory.Address, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xAA, 20).ToArray()), 20) };
    private static NativeBattleShotgun281Frame Frame()
    {
        var pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        var effect = new NativeBattleOriginalEffectState(281, 280, new(281, Entry, Initializer), null, 0) { Pools = pools };
        var identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity.ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = identity };
        var state = NativeBattleShotgun281State.FromColdProcess().WriteDword(CasterGlobal, 0x1D97494);
        state = state.WriteView(BasisAAddress, PackedArena(identity), 32).WriteView(BasisBAddress, PackedArena(identity), 32);
        return new(memory, effect, new(123), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleShotgun281Inputs Inputs(Func<NativeBattleShotgun281Frame, NativeBattleShotgun281Operation, NativeBattleShotgun281Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => 24, (_, _, _) => 0, (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => NativeBattleActionPacketsState.Fresh(), execute ?? ((current, _) => current));
}
