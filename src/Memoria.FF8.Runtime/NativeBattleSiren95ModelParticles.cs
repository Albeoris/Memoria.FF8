using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSiren95ModelParticles
{
    public static NativeBattleSiren95Frame Emit(NativeBattleSiren95Frame frame, uint address, bool secondary, NativeBattleEffectOwnedModelResource resource, NativeBattleActionPacketsState packets, ImmutableArray<byte> selectors)
    {
        if (selectors.IsDefault || selectors.Length != 32)
        {
            throw new InvalidDataException("Native Siren model particles require their original sixteen WORD selectors.");
        }
        NativeBattleSiren95State state = frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren particles require their retained model state.");
        NativeBattleEffectOwnedModelState model = state.ModelState ?? throw new NotSupportedException("Native Siren particles require their actual decoded model pose.");
        if (model.TaskAddress != address)
        {
            throw new InvalidDataException("Native Siren particles reference another model task.");
        }
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        NativeBattleActionMatrix world = new(Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x70 + index * 2))).ToImmutableArray(), unchecked((int)task.ReadDword(0x84)), unchecked((int)task.ReadDword(0x88)), unchecked((int)task.ReadDword(0x8C)), 4096);
        NativeBattleDecodedPose pose = model.DecodedPose ?? throw new NotSupportedException("Native Siren particles require their current decoded pose.");
        for (int index = 0; index < (secondary ? 2 : 4); index++)
        {
            frame = frame with { Random = frame.Random.Draw(out ushort choice) };
            int selected = secondary ? 12 + choice % 4 : choice % 12;
            short selector = BinaryPrimitives.ReadInt16LittleEndian(selectors.AsSpan().Slice(selected * 2));
            if ((ushort)selector > byte.MaxValue)
            {
                throw new NotSupportedException("Native model bone selector exceeds the supplied BYTE selector contract.");
            }
            frame = frame with { Random = frame.Random.Draw(out ushort fraction) };
            var appended = NativeBattleSiren95Actors.AppendTask(frame, 0x257FA90, secondary ? 0x747220u : 0x746FD0u, 0xE0, address, packets);
            frame = appended.Frame;
            NativeBattleModulePoolTask child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(appended.Address);
            NativeBattleVector point = NativeBattlePoseMatrices.ReadEffectOwnedBone(resource.PoseSource, pose, world, (byte)selector, (short)(fraction & 4095));
            child = child.WriteWord(0x6C + index * 8, unchecked((ushort)point.X)).WriteWord(0x6E + index * 8, unchecked((ushort)point.Y)).WriteWord(0x70 + index * 8, unchecked((ushort)point.Z));
            if (secondary)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    frame = frame with { Random = frame.Random.Draw(out ushort velocity) };
                    child = child.WriteWord(0x8C + index * 8 + axis * 2, unchecked((ushort)((velocity & 1023) - 512)));
                }
            }
            else
            {
                frame = frame with { Random = frame.Random.Draw(out ushort velocity) };
                child = child.WriteWord(0x8E + index * 8, (ushort)((velocity & 255) + 256));
            }
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(appended.Address, child).Pools } };
        }
        return frame;
    }
}
