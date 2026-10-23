using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBreath157Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath157ChildrenTests
{
    private const uint TaskAddress = 0x23EBFA0;
    private const uint Bank = 0x23EB628;

    [Fact]
    public void Scale_writes_three_zero_extended_DWORDs_and_preserves_other_controls_and_knownness()
    {
        NativeBattleBreath157Frame frame = Frame(0x604B30);
        NativeBattleEffectArena rotation = NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(0, 20).Select(index => (byte)(40 + index)).ToArray()).MarkUnknown(4, 1);
        frame = frame with { State = frame.State.WriteView(0x23ECE20, rotation, 20) };
        frame = NativeBattleBreath157Geometry.LoadPose(frame, 0x23EB5C0);
        frame = NativeBattleBreath157Geometry.Scale64DDD0(frame, -2);
        ImmutableArray<byte?> actual = frame.Memory.GeometryRegisters!.Rotation20;
        foreach (int offset in new[] { 0, 8, 16 })
        {
            Assert.Equal(new byte?[] { 254, 255, 0, 0 }, actual.Skip(offset).Take(4));
        }
        Assert.Null(actual[4]);
        Assert.Equal((byte)45, actual[5]);
        Assert.Equal((byte)46, actual[6]);
        Assert.Equal(new byte?[] { 52, 53, 54, 55 }, actual.Skip(12).Take(4));
        Assert.Throws<NotSupportedException>(() => frame.State.ReadView(0x23ECE24, 1).Read(0, 1));
    }

    [Fact]
    public void Raw_billboard_saves_full_MAC_but_copies_saturated_IR_to_translation()
    {
        NativeBattleBreath157Frame frame = Frame(0x604C80);
        NativeBattlePackedMatrix32 pose = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 60000, Y = -60000, Z = 80000 }, null);
        frame = frame with { State = frame.State.WriteView(0x23EB5C0, PackedArena(pose), 32) };
        frame = NativeBattleBreath157Geometry.LoadPose(frame, 0x23EB5C0);
        frame = NativeBattleBreath157Geometry.TransformBillboard(frame, PointView(new(1, 2, 3)), 4096, false);
        Assert.Equal(new int?[] { 60001, -59998, 80003 }, frame.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { 32767, -32768, 32767 }, frame.Memory.GeometryRegisters!.Translation);
        Assert.Equal(60001u, frame.State.ReadDword(0x23ECE34));
        Assert.Equal(unchecked((uint)-59998), frame.State.ReadDword(0x23ECE38));
        Assert.Equal(80003u, frame.State.ReadDword(0x23ECE3C));
        Assert.False(frame.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        NativeBattleBreath157Frame offset = NativeBattleBreath157Geometry.LoadPose(frame, 0x23ECDB0);
        offset = offset with { State = offset.State.WriteView(0x23ECDB0, PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 60000 }, null)), 32) };
        offset = NativeBattleBreath157Geometry.LoadPose(offset, 0x23ECDB0);
        offset = NativeBattleBreath157Geometry.TransformBillboard(offset, PointView(new(0, 0, 0)), 4096, true);
        Assert.Equal(new int?[] { 59488, 0, 0 }, offset.Memory.GeometryRegisters!.Translation);
        Assert.Equal(59488u, offset.State.ReadDword(0x23ECE34));
        Assert.Equal(60000, offset.Memory.ScriptedColorRegisters!.Mac[0]);
    }

    [Theory]
    [InlineData(0x6043A0u, 0, 0xF3u, 4096)]
    [InlineData(0x6043A0u, 4, 0x33u, null)]
    [InlineData(0x6043A0u, 22, 0xF3u, 0)]
    [InlineData(0x6043A0u, 29, 0xF3u, 3584)]
    [InlineData(0x6049F0u, 7, 0xF3u, 512)]
    [InlineData(0x6049F0u, 8, 0x33u, null)]
    [InlineData(0x604E90u, 7, 3u, null)]
    [InlineData(0x604E90u, 8, 0xC3u, 0)]
    [InlineData(0x6050A0u, 7, 0x33u, null)]
    [InlineData(0x6050A0u, 17, 0xF3u, 3681)]
    public void Mesh_policy_and_released_second_context_preserve_actual_translation(uint callback, short age, uint flags, int? cue)
    {
        NativeBattleBreath157Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, unchecked((ushort)age)).WriteWord(0x1C, 4096));
        List<NativeBattleEffectArena> contexts = [];
        List<ImmutableArray<int?>> translations = [];
        List<uint> sources = [];
        List<uint> cursors = [];
        var actual = Advance(frame, callback, Inputs(0x200, (current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBreath157DrawMesh88>(operation);
            sources.Add(draw.SourceAddress);
            contexts.Add(Context(current, draw.TemporaryAddress, 88));
            translations.Add(current.Memory.GeometryRegisters!.Translation);
            cursors.Add(BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            return PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 88).Write(16, BitConverter.GetBytes(0xDEADBEEFu)), 88);
        }));
        Assert.Equal(0, actual.Result);
        Assert.Equal(Read(frame, TaskAddress), Read(actual.Frame, TaskAddress));
        Assert.Equal(callback is 0x604E90 or 0x6050A0 ? 2 : 1, contexts.Count);
        Assert.Equal(flags, BinaryPrimitives.ReadUInt32LittleEndian(contexts[0].Read(0x1C, 4)));
        Assert.Throws<NotSupportedException>(() => contexts[0].Read(4, 4));
        if (cue is int value)
        {
            Assert.Equal(value, BinaryPrimitives.ReadInt32LittleEndian(contexts[0].Read(12, 4)));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => contexts[0].Read(12, 4));
        }
        Assert.Equal(NativeBattleTemporaryMemory.Address + 88, cursors[0]);
        if (contexts.Count == 2)
        {
            Assert.Equal(NativeBattleTemporaryMemory.Address, cursors[1]);
            Assert.Equal(translations[0], translations[1]);
            Assert.Equal(0xDEADBEEFu, BinaryPrimitives.ReadUInt32LittleEndian(contexts[1].Read(16, 4)));
            Assert.Equal(callback == 0x604E90 ? new uint[] { 0xDB20DC, 0xDB2F54 } : new uint[] { 0xDB3BDC, 0xDB3BDC }, sources);
        }
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Theory]
    [InlineData(0x6043A0u, 29, -25)]
    [InlineData(0x6049F0u, 9, -24)]
    [InlineData(0x604E90u, 15, -22)]
    [InlineData(0x6050A0u, 17, -22)]
    public void Mesh_signed_damping_and_completion_are_callback_specific(uint callback, short age, short speed)
    {
        NativeBattleBreath157Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, unchecked((ushort)age)).WriteWord(0x1C, 4096).WriteWord(0x1E, unchecked((ushort)-25)).WriteWord(0x1A, unchecked((ushort)-17)).WriteWord(0x14, 25));
        var actual = Advance(frame, callback, Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Equal(unchecked((ushort)speed), Read(actual.Frame, TaskAddress).ReadWord(0x1E));
        if (callback == 0x604E90)
        {
            Assert.Equal((ushort)21, Read(actual.Frame, TaskAddress).ReadWord(0x14));
            Assert.Equal((ushort)5, actual.Frame.State.ReadWord(0x23ECE04));
        }
        if (callback == 0x6050A0)
        {
            Assert.Equal(unchecked((ushort)-15), Read(actual.Frame, TaskAddress).ReadWord(0x1A));
        }
    }

    [Theory]
    [InlineData(0x6044D0u, 1u, 3, 15)]
    [InlineData(0x604C80u, 2u, 6, 24)]
    [InlineData(0x6052A0u, 4u, 6, 24)]
    public void Shared_particle_bank_births_keep_exact_tags_CRT_and_retained_suffix(uint callback, uint tag, int births, int draws)
    {
        NativeBattleBreath157Frame frame = Frame(callback);
        frame = frame with { State = frame.State.WriteView(Bank + 16, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 8) };
        var actual = Advance(frame, callback, Inputs());
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal(births, Enumerable.Range(0, 100).Count(index => actual.Frame.State.ReadDword(Bank + (uint)(index * 24)) == tag));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadView(Bank + 16, 8).Read(0, 8));
        if (callback != 0x6044D0)
        {
            Assert.Equal((ushort)(callback == 0x604C80 ? 0xCAFE : 0xBEEF), actual.Frame.State.ReadWord(Bank + 14));
        }
        NativeBattleBreath157Frame restored = JsonSerializer.Deserialize<NativeBattleBreath157Frame>(JsonSerializer.Serialize(actual.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(Advance(actual.Frame, callback, Inputs()).Frame), JsonSerializer.Serialize(Advance(restored, callback, Inputs()).Frame));
    }

    [Theory]
    [InlineData(0x6044D0u, 1u, 31)]
    [InlineData(0x604C80u, 2u, 5)]
    [InlineData(0x6052A0u, 4u, 5)]
    public void Pause_draws_but_negative_feedback_retires_only_when_unpaused(uint callback, uint tag, ushort age)
    {
        NativeBattleBreath157Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, age));
        frame = frame with { State = frame.State.WriteDword(Bank, tag).WriteWord(Bank + 6, 1024).WriteWord(Bank + 10, 17).WriteWord(Bank + 12, 100) };
        static NativeBattleBreath157Frame Reject(NativeBattleBreath157Frame current, NativeBattleBreath157Operation operation)
        {
            var draw = Assert.IsType<NativeBattleBreath157DrawSprite180>(operation);
            return PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 180).Write(0x28, BitConverter.GetBytes((short)-1)), 180);
        }
        var paused = Advance(frame, callback, Inputs(0x201, Reject));
        Assert.Equal(0, paused.Result);
        Assert.Equal(frame.Random, paused.Frame.Random);
        Assert.Equal(tag, paused.Frame.State.ReadDword(Bank));
        Assert.Equal((ushort)0, paused.Frame.State.ReadWord(Bank + 4));
        var retired = Advance(frame, callback, Inputs(0, Reject));
        Assert.Equal(2, retired.Result);
        Assert.Equal(0u, retired.Frame.State.ReadDword(Bank));
        Assert.Equal((ushort)1, retired.Frame.State.ReadWord(Bank + 4));
        Assert.Equal((ushort)0, retired.Frame.State.ReadWord(Bank + 8));
    }

    [Fact]
    public void Traveling_raw_billboard_moves_Z_only_after_drawing_and_retires_at_five()
    {
        NativeBattleBreath157Frame frame = Frame(0x604B30);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, 4).WriteWord(0x1C, 4096).WriteWord(0x14, 25));
        int submissions = 0;
        var actual = Advance(frame, 0x604B30, Inputs(0, (current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBreath157DrawSprite180>(operation);
            Assert.Equal(0xDB0AC0u, draw.SourceAddress);
            Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(Context(current, draw.TemporaryAddress, 180).Read(4, 2)));
            Assert.Equal((ushort)30, current.State.ReadWord(0x23ECE04));
            submissions++;
            return current;
        }));
        Assert.Equal(1, submissions);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)5, actual.Frame.State.ReadWord(0x23ECE04));
        Assert.Equal((ushort)0xCAFE, actual.Frame.State.ReadWord(0x23ECE06));
    }

    private static (NativeBattleBreath157Frame Frame, int Result) Advance(NativeBattleBreath157Frame frame, uint callback, NativeBattleBreath157Inputs inputs) => callback switch
    {
        0x6043A0 => NativeBattleBreath157Children.Advance6043A0(frame, TaskAddress, null!, inputs),
        0x6049F0 => NativeBattleBreath157Children.Advance6049F0(frame, TaskAddress, null!, inputs),
        0x604E90 => NativeBattleBreath157Children.Advance604E90(frame, TaskAddress, null!, inputs),
        0x6050A0 => NativeBattleBreath157Children.Advance6050A0(frame, TaskAddress, null!, inputs),
        0x6044D0 => NativeBattleBreath157Children.Advance6044D0(frame, TaskAddress, null!, inputs),
        0x604B30 => NativeBattleBreath157Children.Advance604B30(frame, TaskAddress, null!, inputs),
        0x604C80 => NativeBattleBreath157Children.Advance604C80(frame, TaskAddress, null!, inputs),
        _ => NativeBattleBreath157Children.Advance6052A0(frame, TaskAddress, null!, inputs)
    };

    private static NativeBattleBreath157Frame Frame(uint callback)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23EBF90, TaskAddress, 36, 100);
        (pool, _) = pool.Append(callback);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(0x14, 50)) };
        NativeBattleOriginalEffectState effect = new(157, 156, new(157, 0x603E90, 0x603E70), null, 0x23EB618) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleBreath157Frame frame = new(memory, effect, new(123), NativeBattleBreath157State.FromColdProcess() with { RootBankStorage = null, ChildBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = frame with { State = frame.State.WriteView(0x23EB5C0, PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 2300 }, null)), 32).WriteView(0x23ECDB0, PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 500, Y = 600, Z = 2700 }, null)), 32).WriteView(0x23ECE00, PointView(new(10, 20, 30), unchecked((short)0xCAFE)), 8).WriteView(0x23ECDD0, PointView(new(40, 50, 60), unchecked((short)0xBEEF)), 8) };
        return frame;
    }
    private static NativeBattleBreath157Inputs Inputs(uint flags = 0, Func<NativeBattleBreath157Frame, NativeBattleBreath157Operation, NativeBattleBreath157Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 900, Y = 1000, Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, _) => NativeBattleCuragaMatrices.Identity8DD770(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute ?? ((current, operation) => operation is NativeBattleBreath157DrawSprite180 draw ? PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 180).Write(0x28, new byte[2]), 180) : current));
}
