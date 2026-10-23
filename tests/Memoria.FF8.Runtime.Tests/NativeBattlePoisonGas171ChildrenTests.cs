using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattlePoisonGas171Controller;
using static Memoria.FF8.Runtime.NativeBattlePoisonGas171Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePoisonGas171ChildrenTests
{
    private static readonly NativeBattlePoisonGas171Resources Resources = new(null!, null!, null!, Emitters(), null!, 0);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 96)]
    [InlineData(4, 128)]
    [InlineData(15, 128)]
    [InlineData(16, 128)]
    [InlineData(23, 16)]
    [InlineData(24, -1)]
    public void Body_gray_window_and_hide_writes_precede_pause_and_restore_only_saved_bit(int age, int intensity)
    {
        var frame = Frame(0x5DB620, (ushort)age);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x1A, 4));
        List<NativeBattlePoisonGas171Operation> operations = [];
        var input = Inputs((current, operation) =>
        {
            operations.Add(operation);
            if (operation is NativeBattlePoisonGas171DrawBody176 draw)
            {
                Assert.Equal(3, draw.Actor);
                var context = Context(current, draw.TemporaryAddress, 176);
                Assert.Equal((uint)(intensity * 0x10101), Dword(context, 0x6C));
                Assert.Equal(age * 4, unchecked((int)Dword(context, 0x30)));
                Assert.Equal(0u, Dword(context, 0x2C));
                Assert.Equal(0u, Dword(context, 0x34));
                Assert.Equal(0x00800080u, Dword(context, 0x38));
                Assert.Equal(NativeBattleTemporaryMemory.Address + 176, Cursor(current));
            }
            return current;
        });
        var paused = AdvanceBody(frame, ChildBank, Resources, input with { GraphicsFlags = 0x201 });
        Assert.Equal((ushort)age, Read(paused.Frame, ChildBank).ReadWord(12));
        Assert.Equal(age < 24 ? 2 : 0, operations.Count);
        if (age < 24)
        {
            Assert.Equal((ushort)4, Assert.IsType<NativeBattlePoisonGas171ActorFlags>(operations[0]).SetMask);
        }
        operations.Clear();
        var actual = AdvanceBody(frame, ChildBank, Resources, input);
        Assert.Equal(age >= 23 ? 2 : 0, actual.Result);
        if (age >= 23)
        {
            var restore = Assert.IsType<NativeBattlePoisonGas171ActorFlags>(operations[^1]);
            Assert.Equal((ushort)4, restore.ClearMask);
            Assert.Equal((ushort)4, restore.SetMask);
        }
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
    }

    [Theory]
    [InlineData(0, 4096)]
    [InlineData(11, 2226)]
    [InlineData(12, 2048)]
    [InlineData(42, 2048)]
    [InlineData(53, 3918)]
    public void Mesh_uses_current_focal_direct_matrix_and_literal_cue_before_pause(int age, int cue)
    {
        var frame = Frame(0x5DC5B0, (ushort)age);
        int calls = 0;
        var input = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattlePoisonGas171DrawStatic88>(operation);
            Assert.Equal(0xD65A4Cu, draw.SourceAddress);
            Assert.Equal(4, draw.DepthShift);
            var context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal((uint)cue, Dword(context, 12));
            Assert.Equal(0xC3u, Dword(context, 0x1C));
            Assert.Equal(new int?[] { 100, 0, 333 }, current.Memory.GeometryRegisters!.Translation);
            Assert.Equal((short)6144, BinaryPrimitives.ReadInt16LittleEndian(NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(current.Memory.GeometryRegisters).Bytes.AsSpan()));
            calls++;
            return current;
        }) with { ReadView = _ => throw new InvalidOperationException("Direct mesh must not read a camera matrix.") };
        var paused = AdvanceMesh(frame, ChildBank, Resources, input with { GraphicsFlags = 0x201 });
        Assert.Equal((ushort)age, Read(paused.Frame, ChildBank).ReadWord(12));
        var actual = AdvanceMesh(frame, ChildBank, Resources, input);
        Assert.Equal(age == 53 ? 2 : 0, actual.Result);
        Assert.Equal((ushort)177, Read(actual.Frame, ChildBank).ReadWord(0x10));
        Assert.Equal((ushort)7, Read(actual.Frame, ChildBank).ReadWord(0x18));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(0x5DBED0u, 2, 10, 1u)]
    [InlineData(0x5DC270u, 2, 10, 1u)]
    [InlineData(0x5DC700u, 1, 4, 4u)]
    public void Births_use_exact_stream_stack_W_for_smoke_and_task_W_for_target(uint callback, int births, int draws, uint flags)
    {
        var frame = Frame(callback, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, (ushort)flags).WriteWord(0x18, callback == 0x5DC270 ? (ushort)4 : (ushort)0));
        uint bank = callback == 0x5DC270 ? ParticleABase : ParticleBBase;
        for (int index = 0; index < 100; index++)
        {
            frame = frame with { State = frame.State.WriteView(bank + (uint)(index * 24) + 22, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        }
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        Assert.Equal(births, Enumerable.Range(0, 100).Count(index => actual.Frame.State.ReadDword(bank + (uint)(index * 24)) == flags));
        var random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(bank + 22));
        if (callback == 0x5DC700)
        {
            Assert.Equal((ushort)77, actual.Frame.State.ReadWord(bank + 14));
            Assert.Equal((ushort)0, actual.Frame.State.ReadWord(bank + 16));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(bank + 14));
        }
        var restored = JsonSerializer.Deserialize<NativeBattlePoisonGas171Frame>(JsonSerializer.Serialize(actual.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(AdvanceSlots(actual.Frame, ChildBank, Resources, Inputs()).Frame), JsonSerializer.Serialize(AdvanceSlots(restored, ChildBank, Resources, Inputs()).Frame));
    }

    [Theory]
    [InlineData(0x5DBED0u, 2, 1056, 83, -16)]
    [InlineData(0x5DC270u, 5, 1056, 83, -16)]
    [InlineData(0x5DC700u, 5, 1024, 100, -17)]
    public void Phase_selection_SAR_growth_and_drag_are_distinct_from_target_particles(uint callback, int phase, int size, int y, int velocity)
    {
        var frame = Frame(callback, 17);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 1));
        uint bank = callback == 0x5DC270 ? ParticleABase : ParticleBBase;
        frame = frame with { State = frame.State.WriteDword(bank, 1).WriteWord(bank + 4, 5).WriteWord(bank + 6, 1024).WriteWord(bank + 10, 100).WriteWord(bank + 18, unchecked((ushort)-17)) };
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattlePoisonGas171DrawSprite180>(operation);
            Assert.Equal(callback == 0x5DC700 ? 0xD656F4u : 0xD658A0u, draw.SourceAddress);
            Assert.Equal((ushort)phase, BinaryPrimitives.ReadUInt16LittleEndian(Context(current, draw.TemporaryAddress, 180).Read(4, 2)));
            return Feedback(current, draw.TemporaryAddress, 0);
        }));
        Assert.Equal((ushort)6, actual.Frame.State.ReadWord(bank + 4));
        Assert.Equal((ushort)size, actual.Frame.State.ReadWord(bank + 6));
        Assert.Equal((ushort)y, actual.Frame.State.ReadWord(bank + 10));
        Assert.Equal((short)velocity, unchecked((short)actual.Frame.State.ReadWord(bank + 18)));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0x5DBED0u, 5)]
    [InlineData(0x5DC270u, 5)]
    [InlineData(0x5DC700u, 4)]
    public void Partial_bank_and_feedback_pause_preserve_conditional_rng_and_physics(uint callback, int draws)
    {
        var frame = Frame(callback, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 1).WriteWord(0x18, 0));
        uint bank = callback == 0x5DC270 ? ParticleABase : ParticleBBase;
        for (int index = 1; index < 100; index++)
        {
            frame = frame with { State = frame.State.WriteDword(bank + (uint)(index * 24), 0x80) };
        }
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        var random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        frame = actual.Frame;
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, 17));
        var input = Inputs((current, operation) => Feedback(current, Assert.IsType<NativeBattlePoisonGas171DrawSprite180>(operation).TemporaryAddress, -1));
        var paused = AdvanceSlots(frame, ChildBank, Resources, input with { GraphicsFlags = 0x201 });
        Assert.Equal(1u, paused.Frame.State.ReadDword(bank));
        Assert.Equal(frame.Random, paused.Frame.Random);
        var killed = AdvanceSlots(frame, ChildBank, Resources, input);
        Assert.Equal(0u, killed.Frame.State.ReadDword(bank));
        Assert.Equal(2, killed.Result);
    }

    [Fact]
    public void Smoke_reads_current_world_and_aged_endpoint_before_WORD_wrap_normalization()
    {
        var frame = Frame(0x5DBED0, 16);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x18, 2));
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(0, 700, 0));
        int reads = 0;
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs() with { ReadModelWorld = (_, actor) =>
        {
            Assert.Equal(3, actor);
            reads++;
            return NativeBattlePackedMatrix32.FromMatrix(world, null);
        } });
        NativeBattleVector first = NativeBattleGfMatrices.RotateShort56C4F0(world, new(200, 40, -100));
        Assert.Equal(unchecked((ushort)(first.X + 100)), actual.Frame.State.ReadWord(ParticleBBase + 8));
        Assert.Equal(unchecked((ushort)(first.Y + 200)), actual.Frame.State.ReadWord(ParticleBBase + 10));
        Assert.Equal(unchecked((ushort)(first.Z + 300)), actual.Frame.State.ReadWord(ParticleBBase + 12));
        NativeBattleVector second = NativeBattleGfMatrices.RotateShort56C4F0(world, new(444, 60, -390));
        first = new(unchecked((short)(first.X + 100)), unchecked((short)(first.Y + 200)), unchecked((short)(first.Z + 300)));
        second = new(unchecked((short)(second.X + 100)), unchecked((short)(second.Y + 200)), unchecked((short)(second.Z + 300)));
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(second.X - first.X, second.Y - first.Y, second.Z - first.Z);
        NativeCrtRandomState random = frame.Random.Draw(out _);
        random = random.Draw(out ushort speedDraw);
        int speed = speedDraw % 95 + 40;
        int[] lanes = [direction.X, direction.Y, direction.Z];
        for (int axis = 0; axis < 3; axis++)
        {
            random = random.Draw(out ushort jitter);
            Assert.Equal(unchecked((ushort)((speed * lanes[axis] >> 12) + jitter % 40 - 20)), actual.Frame.State.ReadWord(ParticleBBase + 16 + (uint)(axis * 2)));
        }
        Assert.Equal(1, reads);
    }

    private static NativeBattlePoisonGas171Frame Frame(uint callback, ushort age)
    {
        var root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        var child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (child, _) = child.Append(callback);
        NativeBattleOriginalEffectState effect = new(171, 170, new(171, Entry, Initializer), null, RootDescriptor) { Pools = [root, child] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattlePoisonGas171Frame frame = new(memory, effect, new(123), NativeBattlePoisonGas171State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = frame with { State = frame.State.WriteDword(CasterGlobal, 3) };
        return Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age).WriteWord(14, 1).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x16, 77).WriteWord(0x18, 0).WriteWord(0x1A, 7).WriteWord(0x1C, 4096));
    }

    private static ImmutableArray<byte> Emitters()
    {
        byte[] bytes = new byte[64];
        for (int index = 0; index < 8; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8), (short)(index * 100));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 2), (short)(index * 20));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 4), (short)(index * -50));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 6), 1234);
        }
        return bytes.ToImmutableArray();
    }
    private static NativeBattlePoisonGas171Inputs Inputs(Func<NativeBattlePoisonGas171Frame, NativeBattlePoisonGas171Operation, NativeBattlePoisonGas171Frame>? execute = null, uint flags = 0) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), _ => 333, (_, _) => throw new InvalidOperationException(), (_, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (_, _, _, _) => throw new InvalidOperationException(), execute ?? ((current, operation) => operation is NativeBattlePoisonGas171DrawSprite180 draw ? Feedback(current, draw.TemporaryAddress, 0) : current));
    private static NativeBattlePoisonGas171Frame Feedback(NativeBattlePoisonGas171Frame frame, uint temporary, short value) => PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0x28, BitConverter.GetBytes(value)), 180);
    private static uint Cursor(NativeBattlePoisonGas171Frame frame) => BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
}
