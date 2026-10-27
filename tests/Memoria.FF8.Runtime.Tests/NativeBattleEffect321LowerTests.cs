using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect321Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect321Lower;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect321LowerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ring_delay_returns_before_draw_and_pause_prevents_countdown(bool paused)
    {
        Fixture fixture = new(0x597E30) { Flags = paused ? 0x201u : 0u };
        fixture.Task(task => task.WriteWord(14, 1));
        NativeBattleEffect321Step result = NativeBattleEffect321Lower.Advance(fixture.Frame, fixture.Address, null!, fixture.Inputs());
        Assert.Empty(fixture.Operations);
        Assert.Equal(paused ? 1 : 0, Read(result.Frame, fixture.Address).ReadWord(14));
        Assert.Equal(fixture.Frame.Memory.GeometryRegisters, result.Frame.Memory.GeometryRegisters);
    }

    [Theory]
    [InlineData(false, 12, 28, 30, 8)]
    [InlineData(true, 6, 32, 34, 10)]
    public void Rings_draw_before_pause_and_use_native_signed_division(bool anisotropic, int age, int scaleOffset, int velocityOffset, int divisor)
    {
        Fixture fixture = new(anisotropic ? 0x598B90u : 0x597E30u);
        fixture.Task(task => task.WriteWord(12, (ushort)age).WriteWord(20, 3000).WriteWord(28, 2048).WriteWord(scaleOffset, 4096).WriteWord(velocityOffset, unchecked((ushort)-7)));
        NativeBattleEffect321Step result = NativeBattleEffect321Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask task = Read(result.Frame, fixture.Address);
        Assert.Equal(4096 - 7, task.ReadWord(scaleOffset));
        Assert.Equal(unchecked((ushort)(-7 - -7 / divisor)), task.ReadWord(velocityOffset));
        NativeBattleEffectArena context = NativeBattleTemporaryMemory.ReadView(result.Frame.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(0xF3u, Dword(context, 28));
        Assert.Equal(0u, Dword(context, 12));
        Assert.Equal(0x2239528u, Assert.IsType<NativeBattleEffect321DrawBatch>(Assert.Single(fixture.Operations)).CursorGlobal);
        Assert.Equal(3000, result.Frame.Memory.GeometryRegisters!.Translation[2]);
        Assert.Null(result.Frame.Memory.GeometryRegisters.Rotation20[18]);
        fixture = new(anisotropic ? 0x598B90u : 0x597E30u) { Flags = 0x201 };
        fixture.Task(value => value.WriteWord(12, (ushort)age).WriteWord(20, 3000).WriteWord(28, 2048).WriteWord(scaleOffset, 4096).WriteWord(velocityOffset, unchecked((ushort)-7)));
        result = NativeBattleEffect321Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Single(fixture.Operations);
        Assert.Equal(age, Read(result.Frame, fixture.Address).ReadWord(12));
        Assert.Equal(4096, Read(result.Frame, fixture.Address).ReadWord(scaleOffset));
    }

    [Theory]
    [InlineData(false, 6, 5)]
    [InlineData(true, 8, 4)]
    public void Sprite_births_preserve_gap_and_consume_only_per_success_CRT(bool masked, int births, int draws)
    {
        Fixture fixture = new(0x597F90);
        fixture.Fill(99);
        uint last = NativeBattleEffect321Records.First + 99 * 24;
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteView(last, NativeBattleEffectArena.Clear().Write(16, BitConverter.GetBytes((short)1234)).MarkUnknown(22, 2), 24) };
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes((short)4000)).MarkUnknown(6, 2);
        NativeCrtRandomState expected = fixture.Frame.Random;
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffect321Frame result = NativeBattleEffect321Records.Birth(fixture.Frame, point, masked ? 8u : 1u, births, fixture.Inputs());
        Assert.Equal(expected, result.Random);
        NativeBattleEffectArena row = result.State.ReadView(last, 24);
        Assert.Equal(masked ? 8u : 1u, Dword(row, 0));
        Assert.DoesNotContain(true, row.ReadSnapshot(14, 2).Known);
        Assert.DoesNotContain(true, row.ReadSnapshot(22, 2).Known);
        if (!masked)
        {
            Assert.Equal(1234, Word(row, 16));
        }
        NativeBattleEffect321Frame full = NativeBattleEffect321Records.Birth(result, null!, 1, 6, null!);
        Assert.Same(result, full);
    }

    [Fact]
    public void Debris_birth_consumes_eight_CRT_and_overwrites_point_W_with_spin()
    {
        Fixture fixture = new(0x598230);
        fixture.Fill(99);
        NativeCrtRandomState expected = fixture.Frame.Random;
        for (int index = 0; index < 8; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffect321Frame result = NativeBattleEffect321Debris.Birth(fixture.Frame, NativeBattleEffectArena.Clear().MarkUnknown(6, 2));
        Assert.Equal(expected, result.Random);
        NativeBattleEffectArena row = result.State.ReadView(NativeBattleEffect321Records.First + 99 * 24, 24);
        Assert.Equal(2u, Dword(row, 0));
        Assert.Equal(Word(row, 16) >> 1, Word(row, 20));
        Assert.All(row.ReadSnapshot(14, 2).Known, Assert.True);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sprite_scan_preserves_native_velocity_word_quirks_and_mask_matching(bool masked)
    {
        Fixture fixture = new(masked ? 0x5988B0u : 0x597F90u);
        fixture.Task(task => task.WriteWord(12, 20).WriteWord(14, 8));
        uint first = NativeBattleEffect321Records.First;
        NativeBattleEffectArena row = PutWord(PutWord(PutWord(PutWord(PutWord(PutWord(PutWord(Put(NativeBattleEffectArena.Clear(), 0, masked ? 8u : 1u), 4, 1), 6, 2048), 8, 100), 10, 200), 12, 4000), 16, -17), 18, -31);
        row = PutWord(row, 20, 25).Write(14, [0xAB]).MarkUnknown(15, 1);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteView(first, row, 24).WriteDword(first + 24, masked ? 4u : 2u) };
        NativeBattleEffect321Step result = NativeBattleEffect321Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        row = result.Frame.State.ReadView(first, 24);
        Assert.Equal(masked ? 83 : 100, Word(row, 8));
        Assert.Equal(masked ? 200 : 169, Word(row, 10));
        Assert.Equal(masked ? 4025 : 4000, Word(row, 12));
        Assert.Equal(masked ? -14 : -15, Word(row, 16));
        Assert.Equal(-31, Word(row, 18));
        Assert.Equal(masked ? 22 : 25, Word(row, 20));
        Assert.True(result.Frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Frame.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Single(fixture.Operations);
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Paused_empty_sprite_scan_still_loads_alternate_view_and_releases_temporaries()
    {
        Fixture fixture = new(0x597F90) { Flags = 0x201 };
        NativeBattleEffect321Step result = NativeBattleEffect321Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Empty(fixture.Operations);
        Assert.Equal(0, Read(result.Frame, fixture.Address).ReadWord(12));
        NativeBattleScriptedLightingMemory lighting = result.Frame.Memory.ScriptedLighting!;
        Assert.Equal(NativeBattleCuragaMatrices.Identity8DD770().Rotation, lighting.LightMatrix!.Rotation);
        Assert.False(lighting.LightMatrix.Known[18]);
        Assert.Equal(new int?[] { 0, 0, 0 }, Enumerable.Range(0, 3).Select(lighting.BackColor!.ReadLane));
        Assert.Equal(fixture.Frame.Memory.ScriptedColorRegisters, result.Frame.Memory.ScriptedColorRegisters);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Debris_age16_draws_before_clearing_and_pause_retains_old_row()
    {
        Fixture fixture = new(0x598230);
        fixture.Task(task => task.WriteWord(12, 20));
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteView(NativeBattleEffect321Records.First, PutWord(PutWord(PutWord(Put(NativeBattleEffectArena.Clear(), 0, 2), 4, 16), 6, 1024), 12, 4000), 24) };
        NativeBattleEffect321Frame pausedFrame = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Frame));
        NativeBattleEffect321Step result = NativeBattleEffect321Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Single(fixture.Operations);
        Assert.Equal(0u, result.Frame.State.ReadDword(NativeBattleEffect321Records.First));
        Assert.Equal(2, result.Result);
        fixture.Flags = 0x201;
        result = NativeBattleEffect321Lower.Advance(pausedFrame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2u, result.Frame.State.ReadDword(NativeBattleEffect321Records.First));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Render_request_occurs_while_paused_without_logical_actor_mutation()
    {
        Fixture fixture = new(0x598570) { Flags = 0x201 };
        NativeBattleEffectArena model = fixture.Model;
        NativeBattleEffect321Step result = NativeBattleEffect321Lower.Advance(fixture.Frame, fixture.Address, null!, fixture.Inputs());
        Assert.Equal(new NativeBattleEffect321PcRender47CF50(0, 255, 255, 255, 63), Assert.Single(fixture.Operations));
        Assert.Same(model, fixture.Model);
        Assert.Equal(0, Read(result.Frame, fixture.Address).ReadWord(12));
    }

    [Fact]
    public void Actor_age0_capture_repeats_before_pause_and_preserves_saved_padding_masks()
    {
        Fixture fixture = new(0x5985C0) { Flags = 0x201 };
        fixture.Task(task => task.WriteWord(14, 0).WriteWord(18, 200).WriteWord(20, 3000));
        fixture.Model = PutWord(fixture.Model.MarkUnknown(18, 2).MarkUnknown(34, 2), 0, 0x2065);
        NativeBattleEffect321Step result = NativeBattleEffect321Lower.Advance(fixture.Frame, fixture.Address, null!, fixture.Inputs());
        Assert.Equal(0x20, Word(result.Frame.State.ReadView(0x221D420, 2), 0));
        Assert.DoesNotContain(true, result.Frame.State.ReadView(0x221D428, 4).ReadSnapshot(2, 2).Known);
        Assert.DoesNotContain(true, result.Frame.State.ReadView(0x221D430, 4).ReadSnapshot(2, 2).Known);
        Assert.Equal(0, Read(result.Frame, fixture.Address).ReadWord(12));
        Assert.Empty(fixture.Operations.OfType<NativeBattleEffect321Contact506690>());
    }

    [Fact]
    public void Actor_terminal_restores_current_saved_DWORDs_flags_then_contacts_once()
    {
        Fixture fixture = new(0x5985C0);
        fixture.Task(task => task.WriteWord(12, 67).WriteWord(14, 0).WriteWord(26, 2));
        NativeBattleEffectArena saved = PutWord(NativeBattleEffectArena.Clear(), 0, 0x20).Write(4, BitConverter.GetBytes(0x12345678u)).Write(8, BitConverter.GetBytes(0x1122u)).MarkUnknown(10, 2).Write(12, BitConverter.GetBytes(0xABCD0011u)).Write(16, BitConverter.GetBytes(0x22u)).MarkUnknown(18, 2);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteView(0x221D420, saved, 2).WriteView(0x221D424, CopyBytes(saved, 4, NativeBattleEffectArena.Clear(), 0, 16), 16) };
        fixture.Model = PutWord(fixture.Model, 0, 0x1445);
        NativeBattleEffect321Step result = NativeBattleEffect321Lower.Advance(fixture.Frame, fixture.Address, null!, fixture.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(new NativeBattleEffect321Contact506690(2), fixture.Operations[^1]);
        Assert.Equal(0x465, Word(fixture.Model, 0));
        Assert.Equal(saved.Read(4, 6), fixture.Model.Read(12, 6));
        Assert.DoesNotContain(true, fixture.Model.ReadSnapshot(18, 2).Known);
        Assert.DoesNotContain(true, fixture.Model.ReadSnapshot(34, 2).Known);
        Assert.Empty(fixture.Operations.OfType<NativeBattleEffect321DrawBatch>());
    }

    internal sealed class Fixture
    {
        public uint Address => 0x221C610;
        public NativeBattleEffect321Frame Frame;
        public NativeBattleEffect321Resources Resources;
        public NativeBattleEffectArena Model = NativeBattleEffectArena.Clear();
        public uint Flags;
        public List<NativeBattleEffect321Operation> Operations = [];
        public Fixture(uint callback)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x221C600, Address, 36, 100);
            (pool, int slot) = pool.Append(callback);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[24]));
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            Frame = new(memory, new(321, 320, new(321, 0x597A00, 0x5979E0), null, null) { Pools = [pool] }, new(42), NativeBattleEffect321State.FromColdProcess().DetachBank(1)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            byte[] mesh = new byte[40];
            BinaryPrimitives.WriteInt32LittleEndian(mesh, 8);
            byte[] sprite = new byte[48];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 16);
            for (int index = 0; index < 16; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + index * 2), 44);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(42), 0xFFFF);
            Resources = new(null!, null!, new uint[] { 0xCD153C, 0xCD1C24, 0xCD1D5C }.ToImmutableDictionary(address => address, address => new NativeBattleStaticMeshResource(0, address, mesh.ToImmutableArray())), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(0xCD1390, new(5, 0xCD1390, 48, 0, sprite.ToImmutableArray())), null!, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
        }
        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update) => Frame = Write(Frame, Address, update(Read(Frame, Address)));
        public void Fill(int count)
        {
            for (int index = 0; index < count; index++)
            {
                Frame = Frame with { State = Frame.State.WriteDword(NativeBattleEffect321Records.First + (uint)(index * 24), 1) };
            }
        }
        public NativeBattleEffect321Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, null!, (_, _) => Model, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), _ => 0, null!, null!, null!, (frame, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleEffect321WriteModelBytes write)
            {
                Model = CopyBytes(write.Data, 0, Model, write.Offset, write.Length);
            }
            return frame;
        });
    }
}
