using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect70RingAnchorTests
{
    [Fact]
    public void Negative_parent_returns_one_without_reading_world_or_overwriting_retained_unknown_translation()
    {
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(5, [0xA5]);
        NativeBattleEffect70RingAnchorStep step = NativeBattleEffect70RingAnchor.Resolve6DFC70(new(0, 2), Source(), 0, output);
        Assert.Equal(1, step.Result);
        Assert.Same(output, step.Output12);
        Assert.Throws<NotSupportedException>(() => step.Output12.Read(0, 4));
        Assert.Equal((byte)0xA5, step.Output12.Read(5, 1)[0]);
    }

    [Fact]
    public void Valid_bone_rotates_full_dword_translation_then_halves_toward_zero_and_adds_world_translation()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix bone = identity with { X = 70001, Y = -70003, Z = 70005 };
        NativeBattleGraphicsModel model = new(0, 2)
        {
            GeometryWorldMatrix = new([0, 4096, 0, -4096, 0, 0, 0, 0, 4096], 100, -100, 200, 4096),
            CurrentBodyBoneMatrices = new(Convert.ToHexString(SHA256.HashData(source.Skeleton.AsSpan())), [identity, bone])
        };
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(12, [0x11, 0x22, 0x33, 0x44]);
        NativeBattleEffect70RingAnchorStep step = NativeBattleEffect70RingAnchor.Resolve6DFC70(model, source, 1, output);
        Assert.Equal(0, step.Result);
        Assert.Equal(-34901, Dword(step.Output12, 0));
        Assert.Equal(-35100, Dword(step.Output12, 4));
        Assert.Equal(35202, Dword(step.Output12, 8));
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, step.Output12.Read(12, 4));
        Assert.Equal(70001, model.CurrentBodyBoneMatrices.Matrices[1].X);
    }

    [Fact]
    public void Nonroot_requires_the_current_rig_publication_and_valid_selector()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear();
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect70RingAnchor.Resolve6DFC70(new(0, 2), source, 1, output));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect70RingAnchor.Resolve6DFC70(new(0, 2), source, -1, output));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect70RingAnchor.Resolve6DFC70(new(0, 2), source, 2, output));
    }

    private static int Dword(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.Read(offset, 4));

    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        skeleton[5] = 0x81;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), 0);
        return new(skeleton.ToImmutableArray(), [1]);
    }
}
