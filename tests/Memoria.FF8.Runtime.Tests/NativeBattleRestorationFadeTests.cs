using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationFadeTests
{
    [Theory]
    [InlineData((ushort)24, true)]
    [InlineData((ushort)24, false)]
    [InlineData((ushort)27, true)]
    [InlineData((ushort)27, false)]
    public void Shared_original_fades_publish_exact_writes_and_finish_after_five_callbacks(ushort effect, bool fadeIn)
    {
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(profile.Descriptors[0], profile.Banks[0], 100, 2), NativeBattleEffectTaskPool.Initialize(profile.Descriptors[1], profile.Banks[1], 88, 4), NativeBattleEffectTaskPool.Initialize(profile.Descriptors[2], profile.Banks[2], 676, 3), NativeBattleEffectTaskPool.Initialize(profile.Descriptors[3], profile.Banks[3], 64, 10)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.MainCallback, 100);
        NativeBattleEffectHierarchyAppend child = root.Hierarchy.Append8DC540(3, fadeIn ? profile.FadeInCallback : profile.FadeOutCallback, 64, root.Address, (_, _, _) => new(0, 0));
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), profile.Descriptors[0]) { Pools = child.Hierarchy.Pools };
        NativeBattleRestorationFrame frame = new(NativeBattleEffectMemory.FromColdProcess(), selected, new(0, null, null, 0, null));
        List<NativeBattleStageFadeWrite> writes = [];
        for (int callback = 0; callback < 5; callback++)
        {
            NativeBattleRestorationFrame clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleRestorationFrame>(NativeBattleTestSnapshots.Json(frame))!;
            NativeBattleRestorationLeafStep step = NativeBattleRestorationFade.Advance(frame, child.Address, (current, write) =>
            {
                writes.Add(write);
                return current;
            });
            Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleRestorationFade.Advance(clone, child.Address, (current, _) => current)));
            Assert.Equal(callback == 4 ? 2 : 0, step.Result);
            frame = step.Frame;
        }
        Assert.Equal(fadeIn ? new short[] { 0, 256, 512, 768, 1024 } : [768, 512, 256, 0], writes.Select(write => write.Fade));
        Assert.Equal(fadeIn, writes[0].ClearRgb);
        Assert.All(writes.Skip(1), write => Assert.False(write.ClearRgb));
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(root.Address).ReadByte(0x28));
    }

    [Theory]
    [InlineData(true, (ushort)0x7FFF, (short)-32513, false)]
    [InlineData(false, (ushort)0x8000, (short)32512, false)]
    [InlineData(true, (ushort)768, (short)1024, true)]
    [InlineData(false, (ushort)0, (short)0, true)]
    public void Stage_WORD_math_wraps_before_the_signed_clamp(bool fadeIn, ushort initial, short expected, bool complete)
    {
        NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 64, 1).Slots[0].WriteByte(0x29, 1).WriteWord(0x1C, initial);
        NativeBattleStageFadeStep step = NativeBattleStageFadeTask.Advance(task, fadeIn);
        Assert.Equal(expected, step.Write!.Fade);
        Assert.Equal(complete, (step.Task.ReadByte(0x26) & 1) != 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleStageFadeTask.Advance(task.WriteByte(0x29, 255), fadeIn));
    }
}
