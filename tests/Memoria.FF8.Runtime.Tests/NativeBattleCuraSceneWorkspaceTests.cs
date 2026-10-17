using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuraSceneWorkspaceTests
{
    [Fact]
    public void Initializer_reuses_the_instruction_equivalent_cache_layout_and_preserves_uncleared_tail()
    {
        NativeBattleGfAnimationSource source = Source(0x40);
        NativeBattleGfAnimationWorkspace old = new(Enumerable.Repeat((byte)0xA5, 40).ToImmutableArray());
        NativeBattleGfAnimationWorkspace cura = old.Initialize88A7D0(source, 23);
        Assert.Equal(old.Initialize7016B0(source, 23).Data.ToArray(), cura.Data.ToArray());
        Assert.Equal(0x10000000, Read(cura.Data, 8));
        Assert.All(cura.Data.Skip(20), value => Assert.Equal((byte)0xA5, value));
    }

    [Fact]
    public void Morph_keys_use_DWORD_spaced_frame_selectors_but_the_native_twenty_byte_scan_stride()
    {
        byte[] data = Source(0x2000).Data.ToArray();
        Write(data, 32, 16);
        Write(data, 40, 0);
        Write(data, 44, 0x1111FFFF);
        Write(data, 48, 0x22220003);
        Write(data, 52, 7 << 16);
        Write(data, 56, 2 << 16);
        Write(data, 60, -1);
        NativeBattleGfAnimationSource source = new(0x161194C, data.ToImmutableArray());
        NativeBattleGfAnimationWorkspace workspace = new(new byte[24].ToImmutableArray());
        workspace = workspace.Initialize88A7D0(source, 24);
        NativeBattleGfAnimationNode? first = null;
        NativeBattleGfAnimationAdvance step = workspace.Advance88A980(source, (current, node) =>
        {
            first = node;
            return current;
        });
        Assert.Equal((short)-1, first!.Word40);
        Assert.Equal((short)3, first.Word42);
        Assert.Equal((short)7, first.Scalar38);
        Assert.Equal(2 << 16, Read(step.Workspace.Data, 16));
        Assert.Equal(-1, Read(step.Workspace.Data, 20));
        NativeBattleGfAnimationWorkspace restored = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(step.Workspace))!;
        NativeBattleGfAnimationAdvance next = restored.Advance88A980(source, (current, node) =>
        {
            Assert.Equal((short)8, node.Scalar38);
            Assert.Equal((short)3, node.Word42);
            return current;
        });
        Assert.Equal(2, next.Workspace.Frame);
        Assert.Equal(1, next.Remaining);
    }

    [Fact]
    public void Unfrozen_vector_and_color_channels_preserve_the_current_shared_kernel()
    {
        NativeBattleGfAnimationSource source = Source(0x40);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[20].ToImmutableArray());
        workspace = workspace.Initialize88A7D0(source, 20);
        NativeBattleGfAnimationNode? cura = null;
        NativeBattleGfAnimationNode? gf = null;
        var first = workspace.Advance88A980(source, (current, node) =>
        {
            cura = node;
            return current;
        });
        var equivalent = workspace.Advance701970(source, 0, (current, node) =>
        {
            gf = node;
            return current;
        });
        Assert.Equal(cura!.Data.ToArray(), gf!.Data.ToArray());
        Assert.Equal(new NativeBattleVector(4096, 4096, 4096), cura.Vector24);
        Assert.Equal(first.Workspace.Data.ToArray(), equivalent.Workspace.Data.ToArray());
    }

    private static NativeBattleGfAnimationSource Source(uint flags)
    {
        byte[] bytes = new byte[80];
        Write(bytes, 4, 8);
        Write(bytes, 8, 1);
        Write(bytes, 12, 3);
        Write(bytes, 16, 16);
        Write(bytes, 24, 0x1000);
        Write(bytes, 28, unchecked((int)flags));
        if ((flags & 0x40) != 0)
        {
            Write(bytes, 32, 16);
            Write(bytes, 40, -1);
        }
        return new(0x161194C, bytes.ToImmutableArray());
    }

    private static void Write(byte[] data, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
    private static int Read(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset));
}
