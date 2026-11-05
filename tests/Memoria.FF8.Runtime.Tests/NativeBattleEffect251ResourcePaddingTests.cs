using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect251ResourcePaddingTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Bounds_padding_observes_knownness_and_root_restore_writes_only_the_selected_domain(bool remastered, bool logical, bool unknown)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        byte[] guest = Skeleton(0x1111);
        byte[] resource = Skeleton(0x2222);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0];
        bank = unknown && !logical ? bank.BindResource(4, guest.ToImmutableArray(), 0, 50).BindResource(56, guest.ToImmutableArray(), 52, 12) : bank.BindResource(4, guest.ToImmutableArray(), 0, guest.Length);
        int far = layout.BankLength + 4;
        bank = unknown && logical ? bank.BindPartyResource(far, resource.ToImmutableArray(), 0, 50).BindPartyResource(far + 52, resource.ToImmutableArray(), 52, 12) : bank.BindPartyResource(far, resource.ToImmutableArray(), 0, resource.Length);
        NativeBattleModelCpuReference selected = new(0, logical ? far : 4) { Layout = layout, ResourceOnly = logical };
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) }, Rows = cpu.Rows.SetItem(0, cpu.Rows[0].Set(12, selected)) };
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!.SetValue(session, cpu);
        NativeBattlePoseSource source = new((logical ? resource : guest).ToImmutableArray(), [1]);
        NativeBattleGraphicsModel model = new(0, 3) { ActiveResourceRow100 = 0, CurrentBodyBoneMatrices = new(NativeBattleBodyBoneMatrices.Identity(source), [NativeBattleCuragaMatrices.Identity8DD770()]), SequenceState = new() { DecodedPose = new(new(0, 0, 0), [new(new(0, 0, 0), new(1024, 1024, 1024))], false) } };
        NativeBattleGraphicsState graphics = new([model], ["party"], 0, 0);
        NativeBattleEffect251Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), new(251, 250, new(251, 1, 1), null, null), new(42), NativeBattleEffect251State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[128]) };
        MethodInfo read = typeof(BattleSession).GetMethod("ReadNativeEffect251BonePadding", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(unknown ? null : (short?)(logical ? 0x2222 : 0x1111), (short?)read.Invoke(session, [graphics, frame, 0, 0]));
        object?[] arguments = [graphics, frame, 0, source];
        frame = (NativeBattleEffect251Frame)typeof(BattleSession).GetMethod("RestoreNativeEffect251BoundsPose", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments)!;
        graphics = (NativeBattleGraphicsState)arguments[0]!;
        Assert.Equal((short)0, (short?)read.Invoke(session, [graphics, frame, 0, 0]));
        NativeBattleResourcePointer opposite = new(new(0, logical ? 4 : far) { Layout = layout, ResourceOnly = !logical }, null, 64);
        byte[] unchanged = NativeBattleModelResourceView.Open(session.NativeModelCpuResources!, frame.Staging!, opposite, 64).Read(50, 2);
        Assert.Equal(BitConverter.GetBytes((ushort)(logical ? 0x1111 : 0x2222)), unchanged);
        Assert.NotSame(bank, session.NativeModelCpuResources!.Cpu.Banks[0]);
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(session.NativeModelCpuResources));
        Assert.Equal(new byte[2], NativeBattleModelResourceView.Open(restored, frame.Staging!, new NativeBattleResourcePointer(selected, null, 64), 64).Read(50, 2));
    }

    private static byte[] Skeleton(ushort padding)
    {
        byte[] data = new byte[64];
        data[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16), -1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(50), padding);
        return data;
    }
}
