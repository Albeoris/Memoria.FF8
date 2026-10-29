using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRestorationAnimatedAnchorAssetsTests
{
    [Theory]
    [InlineData((ushort)24, (sbyte)0, (byte)3, (short)204, 4921600)]
    [InlineData((ushort)27, (sbyte)12, (byte)0, (short)409, -124528000)]
    public void Actual_first_mode4_sources_read_their_own_curves_and_interpolate_at_frame_one_after_restore(ushort effect, sbyte sourceIndex, byte anchor, short ratio, int expectedY)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRestorationResources resources = game.ReadNativeRestorationResources(effect);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!.BeginLoad(effect - 1);
        NativeBattleRestorationExecutableState bank = (effect == 24 ? memory.EsunaExecutable! : memory.FullLifeExecutable!).Initialize(resources.Executable!);
        memory = effect == 24 ? memory with { EsunaExecutable = bank } : memory with { FullLifeExecutable = bank };
        uint source = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(profile.ExecutableAddress + 4u + 4u * (uint)sourceIndex, 4));
        NativeBattleAnimatedAnchorChannels channels = NativeBattleEffectAnimatedAnchor.ReadChannels(bank.Read(source, 0x158), 1, bank.Read);
        Assert.Equal((byte)0, channels.StartActor);
        Assert.Equal((byte)0, channels.EndActor);
        Assert.Equal(anchor, channels.StartAnchor);
        Assert.Equal(anchor, channels.EndAnchor);
        Assert.Equal(ratio, channels.Ratio);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(profile.Descriptors[0], profile.Banks[0], 100, 2), NativeBattleEffectTaskPool.Initialize(profile.Descriptors[1], profile.Banks[1], 88, 4), NativeBattleEffectTaskPool.Initialize(profile.Descriptors[2], profile.Banks[2], 676, 3), NativeBattleEffectTaskPool.Initialize(profile.Descriptors[3], profile.Banks[3], 64, 10)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.MainCallback, 100);
        NativeBattleEffectHierarchyAppend generic = root.Hierarchy.Append8DC540(2, profile.GenericCallback, 676, root.Address, (_, _, _) => new(0, 0));
        hierarchy = generic.Hierarchy;
        int vector = 0x94 + anchor * 0x40;
        NativeBattleModulePoolTask task = hierarchy.Read(generic.Address).WriteDword(0x34 + 0x224, profile.ExecutableAddress + 4).WriteDword(0x34 + vector, 10u << 16).WriteDword(0x34 + vector + 4, effect == 24 ? 100u << 16 : 0).WriteDword(0x34 + vector + 8, 20u << 16);
        hierarchy = hierarchy.Write(generic.Address, task);
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), profile.Descriptors[0]) { Pools = hierarchy.Pools };
        int first = 2 * profile.DrawBankBytes;
        NativeBattleRestorationFrame frame = new(memory, selected, new(0, null, null, first + profile.EmitterBytes + profile.GeometryBytes, null) { Emitters = first, Geometry = first + profile.EmitterBytes, EmitterScanCursor = 0, GeometryScanCursor = 0 });
        NativeBattleRestorationNodeAllocation allocated = NativeBattleRestorationNodes.AllocateEmitter(frame, generic.Address, 0, sourceIndex, 0);
        frame = allocated.Frame;
        int node = allocated.ArenaOffset!.Value;
        byte[] matrix = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(matrix, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(16), 4096);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(node + 0x2C, matrix).Write(node + 0x60, [1, 0]).Write(node + 0x58, [0xEF, 0xBE, 0xAD, 0xDE]).MarkUnknown(node + 0x2C + 18, 2) } };
        NativeBattleRestorationFrame clone = NativeBattleSnapshotJson.Deserialize<NativeBattleRestorationFrame>(NativeBattleSnapshotJson.Serialize(frame))!;
        NativeBattleRestorationFrame actual = NativeBattleRestorationAnimatedAnchor.Advance(frame, generic.Address, node);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(actual), NativeBattleSnapshotJson.Serialize(NativeBattleRestorationAnimatedAnchor.Advance(clone, generic.Address, node)));
        Assert.Equal(new int[] { 10 << 16, expectedY, 20 << 16 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt32LittleEndian(actual.Memory.Arena!.Read(node + 0x4C + axis * 4, 4))));
        Assert.Equal(0xDEADBEEFu, BinaryPrimitives.ReadUInt32LittleEndian(actual.Memory.Arena!.Read(node + 0x58, 4)));
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena!.Read(node + 0x2C + 18, 2));
    }
}
