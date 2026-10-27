using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTripleControllerTests
{
    [Fact]
    public void Entry_uses_distinct_original_banks_without_hydrating_current_morph_or_skipped_TIM()
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleOriginalEffectState effect = new(21, 20, new(21, 0x89DEF0, 0x89DED0), new(0, 1, 8, 0, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(20) with { Textures = null, Arena = null };
        NativeBattleTripleFrame frame = NativeBattleTripleController.Initialize89DEF0(memory, effect, packets, Resources());
        Assert.Same(memory, frame.Memory);
        Assert.Equal(0x271B0F0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(new uint[] { 0x271B0F0, 0x2720898, 0x2720A20, 0x2720880, 0x271B010, 0x2721570 }, frame.Effect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.Equal(new uint[] { 0x271B028, 0x27208A8, 0x2720A30, 0x271B100, 0x2714F30, 0x2721430 }, frame.Effect.Pools.Select(pool => pool.BankAddress));
        Assert.Equal(new ushort[] { 100, 92, 64, 112, 124, 80 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 40, 200, 200, 4 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattleModulePoolTask root = frame.Effect.Pools[0].Slots[0];
        Assert.Equal(0x89DFF0u, root.Callback);
        Assert.Equal(0x1D99A78u, root.ReadDword(0x0C));
        Assert.Equal((byte)3, root.ReadByte(0x2D));
        Assert.Equal((ushort)0, root.ReadWord(0x58));
        Assert.Equal((ushort)1, root.ReadWord(0x5A));
        Assert.Null(frame.Memory.TripleMorph);
        Assert.Null(frame.State.DrawCursor);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleTripleFrame>(NativeBattleTestSnapshots.Json(frame))));
    }

    [Fact]
    public void Executed_upload_requires_its_own_initializer_pointer_and_never_uses_Double_pointer()
    {
        NativeBattleOriginalEffectState effect = new(21, 20, new(21, 0x89DEF0, 0x89DED0), new(0, 0, 8, 0, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(20);
        Assert.Throws<NotSupportedException>(() => NativeBattleTripleController.Initialize89DEF0(memory, effect, Packets(), Resources()));
        Assert.Throws<InvalidDataException>(() => NativeBattleEnhancementEntry.Initialize(memory, effect, Packets(), Resources().Animation, NativeBattleEnhancementProfile.Double));
    }

    [Theory]
    [InlineData(-32768, 0, 0)]
    [InlineData(-1, 0, 0)]
    [InlineData(1, 1243, 893)]
    [InlineData(10, 4083, 4057)]
    [InlineData(11, 4096, 4096)]
    [InlineData(32767, 4096, 4096)]
    public void Both_curves_clamp_the_signed_WORD_index_and_keep_distinct_original_values(short index, ushort first, ushort second)
    {
        Assert.Equal(first, NativeBattleTripleScalars.Curve89E5E0(index, Resources()));
        Assert.Equal(second, NativeBattleTripleScalars.Curve89E620(index, Resources()));
    }

    [Fact]
    public void Primary_clears_only_three_colour_bytes_then_runs_eleven_distinct_curve_steps()
    {
        NativeBattleModulePoolTask task = Task(0x89E250).WriteWord(0x64, 2048).WriteDword(0x5C, 0xA5FFFFFF).WriteWord(0x26, 0x8000);
        for (int step = 0; step < 4; step++)
        {
            task = NativeBattleTripleScalars.AdvancePrimaryPhase89E560(task, Resources());
        }
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal(0xA5000000u, task.ReadDword(0x5C));
        Assert.Equal((ushort)0, task.ReadWord(0x64));
        Assert.Equal((ushort)0x8002, task.ReadWord(0x26));
        for (int step = 1; step <= 11; step++)
        {
            task = NativeBattleTripleScalars.AdvancePrimaryPhase89E560(task, Resources());
            Assert.Equal(Resources().Curve89E620[step], task.ReadWord(0x64));
            Assert.Equal(Resources().Curve89E5E0[step], task.ReadWord(0x66));
            Assert.Equal(task.ReadWord(0x66), task.ReadWord(0x50));
            Assert.Equal((byte)(step == 11 ? 2 : 1), task.ReadByte(0x29));
        }
        Assert.Equal((ushort)0x8005, task.ReadWord(0x26));
        Assert.Same(task, NativeBattleTripleScalars.AdvancePrimaryPhase89E560(task, Resources()));
    }

    [Fact]
    public void Morph_counter_wrap_is_signed_and_does_not_complete_on_8000()
    {
        NativeBattleModulePoolTask task = Task(0x89E670).WriteWord(0x78, 0x7FFF);
        task = NativeBattleTripleScalars.AdvanceMorphPhase89E780(task, Resources());
        Assert.Equal((ushort)0x8000, task.ReadWord(0x78));
        Assert.Equal((ushort)0, task.ReadWord(0x74));
        Assert.Equal((byte)0, task.ReadByte(0x29));
        Assert.Equal((byte)0, task.ReadByte(0x26));
    }

    [Fact]
    public void Missing_curve_and_wrong_private_stride_fail_before_inventing_a_value()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleTripleScalars.Curve89E620(1, Resources() with { Curve89E620 = [] }));
        Assert.Throws<InvalidDataException>(() => NativeBattleTripleScalars.AdvancePrimaryPhase89E560(new NativeBattleModulePoolTask(1, 0, null, 0x89E250, new byte[96].ToImmutableArray()), Resources()));
    }

    internal static NativeBattleTripleResources Resources() => new(new("unused-controlled-animation", []), [], [], [], [], [0, 1243, 2091, 2657, 3060, 3383, 3635, 3822, 3954, 4038, 4083, 4096], [0, 893, 1611, 2198, 2664, 3016, 3303, 3561, 3779, 3948, 4057, 4096], 300027);

    private static NativeBattleModulePoolTask Task(uint callback) => new(1, 0, null, callback, new byte[112].ToImmutableArray());

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 34, 21, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        return packets;
    }
}
