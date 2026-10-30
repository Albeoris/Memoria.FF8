using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect30Controller;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect30ParticleInputs(NativeBattlePackedMatrix32 Camera, NativeBattleCameraProjection Projection, short CameraRoll, Func<NativeBattleEffect30Frame, NativeBattleGfGeometryBatch, NativeBattleEffect30Frame> DeliverPrivate)
{
    public bool CapturePresentation { get; init; }
}

public static class NativeBattleEffect30Particles
{
    public static NativeBattleEffect30TaskStep Advance(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30Resources resources, NativeBattleEffect30ParticleInputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteDword(0x4C, resources.ParticleSprite.Address).WriteWord(0x52, 6);
                frame = Write(frame, address, task);
                for (int axis = 0; axis < 3; axis++)
                {
                    frame = frame with { Random = frame.Random.Draw(out ushort value) };
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x58 + axis * 2, unchecked((ushort)((value & 1023) - 512))));
                }
                frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 1));
                break;
            case 1:
                for (int axis = 0; axis < 3; axis++)
                {
                    int velocity = unchecked((short)task.ReadWord(0x58 + axis * 2));
                    short damped = unchecked((short)(velocity - unchecked(velocity * 256) / 4096));
                    task = task.WriteWord(0x58 + axis * 2, unchecked((ushort)damped));
                    task = task.WriteWord(0x1C + axis * 2, unchecked((ushort)(task.ReadWord(0x1C + axis * 2) + damped / 16)));
                }
                short phase = unchecked((short)(task.ReadWord(0x50) + 1));
                task = task.WriteWord(0x50, unchecked((ushort)phase));
                if (phase > unchecked((short)task.ReadWord(0x52)))
                {
                    task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)).WriteByte(0x29, 2);
                }
                frame = Write(frame, address, task);
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect30 particle phase leaves its original three-entry table.");
        }
        frame = Draw(frame, address, resources, inputs);
        return Finish(frame, address);
    }

    private static NativeBattleEffect30Frame Draw(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30Resources resources, NativeBattleEffect30ParticleInputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect30 particle requires its retained temporary memory."), 180);
        short? w = task.ReadUnknownPayloadRanges()?.Any(region => region.Offset < 0x24 && region.Offset + region.Length > 0x22) == true ? null : unchecked((short)task.ReadWord(0x22));
        NativeBattleVector position = new(unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)), unchecked((short)task.ReadWord(0x20)));
        NativeBattleEffectMemory memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.Camera, position, w, 4096, unchecked((short)task.ReadWord(0x54)));
        uint source = task.ReadDword(0x4C);
        if (source != resources.ParticleSprite.Address)
        {
            throw new NotSupportedException("Effect30 particle selected an unsupported sprite source.");
        }
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, source, 0).Write(allocation.Address + 4, BitConverter.GetBytes(task.ReadWord(0x50)));
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, allocation.Address, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(memory, temporary, _ => resources.ParticleSprite, inputs.Projection, inputs.CameraRoll, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(30, task) with { ResourceGeneration = source } : null);
        frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, drawn.Temporary180, 180) };
        frame = inputs.DeliverPrivate(frame, drawn.Batch);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging ?? throw new NotSupportedException("Effect30 particle lost its physical temporary owner."), 180) };
    }
}
