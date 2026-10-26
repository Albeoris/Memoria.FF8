using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceScene633C30(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleEffect218Resources resources, NativeBattleSummonColors colors)
    {
        if (Read(frame, address).ReadWord(12) == 0)
        {
            NativeBattleEffectArena world = NativeBattleEffect218Model.InitializeRotation634040(frame.State.ReadView(StreamedModelAddress + 0x40, 32)).Write(20, new byte[12]);
            frame = frame with { State = frame.State.WriteView(StreamedModelAddress + 0x40, world, 32) };
            frame = InitializeOwned633FB0(frame, StreamedModelAddress, StreamedDescriptorAddress, resources.StreamedModel, 15);
            frame = Copy(frame, StreamedModelAddress + 0x40, EmbeddedModelAddress + 0x40, 32);
            frame = InitializeOwned633FB0(frame, EmbeddedModelAddress, EmbeddedDescriptorAddress, resources.EmbeddedModel, 16);
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        short age = Signed(Read(frame, address), 12);
        if (age < 20)
        {
            if (age == 0)
            {
                frame = SelectBoth218(frame, resources, 0);
                frame = Dword(frame, 0x24F35B0, unchecked((uint)-10000));
                frame = Write(frame, address, Read(frame, address).WriteWord(28, 320).WriteWord(32, 80).WriteWord(34, unchecked((ushort)-8)));
            }
            NativeBattleModulePoolTask task = Read(frame, address);
            short yVelocity = Signed(task, 32);
            short zVelocity = Signed(task, 34);
            short scale = Signed(task, 28);
            frame = Dword(frame, 0x24F35B0, unchecked(frame.State.ReadDword(0x24F35B0) + (uint)(int)yVelocity));
            frame = Dword(frame, 0x24F35B4, unchecked(frame.State.ReadDword(0x24F35B4) + (uint)(int)zVelocity));
            task = task.WriteWord(32, unchecked((ushort)(yVelocity + (yVelocity >> 3)))).WriteWord(34, unchecked((ushort)(zVelocity + (zVelocity >> 3)))).WriteWord(28, unchecked((ushort)(scale + (scale >> 3))));
            frame = Write(frame, address, task);
            frame = SetSceneDiagonal218(frame, scale);
            if (age == 0)
            {
                frame = SpawnShaft634070(frame);
            }
        }
        else if (age < 40)
        {
            if (age == 20)
            {
                frame = Dword(frame, 0x24F35B0, unchecked((uint)-5000));
                frame = Dword(frame, 0x24F35B4, 0);
            }
            frame = SetSceneDiagonal218(frame, 4096);
        }
        else
        {
            switch (age)
            {
                case 40:
                    frame = SelectBoth218(frame, resources, 1);
                    frame = Dword(frame, 0x24F35B0, 0);
                    break;
                case 45:
                    (frame, _) = AppendMain632E30(frame, 0x634300);
                    frame = SpawnBurst6345E0(frame);
                    break;
                case 66:
                    frame = SelectBoth218(frame, resources, 2);
                    break;
                case 310:
                    frame = SelectBoth218(frame, resources, 3);
                    (frame, _) = AppendMain632E30(frame, 0x634C00);
                    break;
                case 325:
                    (frame, _) = AppendMain632E30(frame, 0x637200);
                    break;
                case 371:
                    frame = SelectBoth218(frame, resources, 4);
                    break;
                case 401:
                    frame = SelectBoth218(frame, resources, 5);
                    break;
                case 416:
                    frame = frame with { State = frame.State.WriteByte(StreamedModelAddress, (byte)(frame.State.ReadByte(StreamedModelAddress) | 8)) };
                    break;
                case 506:
                    frame = Word(frame, StreamedModelAddress, frame.State.ReadWord(StreamedModelAddress) & 0xFFF7);
                    break;
                case 507:
                    frame = Dword(frame, 0x24F3544, frame.State.ReadDword(0x24F3544) | 2);
                    break;
            }
            if (age >= 510 && age < 526)
            {
                frame = FadeScene633F39(frame, age, colors);
            }
        }
        NativeBattleModulePoolTask current = Read(frame, address);
        current = current.WriteWord(12, unchecked((ushort)(current.ReadWord(12) + 1)));
        frame = Write(frame, address, current);
        if (Signed(current, 12) < 526)
        {
            return (frame, 0);
        }
        return (Dword(frame, 0x24F3544, frame.State.ReadDword(0x24F3544) | 1), 2);
    }

    public static NativeBattleEffect218Frame InitializeOwned633FB0(NativeBattleEffect218Frame frame, uint modelAddress, uint descriptorAddress, NativeBattleEffectOwnedModelResource resource, ushort argument)
    {
        NativeBattleEffect218ModelBuild built = NativeBattleEffect218Model.Initialize633FB0(frame.State.ReadView(modelAddress, 0x90), frame.State.ReadView(descriptorAddress, 0x34), descriptorAddress, resource, argument);
        return frame with { State = frame.State.WriteView(modelAddress, built.Model, 0x90).WriteView(descriptorAddress, built.Descriptor, 0x34) };
    }

    public static NativeBattleEffect218Frame SelectOwned634020(NativeBattleEffect218Frame frame, uint modelAddress, NativeBattleEffectOwnedModelResource resource, byte selector)
    {
        NativeBattleEffectArena model = frame.State.ReadView(modelAddress, 0x90);
        uint indirect = BinaryPrimitives.ReadUInt32LittleEndian(model.Read(0x64, 4));
        uint skeletonAddress = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, indirect, 4).Read(0, 4));
        if (skeletonAddress != resource.Address + resource.Section(0))
        {
            throw new InvalidDataException("Effect218 pose selection leaves its current model skeleton.");
        }
        NativeBattleEffect218ModelPose selected = NativeBattleEffect218Model.Select634020(model, ReadBytes(frame, skeletonAddress, resource.PoseSource.Skeleton.Length), resource.PoseSource.Animations.AsSpan(), selector);
        frame = WriteBytes(frame, modelAddress, selected.Model, 0x90);
        return WriteBytes(frame, skeletonAddress, selected.Skeleton, resource.PoseSource.Skeleton.Length);
    }

    private static NativeBattleEffect218Frame SelectBoth218(NativeBattleEffect218Frame frame, NativeBattleEffect218Resources resources, byte selector)
    {
        frame = SelectOwned634020(frame, EmbeddedModelAddress, resources.EmbeddedModel, selector);
        return SelectOwned634020(frame, StreamedModelAddress, resources.StreamedModel, selector);
    }

    private static NativeBattleEffect218Frame SetSceneDiagonal218(NativeBattleEffect218Frame frame, short scale) => Word(Word(Word(frame, 0x24F3598, scale), 0x24F35A0, scale), 0x24F35A8, scale);

    public static NativeBattleEffect218Frame FadeScene633F39(NativeBattleEffect218Frame frame, short age, NativeBattleSummonColors colors)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect218 scene fade requires its actual FAR registers.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Effect218 scene fade requires its current renderer controls.")) with { FarRed = 0, FarGreen = 0, FarBlue = 0 };
        uint cue = unchecked((uint)((age - 510) * 4096 / 16));
        NativeBattleScriptedDpcsResult shade = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown(), 0x02606060, cue, far, colors);
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = far }, ScriptedColorRegisters = shade.Registers } };
        return Dword(frame, StreamedModelAddress + 0x28, shade.PackedRgb);
    }
}
