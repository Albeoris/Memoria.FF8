using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97SpriteResources(uint Address, ImmutableArray<byte> Bytes);
public sealed record NativeBattleChoco97SpriteStep(NativeBattleModulePoolTask Task, NativeBattleModulePacketBatch? Packets, bool ReadyToComplete);

public static class NativeBattleChoco97Sprite
{
    public static NativeBattleChoco97SpriteStep Advance730C10(NativeBattleModulePoolTask task, NativeBattleChoco97SpriteResources resources, ImmutableArray<byte> contextActorRaw8, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        return AdvanceProfiled(task, resources, contextActorRaw8, view, projection, cameraRoll, 0x152B5C8);
    }

    internal static NativeBattleChoco97SpriteStep AdvanceProfiled(NativeBattleModulePoolTask task, NativeBattleChoco97SpriteResources resources, ImmutableArray<byte> contextActorRaw8, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll, uint sourceAddress, int taskBytes = 0x70, int delayOffset = 0x6C)
    {
        Require(task, resources, sourceAddress, taskBytes);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (contextActorRaw8.IsDefault || contextActorRaw8.Length != 8)
                {
                    throw new InvalidDataException("Native Choco sprite requires its actual raw actor origin.");
                }
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteDword(0x1C, BinaryPrimitives.ReadUInt32LittleEndian(contextActorRaw8.AsSpan())).WriteDword(0x20, BinaryPrimitives.ReadUInt32LittleEndian(contextActorRaw8.AsSpan().Slice(4)));
                task = task.WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) - 0x300))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) - 0x280))).WriteDword(0x4C, resources.Address).WriteWord(0x52, 19).WriteWord(0x54, 0xFF00).WriteWord(delayOffset, 4);
                Next();
                break;
            case 1:
                task = task.WriteWord(delayOffset, unchecked((ushort)(task.ReadWord(delayOffset) - 1)));
                if (Word(delayOffset) <= 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4));
                    Next();
                }
                break;
            case 2:
                AdvanceFrame();
                if (Word(0x50) > 8)
                {
                    task = task.WriteWord(delayOffset, 2);
                    Next();
                }
                break;
            case 3:
                AdvanceFrame();
                if (Word(0x50) > 9)
                {
                    task = task.WriteWord(0x50, 8);
                }
                task = task.WriteWord(delayOffset, unchecked((ushort)(task.ReadWord(delayOffset) - 1)));
                if (Word(delayOffset) <= 0)
                {
                    Next();
                }
                break;
            case 4:
                if (AdvanceFrame())
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next();
                }
                break;
            case 5:
                break;
            default:
                throw new NotSupportedException("Native Choco sprite phase leaves its six original callbacks.");
        }
        NativeBattleModulePacketBatch? packets = DrawProfiled(task, resources, view, projection, cameraRoll, sourceAddress, taskBytes);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, packets, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        bool AdvanceFrame()
        {
            task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
            if (Word(0x50) <= Word(0x52))
            {
                return false;
            }
            task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4));
            return true;
        }
    }

    public static NativeBattleModulePacketBatch? Draw730C90(NativeBattleModulePoolTask task, NativeBattleChoco97SpriteResources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        return DrawProfiled(task, resources, view, projection, cameraRoll, 0x152B5C8);
    }

    internal static NativeBattleModulePacketBatch? DrawProfiled(NativeBattleModulePoolTask task, NativeBattleChoco97SpriteResources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll, uint sourceAddress, int taskBytes = 0x70)
    {
        Require(task, resources, sourceAddress, taskBytes);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return null;
        }
        if (task.ReadDword(0x4C) != resources.Address)
        {
            throw new NotSupportedException("Native Choco sprite requires its actual retained resource pointer.");
        }
        NativeBattleModuleResource resource = new(0, resources.Address, 0, 0, resources.Bytes);
        ushort frame = task.ReadWord(0x50);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = resource.ProjectedFrame571C80(frame),
            Phase = frame,
            Anchor = new(unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)), unchecked((short)task.ReadWord(0x20)), 0),
            Scale = 2048,
            DepthOffset = unchecked((short)task.ReadWord(0x54)),
            SpriteFlags = 0
        };
        return NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2);
    }

    private static void Require(NativeBattleModulePoolTask task, NativeBattleChoco97SpriteResources resources, uint sourceAddress, int taskBytes)
    {
        if (task.Payload.IsDefault || task.Payload.Length != taskBytes - 12 || resources.Address != sourceAddress || resources.Bytes.IsDefault || resources.Bytes.Length != 0x368)
        {
            throw new InvalidDataException("Native Choco sprite requires its complete physical task and original projected resource.");
        }
    }
}
