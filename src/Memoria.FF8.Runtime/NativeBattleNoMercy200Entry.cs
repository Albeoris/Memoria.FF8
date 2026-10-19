using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNoMercy200EntryStep(NativeBattleNoMercy200Frame Frame, short Facing14);

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200EntryStep Enter6391D0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging staging, NativeBattleActionPacketsState packets, NativeBattleNoMercy200Resources resources, NativeCrtRandomState random, NativeBattleVector actorRotation, ushort? actorRotationPadding, NativeBattleVector bone241, NativeBattleVector targetCenter)
    {
        if (effect.Binding is not { Effect: 200, Initializer: 0x575530, Entry: 0x6391D0 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native6391D0 requires its original binding and actual action parameters.");
        }
        resources.Validate();
        NativeBattleNoMercy200State state = memory.NoMercy200 ?? throw new NotSupportedException("Native6391D0 requires its retained process state; migrate the selected standalone explicitly.");
        if (!state.GlobalsInitialized)
        {
            staging = staging.Write(NativeBattleNoMercy200Resources.StaticAddress, resources.StaticData.Data.AsSpan());
            foreach (NativeBattleNoMercy200ColdRegion region in resources.ColdRegions)
            {
                staging = staging.Write(region.Address, region.Data.AsSpan());
            }
        }
        int first = packets.Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Native6391D0 requires its actual first contact pointer.");
        byte recipient = packets.ReadContact(first).Primary.ActorIndex;
        staging = WriteDword(staging, 0x24FBD60, 0x20DFAB8);
        staging = WriteDword(staging, 0x24FBCC8, 0x1D99A78);
        staging = WriteDword(staging, 0x24F8D58, recipient);
        staging = WriteDword(staging, 0x24FBCE0, parameters.Actor78);
        for (uint address = 0x24F8DB0; address < 0x24F9710; address += 24)
        {
            staging = WriteDword(staging, address, 0);
        }
        staging = WritePoint(staging, 0x24F9950, actorRotation);
        staging = actorRotationPadding is ushort padding ? WriteWord(staging, 0x24F9956, padding) : staging.MarkUnknown(0x24F9956, 2);
        staging = WritePoint(staging, 0x24FBCD0, bone241);
        staging = WritePoint(staging, 0x24F9978, targetCenter);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(targetCenter.X - bone241.X, 0, targetCenter.Z - bone241.Z);
        ReadOnlySpan<byte> axis = resources.Read(0xE33D40, 12);
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(BinaryPrimitives.ReadInt32LittleEndian(axis), BinaryPrimitives.ReadInt32LittleEndian(axis.Slice(4)), BinaryPrimitives.ReadInt32LittleEndian(axis.Slice(8)), direction.X, direction.Y, direction.Z);
        staging = StoreMatrix(staging, 0x24F8D90, new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), bone241.X, 0, bone241.Z, 4096));
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(Root, 0x24F8D60, 16, 2);
        (root, int rootSlot) = root.Append(0x63E680);
        root = root with { Slots = root.Slots.SetItem(rootSlot, root.Slots[rootSlot].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(Children, 0x24F99A0, 36, 250);
        (children, int childSlot) = children.Append(0x639380);
        children = children with { Slots = children.Slots.SetItem(childSlot, children.Slots[childSlot].WriteWord(12, 0)) };
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native6391D0 requires its actual texture owner.");
        (textures, _) = textures.EnqueueTim505E30(resources.InitialTim);
        state = state with { GlobalsInitialized = true, Resources = resources, Parameters = parameters, CameraTask = null };
        NativeBattleNoMercy200Frame frame = new(memory with { NoMercy200 = state, Textures = textures }, effect with { Pools = [root, children], ReturnedDescriptor = Root, DrawArenaOffset = 0 }, staging, random);
        short facing = unchecked((short)(alignment.Axis.Y < 0 ? -alignment.Angle : alignment.Angle));
        return new(frame, facing);
    }

    private static NativeBattleFileStaging WriteDword(NativeBattleFileStaging staging, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }

    private static NativeBattleFileStaging WriteWord(NativeBattleFileStaging staging, uint address, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return staging.Write(address, bytes);
    }

    private static NativeBattleFileStaging WritePoint(NativeBattleFileStaging staging, uint address, NativeBattleVector point) => WriteWord(WriteWord(WriteWord(staging, address, point.X), address + 2, point.Y), address + 4, point.Z);
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short ReadWord(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt16LittleEndian(staging.Read(address, 2));
    private static uint TaskDword(NativeBattleModulePoolTask task, int offset) => (uint)task.ReadWord(offset) | (uint)task.ReadWord(offset + 2) << 16;
    private static NativeBattleModulePoolTask TaskDword(NativeBattleModulePoolTask task, int offset, uint value) => task.WriteWord(offset, unchecked((ushort)value)).WriteWord(offset + 2, unchecked((ushort)(value >> 16)));
    private static NativeBattleVector Point(NativeBattleModulePoolTask task) => new(Word(task, 16), Word(task, 18), Word(task, 20));
}
