using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect64Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.Runtime.Tests;

internal static class NativeBattleEffect64LowerFixture
{
    internal const uint Common = 0x20DFAB8;
    internal const uint Row = Common + 0x330;
    internal const uint Script = 0x134B560;
    internal const uint TaskAddress = Common;

    internal static NativeBattleEffect64Frame Frame(params short[] words)
    {
        NativeBattleEffectTaskPool scripts = NativeBattleEffectTaskPool.Initialize(ScriptDescriptor, Common, ScriptStride, 18);
        (scripts, _) = scripts.Append(0x6E5DD0);
        NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, Common + ParticleOffset, ParticleStride, 1);
        NativeBattleEffectTaskPool programmes = NativeBattleEffectTaskPool.Initialize(ProgrammeDescriptor, Common + ProgrammeOffset, ProgrammeStride, 1);
        NativeBattleOriginalEffectState effect = new(64, 63, new(64, Entry, Initializer), null, null) { Pools = [scripts, particles, programmes] };
        byte[] bytes = new byte[ScriptLengths[Script]];
        for (int index = 0; index < words.Length; index++)
        {
            BitConverter.GetBytes(words[index]).CopyTo(bytes, index * 2);
        }
        NativeBattleEffect64State state = NativeBattleEffect64State.FromColdProcess() with { ScriptSources = ImmutableDictionary<uint, NativeBattleEffectArena>.Empty.Add(Script, NativeBattleEffectArena.Clear().Write(0, bytes)) };
        state = state.WriteDword(ArenaGlobal, Common);
        NativeBattleEffect64Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(123), state);
        return Write(frame, TaskAddress, Read(frame, TaskAddress).WriteDword(0xC, 0x1D975CC).WriteDword(0x10, Row).WriteDword(0x14, Row).WriteDword(0x18, Script));
    }

    internal static NativeBattleEffect64Resources Resources() => new(new("texture", []), new("camera", []), new("vibration", []), 0, [], ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
    internal static NativeBattleEffect64Inputs Inputs(List<string>? calls = null) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, null!, null!, (frame, model, bone, distance, retained) =>
    {
        calls?.Add($"bone:{bone}:{distance}");
        return new(frame, retained.Write(0, new byte[] { 10, 0, 20, 0, 30, 0 }));
    }, (frame, operation) =>
    {
        calls?.Add(operation.GetType().Name);
        return frame;
    });
}

public sealed class NativeBattleEffect64LowerTests
{
    [Fact]
    public void Bone_offset_addition_wraps_words_and_preserves_unknown_W()
    {
        NativeBattleEffect64Frame frame = NativeBattleEffect64LowerFixture.Frame();
        frame = WriteBytes(frame, NativeBattleEffect64LowerFixture.Row + 16, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)32767)).Write(2, BitConverter.GetBytes((short)-7)).Write(4, BitConverter.GetBytes((short)9)), 6);
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes((short)2)).Write(2, BitConverter.GetBytes((short)3)).Write(4, BitConverter.GetBytes((short)-4));
        NativeBattleEffectArena result = NativeBattleEffect64Programme.AddOffset(frame, point, NativeBattleEffect64LowerFixture.Row, 1);
        Assert.Equal(new byte[] { 1, 128, 252, 255, 5, 0 }, result.Read(0, 6));
        Assert.DoesNotContain(true, result.ReadSnapshot(6, 2).Known);
    }
}
