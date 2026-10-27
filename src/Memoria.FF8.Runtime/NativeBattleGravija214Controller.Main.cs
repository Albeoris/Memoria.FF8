using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGravija214Controller
{
    private static (NativeBattleGravija214Frame Frame, int Result) AdvanceRoot(NativeBattleGravija214Frame frame, uint address, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool alternate = root.ReadDword(0x10) != 0;
        frame = Dword(frame, 0x2510114, unchecked(frame.State.ReadDword(0x25089B8) + (alternate ? 0x1970u : 0x11970u)));
        frame = Write(frame, address, root.WriteDword(0x10, alternate ? 0u : 1u));
        if (Signed(root, 12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleGravija214Clear45BEE0(new(576, 256, 320, 256), 0, 0, 0));
            frame = inputs.Execute(frame, new NativeBattleGravija214SetOrigin45B5E0(576, 256));
            if (Read(frame, address).ReadByte(15) == 0)
            {
                frame = PreprocessCopiedMesh69E940(frame, resources);
                frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
                frame = InitializeSecondaryBanks(frame);
                (frame, uint main) = Append(frame, 0x2510128, 0x69D870);
                if (main == 0)
                {
                    throw new InvalidDataException("Gravija's freshly reset main pool must admit its sole task.");
                }
                frame = ClearDwords701200(frame, main + 12, 198);
                NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, main + 0x30, 756);
                workspace = workspace.Initialize7016B0(resources.Programme, 756);
                NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, main + 0x30, workspace);
                int count = Header(frame, inputs.ReadPackets(frame)).ReadByte(16);
                if (count == 0 || count > 5)
                {
                    throw new NotSupportedException("Gravija's recipient mean requires one to five physical contact records.");
                }
                frame = Write(frame, main, Read(frame, main).WriteWord(0x10, checked((ushort)count)));
                for (int index = 0; index < count; index++)
                {
                    NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                    int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Gravija requires its current contact pointer.");
                    uint model = ModelAddress(packets.ReadContact(checked(first + index)).Primary.ActorIndex);
                    frame = Write(frame, main, Read(frame, main).WriteDword(0x14 + index * 4, model));
                    NativeBattleGravija214PointStep anchor = inputs.ReadAnchor571400(frame, model);
                    frame = anchor.Frame;
                    NativeBattleModulePoolTask task = Read(frame, main);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        short value = BinaryPrimitives.ReadInt16LittleEndian(anchor.Point8.Read(axis * 2, 2));
                        task = task.WriteWord(0x28 + axis * 2, unchecked((ushort)(task.ReadWord(0x28 + axis * 2) + value)));
                    }
                    frame = Write(frame, main, task);
                }
                NativeBattleModulePoolTask mean = Read(frame, main);
                for (int axis = 0; axis < 3; axis++)
                {
                    mean = mean.WriteWord(0x28 + axis * 2, unchecked((ushort)(Signed(mean, 0x28 + axis * 2) / count)));
                }
                frame = Write(frame, main, mean);
            }
        }
        if (Read(frame, address).ReadByte(15) != 0)
        {
            (frame, int mainSurvivors) = AdvancePool(frame, 2, resources, inputs);
            (frame, _) = AdvancePool(frame, 1, resources, inputs);
            if (mainSurvivors == 0)
            {
                return (frame, 2);
            }
        }
        root = Read(frame, address);
        return (Write(frame, address, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)))), 0);
    }

    private static (NativeBattleGravija214Frame Frame, int Result) AdvanceMain(NativeBattleGravija214Frame frame, uint address, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadWord(12) < 74)
        {
            frame = DrawProgramme69DC40(frame, address, resources, inputs);
        }
        int age = Signed(Read(frame, address), 12);
        int time = age - 18;
        if ((uint)time < 56)
        {
            frame = DrawMainWarp(frame, address, time, resources, inputs);
        }
        if (Read(frame, address).ReadWord(12) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleGravija214Sound501330(resources.Sound, 0, 128));
        }
        time = Signed(Read(frame, address), 12) - 18;
        if ((uint)time < 50)
        {
            if (time == 0)
            {
                for (int index = 0; index < 64; index++)
                {
                    (frame, uint child) = Append(frame, 0x2510118, 0x69DF00);
                    if (child == 0)
                    {
                        continue;
                    }
                    NativeBattleModulePoolTask particle = Read(frame, child).WriteWord(12, 0);
                    frame = DrawRandom(frame, out ushort random);
                    particle = particle.WriteWord(14, unchecked((ushort)(50 - (random & 7))));
                    frame = DrawRandom(frame, out random);
                    particle = particle.WriteWord(16, random);
                    for (int field = 20; field <= 24; field += 2)
                    {
                        frame = DrawRandom(frame, out random);
                        particle = particle.WriteWord(field, random);
                    }
                    frame = DrawRandom(frame, out random);
                    particle = particle.WriteWord(18, unchecked((ushort)((random & 1023) + 1500)));
                    frame = Write(frame, child, particle);
                }
            }
            int scale = time < 16 ? NativeBattleCameraMath.Sine(time << 6) : time <= 34 ? 4096 : NativeBattleCameraMath.Cosine((time - 34) << 6);
            frame = Write(frame, address, Read(frame, address).WriteWord(14, unchecked((ushort)scale)));
            if (time == 49)
            {
                frame = inputs.Execute(frame, new NativeBattleGravija214ContactsAll506BA0(Parameters(frame).HeaderIndex7C));
            }
        }
        task = Read(frame, address);
        short currentAge = Signed(task, 12);
        if (currentAge < 74)
        {
            return (Write(frame, address, task.WriteWord(12, unchecked((ushort)(currentAge + 1)))), 0);
        }
        if (currentAge == 74)
        {
            frame = inputs.Execute(frame, new NativeBattleGravija214CommonTextureLoad508630(unchecked(frame.State.ReadDword(0x25089B8) + 0x1970), address + 18));
            frame = Write(frame, address, Read(frame, address).WriteWord(12, 75));
        }
        return (frame, Read(frame, address).ReadByte(18) != 0 ? 2 : 0);
    }

    private static NativeBattleGravija214Frame DrawMainWarp(NativeBattleGravija214Frame frame, uint address, int time, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs)
    {
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleEffectArena rect = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)32767)).Write(2, BitConverter.GetBytes((short)32767)).Write(4, BitConverter.GetBytes((short)-32767)).Write(6, BitConverter.GetBytes((short)-32767));
        int minimumDepth = 65536;
        bool included = false;
        for (int index = 0; index < Signed(Read(frame, address), 16); index++)
        {
            NativeBattleGravija214BoundsStep bounds = Bounds69E860(frame, Read(frame, address).ReadDword(20 + index * 4), rect, minimumDepth, inputs);
            frame = bounds.Frame;
            rect = bounds.Rect8;
            minimumDepth = bounds.MinimumDepth;
            included |= bounds.Included;
        }
        minimumDepth = unchecked(minimumDepth - 128);
        if (!included || minimumDepth <= 0)
        {
            return frame;
        }
        short left = BinaryPrimitives.ReadInt16LittleEndian(rect.Read(0, 2));
        short top = BinaryPrimitives.ReadInt16LittleEndian(rect.Read(2, 2));
        short width = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(rect.Read(4, 2)) - left));
        short height = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(rect.Read(6, 2)) - top));
        short centerX = unchecked((short)(left + width / 2));
        short centerY = unchecked((short)(top + height / 2));
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Gravija requires its current projection controls.");
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Gravija requires its current arithmetic owner."));
        registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, new(Signed(task, 40), Signed(task, 42), Signed(task, 44)), Signed(task, 46)), inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        short depth = unchecked((short)(registers.Geometry.Sz[3]!.Value >> 2));
        short minimumSize = unchecked((short)(0x18000 / depth));
        width = Math.Max(width, minimumSize);
        height = Math.Max(height, minimumSize);
        frame = Word(Word(frame, 0x2510148, width), 0x2510150, height);
        frame = Dword(Dword(frame, 0x251015C, unchecked((uint)centerX)), 0x2510160, unchecked((uint)centerY));
        int fraction = (time << 12) / 56;
        int sine = NativeBattleCameraMath.Sine(fraction >> 1);
        if (time < 8)
        {
            frame = Word(Word(frame, 0x2510148, unchecked((int)((uint)(width * time) >> 3))), 0x2510150, unchecked((int)((uint)(height * time) >> 3)));
        }
        int cue = time < 48 ? 3072 : unchecked((int)((uint)((time + 0x3FFFD0) << 10) >> 3) + 0xC40);
        frame = LoadMatrix(frame, ReadMatrix214(frame));
        return DrawWarp69E020(frame, 0x25089BC, 0x25089C4, (minimumDepth >> 2) + 17, cue, sine * 2, fraction, resources, inputs);
    }

    private static NativeBattlePackedMatrix32 ReadMatrix214(NativeBattleGravija214Frame frame)
    {
        (byte[] bytes, bool[] known) = frame.State.ReadView(0x2510148, 32).ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public static NativeBattleActionHeaderSlot Header(NativeBattleGravija214Frame frame, NativeBattleActionPacketsState packets)
    {
        if (frame.State.ReadDword(0x25100FC) != 0x1D99A78)
        {
            throw new NotSupportedException("Gravija requires its actual current parameter pointer.");
        }
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Gravija parameter header leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}
