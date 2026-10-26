using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Frame Initialize(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect326 entry requires its inactive original fixed banks.");
        }
        var bound = frame.State.BindSources(RequireStaging(frame), resources);
        frame = frame with { State = bound.State, Staging = bound.Staging };
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect326 requires its actual current parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int target = packets.ReadContact(header.FirstContactIndex ?? throw new NotSupportedException("Effect326 requires its actual first contact.")).Primary.ActorIndex;
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress).WriteDword(0xE19668, CommonAddress + 0x30800).WriteDword(0xE1966C, CommonAddress + 0x31800).WriteDword(0xE19670, CommonAddress + 0x32800).WriteDword(0xE196B0, CommonAddress + 0x38800).WriteDword(0x24F22E4, PixelSource).WriteDword(0x24F2174, 0x1D99A78).WriteDword(0x24F2178, parameters.Actor78).WriteDword(0x24F0C80, unchecked((uint)target)) };
        NativeBattleEffectTaskPool root = InitializeBank(frame, 0);
        NativeBattleEffectTaskPool children = InitializeBank(frame, 1);
        (root, int rootSlot) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(rootSlot, root.Slots[rootSlot].WriteWord(12, 0)) };
        (children, int mainSlot) = children.Append(MainCallback);
        children = children with { Slots = children.Slots.SetItem(mainSlot, children.Slots[mainSlot].WriteWord(12, 0)) };
        frame = frame with { State = frame.State.DetachBank(0).DetachBank(1), Effect = frame.Effect with { Pools = [root, children], ReturnedDescriptor = RootDescriptor } };
        NativeBattleEffectArenaWriter arena = new(frame.Memory.Arena ?? throw new NotSupportedException("Effect326 requires its actual COMMON owner."));
        for (int bank = 0; bank < 3; bank++)
        {
            for (int slot = 0; slot < 150; slot++)
            {
                arena.Write(0x30800 + bank * 0x1000 + slot * 24, BitConverter.GetBytes(0u));
            }
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena.Freeze() } };
        frame = inputs.Execute(frame, new NativeBattleEffect326UploadTim505E30(TextureAddress));
        short yaw = unchecked((short)((inputs.ReadModel(frame, target).Rotation14 ?? throw new NotSupportedException("Effect326 requires its actual target rotation.")) + 0x800));
        NativeBattleActionMatrix orientation = NativeBattleGfMatrices.Euler56CD50(new(0, yaw, 0));
        frame = WriteBytes(frame, BaseMatrix, PackedArena(NativeBattlePackedMatrix32.FromMatrix(orientation, null)), 32);
        NativeBattleEffectArena center = inputs.ReadTargetCenter5020A0(frame, parameters.TargetMask7A, Context(frame, 0x24F1308, 8));
        frame = PublishContext(frame, 0x24F1308, center, 8);
        short centerZ = BinaryPrimitives.ReadInt16LittleEndian(center.Read(4, 2));
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(inputs.ReadWorld(frame, target).ReadMatrix(), new(0, 0, -4400));
        frame = WriteBytes(frame, BaseMatrix, PackedArena(NativeBattlePackedMatrix32.FromMatrix(orientation with { X = offset.X, Y = offset.Y, Z = unchecked(centerZ + offset.Z) }, null)), 32);
        return frame with { State = frame.State.WriteDword(0x24F0C84, 0) };
    }

    private static NativeBattleEffectTaskPool InitializeBank(NativeBattleEffect326Frame frame, int index)
    {
        NativeBattleCureFamilyPool shape = Pools[index];
        NativeBattleEffectArena bank = frame.State.ReadBank(index);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
        for (int slot = 0; slot < shape.Capacity; slot++)
        {
            int offset = slot * shape.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(bank, offset, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
        }
        return pool;
    }

    public static (NativeBattleEffect326Frame Frame, uint Address) Spawn62AFB0(NativeBattleEffect326Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect326 exhausted its mandatory child bank.");
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, 0)), address);
    }

    private static void RequireBinding(NativeBattleEffect326Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect326 requires its actual selected entry.");
        }
    }
}
