using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Frame Initialize(NativeBattleEffect187Frame frame, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect187 entry requires its inactive original fixed banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect187 requires its actual current parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int target = packets.ReadContact(header.FirstContactIndex ?? throw new NotSupportedException("Effect187 requires its actual first contact.")).Primary.ActorIndex;
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress).WriteDword(0xE41E50, CommonAddress + 0x30000).WriteDword(0xE41E54, CommonAddress + 0x31000).WriteDword(0xE41E58, CommonAddress + 0x32000).WriteDword(0xE41E98, CommonAddress + 0x38000).WriteDword(0x24FE8E8, 0xFA54A8).WriteDword(0x24FE7B0, 0x1D99A78).WriteDword(0x24FE7B4, parameters.Actor78).WriteDword(0x24FD508, unchecked((uint)target)) };
        NativeBattleEffectTaskPool root = InitializeBank(frame, 0);
        NativeBattleEffectTaskPool children = InitializeBank(frame, 1);
        (root, int rootSlot) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(rootSlot, root.Slots[rootSlot].WriteWord(12, 0)) };
        (children, int mainSlot) = children.Append(MainCallback);
        children = children with { Slots = children.Slots.SetItem(mainSlot, children.Slots[mainSlot].WriteWord(12, 0)) };
        frame = frame with { State = frame.State.DetachBank(0).DetachBank(1), Effect = frame.Effect with { Pools = [root, children], ReturnedDescriptor = RootDescriptor } };
        NativeBattleEffectArenaWriter arena = new(frame.Memory.Arena ?? throw new NotSupportedException("Effect187 requires its actual COMMON owner."));
        for (int bank = 0; bank < 3; bank++)
        {
            for (int slot = 0; slot < 150; slot++)
            {
                arena.Write(0x30000 + bank * 0x1000 + slot * 24, BitConverter.GetBytes(0u));
            }
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena.Freeze() } };
        frame = inputs.Execute(frame, new NativeBattleEffect187UploadTim505E30(TextureAddress));
        short yaw = unchecked((short)((inputs.ReadModel(frame, target).Rotation14 ?? throw new NotSupportedException("Effect187 requires its actual target rotation.")) + 0x800));
        NativeBattleActionMatrix orientation = NativeBattleGfMatrices.Euler56CD50(new(0, yaw, 0));
        frame = WriteBytes(frame, BaseMatrix, PackedArena(NativeBattlePackedMatrix32.FromMatrix(orientation, null)), 32);
        NativeBattleEffectArena center = inputs.ReadTargetCenter5020A0(frame, parameters.TargetMask7A, Context(frame, 0x24FD978, 8));
        frame = PublishContext(frame, 0x24FD978, center, 8);
        short centerZ = BinaryPrimitives.ReadInt16LittleEndian(center.Read(4, 2));
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(inputs.ReadWorld(frame, target).ReadMatrix(), new(0, 0, -4400));
        frame = WriteBytes(frame, BaseMatrix, PackedArena(NativeBattlePackedMatrix32.FromMatrix(orientation with { X = offset.X, Y = offset.Y, Z = unchecked(centerZ + offset.Z) }, null)), 32);
        return frame with { State = frame.State.WriteDword(0x24FD50C, 0) };
    }

    private static NativeBattleEffectTaskPool InitializeBank(NativeBattleEffect187Frame frame, int index)
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

    public static (NativeBattleEffect187Frame Frame, uint Address) Spawn647900(NativeBattleEffect187Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect187 exhausted its mandatory child bank.");
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, 0)), address);
    }

    private static void RequireBinding(NativeBattleEffect187Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect187 requires its actual selected entry.");
        }
    }
}
