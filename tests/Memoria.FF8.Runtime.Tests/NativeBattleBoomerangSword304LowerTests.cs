using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBoomerangSword304Controller;
using static Memoria.FF8.Runtime.NativeBattleBoomerangSword304Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBoomerangSword304LowerTests
{
    [Theory]
    [InlineData(0u, 1110, 2330, 2780, true, false)]
    [InlineData(0x8000u, 1110, 2330, 2780, true, false)]
    [InlineData(0x200u, 1110, 2320, 2830, false, true)]
    [InlineData(0x8200u, 1110, 2320, 2830, false, true)]
    public void Header200_changes_anchor_space_and_W_while_8000_never_suppresses_parent_rotation(uint header, int x, int y, int z, bool parent, bool knownW)
    {
        var resources = Resources();
        var view = NativeBattleGfMatrices.Euler701310(new(1024, 0, 0)) with { X = 1000, Y = 2000, Z = 3000 };
        var node = Node(header, new(10, 20, 30));
        var actual = DrawProgrammeNode66A440(Frame(), resources.Programme, node, Caller(new(100, 200, 300), 0xBEEF), resources, Inputs() with { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(view, null) });
        var matrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(actual.Memory.GeometryRegisters!).ReadMatrix();
        Assert.Equal(new[] { x, y, z }, new[] { matrix.X, matrix.Y, matrix.Z });
        Assert.Equal(parent ? view.Rotation : NativeBattleCuragaMatrices.Identity8DD770().Rotation, matrix.Rotation);
        Assert.Equal(knownW, actual.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        if (knownW)
        {
            Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6)));
        }
    }

    [Theory]
    [InlineData(0u, -32756)]
    [InlineData(0x200u, 32780)]
    public void Only_clear200_wraps_anchor_plus_node_before_view_rotation(uint header, int expected)
    {
        var resources = Resources();
        var actual = DrawProgrammeNode66A440(Frame(), resources.Programme, Node(header, new(20, 0, 0)), Caller(new(32760, 0, 0), 0), resources, Inputs());
        Assert.Equal(expected, NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(actual.Memory.GeometryRegisters!).ReadMatrix().X);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Alternate_Euler_selector_preserves_the_original_rotation_order(bool alternate)
    {
        var resources = Resources();
        var node = Node(alternate ? 0x40200u : 0x200u, new(10, 20, 30), new(512, 256, 128));
        var actual = DrawProgrammeNode66A440(Frame(), resources.Programme, node, Caller(new(100, 200, 300), 0), resources, Inputs());
        var expected = alternate ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        Assert.Equal(expected.Rotation, NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(actual.Memory.GeometryRegisters!).ReadMatrix().Rotation);
    }

    [Theory]
    [InlineData(false, 1448)]
    [InlineData(true, 1449)]
    public void Header100_rounds_scale_but_keeps_view_anchor_translation(bool rounded, int expected)
    {
        var resources = Resources();
        var node = Node(rounded ? 0x300u : 0x200u, new(10, 20, 30), new(0, 0, 512), new(2049, 4096, 4096));
        var actual = DrawProgrammeNode66A440(Frame(), resources.Programme, node, Caller(new(100, 200, 300), 0), resources, Inputs());
        var matrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(actual.Memory.GeometryRegisters!).ReadMatrix();
        Assert.Equal((short)expected, matrix.Rotation[0]);
        Assert.Equal((short)-1449, matrix.Rotation[3]);
        Assert.Equal(new[] { 110, 220, 330 }, new[] { matrix.X, matrix.Y, matrix.Z });
        Assert.Null(actual.Memory.GeometryRegisters!.Rotation20[18]);
    }

    [Theory]
    [InlineData(0, 100, false)]
    [InlineData(1024, 150, true)]
    [InlineData(4096, 300, false)]
    public void Morph_uses_caller_offset_twelve_and_preserves_current_scratch_W(int ratio, int expected, bool morph)
    {
        var resources = Resources();
        byte[] source = resources.Programme.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(64), 28);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(72), 2);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(76), 100);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(84), 300);
        resources = resources with { Programme = resources.Programme with { Data = source.ToImmutableArray() } };
        byte[] data = Node(0x200, new(0, 0, 0)).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), (short)ratio);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
        var frame = Frame();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(ScratchOffset, BitConverter.GetBytes((short)-777)).Write(ScratchOffset + 6, BitConverter.GetBytes((ushort)0xBEEF)) } };
        uint vertices = 0;
        var actual = DrawProgrammeNode66A440(frame, resources.Programme, new(0, 0, data.ToImmutableArray()), Caller(new(0, 0, 0), 0), resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBoomerangSword304DrawMesh7043B0>(operation);
            vertices = draw.VertexAddress;
            Assert.Equal(unchecked((uint)-128), U(Context(current, draw.TemporaryAddress, 88), 24));
            return current;
        }));
        Assert.Equal(morph ? ArenaAddress + ScratchOffset : ProgrammeAddress + (uint)(ratio == 4096 ? 84 : 76), vertices);
        Assert.Equal((short)(morph ? expected : -777), BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.Arena!.Read(ScratchOffset, 2)));
        Assert.Equal((ushort)0xBEEF, W(actual.Memory.Arena!, ScratchOffset + 6));
    }

    [Fact]
    public void Invisible_node_does_not_read_unwritten_caller_or_view()
    {
        byte[] data = Node(0, new(0, 0, 0)).Data.ToArray();
        Array.Clear(data, 24, 6);
        var frame = Frame();
        var resources = Resources();
        var actual = DrawProgrammeNode66A440(frame, resources.Programme, new(0, 0, data.ToImmutableArray()), NativeBattleEffectArena.Clear().MarkUnknown(0, 16), resources, Inputs() with { ReadView = _ => throw new InvalidOperationException() });
        Assert.Same(frame, actual);
    }

    [Theory]
    [InlineData(12, 0)]
    [InlineData(13, 2)]
    public void Programme_callback_returns_only_its_fourteen_frame_remaining_counter(int oldFrame, int result)
    {
        var resources = Resources();
        byte[] source = resources.Programme.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(0x100), 0);
        resources = resources with { Programme = resources.Programme with { Data = source.ToImmutableArray() } };
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2507590, 0x66A3F0);
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[460].ToImmutableArray()).Initialize7016B0(resources.Programme, 460);
        byte[] cache = NativeBattleStorageSnapshots.Capture(workspace).Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(cache.AsSpan(4), oldFrame);
        var task = Read(frame, address).WriteDword(12, 0x1D972C0);
        task = CopyTask(task, 16, PointView(new(100, 200, 300), 0x1234), 8);
        task = CopyTask(task, 24, NativeBattleEffectArena.Clear().Write(0, cache), 460);
        frame = Write(frame, address, task);
        var actual = AdvanceProgramme66A3F0(frame, address, resources, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Equal(result, actual.Result);
        Assert.Equal((uint)(oldFrame + 1), Read(actual.Frame, address).ReadDword(28));
        Assert.Equal(frame.Memory.GeometryRegisters, actual.Frame.Memory.GeometryRegisters);
    }

    [Theory]
    [InlineData(0, true, 0)]
    [InlineData(8, true, 0)]
    [InlineData(9, false, 2)]
    public void Particle_terminal_ten_precedes_motion_and_all_survivors_accelerate(int phase, bool move, int result)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2507580, 0x66A330);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 300).WriteWord(18, (ushort)phase).WriteWord(20, 10).WriteWord(22, unchecked((ushort)-20)).WriteWord(24, 30).WriteWord(28, unchecked((ushort)-1)).WriteWord(30, 2).WriteWord(32, unchecked((ushort)-3)));
        int calls = 0;
        var actual = AdvanceParticle66A330(frame, address, Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBoomerangSword304DrawSprite180>(operation);
            Assert.Equal(SpriteAddress, draw.SourceAddress);
            Assert.Equal((ushort)phase, W(Context(current, draw.TemporaryAddress, 180), 4));
            Assert.Equal((ushort)phase, BinaryPrimitives.ReadUInt16LittleEndian(current.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
            calls++;
            return current;
        }));
        var task = Read(actual.Frame, address);
        Assert.Equal(1, calls);
        Assert.Equal(result, actual.Result);
        Assert.Equal(move ? new short[] { 110, 180, 330 } : new short[] { 100, 200, 300 }, new[] { Signed(task, 12), Signed(task, 14), Signed(task, 16) });
        Assert.Equal(move ? new short[] { 9, -18, 27 } : new short[] { 10, -20, 30 }, new[] { Signed(task, 20), Signed(task, 22), Signed(task, 24) });
        Assert.Equal((ushort)(phase + 1), task.ReadWord(18));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0, 4096, 0, 255, 0)]
    [InlineData(0, 8192, 0, 255, 0)]
    [InlineData(0, 0, 4096, 0, 0)]
    [InlineData(0, 2048, 2048, 128, 0)]
    [InlineData(0, -1024, 5120, 192, 0)]
    [InlineData(7, 4096, -1, -1, 2)]
    public void Static_trail_blend_uses_sine_and_only_upper_clamps_blue_before_age_eight(int age, int blend, int expectedCue, int expectedBlue, int result)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x25075A0, 0x66A730);
        var task = Read(frame, address).WriteWord(12, (ushort)age).WriteWord(14, unchecked((ushort)blend)).WriteDword(36, 100).WriteDword(40, 200).WriteDword(44, 300);
        var rotation = NativeBattleGfMatrices.Euler701310(new(0, 1024, 0));
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(16 + index * 2, unchecked((ushort)rotation.Rotation[index]));
        }
        frame = Write(frame, address, task);
        if (expectedCue < 0)
        {
            expectedCue = NativeBattleCameraMath.Sine(age * 128);
            expectedBlue = Math.Min(255, (4096 - expectedCue) >> 4);
        }
        int calls = 0;
        var actual = AdvanceMesh66A730(frame, address, Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBoomerangSword304DrawStatic88>(operation);
            Assert.Equal(TrailMeshAddress, draw.SourceAddress);
            var context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(unchecked((uint)expectedCue), U(context, 12));
            Assert.Equal(new byte[] { 0, 0, unchecked((byte)expectedBlue) }, context.Read(8, 3));
            Assert.False(context.ReadSnapshot(11, 1).Known[0]);
            Assert.Throws<NotSupportedException>(() => context.Read(24, 4));
            Assert.Equal(0xC0u, U(context, 28));
            var matrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(current.Memory.GeometryRegisters!).ReadMatrix();
            Assert.Equal(rotation.Rotation, matrix.Rotation);
            Assert.Equal(new[] { 100, 200, 300 }, new[] { matrix.X, matrix.Y, matrix.Z });
            calls++;
            return current;
        }));
        Assert.Equal(1, calls);
        Assert.Equal(result, actual.Result);
        Assert.Equal((ushort)(age + 1), Read(actual.Frame, address).ReadWord(12));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Group21_trail_and_particle_operations_keep_actual_task_generation_and_native_state(bool particle)
    {
        (NativeBattleBoomerangSword304Frame Frame, List<NativeBattleBoomerangSword304Operation> Operations) Run(bool capture)
        {
            var frame = Frame();
            (frame, uint address) = Append(frame, particle ? 0x2507580u : 0x25075A0u, particle ? 0x66A330u : 0x66A730u);
            var task = Read(frame, address).WriteWord(12, 0).WriteWord(14, 0).WriteWord(18, 0);
            if (!particle)
            {
                for (int index = 0; index < 9; index++)
                {
                    task = task.WriteWord(16 + index * 2, unchecked((ushort)NativeBattleCuragaMatrices.Identity8DD770().Rotation[index]));
                }
                task = task.WriteDword(36, 100).WriteDword(40, 200).WriteDword(44, 300);
            }
            frame = Write(frame, address, task);
            List<NativeBattleBoomerangSword304Operation> operations = [];
            var inputs = Inputs((current, operation) =>
            {
                operations.Add(operation);
                return current;
            }) with { CapturePresentation = capture };
            var result = particle ? AdvanceParticle66A330(frame, address, Resources(), inputs) : AdvanceMesh66A730(frame, address, Resources(), inputs);
            return (result.Frame, operations);
        }
        var native = Run(false);
        string expected = NativeBattleTestSnapshots.Json(native.Frame);
        var captured = Run(true);
        Assert.Equal(expected, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Operations), NativeBattleTestSnapshots.Json(captured.Operations));
        var identity = Assert.Single(captured.Operations).PilotIdentity!.Value;
        Assert.Equal((ushort)304, identity.Effect);
        Assert.Equal(particle ? 0x2507580u : 0x25075A0u, identity.Bank);
        Assert.Equal(particle ? SpriteAddress : TrailMeshAddress, identity.ResourceGeneration);
        Assert.True(identity.Generation > 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void Group21_ACT_owner_survives_real_selected_and_morph_scratch_discrimination(int ratio)
    {
        (NativeBattleBoomerangSword304Frame Frame, NativeBattleBoomerangSword304DrawMesh7043B0 Draw) Run(bool capture)
        {
            var resources = Resources();
            byte[] source = resources.Programme.Data.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(64), 28);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(72), 2);
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(76), 100);
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(84), 300);
            resources = resources with { Programme = resources.Programme with { Data = source.ToImmutableArray() } };
            byte[] data = Node(0x200, new(0, 0, 0)).Data.ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), (short)ratio);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
            var frame = Frame();
            (frame, uint taskAddress) = Append(frame, 0x2507590, 0x66A3F0);
            frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(ScratchOffset, BitConverter.GetBytes((short)-777)).Write(ScratchOffset + 6, BitConverter.GetBytes((ushort)0xBEEF)) } };
            NativeBattleBoomerangSword304DrawMesh7043B0? draw = null;
            var inputs = Inputs((current, operation) =>
            {
                draw = Assert.IsType<NativeBattleBoomerangSword304DrawMesh7043B0>(operation);
                return current;
            });
            var owner = capture ? NativeBattlePilotVisualLifetimes.Identity(304, Read(frame, taskAddress)) with { ResourceGeneration = ((long)resources.Programme.Address << 32) | 24 } : (NativeBattlePilotVisualIdentity?)null;
            frame = DrawProgrammeNode66A440(frame, resources.Programme, new(0, 0, data.ToImmutableArray()), Caller(new(0, 0, 0), 0), resources, inputs, pilotIdentity: owner);
            return (frame, Assert.IsType<NativeBattleBoomerangSword304DrawMesh7043B0>(draw));
        }
        var native = Run(false);
        string before = NativeBattleTestSnapshots.Json(native.Frame);
        var captured = Run(true);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Draw), NativeBattleTestSnapshots.Json(captured.Draw));
        Assert.Equal(ratio == 2048 ? ArenaAddress + ScratchOffset : ProgrammeAddress + (uint)(ratio == 4096 ? 84 : 76), captured.Draw.VertexAddress);
        Assert.Equal((short)(ratio == 2048 ? 200 : -777), BinaryPrimitives.ReadInt16LittleEndian(captured.Frame.Memory.Arena!.Read(ScratchOffset, 2)));
        Assert.Equal((ushort)0xBEEF, W(captured.Frame.Memory.Arena!, ScratchOffset + 6));
        Assert.Equal(0x2507590u, captured.Draw.PilotIdentity!.Value.Bank);
        Assert.Null(native.Draw.PilotIdentity);
    }

    private static NativeBattleGfAnimationNode Node(uint header, NativeBattleVector position, NativeBattleVector? rotation = null, NativeBattleVector? scale = null)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        NativeBattleVector[] vectors = [position, rotation ?? new(0, 0, 0), scale ?? new(4096, 4096, 4096)];
        for (int vector = 0; vector < 3; vector++)
        {
            short[] axes = [vectors[vector].X, vectors[vector].Y, vectors[vector].Z];
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vector * 8 + axis * 2), axes[axis]);
            }
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleEffectArena Caller(NativeBattleVector anchor, ushort w) => PointView(anchor, unchecked((short)w)).Write(8, BitConverter.GetBytes(-128)).Write(12, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
    private static NativeBattleBoomerangSword304Resources Resources() => NativeBattleBoomerangSword304StateTests.Resources();
    private static NativeBattlePackedMatrix32 Identity() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static NativeBattleBoomerangSword304Frame Frame()
    {
        var identity = Identity();
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity.ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = identity };
        var effect = new NativeBattleOriginalEffectState(304, 303, new(304, Entry, Initializer), null, 0) { Pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray() };
        return new(memory, effect, new(123), NativeBattleBoomerangSword304State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static ushort W(NativeBattleEffectArena point, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(point.Read(offset, 2));
    private static uint U(NativeBattleEffectArena point, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(point.Read(offset, 4));
    private static NativeBattleBoomerangSword304Inputs Inputs(Func<NativeBattleBoomerangSword304Frame, NativeBattleBoomerangSword304Operation, NativeBattleBoomerangSword304Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => Identity(), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => NativeBattleActionPacketsState.Fresh(), execute ?? ((current, _) => current));
}
