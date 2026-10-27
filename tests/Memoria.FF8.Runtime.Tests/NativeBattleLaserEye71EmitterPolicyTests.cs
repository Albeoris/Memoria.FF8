using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleLaserEye71Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLaserEye71EmitterPolicyTests
{
    [Fact]
    public void Three_dimensional_policy_uses_targetF1_minus_casterF0_signed_truncation_and_rotation_only_stores()
    {
        Fixture fixture = new();
        NativeBattleEffectNodeMemory memory = fixture.Memory();
        memory.ContextDword(0xD4, unchecked((uint)-65537));
        memory.ContextDword(0xD8, 196607);
        memory.ContextDword(0xDC, 327680);
        memory.ContextDword(0x1F4, 0);
        memory.ContextDword(0x1F8, 0);
        memory.ContextDword(0x1FC, 0);
        memory.MarkUnknown(EmittersOffset + 0x2C + 18, 14);
        NativeBattleVector? observed = null;
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleEffectNodeInputs inputs = fixture.Inputs() with
        {
            Orientation2Policy = NativeBattleEffectNodeOrientation2Policy.TargetF1MinusCasterF0Look3D,
            LookBasis8DDA50 = (owner, destination, direction, up) =>
            {
                observed = direction;
                Assert.Equal(new NativeBattleVector(0, 4096, 0), up);
                (byte[] data, bool[] known) = owner.FlushStorage().Memory.Arena!.ReadSnapshot(destination, 32);
                NativeBattleLookBasisStep result = NativeBattleLookBasis.Execute8DDA50(owner.CaptureProcessMemory(), staging, new(data.ToImmutableArray(), known.ToImmutableArray()), direction, up);
                staging = result.Staging;
                owner.PublishProcessMemory(result.Memory);
                for (int index = 0; index < 18; index += 2)
                {
                    owner.Word(destination + index, BinaryPrimitives.ReadUInt16LittleEndian(result.Matrix.Bytes.AsSpan().Slice(index, 2)));
                }
            }
        };
        new NativeBattleEffectNodeLifecycle(memory, fixture.Bank, inputs).InitializeEmitter(EmittersOffset, Source());
        Assert.Equal(new NativeBattleVector(-1, 2, 5), observed);
        NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
        Assert.Equal(new short[] { 5, 0, -1, 0, 4096, 2, 1, 0, 5 }, Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(written.Memory.Arena!.Read(EmittersOffset + 0x2C + index * 2, 2))).ToArray());
        Assert.Throws<NotSupportedException>(() => written.Memory.Arena!.Read(EmittersOffset + 0x2C + 18, 14));
        Assert.Equal(new int?[] { 5, 0, 1 }, written.Memory.ScriptedColorRegisters!.Mac.ToArray());
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Required_basis_service_is_lazy_and_default_policy_keeps_existing_horizontal_producer()
    {
        Fixture fixture = new();
        NativeBattleEffectNodeMemory memory = fixture.Memory();
        NativeBattleEffectNodeInputs selected = fixture.Inputs() with { Orientation2Policy = NativeBattleEffectNodeOrientation2Policy.TargetF1MinusCasterF0Look3D };
        NativeBattleEffectNodeLifecycle lifecycle = new(memory, fixture.Bank, selected);
        byte[] source = Source();
        source[0x1C] = 0;
        lifecycle.InitializeEmitter(EmittersOffset, source);
        source[0x1C] = 2;
        Assert.Throws<NotSupportedException>(() => lifecycle.InitializeEmitter(EmittersOffset, source));
        memory = fixture.Memory();
        memory.ContextDword(0x1C4, 65536);
        memory.ContextDword(0x1CC, 0);
        NativeBattleEffectNodeInputs original = fixture.Inputs() with { LookBasis8DDA50 = (_, _, _, _) => throw new InvalidOperationException("Default policy must retain its native yaw producer") };
        new NativeBattleEffectNodeLifecycle(memory, fixture.Bank, original).InitializeEmitter(EmittersOffset, source);
        Assert.Equal(0u, memory.Dword(EmittersOffset + 0x40));
        Assert.Equal(0u, memory.Dword(EmittersOffset + 0x44));
        Assert.Equal(0u, memory.Dword(EmittersOffset + 0x48));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_20_and_50_slot_scans_keep_native_last_slot_and_suffix_ownership(bool geometry)
    {
        Fixture fixture = new();
        NativeBattleEffectNodeMemory memory = fixture.Memory();
        int count = geometry ? GeometryCount : EmitterCount;
        int start = geometry ? GeometryOffset : EmittersOffset;
        int stride = geometry ? 672 : 108;
        int occupied = geometry ? 0x1D7 : 0x69;
        for (int index = 0; index < count - 1; index++)
        {
            memory.Byte(start + index * stride + occupied, 1);
        }
        ushort scan = 0;
        int? rejected = geometry ? NativeBattleEffectNodeAllocation.AllocateGeometry(memory, start, count, ref scan, EmittersOffset, 0) : NativeBattleEffectNodeAllocation.AllocateEmitter(memory, start, count, ref scan, 0, 0, 0);
        Assert.Null(rejected);
        Assert.Equal((byte)0, memory.Byte(start + (count - 1) * stride + occupied));
        scan = (ushort)(count - 1);
        int? accepted = geometry ? NativeBattleEffectNodeAllocation.AllocateGeometry(memory, start, count, ref scan, EmittersOffset, 0) : NativeBattleEffectNodeAllocation.AllocateEmitter(memory, start, count, ref scan, 0, 0, 0);
        Assert.Equal(start + (count - 1) * stride, accepted);
        Assert.Equal((ushort)0, scan);
        NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
        NativeBattleEffectArena restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(written.Memory.Arena)))!);
        Assert.Equal(written.Memory.Arena!.Read(start + (count - 1) * stride, stride), restored.Read(start + (count - 1) * stride, stride));
    }

    private static byte[] Source()
    {
        byte[] source = new byte[0x158];
        source[0x1C] = 2;
        source[0x11] = 2;
        return source;
    }
    private sealed class Fixture
    {
        private readonly NativeBattleEffectMemory memory;
        private readonly NativeBattleOriginalEffectState effect;
        private readonly uint address;
        public NativeBattleEffectNodeBank Bank { get; } = new(SourceAddress, new byte[SourceLength].ToImmutableArray());
        public Fixture()
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(Pools[2].Descriptor, Pools[2].Bank, 676, 3);
            (pool, int slot) = pool.Append(NativeBattleLaserEye71Controller.NodeCallback);
            pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[664]));
            address = pool.BankAddress;
            effect = new(71, 70, new(71, Entry, Initializer), null, null) { Pools = [pool] };
            memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        }
        public NativeBattleEffectNodeMemory Memory() => new(memory, effect, address, EmittersOffset, EmitterBytes, GeometryOffset, GeometryBytes, useSharedScratch: true);
        public NativeBattleEffectNodeInputs Inputs() => new(null, _ => throw new InvalidOperationException("Unreached model"), (_, _, _) => throw new InvalidOperationException("Unreached allocation"), (_, _) => throw new InvalidOperationException("Unreached allocation"));
    }
}
