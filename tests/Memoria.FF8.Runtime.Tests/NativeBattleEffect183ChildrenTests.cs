using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect183Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect183ChildrenTests
{
    private const uint Address = 0x22F3400;
    private static readonly NativeBattleEffect183Resources Resources = new(null!, null!, [0x808080, 0x608080, 0x408080, 0x406080, 0x404080, 0x604080, 0x804080, 0x804060, 0x804040, 0x806060], 0x49490);

    [Theory]
    [InlineData(false, 0, 0xF3u, 4096)]
    [InlineData(false, 3, 0xF3u, 1024)]
    [InlineData(false, 4, 0x33u, null)]
    [InlineData(false, 15, 0x33u, null)]
    [InlineData(true, 0, 0xF3u, 4096)]
    [InlineData(true, 4, 0x33u, null)]
    [InlineData(true, 15, 0x33u, null)]
    [InlineData(true, 16, 0xF3u, 0)]
    [InlineData(true, 19, 0xF3u, 3072)]
    public void Windowed_context_preserves_retained_fields_and_exact_policy(bool second, short age, uint flags, int? fade)
    {
        NativeBattleEffect183Frame frame = Frame(second, age);
        int calls = 0;
        var step = Advance(frame, second, Inputs(0x200, (current, operation) =>
        {
            NativeBattleEffect183DrawWindowed144 draw = Assert.IsType<NativeBattleEffect183DrawWindowed144>(operation);
            uint expectedSource = second ? 0xD34CE4u : 0xD348C4u;
            Assert.Equal(expectedSource, draw.SourceAddress);
            NativeBattleEffectArena context = Context(current, draw.TemporaryAddress, 144);
            Assert.Equal(expectedSource, Dword(context, 0));
            Assert.Throws<NotSupportedException>(() => context.Read(4, 4));
            Assert.Equal(0u, Dword(context, 8));
            Assert.Equal(0u, Dword(context, 16));
            Assert.Equal(age * (second ? -6 : -10), unchecked((int)Dword(context, 20)));
            Assert.Equal((ushort)(second ? 64 : 0), Word(context, 24));
            Assert.Equal((ushort)0, Word(context, 26));
            Assert.Equal((ushort)64, Word(context, 28));
            Assert.Equal((ushort)128, Word(context, 30));
            Assert.Equal(flags, Dword(context, 32));
            Assert.Equal(second ? Resources.Colors[age % 10] : 0x808080u, Dword(context, 36));
            if (fade.HasValue)
            {
                Assert.Equal(fade.Value, unchecked((int)Dword(context, 12)));
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => context.Read(12, 4));
            }
            Assert.Throws<NotSupportedException>(() => context.Read(40, 1));
            Assert.Null(current.Memory.GeometryRegisters!.Rotation20[18]);
            Assert.Null(current.Memory.GeometryRegisters.Rotation20[19]);
            Assert.Equal(new int?[] { 110, 220, second ? 530 : -270 }, current.Memory.GeometryRegisters.Translation);
            Assert.Equal(NativeBattleTemporaryMemory.Address + 144, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            calls++;
            return PublishContext(current, draw.TemporaryAddress, context.Write(40, BitConverter.GetBytes(0xCAFE1234u)), 144);
        }));
        Assert.Equal(1, calls);
        Assert.Equal(0, step.Result);
        Assert.Equal(Read(frame, Address), Read(step.Frame, Address));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        Assert.Equal(0xCAFE1234u, Dword(Context(step.Frame, NativeBattleTemporaryMemory.Address, 144), 40));
    }

    [Theory]
    [InlineData(false, 14, 0)]
    [InlineData(false, 15, 2)]
    [InlineData(true, 18, 0)]
    [InlineData(true, 19, 2)]
    public void Draw_precedes_mutation_and_completion_and_paused_draw_retains_task(bool second, short age, int result)
    {
        NativeBattleEffect183Frame frame = Frame(second, age);
        frame = Write(frame, Address, Read(frame, Address).WriteWord(0x1C, unchecked((ushort)-32760)).WriteWord(0x1E, 20));
        int calls = 0;
        NativeBattleEffect183Inputs inputs = Inputs(0, (current, _) =>
        {
            Assert.Equal(unchecked((ushort)age), Read(current, Address).ReadWord(12));
            Assert.Equal(unchecked((ushort)-32760), Read(current, Address).ReadWord(0x1C));
            calls++;
            return current;
        });
        NativeBattleEffect183Frame pausedFrame = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        var step = Advance(frame, second, inputs);
        Assert.Equal(1, calls);
        Assert.Equal(result, step.Result);
        Assert.Equal((ushort)(age + 1), Read(step.Frame, Address).ReadWord(12));
        Assert.Equal(unchecked((ushort)(second ? -32760 : -32780)), Read(step.Frame, Address).ReadWord(0x1C));
        Assert.Equal((ushort)20, Read(step.Frame, Address).ReadWord(0x1E));
        var paused = Advance(pausedFrame, second, inputs with { GraphicsFlags = 0x201 });
        Assert.Equal(2, calls);
        Assert.Equal(0, paused.Result);
        Assert.Equal(Read(pausedFrame, Address), Read(paused.Frame, Address));
        NativeBattleEffect183Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect183Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(step.Frame)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Advance(step.Frame, second, Inputs()).Frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Advance(restored, second, Inputs()).Frame)));
    }

    [Fact]
    public void Heading_rotates_offset_before_scale_and_view_translation()
    {
        NativeBattleEffect183Frame frame = Frame(false, 4);
        frame = Write(frame, Address, Read(frame, Address).WriteWord(0x18, 1024).WriteWord(0x1C, 2048));
        _ = Advance(frame, false, Inputs(0x200, (current, _) =>
        {
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, 1024, 0));
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, -600));
            Assert.Equal(new int?[] { 110 + offset.X, 220, 330 + offset.Z }, current.Memory.GeometryRegisters!.Translation);
            Assert.Equal(2048, NativeBattleGeometry.CaptureMatrix(current.Memory.GeometryRegisters).Rotation.Select(value => Math.Abs((int)value)).Max());
            return current;
        }));
    }

    private static NativeBattleEffect183Frame Frame(bool second, short age)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22F33F0, Address, 36, 1);
        (pool, _) = pool.Append(second ? 0x5C9DD0u : 0x5C9C40u);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteWord(12, unchecked((ushort)age)).WriteWord(16, 10).WriteWord(18, 20).WriteWord(20, 30).WriteWord(24, 0).WriteWord(28, 4096).WriteWord(30, 512);
        pool = pool.WriteSlot(0, task);
        NativeBattleOriginalEffectState effect = new(183, 182, new(183, 0x5C9A50, 0x5C9A30), null, 0x22F33E0) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        return new(memory, effect, NativeBattleEffect183State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect183Inputs Inputs(uint flags = 0, Func<NativeBattleEffect183Frame, NativeBattleEffect183Operation, NativeBattleEffect183Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 }, null), _ => new(null, 100, 160, 108), (_, _, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute ?? ((current, _) => current));
    private static (NativeBattleEffect183Frame Frame, int Result) Advance(NativeBattleEffect183Frame frame, bool second, NativeBattleEffect183Inputs inputs) => second ? NativeBattleEffect183Children.Advance5C9DD0(frame, Address, Resources, inputs) : NativeBattleEffect183Children.Advance5C9C40(frame, Address, Resources, inputs);
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(context.Read(offset, 2));
}
