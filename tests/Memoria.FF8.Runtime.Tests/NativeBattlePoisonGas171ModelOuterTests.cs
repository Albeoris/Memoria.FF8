using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePoisonGas171ModelOuterTests
{
    [Fact]
    public void Outer_keeps_actual_service_order_partial_temporary_and_linked_restore_boundary()
    {
        List<string> calls = [];
        NativeBattlePoisonGas171ModelFrame frame = Frame(4);
        NativeBattlePoisonGas171ModelServices services = new(
            current =>
            {
                Assert.Equal(0xE2000210u, Dword(current, 0x88));
                Assert.Equal(0xE2000000u, Dword(current, 0x8C));
                Assert.Equal(-4, unchecked((int)Dword(current, 0x84)));
                return Record(current, "shadow");
            },
            (current, world) =>
            {
                calls.Add("compose");
                Assert.Equal(100, world.X);
                Assert.Equal(0xCAFE, BinaryPrimitives.ReadUInt16LittleEndian(current.Temporary176.Read(0x6C, 2)));
                Assert.Throws<NotSupportedException>(() => current.Temporary176.Read(0xA2, 2));
                return current;
            },
            current =>
            {
                calls.Add("draw-common");
                Assert.Equal(0x196B000u, Dword(current, 4));
                Assert.Equal(0x82112233u, Dword(current, 0x1C));
                Assert.Equal(0x12345678u, Dword(current, 0x20));
                Assert.Equal(new byte[] { 60, 60, 60 }, current.Temporary176.Read(0x28, 3));
                Assert.Equal(0xCAFEBABEu, Dword(current, 0x10));
                Assert.Equal(0u, Dword(current, 0x14));
                Assert.Equal(320, BinaryPrimitives.ReadUInt16LittleEndian(current.Temporary176.Read(0x18, 2)));
                Assert.Equal(216, BinaryPrimitives.ReadUInt16LittleEndian(current.Temporary176.Read(0x1A, 2)));
                Assert.Equal(0u, Dword(current, 0x34));
                return current;
            },
            current => Record(current, "restore"),
            current =>
            {
                calls.Add("linked-compose-draw-restore");
                Assert.Equal(uint.MaxValue, Dword(current, 0x20));
                Assert.Equal(2100, unchecked((int)Dword(current, 0xA4)));
                return current;
            });
        _ = NativeBattlePoisonGas171ModelDrawing.DrawOuter(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 2000 }, null), 0x196B000, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0xCAFEBABEu)), true, services);
        Assert.Equal(new[] { "shadow", "compose", "draw-common", "restore", "linked-compose-draw-restore" }, calls);

        NativeBattlePoisonGas171ModelFrame Record(NativeBattlePoisonGas171ModelFrame current, string name)
        {
            calls.Add(name);
            return current;
        }
    }

    [Theory]
    [InlineData(0x24, false)]
    [InlineData(0x224, true)]
    public void Disabled_shadow_and_absent_or_suppressed_linked_source_are_lazy(ushort flags, bool linked)
    {
        int draws = 0;
        NativeBattlePoisonGas171ModelServices services = new(_ => throw new InvalidOperationException("Shadow must remain uncalled."), (current, _) => current, current =>
        {
            draws++;
            return current;
        }, current => current, null);
        NativeBattlePoisonGas171ModelFrame result = NativeBattlePoisonGas171ModelDrawing.DrawOuter(Frame(flags), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), 0x196B000, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), linked, services);
        Assert.Equal(1, draws);
        Assert.Throws<NotSupportedException>(() => result.Temporary176.Read(0x10, 4));
    }

    [Fact]
    public void Reached_missing_linked_service_fails_after_actual_primary_restore()
    {
        bool restored = false;
        NativeBattlePoisonGas171ModelServices services = new(current => current, (current, _) => current, current => current, current =>
        {
            restored = true;
            return current;
        }, null);
        Assert.Throws<NotSupportedException>(() => NativeBattlePoisonGas171ModelDrawing.DrawOuter(Frame(4), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), 0x196B000, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), true, services));
        Assert.True(restored);
    }

    private static uint Dword(NativeBattlePoisonGas171ModelFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Temporary176.Read(offset, 4));
    private static NativeBattlePoisonGas171ModelFrame Frame(ushort flags) => new(NativeBattleEffectMemory.FromColdProcess(), NativeBattleFileStaging.Unknown(), new(3, flags)
    {
        GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100 },
        Color40 = 0x82112233,
        Alpha7 = 60,
        SequenceState = new() { MeshMask124 = 0x12345678 }
    }, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 176).ToArray()).Write(0x6C, BitConverter.GetBytes((ushort)0xCAFE)).Write(0x34, new byte[4]).Write(0x38, BitConverter.GetBytes((short)128)).Write(0x3A, BitConverter.GetBytes((short)128)).Write(0x30, BitConverter.GetBytes(-132)));
}
