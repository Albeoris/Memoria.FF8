using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleCannonBlow151Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCannonBlow151ChildrenTests
{
    private const uint TaskAddress = 0x244E9F8;

    [Fact]
    public void Two_stage_mesh_forwards_returned_T_and_publishes_actual_scroll_context_after_releasing_88()
    {
        NativeBattleCannonBlow151Frame frame = Frame(0x60DE40);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, 9).WriteWord(0x1C, 4096));
        int calls = 0;
        var actual = Advance(frame, 0x60DE40, Inputs(0x201, (current, operation) =>
        {
            calls++;
            if (operation is NativeBattleCannonBlow151DrawStatic88)
            {
                return current with { Memory = current.Memory with { GeometryRegisters = current.Memory.GeometryRegisters! with { Translation = [400, 500, 600] } } };
            }
            var scroll = Assert.IsType<NativeBattleCannonBlow151DrawScrolling124>(operation);
            Assert.Equal(NativeBattleTemporaryMemory.Address, scroll.TemporaryAddress);
            Assert.Equal(0xDC00F0u, scroll.SourceAddress);
            Assert.Equal(new int?[] { 400, 500, 600 }, current.Memory.GeometryRegisters!.Translation);
            NativeBattleEffectArena context = Context(current, scroll.TemporaryAddress, 124);
            Assert.Equal(126, BinaryPrimitives.ReadInt32LittleEndian(context.Read(0x14, 4)));
            Assert.Equal(512, BinaryPrimitives.ReadInt32LittleEndian(context.Read(12, 4)));
            Assert.Equal(0xF3u, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x20, 4)));
            Assert.Equal(new byte[] { 0, 0, 0, 0, 64, 0, 128, 0 }, context.Read(0x18, 8));
            Assert.Equal(NativeBattleTemporaryMemory.Address + 124, BitConverter.ToUInt32(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            return current;
        }));
        Assert.Equal(2, calls);
        Assert.Equal(0, actual.Result);
        Assert.Equal((ushort)9, Read(actual.Frame, TaskAddress).ReadWord(12));
    }

    [Fact]
    public void Impact_sprite_countdown_obeys_pause_then_uses_actual_feedback_instead_of_frame_count()
    {
        NativeBattleCannonBlow151Frame frame = Frame(0x60E1B0);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(14, 1).WriteWord(0x1C, 1024).WriteWord(12, 30));
        var paused = Advance(frame, 0x60E1B0, Inputs(0x201, (_, _) => throw new InvalidOperationException()));
        Assert.Equal((ushort)1, Read(paused.Frame, TaskAddress).ReadWord(14));
        var countdown = Advance(frame, 0x60E1B0, Inputs(0, (_, _) => throw new InvalidOperationException()));
        Assert.Equal((ushort)0, Read(countdown.Frame, TaskAddress).ReadWord(14));
        var alive = Advance(countdown.Frame, 0x60E1B0, Inputs());
        Assert.Equal(0, alive.Result);
        Assert.Equal((ushort)31, Read(alive.Frame, TaskAddress).ReadWord(12));
        var dead = Advance(alive.Frame, 0x60E1B0, Inputs(0, (current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleCannonBlow151DrawSprite180>(operation);
            Assert.Equal(0xDBED9Cu, draw.SourceAddress);
            return PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 180).Write(0x28, BitConverter.GetBytes((short)-1)), 180);
        }));
        Assert.Equal(2, dead.Result);
        Assert.Equal((ushort)32, Read(dead.Frame, TaskAddress).ReadWord(12));
    }

    [Theory]
    [InlineData(0x60D260u, 3, 0xDC0E10u, 0x33u, null)]
    [InlineData(0x60D260u, 4, 0xDC0E10u, 0xF3u, 0)]
    [InlineData(0x60D670u, 0, 0xDC0900u, 0xF3u, 0)]
    [InlineData(0x60DE40u, 7, 0xDBF4C0u, 0x33u, null)]
    [InlineData(0x60DE40u, 8, 0xDBF4C0u, 0xF3u, 0)]
    [InlineData(0x60E060u, 2, 0xDC1498u, 0x33u, null)]
    [InlineData(0x60E060u, 3, 0xDC1498u, 0xF3u, 0)]
    [InlineData(0x60E060u, 13, 0xDC1498u, 0xF3u, 4090)]
    public void Mesh_submits_actual_source_and_retained_context_before_pause(uint callback, short age, uint source, uint flags, int? cue)
    {
        NativeBattleCannonBlow151Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, unchecked((ushort)age)).WriteWord(0x1C, 4096));
        NativeBattleEffectArena? submitted = null;
        NativeBattleCannonBlow151Inputs inputs = Inputs(0x200, (current, operation) =>
        {
            if (operation is NativeBattleCannonBlow151DrawScrolling124)
            {
                return current;
            }
            NativeBattleCannonBlow151DrawStatic88 draw = Assert.IsType<NativeBattleCannonBlow151DrawStatic88>(operation);
            Assert.Equal(source, draw.SourceAddress);
            submitted = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(new int?[] { 110, 20, 2300 }, current.Memory.GeometryRegisters!.Translation.ToArray());
            return current;
        });
        var actual = Advance(frame, callback, inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal(Read(frame, TaskAddress), Read(actual.Frame, TaskAddress));
        Assert.Equal(source, BinaryPrimitives.ReadUInt32LittleEndian(submitted!.Read(0, 4)));
        Assert.Equal(flags, BinaryPrimitives.ReadUInt32LittleEndian(submitted.Read(0x1C, 4)));
        Assert.Throws<NotSupportedException>(() => submitted.Read(4, 4));
        if (cue is int value)
        {
            Assert.Equal(value, BinaryPrimitives.ReadInt32LittleEndian(submitted.Read(12, 4)));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => submitted.Read(12, 4));
        }
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Theory]
    [InlineData(0x60D260u, 11, -19, -18)]
    [InlineData(0x60D670u, 3, -19, -19)]
    [InlineData(0x60DE40u, 16, -19, -16)]
    [InlineData(0x60E060u, 13, -19, -17)]
    public void Mesh_completion_and_signed_damping_follow_each_callback(uint callback, short age, short speed, short expectedSpeed)
    {
        NativeBattleCannonBlow151Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, unchecked((ushort)age)).WriteWord(0x1C, 4096).WriteWord(0x1E, unchecked((ushort)speed)).WriteWord(0x1A, unchecked((ushort)-9)));
        var actual = Advance(frame, callback, Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Equal(expectedSpeed, unchecked((short)Read(actual.Frame, TaskAddress).ReadWord(0x1E)));
        Assert.Equal(callback == 0x60D670 ? 4096 : 4077, Read(actual.Frame, TaskAddress).ReadWord(0x1C));
        if (callback == 0x60DE40)
        {
            Assert.Equal(unchecked((ushort)-9), Read(actual.Frame, TaskAddress).ReadWord(0x18));
            Assert.Equal(unchecked((ushort)-7), Read(actual.Frame, TaskAddress).ReadWord(0x1A));
        }
    }

    [Theory]
    [InlineData(0x60D3B0u, 0x244E008u, 12, 84)]
    [InlineData(0x60D850u, 0x244D6A8u, 3, 3)]
    [InlineData(0x60DA10u, 0x244D6A8u, 3, 12)]
    [InlineData(0x60DC20u, 0x244E008u, 6, 24)]
    public void Births_preserve_native_counts_random_order_and_unwritten_suffix(uint callback, uint bank, int births, int draws)
    {
        NativeBattleCannonBlow151Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(0x18, 2).WriteWord(0x1E, 2));
        frame = frame with { State = frame.State.WriteView(bank + 22, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        if (callback == 0x60D3B0)
        {
            frame = frame with { State = frame.State.WriteView(bank + 14, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        }
        var actual = Advance(frame, callback, Inputs());
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        Assert.Equal(births, Enumerable.Range(0, 100).Count(index => actual.Frame.State.ReadDword(bank + (uint)(index * 24)) == 2));
        Assert.Equal(0, actual.Result);
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadView(bank + 22, 2).Read(0, 2));
        if (callback == 0x60D3B0)
        {
            Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadView(bank + 14, 2).Read(0, 2));
            NativeCrtRandomState first = frame.Random.Draw(out ushort size);
            first = first.Draw(out ushort x);
            first = first.Draw(out ushort y);
            first = first.Draw(out ushort z);
            first = first.Draw(out ushort angle);
            first = first.Draw(out ushort speed);
            _ = first.Draw(out ushort velocityZ);
            Assert.Equal((ushort)((size & 2047) + 1024), actual.Frame.State.ReadWord(bank + 6));
            Assert.Equal(unchecked((ushort)(x % 200 - 100)), actual.Frame.State.ReadWord(bank + 8));
            Assert.Equal(unchecked((ushort)(y % 200 - 100)), actual.Frame.State.ReadWord(bank + 10));
            Assert.Equal((ushort)(z % 100), actual.Frame.State.ReadWord(bank + 12));
            Assert.Equal(unchecked((ushort)(unchecked(NativeBattleCameraMath.Cosine(angle & 4095) * (speed % 110 + 90)) >> 12)), actual.Frame.State.ReadWord(bank + 16));
            Assert.Equal(unchecked((ushort)(unchecked(NativeBattleCameraMath.Sine(angle & 4095) * (speed % 110 + 90)) >> 12)), actual.Frame.State.ReadWord(bank + 18));
            Assert.Equal((ushort)(velocityZ % 40 + 20), actual.Frame.State.ReadWord(bank + 20));
        }
        if (callback == 0x60D850)
        {
            Assert.Equal((ushort)102, actual.Frame.State.ReadWord(bank + 8));
            Assert.Equal((ushort)101, actual.Frame.State.ReadWord(bank + 24 + 8));
            Assert.Equal((ushort)100, actual.Frame.State.ReadWord(bank + 48 + 8));
        }
        NativeBattleCannonBlow151Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCannonBlow151Frame>(NativeBattleTestSnapshots.Json(actual.Frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Advance(actual.Frame, callback, Inputs()).Frame), NativeBattleTestSnapshots.Json(Advance(restored, callback, Inputs()).Frame));
    }

    [Theory]
    [InlineData(0x60D3B0u, 0x244E008u)]
    [InlineData(0x60D850u, 0x244D6A8u)]
    [InlineData(0x60DA10u, 0x244D6A8u)]
    [InlineData(0x60DC20u, 0x244E008u)]
    public void Negative_draw_feedback_retires_only_unpaused_existing_particles(uint callback, uint bank)
    {
        NativeBattleCannonBlow151Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, 3).WriteWord(0x18, 2).WriteWord(0x1C, 32));
        frame = frame with { State = frame.State.WriteDword(bank, 2).WriteWord(bank + 6, 4096).WriteWord(bank + 12, 1000) };
        NativeBattleCannonBlow151Inputs paused = Inputs(0x201, Reject);
        var unchanged = Advance(frame, callback, paused);
        Assert.Equal(0, unchanged.Result);
        Assert.Equal(frame.Random, unchanged.Frame.Random);
        Assert.Equal(2u, unchanged.Frame.State.ReadDword(bank));
        Assert.Equal((ushort)0, unchanged.Frame.State.ReadWord(bank + 4));
        var retired = Advance(frame, callback, Inputs(0, Reject));
        Assert.Equal(2, retired.Result);
        Assert.Equal(0u, retired.Frame.State.ReadDword(bank));
        Assert.Equal((ushort)1, retired.Frame.State.ReadWord(bank + 4));

        static NativeBattleCannonBlow151Frame Reject(NativeBattleCannonBlow151Frame current, NativeBattleCannonBlow151Operation operation)
        {
            NativeBattleCannonBlow151DrawSprite180 draw = Assert.IsType<NativeBattleCannonBlow151DrawSprite180>(operation);
            return PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 180).Write(0x28, BitConverter.GetBytes((short)-1)), 180);
        }
    }

    [Fact]
    public void Traveling_sprite_uses_current_sample_then_updates_index_and_retires_at_32()
    {
        NativeBattleCannonBlow151Frame frame = Frame(0x60D790);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(0x18, 31).WriteWord(0x1A, 4).WriteWord(0x1C, 4096).WriteWord(12, 7));
        int submissions = 0;
        var actual = Advance(frame, 0x60D790, Inputs(0, (current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleCannonBlow151DrawSprite180>(operation);
            Assert.Equal(0xDBF2A0u, draw.SourceAddress);
            Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16LittleEndian(Context(current, draw.TemporaryAddress, 180).Read(4, 2)));
            submissions++;
            return current;
        }));
        Assert.Equal(1, submissions);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)35, Read(actual.Frame, TaskAddress).ReadWord(0x18));
        Assert.Equal((ushort)3, Read(actual.Frame, TaskAddress).ReadWord(0x1A));
    }

    private static (NativeBattleCannonBlow151Frame Frame, int Result) Advance(NativeBattleCannonBlow151Frame frame, uint callback, NativeBattleCannonBlow151Inputs inputs) => callback switch
    {
        0x60D260 or 0x60D670 or 0x60DE40 or 0x60E060 => AdvanceMesh(frame, TaskAddress, null!, inputs),
        0x60D790 or 0x60E1B0 => AdvanceSprite(frame, TaskAddress, null!, inputs),
        _ => AdvanceSlots(frame, TaskAddress, null!, inputs)
    };

    private static NativeBattleCannonBlow151Frame Frame(uint callback)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x244E9E8, TaskAddress, 36, 100);
        (pool, _) = pool.Append(callback);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(0x10, 100).WriteWord(0x12, 0).WriteWord(0x14, 300)) });
        NativeBattleOriginalEffectState effect = new(151, 150, new(151, 0x60C9C0, 0x60C9A0), null, 0x244D698) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleCannonBlow151Frame frame = new(memory, effect, new(123), NativeBattleCannonBlow151State.FromColdProcess() with { RootBankStorage = null, ChildBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 0, Z = 300 }, null);
        frame = frame with { State = frame.State.WriteView(0x244F8A8, PackedArena(matrix), 32) };
        for (int index = 0; index < 32; index++)
        {
            frame = frame with { State = frame.State.WriteWord(0x244F958u + (uint)(index * 8), (ushort)(100 + index)).WriteWord(0x244F95Cu + (uint)(index * 8), 300) };
        }
        return frame;
    }
    private static NativeBattleCannonBlow151Inputs Inputs(uint flags = 0, Func<NativeBattleCannonBlow151Frame, NativeBattleCannonBlow151Operation, NativeBattleCannonBlow151Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, _) => NativeBattleCuragaMatrices.Identity8DD770(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute ?? ((current, operation) => operation is NativeBattleCannonBlow151DrawSprite180 draw ? PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 180).Write(0x28, new byte[2]), 180) : current));
}
