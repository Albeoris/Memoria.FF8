using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleShotgun281Controller;
using static Memoria.FF8.Runtime.NativeBattleShotgun281Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShotgun281ControllerTests
{
    [Fact]
    public void Entry_resets_only_BSS_root_and_defers_common_reset()
    {
        var inactive = Inactive();
        inactive = inactive with { Memory = inactive.Memory with { Arena = inactive.Memory.Arena!.Write(0x1BC8, new byte[] { 0xAB, 0xCD }) } };
        var frame = Initialize(inactive, Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Equal(new byte[8], TaskView(Read(frame, RootBank), 12, 8).Read(0, 8));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, frame.Memory.Arena!.Read(0x1BC8, 2));
        Assert.Equal(ModelAddressFor(3), frame.State.ReadDword(CasterGlobal));
        frame = Advance(frame, Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Equal((ushort)1, Read(frame, RootBank).ReadWord(12));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, frame.Memory.Arena!.Read(0x1BC8, 2));
    }

    [Theory]
    [InlineData(0u, 0x12018u, 1u)]
    [InlineData(1u, 0x2018u, 0u)]
    [InlineData(0x80000000u, 0x2018u, 0u)]
    public void Root_parity_uses_full_DWORD_without_graphics_pause(uint old, uint offset, uint next)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, old));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]) with { GraphicsFlags = 0x201 }).Frame;
        Assert.Equal(ArenaAddress + offset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(16));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Setup_uses_first_physical_recipient_and_rotation_only_basis_then_composes_current_caster()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = frame with { State = frame.State.WriteWord(BasisAAddress + 18, 0xA55A).WriteDword(BasisBAddress + 20, 0x12345678) };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        List<string> order = [];
        var inputs = Inputs([]);
        inputs = inputs with
        {
            Execute = (current, operation) =>
            {
                order.Add(operation is NativeBattleShotgun281ComposeCaster5095B0 ? "compose" : operation is NativeBattleShotgun281RestoreCaster508C90 ? "restore" : "other");
                return current;
            },
            ReadCurrentBoneMatrix = (current, model, bone) =>
            {
                Assert.Equal(new[] { "compose" }, order);
                Assert.Equal(ModelAddressFor(3), model);
                Assert.Equal(23, bone);
                order.Add("bone23");
                return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 321, Y = -654, Z = 987 }, null);
            }
        };
        var actual = AdvanceTask(frame, RootBank, Resources(), inputs).Frame;
        Assert.Equal(new[] { "compose", "bone23", "restore" }, order.Where(entry => entry != "other"));
        Assert.Contains("other", order);
        Assert.All(order.Select((entry, index) => (entry, index)).Where(item => item.entry == "other"), item => Assert.InRange(item.index, order.IndexOf("bone23") + 1, order.IndexOf("restore") - 1));
        Assert.Equal(ModelAddressFor(0), Read(actual, Pools[3].Bank).ReadDword(16));
        Assert.Equal((ushort)1, Read(actual, Pools[3].Bank).ReadWord(12));
        Assert.Equal((ushort)0xA55A, actual.State.ReadWord(BasisAAddress + 18));
        Assert.Equal(0x12345678u, actual.State.ReadDword(BasisBAddress + 20));
        Assert.Equal(new byte[6], Resources().MuzzlePoint8.AsSpan().Slice(0, 6).ToArray());
        Assert.Equal(321u, actual.State.ReadDword(BasisAAddress + 20));
        Assert.Equal(unchecked((uint)-654), actual.State.ReadDword(BasisAAddress + 24));
        Assert.Equal(987u, actual.State.ReadDword(BasisAAddress + 28));
        Assert.Equal(frame.Random, actual.Random);
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena!.Read(0, 1));
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)2)]
    public void Muzzle_burst_has_sixteen_three_draw_particles_and_exact_acceleration(ushort age)
    {
        var frame = MainAt(age);
        List<NativeBattleShotgun281Operation> operations = [];
        var actual = AdvanceTask(frame, Pools[3].Bank, Resources(), Inputs(operations) with { GraphicsFlags = 0x201 }).Frame;
        var random = frame.Random;
        var basis = ReadPacked281(actual, BasisAAddress).ReadMatrix();
        for (int index = 0; index < 16; index++)
        {
            random = random.Draw(out ushort angle).Draw(out ushort radius).Draw(out ushort depth);
            var velocity = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(unchecked((short)(NativeBattleCameraMath.Sine(angle) * (radius & 127) >> 12)), unchecked((short)(NativeBattleCameraMath.Cosine(angle) * (radius & 127) >> 12)), unchecked((short)(-128 - (depth & 127)))));
            var task = actual.Effect.Pools[1].Slots[index];
            Assert.Equal(SpriteAAddress, task.ReadDword(12));
            Assert.Equal((ushort)8, task.ReadWord(30));
            Assert.Equal(unchecked((ushort)velocity.X), task.ReadWord(24));
            Assert.Equal(unchecked((ushort)(-velocity.X >> 3)), task.ReadWord(32));
            Assert.Equal(unchecked((ushort)(-velocity.Y >> 3)), task.ReadWord(34));
            Assert.Equal(unchecked((ushort)(-velocity.Z >> 3)), task.ReadWord(36));
            Assert.Equal((ushort)0, task.ReadWord(38));
        }
        Assert.Equal(random, actual.Random);
        Assert.Equal(age == 1 ? 1 : 0, operations.OfType<NativeBattleShotgun281Flash5712C0>().Count());
        Assert.Equal(age == 2 ? 1 : 0, operations.OfType<NativeBattleShotgun281Sound501330>().Count());
    }

    [Fact]
    public void Successful_muzzle_append_preserves_unwritten_tail_knownness()
    {
        var frame = MainAt(1);
        frame = Write(frame, Pools[1].Bank, Read(frame, Pools[1].Bank).MarkPayloadUnknown(38, 2));
        var actual = AdvanceTask(frame, Pools[3].Bank, Resources(), Inputs([])).Frame;
        Assert.Throws<NotSupportedException>(() => Read(actual, Pools[1].Bank).ReadWord(38));
        Assert.Equal((ushort)8, Read(actual, Pools[1].Bank).ReadWord(30));
    }

    [Fact]
    public void Failed_muzzle_append_breaks_without_CRT_but_still_flashes_at_one()
    {
        var frame = MainAt(1);
        for (int index = 0; index < 128; index++)
        {
            (frame, _) = Append(frame, Pools[1].Descriptor, 0x67E5F0);
        }
        List<NativeBattleShotgun281Operation> operations = [];
        var actual = AdvanceTask(frame, Pools[3].Bank, Resources(), Inputs(operations)).Frame;
        Assert.Equal(frame.Random, actual.Random);
        Assert.Equal(new NativeBattleShotgun281Flash5712C0(0, 1, 0, 96), Assert.Single(operations.OfType<NativeBattleShotgun281Flash5712C0>()));
    }

    [Fact]
    public void Impact_retries_negative_parent_then_uses_current_translation_and_twenty_four_particle_draws()
    {
        var frame = MainAt(4);
        var random = frame.Random;
        List<int> expectedBones = [];
        int selected;
        do
        {
            random = random.Draw(out ushort next);
            selected = next * 24 >> 15;
            expectedBones.Add(selected);
        }
        while (selected == 0);
        Assert.True(expectedBones.Count > 1);
        List<int> actualBones = [];
        var inputs = Inputs([]) with { ReadBoneParent = (_, model, bone) =>
        {
            Assert.Equal(ModelAddressFor(0), model);
            actualBones.Add(bone);
            return bone == 0 ? (short)-1 : (short)0;
        } };
        var actual = AdvanceTask(frame, Pools[3].Bank, Resources(), inputs).Frame;
        Assert.Equal(expectedBones, actualBones);
        var impact = Read(actual, Pools[2].Bank);
        Assert.Equal(unchecked((ushort)(100 + selected * 10)), impact.ReadWord(12));
        Assert.Equal(unchecked((ushort)(200 + selected * 20)), impact.ReadWord(14));
        Assert.Equal(unchecked((ushort)(2000 + selected * 30)), impact.ReadWord(16));
        Assert.Equal((ushort)0, impact.ReadWord(18));
        for (int index = 0; index < 8; index++)
        {
            random = random.Draw(out ushort first).Draw(out ushort second).Draw(out ushort radius);
            var direction = NativeBattleRinoa89Controller.Direction6A6020(second, first);
            short x = unchecked((short)(direction.X * ((radius & 63) + 32) >> 11));
            var particle = actual.Effect.Pools[1].Slots[index];
            Assert.Equal(SpriteBAddress, particle.ReadDword(12));
            Assert.Equal((ushort)10, particle.ReadWord(30));
            Assert.Equal(unchecked((ushort)x), particle.ReadWord(24));
            Assert.Equal(unchecked((ushort)(-x >> 3)), particle.ReadWord(32));
            Assert.Equal(impact.ReadWord(12), particle.ReadWord(16));
        }
        Assert.Equal(random, actual.Random);
    }

    [Fact]
    public void Full_secondary_pool_skips_bone_queries_and_all_burst_CRT()
    {
        var frame = MainAt(4);
        for (int index = 0; index < 6; index++)
        {
            (frame, _) = Append(frame, Pools[2].Descriptor, 0x67E6B0);
        }
        var inputs = Inputs([]) with { ReadBoneCount = (_, _) => throw new InvalidOperationException("Full pool must not query a bone.") };
        var actual = AdvanceTask(frame, Pools[3].Bank, Resources(), inputs).Frame;
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Old_thirteen_increments_then_applies_one_current_physical_first_contact_ignoring_count()
    {
        var frame = MainAt(13);
        List<NativeBattleShotgun281Operation> operations = [];
        var packets = SetCount(Packets(2), 0);
        var actual = AdvanceTask(frame, Pools[3].Bank, Resources(), Inputs(operations, packets));
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)14, Read(actual.Frame, Pools[3].Bank).ReadWord(12));
        Assert.Equal(1, Assert.Single(operations.OfType<NativeBattleShotgun281Contact506690>()).ContactIndex);
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Root_waits_for_particle_survivors_after_main_retires_without_repeating_contact()
    {
        var frame = MainAt(13);
        (frame, uint child) = Append(frame, Pools[1].Descriptor, 0x67E5F0);
        frame = Write(frame, child, Read(frame, child).WriteDword(12, SpriteAAddress).WriteWord(30, 3));
        List<NativeBattleShotgun281Operation> operations = [];
        frame = Advance(frame, Resources(), Inputs(operations));
        Assert.NotEmpty(frame.Effect.Pools);
        Assert.Null(frame.Effect.Pools[3].Head);
        Assert.Single(operations.OfType<NativeBattleShotgun281Contact506690>());
        frame = Advance(frame, Resources(), Inputs(operations));
        Assert.NotEmpty(frame.Effect.Pools);
        frame = Advance(frame, Resources(), Inputs(operations));
        Assert.Empty(frame.Effect.Pools);
        Assert.Single(operations.OfType<NativeBattleShotgun281Contact506690>());
    }

    [Fact]
    public void Retirement_ignores_surviving_secondary_and_preserves_its_physical_bank_through_JSON_warm_entry()
    {
        var frame = MainAt(13);
        (frame, uint impact) = Append(frame, Pools[2].Descriptor, 0x67E6B0);
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[312].ToImmutableArray()).Initialize7016B0(Resources().ProgrammeB, 312);
        frame = WriteBytes(frame, impact + 20, NativeBattleEffectArena.Clear().Write(0, NativeBattleStorageSnapshots.Capture(workspace).Data.AsSpan()), 312);
        frame = Advance(frame, Resources(), Inputs([]));
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.Equal(1, frame.Memory.Arena!.Read(0x1400, 1)[0] & 1);
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleShotgun281Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = Initialize(frame, Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Equal(1, frame.Memory.Arena!.Read(0x1400, 1)[0] & 1);
    }

    private static NativeBattleShotgun281Frame Active()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        return AdvanceTask(frame, RootBank, Resources(), Inputs([])).Frame;
    }

    private static NativeBattleShotgun281Frame MainAt(ushort age)
    {
        var frame = Active();
        return Write(frame, Pools[3].Bank, Read(frame, Pools[3].Bank).WriteWord(12, age));
    }

    private static NativeBattleShotgun281Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleOriginalEffectState effect = new(281, 280, new(281, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleShotgun281State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleShotgun281Inputs Inputs(List<NativeBattleShotgun281Operation> operations, NativeBattleActionPacketsState? packets = null)
    {
        packets ??= Packets(1);
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => Identity(), _ => new(null, 100, 160, 108), (_, address) => Model(address), (_, _) => 24, (_, _, bone) => bone == 0 ? (short)-1 : (short)0, (_, _, _) => (byte)1, (_, _, bone) => Identity() with { Bytes = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = bone * 10, Y = bone * 20, Z = 1000 + bone * 30 }, null).Bytes }, (_, address) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = Model(address).Transform!.Position28.X, Y = Model(address).Transform!.Position28.Y, Z = Model(address).Transform!.Position28.Z }, null), _ => packets, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
    }

    private static NativeBattlePackedMatrix32 Identity() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);

    private static NativeBattleGraphicsModel Model(uint address)
    {
        int actor = checked((int)((address - 0x1D972C0) / 156));
        return new(actor, 2) { Rotation14 = 0, MinimumY146 = -100, Transform = new(new(0, 0, 0), new((short)(100 + actor * 100), (short)(200 + actor), (short)(1000 - actor * 1000)), (short)(444 + actor), 777, 4096) };
    }

    private static uint ModelAddressFor(int actor) => 0x1D972C0u + (uint)(actor * 156);

    private static NativeBattleActionPacketsState Packets(int count)
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 61, 281, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 61, 281, null));
        for (int index = 0; index < count; index++)
        {
            (packets, _) = packets.AppendContact(new(new((byte)index, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(checked((byte)count));
        return packets.WritePlayerRepeatMode(1, 1);
    }

    private static NativeBattleActionPacketsState SetCount(NativeBattleActionPacketsState packets, byte count)
    {
        NativeBattleActionHeaderSlot header = packets.Headers[1];
        return packets with { Headers = packets.Headers.SetItem(1, header with { Payload = header.Payload.SetItem(16, count), UnknownByteMask = header.UnknownByteMask & ~(1u << 16) }) };
    }

    private static NativeBattleShotgun281Resources Resources() => NativeBattleShotgun281StateTests.Resources();
}
