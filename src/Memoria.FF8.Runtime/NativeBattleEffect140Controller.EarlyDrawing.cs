using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect140Controller
{
    private static NativeBattleEffect140Frame DrawEarly6A67AB(NativeBattleEffect140Frame frame, uint task, int relative, int center, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 152);
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(2048) with { X = 0, Y = 0, Z = center };
        NativeBattleEffectArena context = Context(frame, temporary, 152);
        context = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(local, 0)), 0, context, 0x78, 32);
        frame = PublishContext(frame, temporary, context, 152);
        for (int index = 0; index < 26; index++)
        {
            int position = relative * 86 - index * 43;
            if (position < 0 || position >= 8352)
            {
                continue;
            }
            int factor = (position % 96) * 4096 / 96;
            uint first = 0x11D04BCu + (uint)(position / 96 * 8);
            NativeBattleVector a = Point(ReadBytes(frame, first, 8));
            NativeBattleVector b = Point(ReadBytes(frame, first + 8, 8));
            NativeBattleWeightedPointsStep blend = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, a, b, unchecked((short)(4096 - factor)), unchecked((short)factor), inputs.Colors);
            frame = frame with { Memory = blend.Memory };
            NativeBattleVector point = blend.Position with { Z = unchecked((short)(center - blend.Position.Z)) };
            (frame, uint sprite) = Allocate(frame, 180);
            frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), point, null, 1600, -256) };
            frame = NativeBattleEffect140Particles.Draw85A0(frame, sprite, (short)(index >> 1), resources, inputs);
            if (index == 0 && relative == 20)
            {
                NativeBattleCameraPose pose = inputs.ReadCameraPose(frame);
                frame = inputs.Execute(frame, new NativeBattleEffect140PublishCamera(pose with { Target = V(point.X, point.Y, point.Z), Eye = pose.Eye with { Z = unchecked((short)(center + 2000)) } }, inputs.ReadEyePadding(frame), null));
            }
            point = point with { X = unchecked((short)-point.X) };
            frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), point, null, 1600, -256) };
            frame = NativeBattleEffect140Particles.Draw85A0(frame, sprite, (short)(index >> 1), resources, inputs);
            if (index == 0 && relative == 40)
            {
                NativeBattleCameraPose pose = inputs.ReadCameraPose(frame);
                frame = inputs.Execute(frame, new NativeBattleEffect140PublishCamera(pose with { Target = V(point.X, point.Y, point.Z), Eye = pose.Eye with { Z = unchecked((short)(center + 2000)) } }, inputs.ReadEyePadding(frame), null));
            }
            frame = Release(frame, 180);
        }
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        context = Context(frame, temporary, 152);
        context = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(composed, 0)), 0, context, 0x78, 32);
        uint raw = unchecked((uint)(relative << 12));
        uint high = (uint)((ulong)0x58ED2309 * raw >> 32);
        int brightness = unchecked((int)((((raw - high) >> 1) + high) >> 6)) - 4096;
        context = context.Write(0x38, BitConverter.GetBytes(brightness)).Write(0x14, BitConverter.GetBytes((short)0)).Write(0x16, BitConverter.GetBytes((short)0)).Write(0x18, BitConverter.GetBytes((short)256)).Write(0x1A, BitConverter.GetBytes((short)256)).Write(0x1C, BitConverter.GetBytes((short)0)).Write(0x1E, BitConverter.GetBytes((short)0)).Write(0x20, BitConverter.GetBytes((short)128)).Write(0x22, BitConverter.GetBytes((short)256));
        frame = PublishContext(frame, temporary, context, 152);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, 0));
        uint[] sources = [0x11D04B4, 0x11D2594, 0x11D3778];
        int[] phases = [relative & 127, (relative & 63) * 2, (-relative) & 127];
        for (int index = 0; index < sources.Length; index++)
        {
            context = Context(frame, temporary, 152).Write(0, BitConverter.GetBytes(sources[index])).Write(4, BitConverter.GetBytes(sources[index] + 8)).Write(0x10, BitConverter.GetBytes(unchecked((short)phases[index])));
            frame = PublishContext(frame, temporary, context, 152);
            frame = NativeBattleEffect140Meshes.DrawMirrored(frame, temporary, resources, inputs);
        }
        frame = Release(frame, 152);
        return AdvanceEarlyClock6A6A6E(frame, task, relative, center, inputs);
    }

    public static NativeBattleEffect140Frame AdvanceEarlyClock6A6A6E(NativeBattleEffect140Frame frame, uint task, int relative, int center, NativeBattleEffect140Inputs inputs)
    {
        if (frame.State.ReadDword(0x2517AAC) != 0)
        {
            return frame;
        }
        NativeBattleCameraPose current = inputs.ReadCameraPose(frame);
        if (relative < 60)
        {
            if (relative >= 20)
            {
                current = current with { Eye = current.Eye with { Z = unchecked((short)(current.Eye.Z + 200)) }, Target = current.Target with { Z = unchecked((short)(current.Target.Z + 100)) } };
                frame = inputs.Execute(frame, new NativeBattleEffect140PublishCamera(current, inputs.ReadEyePadding(frame), inputs.ReadTargetPadding(frame)));
            }
            return Write(frame, task, Read(frame, task).WriteWord(0xC, unchecked((ushort)(Read(frame, task).ReadWord(0xC) + 1))));
        }
        if (relative == 60)
        {
            frame = Write(frame, task, Read(frame, task).WriteWord(0xC, 81));
            return inputs.Execute(frame, new NativeBattleEffect140PublishCamera(current with { Target = V(0, 0, center), Eye = V(0, -2000, center + 2000) }, inputs.ReadEyePadding(frame), inputs.ReadTargetPadding(frame)));
        }
        if (relative >= 80)
        {
            int factor = unchecked((relative + 0xFFFB0) << 12) / 20;
            NativeBattleWeightedPointsStep target = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, new(0, 0, unchecked((short)center)), new(0, 293, unchecked((short)(center + 209))), unchecked((short)(4096 - factor)), unchecked((short)factor), inputs.Colors);
            frame = frame with { Memory = target.Memory };
            NativeBattleWeightedPointsStep eye = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, new(0, -2000, unchecked((short)(center + 2000))), new(0, -3000, unchecked((short)(center + 4500))), unchecked((short)(4096 - factor)), unchecked((short)factor), inputs.Colors);
            frame = frame with { Memory = eye.Memory };
            return inputs.Execute(frame, new NativeBattleEffect140PublishCamera(current with { Target = V(target.Position.X, target.Position.Y, target.Position.Z), Eye = V(eye.Position.X, eye.Position.Y, eye.Position.Z) }, inputs.ReadEyePadding(frame), inputs.ReadTargetPadding(frame)));
        }
        return frame;
    }

    private static NativeBattleVector Point(NativeBattleEffectArena arena) => new(BinaryPrimitives.ReadInt16LittleEndian(arena.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(arena.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(arena.Read(4, 2)));
}
