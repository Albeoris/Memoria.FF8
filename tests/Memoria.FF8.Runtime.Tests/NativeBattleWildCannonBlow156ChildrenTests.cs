using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleWildCannonBlow156Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWildCannonBlow156ChildrenTests
{
    private const uint TaskAddress = 0x23EF488;

    [Theory]
    [InlineData(0x605DF0u, 3, 0xDB6DFCu, 0x33u, null)]
    [InlineData(0x605DF0u, 4, 0xDB6DFCu, 0xF3u, 0)]
    [InlineData(0x606200u, 0, 0xDB68ECu, 0xF3u, 0)]
    [InlineData(0x6067B0u, 7, 0xDB54ACu, 3u, null)]
    [InlineData(0x6067B0u, 8, 0xDB54ACu, 0xC3u, 0)]
    [InlineData(0x606910u, 2, 0xDB7484u, 0x33u, null)]
    [InlineData(0x606910u, 3, 0xDB7484u, 0xF3u, 0)]
    [InlineData(0x606910u, 13, 0xDB7484u, 0xF3u, 4090)]
    public void Mesh_submits_actual_source_and_retained_context_before_pause(uint callback, short age, uint source, uint flags, int? cue)
    {
        NativeBattleWildCannonBlow156Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, unchecked((ushort)age)).WriteWord(0x1C, 4096));
        NativeBattleEffectArena? submitted = null;
        NativeBattleWildCannonBlow156Inputs inputs = Inputs(0x200, (current, operation) =>
        {
            NativeBattleWildCannonBlow156DrawMesh88 draw = Assert.IsType<NativeBattleWildCannonBlow156DrawMesh88>(operation);
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
    [InlineData(0x605DF0u, 11, -19, -18)]
    [InlineData(0x606200u, 3, -19, -19)]
    [InlineData(0x6067B0u, 16, -19, -16)]
    [InlineData(0x606910u, 13, -19, -17)]
    public void Mesh_completion_and_signed_damping_follow_each_callback(uint callback, short age, short speed, short expectedSpeed)
    {
        NativeBattleWildCannonBlow156Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, unchecked((ushort)age)).WriteWord(0x1C, 4096).WriteWord(0x1E, unchecked((ushort)speed)).WriteWord(0x1A, unchecked((ushort)-9)));
        var actual = Advance(frame, callback, Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Equal(expectedSpeed, unchecked((short)Read(actual.Frame, TaskAddress).ReadWord(0x1E)));
        Assert.Equal(callback == 0x606200 ? 4096 : 4077, Read(actual.Frame, TaskAddress).ReadWord(0x1C));
        if (callback == 0x6067B0)
        {
            Assert.Equal(unchecked((ushort)-9), Read(actual.Frame, TaskAddress).ReadWord(0x18));
            Assert.Equal(unchecked((ushort)-7), Read(actual.Frame, TaskAddress).ReadWord(0x1A));
        }
    }

    [Theory]
    [InlineData(0x605F40u, 0x23EEA98u, 12, 84)]
    [InlineData(0x6063E0u, 0x23ECE78u, 3, 3)]
    [InlineData(0x6065A0u, 0x23EDC88u, 3, 12)]
    [InlineData(0x606A60u, 0x23EEA98u, 1, 4)]
    public void Births_preserve_native_counts_random_order_and_unwritten_suffix(uint callback, uint bank, int births, int draws)
    {
        NativeBattleWildCannonBlow156Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(0x18, 2).WriteWord(0x1E, 2));
        frame = frame with { State = frame.State.WriteView(bank + 22, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        if (callback == 0x605F40)
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
        Assert.Equal(births, Enumerable.Range(0, callback == 0x6063E0 || callback == 0x6065A0 ? 150 : 100).Count(index => actual.Frame.State.ReadDword(bank + (uint)(index * 24)) == 2));
        Assert.Equal(0, actual.Result);
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadView(bank + 22, 2).Read(0, 2));
        if (callback == 0x605F40)
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
        if (callback == 0x6063E0)
        {
            Assert.Equal((ushort)102, actual.Frame.State.ReadWord(bank + 8));
            Assert.Equal((ushort)101, actual.Frame.State.ReadWord(bank + 24 + 8));
            Assert.Equal((ushort)100, actual.Frame.State.ReadWord(bank + 48 + 8));
        }
        NativeBattleWildCannonBlow156Frame restored = JsonSerializer.Deserialize<NativeBattleWildCannonBlow156Frame>(JsonSerializer.Serialize(actual.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(Advance(actual.Frame, callback, Inputs()).Frame), JsonSerializer.Serialize(Advance(restored, callback, Inputs()).Frame));
    }

    [Theory]
    [InlineData(0x605F40u, 0x23EEA98u)]
    [InlineData(0x6063E0u, 0x23ECE78u)]
    [InlineData(0x6065A0u, 0x23EDC88u)]
    [InlineData(0x606A60u, 0x23EEA98u)]
    public void Negative_draw_feedback_retires_only_unpaused_existing_particles(uint callback, uint bank)
    {
        NativeBattleWildCannonBlow156Frame frame = Frame(callback);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(12, 3).WriteWord(0x18, 2).WriteWord(0x1C, 32));
        frame = frame with { State = frame.State.WriteDword(bank, 2).WriteWord(bank + 6, 4096).WriteWord(bank + 12, 1000) };
        NativeBattleWildCannonBlow156Inputs paused = Inputs(0x201, Reject);
        var unchanged = Advance(frame, callback, paused);
        Assert.Equal(0, unchanged.Result);
        Assert.Equal(frame.Random, unchanged.Frame.Random);
        Assert.Equal(2u, unchanged.Frame.State.ReadDword(bank));
        Assert.Equal((ushort)0, unchanged.Frame.State.ReadWord(bank + 4));
        var retired = Advance(frame, callback, Inputs(0, Reject));
        Assert.Equal(2, retired.Result);
        Assert.Equal(0u, retired.Frame.State.ReadDword(bank));
        Assert.Equal((ushort)1, retired.Frame.State.ReadWord(bank + 4));

        static NativeBattleWildCannonBlow156Frame Reject(NativeBattleWildCannonBlow156Frame current, NativeBattleWildCannonBlow156Operation operation)
        {
            NativeBattleWildCannonBlow156DrawSprite180 draw = Assert.IsType<NativeBattleWildCannonBlow156DrawSprite180>(operation);
            return PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 180).Write(0x28, BitConverter.GetBytes((short)-1)), 180);
        }
    }

    [Fact]
    public void Traveling_sprite_uses_current_sample_then_updates_index_and_retires_at_32()
    {
        NativeBattleWildCannonBlow156Frame frame = Frame(0x606320);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(0x18, 31).WriteWord(0x1A, 4).WriteWord(0x1C, 4096).WriteWord(12, 7));
        int submissions = 0;
        var actual = Advance(frame, 0x606320, Inputs(0, (current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleWildCannonBlow156DrawSprite180>(operation);
            Assert.Equal(0xDB528Cu, draw.SourceAddress);
            Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16LittleEndian(Context(current, draw.TemporaryAddress, 180).Read(4, 2)));
            submissions++;
            return current;
        }));
        Assert.Equal(1, submissions);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)35, Read(actual.Frame, TaskAddress).ReadWord(0x18));
        Assert.Equal((ushort)3, Read(actual.Frame, TaskAddress).ReadWord(0x1A));
    }

    private static (NativeBattleWildCannonBlow156Frame Frame, int Result) Advance(NativeBattleWildCannonBlow156Frame frame, uint callback, NativeBattleWildCannonBlow156Inputs inputs) => callback switch
    {
        0x605DF0 => NativeBattleWildCannonBlow156Children.Advance605DF0(frame, TaskAddress, null!, inputs),
        0x606200 => NativeBattleWildCannonBlow156Children.Advance606200(frame, TaskAddress, null!, inputs),
        0x6067B0 => NativeBattleWildCannonBlow156Children.Advance6067B0(frame, TaskAddress, null!, inputs),
        0x606910 => NativeBattleWildCannonBlow156Children.Advance606910(frame, TaskAddress, null!, inputs),
        0x605F40 => NativeBattleWildCannonBlow156Children.Advance605F40(frame, TaskAddress, null!, inputs),
        0x606320 => NativeBattleWildCannonBlow156Children.Advance606320(frame, TaskAddress, null!, inputs),
        0x6063E0 => NativeBattleWildCannonBlow156Children.Advance6063E0(frame, TaskAddress, null!, inputs),
        0x6065A0 => NativeBattleWildCannonBlow156Children.Advance6065A0(frame, TaskAddress, null!, inputs),
        _ => NativeBattleWildCannonBlow156Children.Advance606A60(frame, TaskAddress, null!, inputs)
    };

    private static NativeBattleWildCannonBlow156Frame Frame(uint callback)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23EF478, TaskAddress, 36, 100);
        (pool, _) = pool.Append(callback);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(0x10, 100).WriteWord(0x12, 0).WriteWord(0x14, 300)) };
        NativeBattleOriginalEffectState effect = new(156, 155, new(156, 0x605530, 0x605510), null, 0x23ECE68) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleWildCannonBlow156Frame frame = new(memory, effect, new(123), NativeBattleWildCannonBlow156State.FromColdProcess() with { RootBankStorage = null, ChildBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 0, Z = 300 }, null);
        frame = frame with { State = frame.State.WriteView(0x23F0338, PackedArena(matrix), 32) };
        for (int index = 0; index < 32; index++)
        {
            frame = frame with { State = frame.State.WriteWord(0x23F03E8u + (uint)(index * 8), (ushort)(100 + index)).WriteWord(0x23F03ECu + (uint)(index * 8), 300) };
        }
        return frame;
    }
    private static NativeBattleWildCannonBlow156Inputs Inputs(uint flags = 0, Func<NativeBattleWildCannonBlow156Frame, NativeBattleWildCannonBlow156Operation, NativeBattleWildCannonBlow156Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, _) => NativeBattleCuragaMatrices.Identity8DD770(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute ?? ((current, operation) => operation is NativeBattleWildCannonBlow156DrawSprite180 draw ? PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 180).Write(0x28, new byte[2]), 180) : current));
}
