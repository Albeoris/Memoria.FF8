using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect9ParticleInputs(NativeBattleActionPacketsState Packets, ImmutableArray<byte> BirthSchedule, NativeBattleModuleResource Sprite, Func<NativeBattleEffect9Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleEffect9Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect9Frame, short> ReadCameraRoll, Func<NativeBattleEffect9Frame, NativeBattleGfGeometryBatch, NativeBattleEffect9Frame> AppendCommon);

public static class NativeBattleEffect9Particles
{
    public static NativeBattleEffect9Step Advance(NativeBattleEffect9Frame frame, uint address, NativeBattleEffect9ParticleInputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect9Controller.Read(frame, address);
        if (task.Callback == 0x8C3D40)
        {
            return AdvanceBirth(frame, address, inputs);
        }
        if (task.Callback != 0x8C3E00)
        {
            throw new NotSupportedException("Effect9 particles require their selected native callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                (NativeCrtRandomState random, NativeBattleModulePoolTask initialized) = Initialize(frame.Random, task);
                frame = frame with { Random = random };
                task = initialized;
                break;
            case 1:
                task = AdvanceLife(task);
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Effect9 particle selected an unsupported phase.");
        }
        NativeBattleVector anchor = new(unchecked((short)frame.State.ReadWord(0x27571B8)), unchecked((short)frame.State.ReadWord(0x27571BA)), unchecked((short)frame.State.ReadWord(0x27571BC)));
        task = Position(task, anchor);
        frame = NativeBattleEffect9Controller.Write(frame, address, task);
        if ((task.ReadByte(0x26) & 4) == 0)
        {
            frame = Draw(frame, task, inputs);
        }
        return NativeBattleEffect9Controller.Finish(frame, address);
    }

    public static (NativeCrtRandomState Random, NativeBattleModulePoolTask Task) Initialize(NativeCrtRandomState random, NativeBattleModulePoolTask task)
    {
        random = random.Draw(out ushort x);
        random = random.Draw(out ushort y);
        random = random.Draw(out ushort z);
        task = task.WriteDword(0x4C, 0x1634B84).WriteWord(0x52, 10).WriteWord(0x40, (ushort)(x & 0xFFF)).WriteWord(0x42, (ushort)(y & 0xFFF)).WriteWord(0x44, (ushort)(z & 0xFFF)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        return (random, task);
    }

    public static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleVector anchor)
    {
        task = task.WriteWord(0x42, unchecked((ushort)(task.ReadWord(0x42) + 0x100)));
        NativeBattleVector angles = new(unchecked((short)task.ReadWord(0x40)), unchecked((short)task.ReadWord(0x42)), unchecked((short)task.ReadWord(0x44)));
        NativeBattleActionMatrix basis = NativeBattleCuragaMatrices.Euler8818A0(angles);
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, 0, 192));
        return task.WriteWord(0x1C, unchecked((ushort)(anchor.X + offset.X))).WriteWord(0x1E, unchecked((ushort)(anchor.Y + offset.Y))).WriteWord(0x20, unchecked((ushort)(anchor.Z + offset.Z)));
    }

    public static NativeBattleModulePoolTask AdvanceLife(NativeBattleModulePoolTask task)
    {
        short next = unchecked((short)(task.ReadWord(0x50) + 1));
        short maximum = unchecked((short)task.ReadWord(0x52));
        task = task.WriteWord(0x50, unchecked((ushort)next));
        if (next > maximum)
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)).WriteWord(0x50, unchecked((ushort)maximum)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        }
        return task;
    }

    private static NativeBattleEffect9Step AdvanceBirth(NativeBattleEffect9Frame frame, uint address, NativeBattleEffect9ParticleInputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect9Controller.Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                int counter = unchecked((short)task.ReadWord(0x24));
                if (inputs.BirthSchedule.IsDefault || (uint)counter >= (uint)inputs.BirthSchedule.Length)
                {
                    throw new NotSupportedException("Effect9 particle birth requires its current original schedule byte.");
                }
                int count = inputs.BirthSchedule[counter];
                if (unchecked((short)frame.State.ReadWord(NativeBattleEffect9State.ControlAddress + 0x40)) >= 4 || count == 255)
                {
                    frame = NativeBattleEffect9Controller.Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                else
                {
                    for (int child = 0; child < count; child++)
                    {
                        frame = NativeBattleEffect9Controller.Append(frame, 5, 0x8C3E00, 92, address, inputs.Packets).Frame;
                    }
                }
                break;
            case 1:
                break;
            default:
                throw new NotSupportedException("Effect9 birth selected an unsupported phase.");
        }
        return NativeBattleEffect9Controller.Finish(frame, address);
    }

    private static NativeBattleEffect9Frame Draw(NativeBattleEffect9Frame frame, NativeBattleModulePoolTask task, NativeBattleEffect9ParticleInputs inputs)
    {
        if (task.ReadDword(0x4C) != 0x1634B84 || inputs.Sprite.Address != 0x1634B84)
        {
            throw new NotSupportedException("Effect9 particle drawing requires its actual selected sprite.");
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect9 particles require their current physical temporary memory."), 0xD8);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 0xD8);
        NativeBattleProjectedSpriteHead head = NativeBattleProjectedSpriteD8.PrepareHead(frame.Memory, task, inputs.ReadCamera(frame), temporary);
        NativeBattleProjectedSpriteDraw drawn = NativeBattleProjectedSpriteD8.Draw(head, address => address == inputs.Sprite.Address ? inputs.Sprite : throw new NotSupportedException("Effect9 particle selected an unsupported sprite source."), inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.TemporaryD8, 0xD8);
        frame = inputs.AppendCommon(frame with { Memory = drawn.Memory, Staging = staging }, drawn.Batch);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging ?? throw new NotSupportedException("Effect9 particle drawing lost its temporary memory."), 0xD8) };
    }
}
