using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMiniPulseCannon286Controller;
using static Memoria.FF8.Runtime.NativeBattleMiniPulseCannon286Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMiniPulseCannon286LowerTests
{
    [Theory]
    [InlineData(0u, 1100, 2300, 2800, true)]
    [InlineData(0x200u, 1100, 2200, 3300, false)]
    [InlineData(0x8000u, 1100, 2300, 2800, false)]
    [InlineData(0x8200u, 1100, 2200, 3300, false)]
    public void Node_headers_preserve_caller_translation_and_literal_rotation_selection(uint header, int x, int y, int z, bool parentRotation)
    {
        var matrix = NativeBattleGfMatrices.Euler701310(new(1024, 0, 0)) with { X = 1000, Y = 2000, Z = 3000 };
        var caller = Caller(matrix, 0);
        var resources = NativeBattleMiniPulseCannon286StateTests.Resources();
        int draws = 0;
        var frame = Frame();
        var actual = DrawProgrammeNode67A4E0(frame, resources.ProgrammeA, Node(header), caller, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleMiniPulseCannon286DrawWindow108>(operation);
            var loaded = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(current.Memory.GeometryRegisters!).ReadMatrix();
            Assert.Equal(new[] { x, y, z }, new[] { loaded.X, loaded.Y, loaded.Z });
            Assert.Equal(parentRotation ? matrix.Rotation : NativeBattleCuragaMatrices.Identity8DD770().Rotation, loaded.Rotation);
            var context = Context(current, draw.TemporaryAddress, 108);
            Assert.Equal(0x2030u, U(context, 28));
            Assert.Equal(unchecked((uint)-32), U(context, 24));
            Assert.Equal(new byte[4], context.Read(32, 4));
            Assert.Throws<NotSupportedException>(() => context.Read(36, 16));
            draws++;
            return current;
        }));
        Assert.Equal(1, draws);
        Assert.Equal(frame.Random, actual.Random);
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 128)]
    [InlineData(2, -1)]
    public void Window_applies_only_to_first_two_geometries_and_retains_unused_rectangle(int geometry, int afterY)
    {
        var resources = NativeBattleMiniPulseCannon286StateTests.Resources();
        byte[] node = Node(0x200).Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(node.AsSpan(2), (ushort)geometry);
        var actual = DrawProgrammeNode67A4E0(Frame(), resources.ProgrammeA, new(0, 0, node.ToImmutableArray()), Caller(NativeBattleCuragaMatrices.Identity8DD770(), 96), resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleMiniPulseCannon286DrawWindow108>(operation);
            var context = Context(current, draw.TemporaryAddress, 108);
            Assert.Equal((ushort)0, W(context, 32));
            Assert.Equal((ushort)(geometry <= 1 ? 96 : 0), W(context, 34));
            if (geometry <= 1)
            {
                Assert.Equal(new ushort[] { 0, 0, 256, 256, 256, (ushort)afterY, 256, 128 }, Enumerable.Range(0, 8).Select(index => W(context, 36 + index * 2)));
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => context.Read(36, 16));
            }
            return current;
        }));
        Assert.NotNull(actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invisible_node_rejects_before_any_unwritten_caller_or_temporary_reads(bool fade)
    {
        byte[] bytes = Node(0x200).Data.ToArray();
        if (fade)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 4096);
        }
        else
        {
            Array.Clear(bytes, 24, 6);
        }
        var frame = Frame();
        var actual = DrawProgrammeNode67A4E0(frame, NativeBattleMiniPulseCannon286StateTests.Resources().ProgrammeC, new(0, 0, bytes.ToImmutableArray()), NativeBattleEffectArena.Clear().MarkUnknown(0, 40), NativeBattleMiniPulseCannon286StateTests.Resources(), Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Same(frame, actual);
    }

    [Theory]
    [InlineData(0, 100, false)]
    [InlineData(1024, 150, true)]
    [InlineData(4096, 300, false)]
    public void Shape_endpoints_use_original_rows_and_intermediate_morph_retains_scratch_W(int ratio, int expected, bool morphed)
    {
        var resources = NativeBattleMiniPulseCannon286StateTests.Resources();
        byte[] data = resources.ProgrammeA.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(64), 28);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(72), 2);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(76), 100);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(84), 300);
        resources = resources with { ProgrammeA = resources.ProgrammeA with { Data = data.ToImmutableArray() } };
        byte[] node = Node(0x200).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), (short)ratio);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        var frame = Frame();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(ScratchOffset, BitConverter.GetBytes((short)-777)).Write(ScratchOffset + 6, BitConverter.GetBytes((ushort)0xBEEF)) } };
        uint selected = 0;
        var actual = DrawProgrammeNode67A4E0(frame, resources.ProgrammeA, new(0, 0, node.ToImmutableArray()), Caller(NativeBattleCuragaMatrices.Identity8DD770(), 0), resources, Inputs((current, operation) =>
        {
            selected = Assert.IsType<NativeBattleMiniPulseCannon286DrawWindow108>(operation).VertexAddress;
            return current;
        }));
        Assert.Equal(morphed ? ArenaAddress + ScratchOffset : ProgrammeAAddress + (uint)(ratio == 4096 ? 84 : 76), selected);
        Assert.Equal((short)(morphed ? expected : -777), BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.Arena!.Read(ScratchOffset, 2)));
        Assert.Equal((ushort)0xBEEF, W(actual.Memory.Arena, ScratchOffset + 6));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Muzzle_loads_actual_current_bone_two_without_composing_world_again()
    {
        var resources = NativeBattleMiniPulseCannon286StateTests.Resources();
        byte[] point = resources.SourcePoint8.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(point, 10);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(2), 20);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(4), 30);
        resources = resources with { SourcePoint8 = point.ToImmutableArray() };
        var matrix = NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)) with { X = 40000, Y = -50000, Z = 60000 };
        int reads = 0;
        var frame = Frame();
        var actual = UpdateMuzzle67A770(frame, resources, Inputs() with { ReadCurrentBoneMatrix = (_, model, bone) =>
        {
            Assert.Equal(0x1D97530u, model);
            Assert.Equal(2, bone);
            reads++;
            return NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        } });
        Assert.Equal(1, reads);
        Assert.Equal(new[] { 40030, -49980, 59990 }, new[] { actual.X, actual.Y, actual.Z });
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(actual.Frame.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.Null(actual.Frame.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0, 1200, 10, 0, -300)]
    [InlineData(9, 1200, 10, 2, -300)]
    [InlineData(0, -3, -1, 2, 1)]
    public void Sprite_uses_old_phase_and_full_size_SAR_before_low_WORD_velocity_and_signed_retirement(int phase, int size, int terminal, int result, int expectedDepth)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2507CB8, 0x67A7C0);
        frame = Write(frame, address, Read(frame, address).WriteDword(12, SpriteAddress).WriteDword(16, unchecked((uint)size)).WriteWord(20, 32760).WriteWord(22, 200).WriteWord(24, 300).WriteWord(26, (ushort)phase).WriteWord(28, 20).WriteWord(30, unchecked((ushort)-10)).WriteWord(32, 3).WriteWord(34, unchecked((ushort)terminal)));
        int draws = 0;
        var actual = AdvanceSprite67A7C0(frame, address, NativeBattleMiniPulseCannon286StateTests.Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleMiniPulseCannon286DrawSprite180>(operation);
            Assert.Equal(SpriteAddress, draw.SourceAddress);
            var billboard = current.Memory.ModuleSpriteBillboard21DFED0!.ReadMatrix();
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(32760, 200, 300);
            Assert.Equal(unchecked((short)size), billboard.Rotation[0]);
            Assert.Equal(new[] { 32760 + (direction.X * expectedDepth >> 12), 200 + (direction.Y * expectedDepth >> 12), 300 + (direction.Z * expectedDepth >> 12) }, new[] { billboard.X, billboard.Y, billboard.Z });
            Assert.Equal((ushort)phase, W(Context(current, draw.TemporaryAddress, 180), 4));
            Assert.Equal((ushort)phase, BinaryPrimitives.ReadUInt16LittleEndian(current.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
            draws++;
            return current;
        }));
        Assert.Equal(1, draws);
        Assert.Equal(result, actual.Result);
        var task = Read(actual.Frame, address);
        Assert.Equal(new ushort[] { unchecked((ushort)32780), 190, 303, (ushort)(phase + 1) }, new[] { task.ReadWord(20), task.ReadWord(22), task.ReadWord(24), task.ReadWord(26) });
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    private static NativeBattleGfAnimationNode Node(uint header)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 200);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 300);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleEffectArena Caller(NativeBattleActionMatrix matrix, ushort window) => PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(32, BitConverter.GetBytes((short)-32)).Write(34, BitConverter.GetBytes(window)).Write(36, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort W(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattlePackedMatrix32 Identity() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static NativeBattleMiniPulseCannon286Frame Frame()
    {
        var pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        var effect = new NativeBattleOriginalEffectState(286, 285, new(286, Entry, Initializer), null, 0) { Pools = pools };
        var identity = Identity();
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity.ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = identity };
        return new(memory, effect, new(123), NativeBattleMiniPulseCannon286State.FromColdProcess().WriteDword(CasterGlobal, 0x1D97530)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleMiniPulseCannon286Inputs Inputs(Func<NativeBattleMiniPulseCannon286Frame, NativeBattleMiniPulseCannon286Operation, NativeBattleMiniPulseCannon286Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => Identity(), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => NativeBattleActionPacketsState.Fresh(), execute ?? ((current, _) => current));
}
