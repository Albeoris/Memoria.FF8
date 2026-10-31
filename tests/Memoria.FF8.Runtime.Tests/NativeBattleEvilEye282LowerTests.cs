using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEvilEye282Controller;
using static Memoria.FF8.Runtime.NativeBattleEvilEye282Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEvilEye282LowerTests
{
    [Fact]
    public void Captured282_sprite_forwards_actual_physical_task_birth_and_resource_before_temporary_release()
    {
        NativeBattleEvilEye282Frame Build(out uint address)
        {
            var frame = Frame();
            (frame, address) = Append(frame, 0x2507EA0, 0x67DC60);
            return Write(frame, address, Read(frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 300).WriteWord(18, 0));
        }
        var nativeFrame = Build(out uint nativeAddress);
        var capturedFrame = Build(out uint capturedAddress);
        NativeBattlePilotVisualIdentity? owner = null;
        var native = AdvanceSprite67DC60(nativeFrame, nativeAddress, NativeBattleEvilEye282StateTests.Resources(), Inputs());
        var captured = AdvanceSprite67DC60(capturedFrame, capturedAddress, NativeBattleEvilEye282StateTests.Resources(), Inputs((current, operation) =>
        {
            owner = Assert.IsType<NativeBattleEvilEye282DrawSprite180>(operation).PilotIdentity;
            Assert.NotNull(owner);
            return current;
        }) with { CapturePresentation = true });
        Assert.Equal(native.Result, captured.Result);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(native.Frame)), System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Frame)));
        Assert.Equal((ushort)282, owner!.Value.Effect);
        Assert.Equal(0x2507EA0u, owner.Value.Bank);
        Assert.Equal(NativeBattleSelectedProgrammeVisuals.Resource(NativeBattlePilotVisualLifetimes.Identity(282, Read(capturedFrame, capturedAddress)), SpriteAddress).ResourceGeneration, owner.Value.ResourceGeneration);
    }

    [Theory]
    [InlineData(0u, 1100, 2300, 2800, 1)]
    [InlineData(0x200u, 1100, 2200, 3300, 0)]
    [InlineData(0x8000u, 1100, 2300, 2800, 0)]
    [InlineData(0x8200u, 1100, 2200, 3300, 0)]
    [InlineData(0x1000u, 1300, 2200, 2900, 2)]
    [InlineData(0x1200u, 1300, 2200, 2900, 2)]
    [InlineData(0x9000u, 1300, 2200, 2900, 0)]
    [InlineData(0x9200u, 1300, 2200, 2900, 0)]
    public void Header_precedence_uses_normalized_rotation_but_always_caller_translation(uint header, int x, int y, int z, int rotation)
    {
        var resources = NativeBattleEvilEye282StateTests.Resources();
        var caller = NativeBattleGfMatrices.Euler701310(new(1024, 0, 0)) with { X = 1000, Y = 2000, Z = 3000 };
        var normalized = NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)) with { X = 9000, Y = 8000, Z = 7000 };
        var frame = Frame();
        frame = frame with { State = frame.State.WriteView(NormalizedViewAddress, PackedArena(NativeBattlePackedMatrix32.FromMatrix(normalized, null)), 32) };
        int calls = 0;
        var actual = DrawProgrammeNode282(frame, resources.ProgrammeA, Node(header, new(100, 200, 300)), NativeBattlePackedMatrix32.FromMatrix(caller, 0), -128, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEvilEye282DrawMesh7043B0>(operation);
            var matrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(current.Memory.GeometryRegisters!).ReadMatrix();
            Assert.Equal(new[] { x, y, z }, new[] { matrix.X, matrix.Y, matrix.Z });
            Assert.Equal(rotation == 1 ? caller.Rotation : rotation == 2 ? normalized.Rotation : NativeBattleCuragaMatrices.Identity8DD770().Rotation, matrix.Rotation);
            var context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(0x2030u, U(context, 28));
            Assert.Equal(unchecked((uint)-128), U(context, 24));
            Assert.Equal(ProgrammeAAddress + 76, draw.VertexAddress);
            calls++;
            return current;
        }));
        Assert.Equal(1, calls);
        Assert.Equal(frame.Random, actual.Random);
        Assert.Null(actual.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Alternate_Euler_selector_changes_only_the_selected_rotation_order(bool alternate)
    {
        var resources = NativeBattleEvilEye282StateTests.Resources();
        var angles = new NativeBattleVector(512, 256, 128);
        byte[] data = Node(alternate ? 0x40200u : 0x200u, new(0, 0, 0)).Data.ToArray();
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16 + axis * 2), (short)new[] { angles.X, angles.Y, angles.Z }[axis]);
        }
        var node = new NativeBattleGfAnimationNode(0, 0, data.ToImmutableArray());
        var expected = alternate ? NativeBattleGfMatrices.Euler7015B0(angles) : NativeBattleGfMatrices.Euler701310(angles);
        var actual = DrawProgrammeNode282(Frame(), resources.ProgrammeA, node, Identity(), -128, resources, Inputs());
        Assert.Equal(expected.Rotation, NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(actual.Memory.GeometryRegisters!).ReadMatrix().Rotation);
        Assert.NotEqual(NativeBattleGfMatrices.Euler701310(angles).Rotation, NativeBattleGfMatrices.Euler7015B0(angles).Rotation);
    }

    [Theory]
    [InlineData(1000, 2000, 3000)]
    [InlineData(40000, -50000, 60000)]
    public void Muzzle_uses_current_bone_and_world_with_full_MAC_output(int x, int y, int z)
    {
        var resources = NativeBattleEvilEye282StateTests.Resources();
        byte[] point = resources.SourcePoint8.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(point, 10);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(2), 20);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(4), 30);
        resources = resources with { SourcePoint8 = point.ToImmutableArray() };
        var bone = NativeBattleGfMatrices.Euler701310(new(1024, 0, 0)) with { X = 1, Y = 2, Z = 3 };
        var world = NativeBattleGfMatrices.Euler701310(new(0, 1024, 0)) with { X = x, Y = y, Z = z };
        int calls = 0;
        var frame = Frame();
        var actual = UpdateMuzzle67DAC0(frame, resources, Inputs() with
        {
            ReadCurrentBoneMatrix = (_, model, index) =>
            {
                Assert.Equal(0x1D97668u, model);
                Assert.Equal(13, index);
                calls++;
                return NativeBattlePackedMatrix32.FromMatrix(bone, null);
            },
            ReadModelWorld = (_, model) =>
            {
                Assert.Equal(0x1D97668u, model);
                return NativeBattlePackedMatrix32.FromMatrix(world, null);
            }
        });
        Assert.Equal(1, calls);
        Assert.Equal(new[] { x - 17, y + 32, z - 11 }, new[] { actual.X, actual.Y, actual.Z });
        Assert.Equal((ushort)13, BinaryPrimitives.ReadUInt16LittleEndian(actual.Frame.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.Null(actual.Frame.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(4, true)]
    [InlineData(24, true)]
    [InlineData(27, false)]
    [InlineData(28, false)]
    public void Secondary_remaining_gate_births_once_only_before_twenty_eight(int remaining, bool birth)
    {
        var (frame, resources, address) = Secondary(remaining);
        int parents = 0;
        var inputs = Inputs() with { ReadBoneParent = (_, model, _) =>
        {
            Assert.Equal(0x1D972C0u, model);
            parents++;
            return 0;
        } };
        var actual = AdvanceProgramme67DB20(frame, address, resources, inputs);
        Assert.Equal(remaining == 0 ? 2 : 0, actual.Result);
        Assert.Equal(birth ? 1 : 0, actual.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(birth ? 1 : 0, parents);
        Assert.Equal(frame.Memory.GeometryRegisters, actual.Frame.Memory.GeometryRegisters);
        Assert.Equal(frame.Memory.ScriptedColorRegisters, actual.Frame.Memory.ScriptedColorRegisters);
        if (!birth)
        {
            Assert.Equal(frame.Random, actual.Frame.Random);
        }
    }

    [Fact]
    public void Failed_sprite_allocation_consumes_no_random_or_bone_reads()
    {
        var (frame, resources, address) = Secondary(4);
        var pool = frame.Effect.Pools[1];
        for (int index = 0; index < 128; index++)
        {
            (pool, _) = pool.Append(0x67DC60);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        var actual = AdvanceProgramme67DB20(frame, address, resources, Inputs() with { ReadBoneCount = (_, _) => throw new InvalidOperationException() });
        Assert.Equal(0, actual.Result);
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(128, actual.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(28u, Read(actual.Frame, address).ReadDword(28));
    }

    [Fact]
    public void Negative_parent_retries_with_a_second_random_draw_before_reading_current_matrix()
    {
        var (frame, resources, address) = Secondary(4);
        var expected = frame.Random.Draw(out ushort first).Draw(out ushort second);
        List<int> selectors = [];
        int matrices = 0;
        var actual = AdvanceProgramme67DB20(frame, address, resources, Inputs() with
        {
            ReadBoneParent = (_, _, selector) =>
            {
                selectors.Add(selector);
                return selectors.Count == 1 ? (short)-1 : (short)0;
            },
            ReadCurrentBoneMatrix = (_, _, _) =>
            {
                matrices++;
                return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 11, Y = 22, Z = 33 }, null);
            },
            ReadModelWorld = (_, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 }, null)
        });
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal(new[] { first * 40 >> 15, second * 40 >> 15 }, selectors);
        Assert.Equal(1, matrices);
        var child = Read(actual.Frame, ArenaAddress);
        Assert.Equal(new ushort[] { 111, 222, 333, 0 }, new[] { child.ReadWord(12), child.ReadWord(14), child.ReadWord(16), child.ReadWord(18) });
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 2)]
    public void Sprite_draw_consumes_old_phase_as_W_then_moves_only_Y_and_increments_phase(int phase, int result)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2507EA0, 0x67DC60);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 300).WriteWord(18, (ushort)phase));
        int draws = 0;
        var actual = AdvanceSprite67DC60(frame, address, NativeBattleEvilEye282StateTests.Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEvilEye282DrawSprite180>(operation);
            Assert.Equal(SpriteAddress, draw.SourceAddress);
            Assert.Equal((ushort)phase, BinaryPrimitives.ReadUInt16LittleEndian(Context(current, draw.TemporaryAddress, 180).Read(4, 2)));
            Assert.Equal((ushort)phase, BinaryPrimitives.ReadUInt16LittleEndian(current.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
            draws++;
            return current;
        }));
        Assert.Equal(1, draws);
        Assert.Equal(result, actual.Result);
        var task = Read(actual.Frame, address);
        Assert.Equal(new ushort[] { 100, 190, 300, (ushort)(phase + 1) }, new[] { task.ReadWord(12), task.ReadWord(14), task.ReadWord(16), task.ReadWord(18) });
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    private static (NativeBattleEvilEye282Frame Frame, NativeBattleEvilEye282Resources Resources, uint Address) Secondary(int remaining)
    {
        var resources = NativeBattleEvilEye282StateTests.Resources();
        byte[] bytes = resources.ProgrammeB.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x100), 0);
        resources = resources with { ProgrammeB = resources.ProgrammeB with { Data = bytes.ToImmutableArray() } };
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2507EB0, 0x67DB20);
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[384].ToImmutableArray()).Initialize7016B0(resources.ProgrammeB, 384);
        byte[] cache = NativeBattleStorageSnapshots.Capture(workspace).Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(cache.AsSpan(4), 31 - remaining);
        var task = Read(frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 300).WriteDword(20, 0x1D972C0);
        task = CopyTask(task, 24, NativeBattleEffectArena.Clear().Write(0, cache), 384);
        return (Write(frame, address, task), resources, address);
    }

    private static NativeBattleGfAnimationNode Node(uint header, NativeBattleVector position)
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
        return new(0, 0, bytes.ToImmutableArray());
    }

    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattlePackedMatrix32 Identity() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static NativeBattleEvilEye282Frame Frame()
    {
        var pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        var effect = new NativeBattleOriginalEffectState(282, 281, new(282, Entry, Initializer), null, 0) { Pools = pools };
        var identity = Identity();
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity.ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = identity };
        var state = NativeBattleEvilEye282State.FromColdProcess().WriteDword(CasterGlobal, 0x1D97668).WriteView(NormalizedViewAddress, PackedArena(identity), 32);
        return new(memory, effect, new(123), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleEvilEye282Inputs Inputs(Func<NativeBattleEvilEye282Frame, NativeBattleEvilEye282Operation, NativeBattleEvilEye282Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => Identity(), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => 40, (_, _, _) => 0, (_, _, _) => throw new InvalidOperationException(), (_, _, _) => Identity(), (_, _) => Identity(), (_, _, _, _) => throw new InvalidOperationException(), _ => NativeBattleActionPacketsState.Fresh(), execute ?? ((current, _) => current));
}
