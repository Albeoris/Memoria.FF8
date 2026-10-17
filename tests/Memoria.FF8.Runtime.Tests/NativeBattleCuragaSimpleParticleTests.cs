using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaSimpleParticleTests
{
    [Theory]
    [InlineData(0x87DE50u)]
    [InlineData(0x882FC0u)]
    public void Stage_fades_have_their_original_five_callback_lifetime_and_initial_clear_contract(uint callback)
    {
        var fixture = Fixture(callback);
        NativeBattleCuragaFrame frame = fixture.Frame;
        List<NativeBattleCuragaStageFadeOperation> fades = [];
        for (int counter = 0; counter < 5; counter++)
        {
            NativeBattleCuragaLeafStep step = NativeBattleCuragaParticles.AdvanceSimple(frame, fixture.Address, Resources(), (current, operation) =>
            {
                fades.Add(Assert.IsType<NativeBattleCuragaStageFadeOperation>(operation));
                return current;
            });
            Assert.Equal(counter == 4 ? 2 : 0, step.Result);
            frame = step.Frame;
        }
        if (callback == 0x87DE50)
        {
            Assert.Equal(new short[] { 0, 256, 512, 768, 1024 }, fades.Select(fade => fade.Fade));
            Assert.True(fades[0].ClearRgb);
            Assert.All(fades.Skip(1), fade => Assert.False(fade.ClearRgb));
        }
        else
        {
            Assert.Equal(new short[] { 768, 512, 256, 0 }, fades.Select(fade => fade.Fade));
            Assert.All(fades, fade => Assert.False(fade.ClearRgb));
        }
    }

    [Fact]
    public void Actual_flash_program_emits_two_raw_F4_packets_and_native_E1_with_NOP_before_terminating()
    {
        var fixture = Fixture(0x87E020);
        NativeBattleCuragaFrame frame = fixture.Frame;
        List<NativeBattleModulePacketBatch> batches = [];
        for (int counter = 0; counter < 3; counter++)
        {
            NativeBattleCuragaLeafStep step = NativeBattleCuragaParticles.AdvanceSimple(frame, fixture.Address, Resources(), Collect);
            Assert.Equal(counter == 2 ? 2 : 0, step.Result);
            frame = step.Frame;
        }
        Assert.Equal(2, batches.Count);
        Assert.Equal(0x2A7F7F7Fu, batches[0].Primitives[0].Primitive.Words[1]);
        Assert.Equal(0x2A3F3F3Fu, batches[1].Primitives[0].Primitive.Words[1]);
        foreach (NativeBattleModulePacketBatch batch in batches)
        {
            Assert.Equal(36, batch.AllocationBytes);
            Assert.Equal(new[] { 0, 24 }, batch.Primitives.Select(item => item.ByteOffset));
            Assert.Equal(new ushort[] { 1, 0 }, batch.Primitives.Select(item => item.Flags20));
            Assert.All(batch.Primitives, item => Assert.Equal(7, item.Primitive.Bucket));
            Assert.Equal(new uint[] { 0, 320, 216u << 16, (216u << 16) | 320 }, batch.Primitives[0].Primitive.Words.Skip(2));
            Assert.Equal(new uint[] { 0x02000000, 0xE100002A, 0 }, batch.Primitives[1].Primitive.Words);
            Assert.All(batch.Primitives, item => Assert.Equal(new[] { 0, 0, 0, 0 }, item.Primitive.VertexPayload4));
        }

        NativeBattleCuragaFrame Collect(NativeBattleCuragaFrame current, NativeBattleCuragaOperation operation)
        {
            batches.Add(Assert.IsType<NativeBattleCuragaPacketOperation>(operation).Batch);
            return current;
        }
    }

    [Fact]
    public void Flash_FE_wait_compares_signed_WORD_counter_and_reuses_its_next_color_on_the_following_callback_after_JSON()
    {
        var fixture = Fixture(0x87E020);
        NativeBattleCuragaResources resources = Resources() with { FlashProgram = [0xFE020000, 0x00445566, 0xFF000000] };
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteDword(0x34, 0x112233).WriteWord(0x24, 0xFFFF));
        NativeBattleCuragaFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        List<uint> colors = [];
        for (int index = 0; index < 5; index++)
        {
            NativeBattleCuragaFrame clone = JsonSerializer.Deserialize<NativeBattleCuragaFrame>(JsonSerializer.Serialize(frame))!;
            NativeBattleCuragaLeafStep step = NativeBattleCuragaParticles.AdvanceSimple(frame, fixture.Address, resources, (current, operation) =>
            {
                colors.Add(Assert.IsType<NativeBattleCuragaPacketOperation>(operation).Batch.Primitives[0].Primitive.Words[1]);
                return current;
            });
            NativeBattleCuragaLeafStep restored = NativeBattleCuragaParticles.AdvanceSimple(clone, fixture.Address, resources, (current, _) => current);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(restored));
            frame = step.Frame;
        }
        Assert.Equal(new uint[] { 0x2A112233, 0x2A112233, 0x2A112233, 0x2A445566, 0x2A445566 }, colors);
    }

    private static NativeBattleCuragaResources Resources() => new(new("skipped-registration", []), 0x4946F) { FlashProgram = [0x007F7F7F, 0x003F3F3F, 0xFF000000] };

    private static (NativeBattleCuragaFrame Frame, uint Address) Fixture(uint callback)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 23, 28, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(28, 27, new(28, 0x87DB10, 0x87DAF0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x26D75BC, 0)] };
        NativeBattleCuragaFrame frame = NativeBattleCuragaController.Initialize87DB10(NativeBattleEffectMemory.FromColdProcess().BeginLoad(27), selected, packets, Resources());
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(callback == 0x87E020 ? 2 : 4, callback, callback == 0x87E020 ? 60 : 64, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = appended.Hierarchy;
        if (callback == 0x87E020)
        {
            hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteDword(0x30, 0x160E0A0));
        }
        return (frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, appended.Address);
    }
}
