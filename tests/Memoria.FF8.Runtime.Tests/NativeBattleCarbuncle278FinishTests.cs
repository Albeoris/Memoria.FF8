using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278FinishTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(86, 1)]
    [InlineData(145, 2)]
    [InlineData(215, 3)]
    [InlineData(0, null)]
    [InlineData(144, null)]
    [InlineData(146, null)]
    [InlineData(214, null)]
    [InlineData(216, null)]
    public void Sound_selectors_keep_all_four_exact_native_counter_boundaries(ushort counter, int? expected)
    {
        Assert.Equal(expected, NativeBattleCarbuncle278Finish.SoundIndex681ACE(counter));
    }

    [Fact]
    public void Restoration_changes_only_party_hide_bits_and_loaded_enemy_Z_words()
    {
        ImmutableArray<NativeBattleGraphicsModel> source = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, index < 3 ? (ushort)7 : index == 3 ? (ushort)3 : (ushort)1) { Transform = index == 3 ? new(new(1, 2, 3), new(4, 5, -700), 6, 7, 8) : null }).ToImmutableArray();
        ImmutableArray<NativeBattleGraphicsModel> restored = NativeBattleCarbuncle278Finish.Restore681199(source, -500);
        Assert.All(restored.Take(3), model => Assert.Equal((ushort)3, model.Flags0));
        Assert.Equal((short)-200, restored[3].Transform!.Position28.Z);
        Assert.Equal((short)4, restored[3].Transform!.Position28.X);
        Assert.Equal((short)-700, source[3].Transform!.Position28.Z);
        Assert.Same(source[4], restored[4]);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Assert.Equal((byte)255, NativeBattleCarbuncle278Finish.WriteCompletion508660(memory, 0x25081D8, 255).Carbuncle278!.Done81D8);
        Assert.Equal((byte)0, memory.Carbuncle278!.Done81D8);
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Finish.WriteCompletion508660(memory, 0x25081D9, 1));
    }

    [Fact]
    public void Return_camera_uses_current_anchor_then_fixed_pose_and_later_eye_target_deltas()
    {
        NativeBattleCameraState source = NativeBattleCameraState.Initialize() with { CurrentPose = new(new(20, -30, 40), new(0, 0, 0), 288, 0), FocalLength = -123, Roll = 17, EyePaddingF6 = 7, TargetPaddingFE = 9 };
        NativeBattleCameraState first = NativeBattleCarbuncle278Camera.AdvanceReturnWindow(source, -200, 215, new(100, 200, 300));
        Assert.Equal(new NativeBattleCameraPoint(2, 200, 1764), first.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(100, 200, 300), first.CurrentPose.Target);
        Assert.Equal((short)-123, first.CurrentPose.FocalLength);
        Assert.Equal((short)17, first.CurrentPose.Roll);
        Assert.Equal((short)7, first.EyePaddingF6);
        Assert.Equal((short)9, first.TargetPaddingFE);
        NativeBattleCameraState fixedPose = NativeBattleCarbuncle278Camera.AdvanceReturnWindow(first, -200, 231, null);
        Assert.Equal(new NativeBattleCameraPoint(0, -300, -200), fixedPose.CurrentPose!.Target);
        Assert.Equal(new NativeBattleCameraPoint(0, -600, 1800), fixedPose.CurrentPose.Eye);
        Assert.Same(fixedPose, NativeBattleCarbuncle278Camera.AdvanceReturnWindow(fixedPose, -200, 246, null));
        NativeBattleCameraState later = NativeBattleCarbuncle278Camera.AdvanceReturnWindow(fixedPose, -200, 247, null);
        Assert.Equal((short)-330, later.CurrentPose!.Target.Y);
        Assert.Equal((short)-654, later.CurrentPose.Eye.Y);
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Camera.AdvanceReturnWindow(source, -200, 230, null));
    }
}
