using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEnemyMicroMissiles284Controller;
using static Memoria.FF8.Runtime.NativeBattleEnemyMicroMissiles284Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnemyMicroMissiles284LowerTests
{
    [Theory]
    [InlineData(102, 1, 0)]
    [InlineData(1224, 12, 0)]
    [InlineData(4096, 12, 0)]
    [InlineData(5280, 1, 2)]
    public void Trail_uses_twelve_postdecrement_candidates_step102_and_own_retirement(int parameter, int draws, int result)
    {
        var resources = Resources();
        var (frame, address) = Missile(resources, parameter, -1);
        List<ushort> phases = [];
        List<uint> temporaries = [];
        var actual = AdvanceMissile67BBB0(frame, address, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEnemyMicroMissiles284DrawSprite180>(operation);
            Assert.Equal(TrailSpriteAddress, draw.SourceAddress);
            phases.Add(W(Context(current, draw.TemporaryAddress, 180), 4));
            temporaries.Add(draw.TemporaryAddress);
            return current;
        }));
        Assert.Equal(draws, phases.Count);
        Assert.Equal(result, actual.Result);
        Assert.Equal(draws == 1 ? new ushort[] { (ushort)(parameter == 102 ? 0 : 11) } : Enumerable.Range(0, 12).Reverse().Select(value => (ushort)value), phases);
        Assert.All(temporaries, temporary => Assert.Equal(temporaries[0], temporary));
        Assert.Equal(unchecked((ushort)(parameter + 102)), Read(actual.Frame, address).ReadWord(0x2A));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        if (parameter == 1224)
        {
            int along = 1000 * parameter >> 12;
            short x = unchecked((short)(unchecked(-NativeBattleCameraMath.Sine(parameter >> 1) * along) >> 12));
            short z = unchecked((short)(unchecked(-NativeBattleCameraMath.Cosine(parameter >> 1) * along) >> 12));
            var task = Read(actual.Frame, address);
            Assert.Equal(new short[] { x, 0, unchecked((short)(3000 + z)) }, new[] { Signed(task, 44), Signed(task, 46), Signed(task, 48) });
        }
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Impact_reaction_is_sequence_four_only_for_narrow_variant_and_keeps_clear_suffix(int variant, bool reaction)
    {
        var resources = Resources();
        var (frame, address) = Missile(resources, 4096, variant);
        frame = Fill(frame, 1, 96, 0x67C240);
        var impactPool = frame.Effect.Pools[2];
        var retained = CopyTask(impactPool.Slots[0], 208, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 116).ToArray()), 116);
        impactPool = impactPool with { Slots = impactPool.Slots.SetItem(0, retained) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, impactPool) } };
        List<NativeBattleEnemyMicroMissiles284Operation> operations = [];
        var actual = AdvanceMissile67BBB0(frame, address, resources, Inputs((current, operation) =>
        {
            operations.Add(operation);
            return current;
        }));
        Assert.Equal(1, actual.Frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(reaction ? 1 : 0, operations.OfType<NativeBattleEnemyMicroMissiles284Reaction505C00>().Count());
        Assert.Empty(operations.OfType<NativeBattleEnemyMicroMissiles284Contact506690>());
        if (reaction)
        {
            var command = Assert.Single(operations.OfType<NativeBattleEnemyMicroMissiles284Reaction505C00>());
            Assert.Equal((byte)4, command.Sequence);
            Assert.Equal(0x1D97494u, command.ModelAddress);
        }
        var impact = Read(actual.Frame, ArenaAddress + 0xD80);
        Assert.Equal(variant == 0 ? ImpactAddress : WideImpactAddress, impact.ReadDword(48));
        if (variant == 0)
        {
            Assert.All(TaskView(impact, 208, 116).Read(0, 116), value => Assert.Equal((byte)0xA5, value));
        }
        Assert.Equal((ushort)0xFFFF, Read(actual.Frame, address).ReadWord(34));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Failed_impact_still_attempts_sixteen_particles_and_only_successes_consume_random()
    {
        var resources = Resources();
        var (frame, address) = Missile(resources, 4096, 0);
        frame = Fill(Fill(frame, 2, 6, 0x67C300), 1, 94, 0x67C240);
        var expected = frame.Random;
        for (int index = 0; index < 6; index++)
        {
            expected = expected.Draw(out _);
        }
        var actual = AdvanceMissile67BBB0(frame, address, resources, Inputs((current, operation) =>
        {
            Assert.IsNotType<NativeBattleEnemyMicroMissiles284Reaction505C00>(operation);
            Assert.IsNotType<NativeBattleEnemyMicroMissiles284Contact506690>(operation);
            return current;
        }));
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal(96, actual.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal((ushort)0xFFFF, Read(actual.Frame, address).ReadWord(34));
        var child = Read(actual.Frame, ArenaAddress + 94 * 36);
        Assert.Equal((ushort)0, child.ReadWord(18));
        for (int axis = 0; axis < 3; axis++)
        {
            Assert.Equal(unchecked((short)(-Signed(child, 20 + axis * 2) >> 4)), Signed(child, 28 + axis * 2));
        }
    }

    [Theory]
    [InlineData(13, 11, 0, true)]
    [InlineData(14, 11, 2, false)]
    [InlineData(14, 10, 0, false)]
    public void Impact_draws_identity4096_before_age14_and_then_waits_for_programme(int age, int oldFrame, int result, bool drawSprite)
    {
        var resources = Resources();
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2507DD0, 0x67C300);
        NativeBattleModulePoolTask task = Rotation(Read(frame, address).WriteWord(12, (ushort)age).WriteDword(36, 100).WriteDword(40, 200).WriteDword(44, 300), 16, NativeBattleGfMatrices.Euler701310(new(512, 0, 0)));
        task = Cache(task, 48, 160, resources.Impact, oldFrame);
        frame = Write(frame, address, task);
        int draws = 0;
        var actual = AdvanceImpact67C300(frame, address, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEnemyMicroMissiles284DrawSprite180>(operation);
            Assert.Equal(ImpactSpriteAddress, draw.SourceAddress);
            var matrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(current.Memory.GeometryRegisters!).ReadMatrix();
            Assert.Equal(NativeBattleCuragaMatrices.Identity8DD770().Rotation, matrix.Rotation);
            Assert.Equal(new[] { 100, 200, 300 }, new[] { matrix.X, matrix.Y, matrix.Z });
            Assert.Equal((ushort)age, W(Context(current, draw.TemporaryAddress, 180), 4));
            draws++;
            return current;
        }));
        Assert.Equal(drawSprite ? 1 : 0, draws);
        Assert.Equal(result, actual.Result);
        Assert.Equal((ushort)(result == 2 ? age : age + 1), Read(actual.Frame, address).ReadWord(12));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(11, 2)]
    public void Particle_draws_old_point_and_phase_then_integrates_velocity_before_acceleration(int age, int result)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2507DC0, 0x67C240);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 100).WriteWord(14, 200).WriteWord(16, 300).WriteWord(18, (ushort)age).WriteWord(20, 10).WriteWord(22, unchecked((ushort)-20)).WriteWord(24, 30).WriteWord(28, unchecked((ushort)-1)).WriteWord(30, 2).WriteWord(32, unchecked((ushort)-3)));
        var actual = AdvanceParticle67C240(frame, address, Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEnemyMicroMissiles284DrawSprite180>(operation);
            Assert.Equal(ParticleSpriteAddress, draw.SourceAddress);
            Assert.Equal((ushort)age, W(Context(current, draw.TemporaryAddress, 180), 4));
            Assert.Equal((ushort)age, BinaryPrimitives.ReadUInt16LittleEndian(current.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
            Assert.Equal((ushort)100, Read(current, address).ReadWord(12));
            return current;
        }));
        var task = Read(actual.Frame, address);
        Assert.Equal(new ushort[] { 110, 180, 330, (ushort)(age + 1) }, new[] { task.ReadWord(12), task.ReadWord(14), task.ReadWord(16), task.ReadWord(18) });
        Assert.Equal(new short[] { 9, -18, 27 }, new[] { Signed(task, 20), Signed(task, 22), Signed(task, 24) });
        Assert.Equal(result, actual.Result);
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0u, true)]
    [InlineData(0x200u, false)]
    [InlineData(0x8000u, false)]
    public void Programme_uses_DWORD_bias_and_literal_parent_rotation_without_window_policy(uint header, bool parent)
    {
        var resources = Resources();
        var matrix = NativeBattleGfMatrices.Euler701310(new(1024, 0, 0)) with { X = 1000, Y = 2000, Z = 3000 };
        var caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(32, BitConverter.GetBytes(-65568)).Write(36, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
        var actual = DrawProgrammeNode67C010(Frame(), resources.Missile, Node(header), caller, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleEnemyMicroMissiles284DrawMesh7043B0>(operation);
            var context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(unchecked((uint)-65568), U(context, 24));
            Assert.Equal(0x2030u, U(context, 28));
            var loaded = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(current.Memory.GeometryRegisters!).ReadMatrix();
            Assert.Equal(parent ? matrix.Rotation : NativeBattleCuragaMatrices.Identity8DD770().Rotation, loaded.Rotation);
            Assert.Equal((header & 0x200) == 0 ? new[] { 1100, 2300, 2800 } : new[] { 1100, 2200, 3300 }, new[] { loaded.X, loaded.Y, loaded.Z });
            return current;
        }));
        Assert.NotNull(actual);
    }

    [Fact]
    public void Invisible_programme_ignores_unwritten_caller_and_inactive_geometry()
    {
        byte[] node = Node(0).Data.ToArray();
        Array.Clear(node, 24, 6);
        var frame = Frame();
        var actual = DrawProgrammeNode67C010(frame, Resources().Missile, new(0, 0, node.ToImmutableArray()), NativeBattleEffectArena.Clear().MarkUnknown(0, 40), Resources(), Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Same(frame, actual);
    }

    private static (NativeBattleEnemyMicroMissiles284Frame Frame, uint Address) Missile(NativeBattleEnemyMicroMissiles284Resources resources, int parameter, int variant)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2507DE0, 0x67BBB0);
        var task = Rotation(Read(frame, address), 12, NativeBattleCuragaMatrices.Identity8DD770()).WriteWord(30, 0xBEEF).WriteWord(32, 1000).WriteWord(34, unchecked((ushort)variant)).WriteWord(36, 0).WriteWord(38, 0).WriteWord(40, 3000).WriteWord(42, (ushort)parameter).WriteWord(44, 0).WriteWord(46, 0).WriteWord(48, 3000).WriteWord(50, 102);
        return (Write(frame, address, Cache(task, 52, 2020, resources.Missile, 0)), address);
    }
    private static NativeBattleModulePoolTask Cache(NativeBattleModulePoolTask task, int offset, int bytes, NativeBattleGfAnimationSource source, int frame)
    {
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[bytes].ToImmutableArray()).Initialize7016B0(source, bytes);
        byte[] data = workspace.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), frame);
        return CopyTask(task, offset, NativeBattleEffectArena.Clear().Write(0, data), bytes);
    }
    private static NativeBattleEnemyMicroMissiles284Frame Fill(NativeBattleEnemyMicroMissiles284Frame frame, int poolIndex, int count, uint callback)
    {
        var pool = frame.Effect.Pools[poolIndex];
        for (int index = 0; index < count; index++)
        {
            (pool, _) = pool.Append(callback);
        }
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(poolIndex, pool) } };
    }
    private static NativeBattleModulePoolTask Rotation(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task;
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
    private static NativeBattleEnemyMicroMissiles284Resources Resources()
    {
        var resources = NativeBattleEnemyMicroMissiles284StateTests.Resources();
        NativeBattleGfAnimationSource Empty(NativeBattleGfAnimationSource source)
        {
            byte[] data = source.Data.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x100), 0);
            return source with { Data = data.ToImmutableArray() };
        }
        return resources with { Missile = Empty(resources.Missile), Impact = Empty(resources.Impact), WideImpact = Empty(resources.WideImpact) };
    }
    private static NativeBattleEnemyMicroMissiles284Frame Frame()
    {
        var identity = Identity();
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity.ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = identity };
        var effect = new NativeBattleOriginalEffectState(284, 283, new(284, Entry, Initializer), new(3, 0, 0x7F, 0, 0, 0), null) { Pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray() };
        return new(memory, effect, new(123), NativeBattleEnemyMicroMissiles284State.FromColdProcess().WriteDword(ParametersGlobal, 0x1D99A78)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleEnemyMicroMissiles284Inputs Inputs(Func<NativeBattleEnemyMicroMissiles284Frame, NativeBattleEnemyMicroMissiles284Operation, NativeBattleEnemyMicroMissiles284Frame>? execute = null)
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 12, -1, 201, 284, null));
        (packets, _) = packets.AppendContact(new(new(3, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => Identity(), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => packets, execute ?? ((current, _) => current));
    }
    private static NativeBattlePackedMatrix32 Identity() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static ushort W(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
