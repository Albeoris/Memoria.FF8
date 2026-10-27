using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect301Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect301ControllerTests
{
    private const uint Arena = 0x20DFAB8;

    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    public void Root_first_visit_uses_second_buffer_without_touching_uninitialized_pools(uint side, int offset, uint next)
    {
        NativeBattleEffect301Frame frame = Frame(0, false);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, side));
        Harness harness = new();
        NativeBattleEffect301Step result = AdvanceRoot66D9F0(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + (uint)offset, result.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(result.Frame, RootBank).ReadDword(16));
        Assert.Equal(1, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Single(result.Frame.Effect.Pools);
        Assert.Equal(new NativeBattleEffect301ModelFlags(ModelAddress(6), 1, 255, 12), Assert.Single(harness.Operations));
        Assert.True(result.Frame.State.ScriptSourcePending);
    }

    [Fact]
    public void Main_preincrements_and_returns_before_any_age142_body_color_or_records()
    {
        Harness harness = new();
        NativeBattleEffect301Frame frame = AddMain(Frame(10), 141);
        NativeBattleEffect301Step result = AdvanceMain66DBD0(frame, Arena, null!, harness.Inputs());
        Assert.Equal(142, Read(result.Frame, Arena).ReadWord(12));
        Assert.Equal(2, result.Result);
        Assert.Equal(new NativeBattleEffect301ModelFlags(ModelAddress(6), 2, 0xFFF3, 0), Assert.Single(harness.Operations));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(frame.Staging, result.Frame.Staging);
    }

    [Fact]
    public void Last_live_age_keeps_scaled_model_color_and_does_not_restore_original_dword()
    {
        Harness harness = new();
        NativeBattleEffect301Frame frame = AddMain(Frame(10), 140);
        NativeBattleEffect301Step live = AdvanceMain66DBD0(frame, Arena, null!, harness.Inputs());
        Assert.Equal(0, live.Result);
        Assert.Equal(141, Read(live.Frame, Arena).ReadWord(12));
        Assert.Equal(0x32u, harness.Colors[6] >> 24);
        Assert.DoesNotContain(harness.Operations, operation => operation is NativeBattleEffect301WriteModelBytes { Offset: 44, Length: 4 });
        uint retained = harness.Colors[6];
        harness.Operations.Clear();
        NativeBattleEffect301Step retired = AdvanceMain66DBD0(live.Frame, Arena, null!, harness.Inputs());
        Assert.Equal(2, retired.Result);
        Assert.Equal(retained, harness.Colors[6]);
        Assert.Single(harness.Operations);
    }

    [Fact]
    public void Gpf_uses_rgb_bytes_and_preserves_material_while_advancing_color_fifo()
    {
        Harness harness = new();
        NativeBattleEffect301Frame frame = AddMain(Frame(10), 121);
        NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters!;
        NativeBattleScriptedColorRegisters input = frame.Memory.ScriptedColorRegisters! with { Material = null, Rgb = [1u, 2u, 3u] };
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = input } };
        frame = WriteBytes(frame, Arena + 0xE13, NativeBattleEffectArena.Clear().MarkUnknown(0, 1), 1);
        NativeBattleEffect301Frame result = FadeModelColor(frame, Arena, ModelAddress(6), 0, harness.Inputs());
        NativeBattleScriptedColorRegisters output = result.Memory.ScriptedColorRegisters!;
        Assert.Equal(geometry, result.Memory.GeometryRegisters);
        Assert.Null(output.Material);
        Assert.Equal(new uint?[] { 2, 3, null }, output.Rgb.ToArray());
        Assert.Equal(0u, output.Ir0);
        Assert.Equal(new int?[] { 0, 0, 0 }, output.Mac.ToArray());
        Assert.Equal(new int?[] { 0, 0, 0 }, output.Ir.ToArray());
        Assert.Equal(0u, output.Flags);
        Assert.Equal(0x32000000u, harness.Colors[6]);
        Assert.Equal(input.NormalInput, output.NormalInput);
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(19)]
    public void Gpf_factor_uses_native_sine_and_three_low_ir_bytes(int fraction)
    {
        Harness harness = new();
        NativeBattleEffect301Frame frame = AddMain(Frame(10), 121);
        frame = FadeModelColor(frame, Arena, ModelAddress(6), fraction, harness.Inputs());
        int factor = 4096 - NativeBattleCameraMath.Sine(((20 - fraction) << 10) / 20);
        Assert.Equal(unchecked((uint)factor), frame.Memory.ScriptedColorRegisters!.Ir0);
        uint expected = 0x32000000;
        for (int lane = 0; lane < 3; lane++)
        {
            int original = new[] { 0x20, 0x40, 0x60 }[lane];
            expected |= (uint)unchecked((byte)NativePrecision53.RoundInteger(original * (factor / 4096d))) << (lane * 8);
        }
        Assert.Equal(expected, harness.Colors[6]);
    }

    [Theory]
    [InlineData(136, false)]
    [InlineData(137, true)]
    [InlineData(138, false)]
    public void Vibration_stop_occurs_only_at_new_age138_and_uses_actual_handle(int oldAge, bool stops)
    {
        Harness harness = new();
        NativeBattleEffect301Frame result = AdvanceMain66DBD0(AddMain(Frame(10), oldAge), Arena, null!, harness.Inputs()).Frame;
        Assert.Equal(oldAge + 1, Read(result, Arena).ReadWord(12));
        if (stops)
        {
            Assert.Equal(new NativeBattleEffect301VibrationStop4A2940(987), Assert.Single(harness.Operations.OfType<NativeBattleEffect301VibrationStop4A2940>()));
        }
        else
        {
            Assert.DoesNotContain(harness.Operations, operation => operation is NativeBattleEffect301VibrationStop4A2940);
        }
    }

    [Fact]
    public void Main_only_drain_restores_current_party_bit4_and_retains_orphan_common_bytes()
    {
        Harness harness = new();
        harness.Flags[0] = 0x1234;
        harness.Flags[1] = 0x5678;
        harness.Flags[2] = 0x9014;
        NativeBattleEffect301Frame frame = Frame(10);
        frame = frame with { State = frame.State.WriteDword(SavedPartyFlagsAddress, 0).WriteDword(SavedPartyFlagsAddress + 4, 4).WriteDword(SavedPartyFlagsAddress + 8, 0).WriteDword(SavedCasterFlagsGlobal, 0xFFFF) };
        NativeBattleEffectTaskPool mesh = frame.Effect.Pools[2];
        (mesh, int slot) = mesh.Append(0x66EC50);
        mesh = mesh with { Head = null, Tail = null, Slots = mesh.Slots.SetItem(slot, mesh.Slots[slot].WriteWord(12, 9).MarkPayloadUnknown(15, 1)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, mesh) } };
        NativeBattleEffect301Frame result = Advance(frame, null!, harness.Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        Assert.Equal((ushort)0x1230, harness.Flags[0]);
        Assert.Equal((ushort)0x567C, harness.Flags[1]);
        Assert.Equal((ushort)0x9010, harness.Flags[2]);
        Assert.Equal((ushort)(0xABCD & 0xFFF3), harness.Flags[6]);
        Assert.Equal(9, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(result.Memory.Arena!.Read(0xE14 + 12, 2)));
        Assert.False(result.Memory.Arena.ReadSnapshot(0xE14 + 15, 1).Known[0]);
        Assert.True(result.State.ScriptSourcePending);
        NativeBattleEffect301Frame restored = JsonSerializer.Deserialize<NativeBattleEffect301Frame>(JsonSerializer.Serialize(result))!;
        Assert.False(restored.Memory.Arena!.ReadSnapshot(0xE14 + 15, 1).Known[0]);
    }

    [Fact]
    public void Missing_current_color_byte_fails_without_defaulting_it()
    {
        NativeBattleEffect301Frame frame = AddMain(Frame(10), 121);
        frame = WriteBytes(frame, Arena + 0xE11, NativeBattleEffectArena.Clear().MarkUnknown(0, 1), 1);
        Assert.Throws<NotSupportedException>(() => FadeModelColor(frame, Arena, ModelAddress(6), 1, new Harness().Inputs()));
    }

    private static NativeBattleEffect301Frame AddMain(NativeBattleEffect301Frame frame, int age)
    {
        (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
        frame = ClearDwords701200(frame, main + 12, 0x382);
        return Write(frame, main, Read(frame, main).WriteWord(12, (ushort)age).WriteDword(16, 987).WriteDword(0xE10, 0xA0604020));
    }

    private static NativeBattleEffect301Frame Frame(int age, bool initialized = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootBankLength, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteBytes(12, new byte[8]).WriteWord(12, (ushort)age).WriteByte(15, initialized ? (byte)1 : (byte)0)) };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (initialized)
        {
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                pools = pools.Add(NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity));
            }
        }
        NativeBattleEffect301State state = NativeBattleEffect301State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(6));
        NativeBattleOriginalEffectState effect = new(301, 300, new(301, Entry, Initializer), new(6, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        return new(NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools), effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public ushort[] Flags { get; } = [0, 0, 0, 0, 0, 0, 0xABCD];
        public uint[] Colors { get; } = Enumerable.Repeat(0xA0604020u, 7).ToArray();
        public List<NativeBattleEffect301Operation> Operations { get; } = [];

        public NativeBattleEffect301Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, (_, model) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(Flags[(model - ModelAddress(0)) / 156])).Write(44, BitConverter.GetBytes(Colors[(model - ModelAddress(0)) / 156])), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, _, retained) => new(frame, retained), (frame, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleEffect301ModelFlags flags)
            {
                int actor = (int)((flags.ModelAddress - ModelAddress(0)) / 156);
                ushort current = Flags[actor];
                Flags[actor] = flags.Width == 1 ? (ushort)((current & 0xFF00) | (((current & flags.AndMask) | flags.OrMask) & 255)) : (ushort)((current & flags.AndMask) | flags.OrMask);
            }
            if (operation is NativeBattleEffect301WriteModelBytes write)
            {
                int actor = (int)((write.ModelAddress - ModelAddress(0)) / 156);
                if (write.Offset == 0)
                {
                    Flags[actor] = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(write.Data.Read(0, 2));
                }
                else
                {
                    byte[] bytes = BitConverter.GetBytes(Colors[actor]);
                    write.Data.Read(0, write.Length).CopyTo(bytes, write.Offset - 44);
                    Colors[actor] = BitConverter.ToUInt32(bytes);
                }
            }
            return frame;
        });
    }
}
