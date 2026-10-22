using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshZantetsukenDirectorTests
{
    [Fact]
    public void Missing_attack_file_skips_load_without_publishing_file_zero()
    {
        List<NativeBattleGilgameshDirectorOperation> operations = [];
        NativeBattleGilgameshDirectorStep step = NativeBattleGilgameshDirector.Advance592300(Task(238), new(0, () => { }, () => throw new InvalidOperationException(), operations.Add) { Effect = 327 });
        Assert.Empty(operations);
        Assert.Equal((ushort)239, step.Task.ReadWord(12));
    }

    [Theory]
    [InlineData(327, false)]
    [InlineData(328, true)]
    [InlineData(329, true)]
    [InlineData(330, true)]
    public void Only_existing_attack_files_require_their_pending_gate_before_common_file753(ushort effect, bool blocked)
    {
        List<NativeBattleGilgameshDirectorOperation> operations = [];
        int pendingReads = 0;
        NativeBattleGilgameshDirectorStep step = NativeBattleGilgameshDirector.Advance592300(Task(253), new(0, () => { }, () => { pendingReads++; return -1; }, operations.Add) { Effect = effect });
        Assert.Equal(blocked ? 1 : 0, pendingReads);
        Assert.Equal(blocked ? (ushort)253 : (ushort)254, step.Task.ReadWord(12));
        if (blocked)
        {
            Assert.Empty(operations);
        }
        else
        {
            NativeBattleGilgameshDirectorOperation file = Assert.Single(operations);
            Assert.Equal(NativeBattleGilgameshDirectorService.File, file.Service);
            Assert.Equal(753, file.First);
            Assert.True(file.UseWorkBuffer);
        }
    }

    [Fact]
    public void Variant_zero_still_waits_for_actual_preceding_file_at237()
    {
        NativeBattleModulePoolTask task = Task(237);
        NativeBattleGilgameshDirectorStep step = NativeBattleGilgameshDirector.Advance592300(task, new(0, () => { }, () => -1, _ => throw new InvalidOperationException()) { Effect = 327 });
        Assert.Same(task, step.Task);
    }

    private static NativeBattleModulePoolTask Task(ushort counter) => new NativeBattleModulePoolTask(1, 0, 0, 0x592300, new byte[24].ToImmutableArray()).WriteWord(12, counter);
}
