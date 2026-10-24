using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Controller
{
    public static NativeBattleEffect65Step AdvanceMain6E07D0(NativeBattleEffect65Frame frame, uint address, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        uint caster = frame.State.ReadDword(CasterGlobal);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        NativeBattleVector saved = Vector(frame.State.ReadView(CasterPointAddress, 8));
        if (age is >= 1 and < 32)
        {
            int relative = age - 1;
            int scale = (relative << 11) / 31;
            frame = inputs.Execute(frame, new NativeBattleEffect65ModelFlags(caster, 0xFFF7, (ushort)(relative < 10 ? 8 : 0)));
            frame = NativeBattleEffect65Mesh.Draw6E4230(frame, saved.X, -1000, saved.Z - 1000, 0, unchecked((short)scale), 0, 0x800C, 2048, 0x1334FFC, CursorGlobal, resources, inputs);
            frame = NativeBattleEffect65Mesh.Draw6E4230(frame, saved.X, -1000, saved.Z - 1000, 0, unchecked((short)scale), relative << 5, 0x8030, -512, 0x1336924, CursorGlobal, resources, inputs);
            frame = NativeBattleEffect65Mesh.Draw6E4350(frame, 0, saved.X, 0, saved.Z, scale / 4, CursorGlobal, resources, inputs);
            if (frame.State.ReadDword(ControlGlobal) == 0)
            {
                frame = MoveCasterZ(frame, -322, inputs);
            }
        }
        if (age is >= 32 and < 542)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect65StageFlags(0xFD, 0, 3));
            frame = NativeBattleEffect65Mesh.Draw6E4230(frame, saved.X, 0, saved.Z, 0, 4096, 0, 12, 0, 0x1334FFC, 0x1D8E054, resources, inputs);
            frame = NativeBattleEffect65Mesh.Draw6E4350(frame, 0, saved.X, 0, saved.Z, 65536, 0x1D8E054, resources, inputs);
        }
        if (age is >= 42 and < 67)
        {
            frame = DrawPairedProgrammes(frame, address, age, resources, inputs);
        }
        (int Start, int End, int Index, int Size)[] first = [(67, 97, 2, 0x280), (97, 123, 3, 0x280), (123, 145, 4, 0x280), (145, 167, 5, 0x9C4)];
        foreach (var phase in first)
        {
            if (age >= phase.Start && age < phase.End)
            {
                if (age == phase.Start)
                {
                    frame = InitializeProgramme(frame, address + 0x48, phase.Size, phase.Index, resources);
                    if (age == 67)
                    {
                        frame = inputs.Execute(frame, new NativeBattleEffect65ModelFlags(caster, 0xFFFF, 8));
                        frame = inputs.Execute(frame, new NativeBattleEffect65StageFlags(0xFD, 0));
                        frame = HideEnabledActors(frame, 0, inputs);
                    }
                }
                frame = DrawMainProgramme6E45D0(frame, address, age - phase.Start, phase.Size, resources, inputs);
            }
        }
        if (age is >= 167 and < 238)
        {
            frame = DrawCasterStrands(frame, age - 167, resources, inputs);
        }
        (int Start, int End, int Index, int Size)[] second = [(238, 264, 0, 0x120), (264, 290, 1, 0x120), (290, 316, 2, 0x120), (316, 342, 3, 0x85C)];
        foreach (var phase in second)
        {
            if (age >= phase.Start && age < phase.End)
            {
                if (age == phase.Start)
                {
                    frame = InitializeProgramme(frame, address + 0x48, phase.Size, phase.Index, resources);
                    if (age == 238)
                    {
                        frame = inputs.Execute(frame, new NativeBattleEffect65ModelFlags(caster, 0xFFFF, 8));
                        frame = HideEnabledActors(frame, 0, inputs);
                    }
                }
                frame = DrawMainProgramme6E4670(frame, address, phase.Size, resources, inputs);
            }
        }
        if (age is >= 342 and < 402)
        {
            frame = DrawMiddleStrands(frame, address, age - 342, resources, inputs);
        }
        if (age is >= 402 and < 470)
        {
            frame = AdvanceGrowingStrips(frame, address, age - 402, resources, inputs);
        }
        if (age is >= 466 and < 497)
        {
            frame = Fade(frame, age - 466, 4, 2, 25, inputs);
        }
        if (age is >= 472 and < 542)
        {
            frame = AdvanceLateBurst(frame, address, age - 472, resources, inputs);
        }
        if (age is >= 532 and < 552)
        {
            frame = Fade(frame, age - 532, 8, 4, 8, inputs);
            if (age == 542)
            {
                frame = RestoreMainActors(frame, address, inputs);
            }
        }
        frame = DrawParticles(frame, resources, inputs);
        if (age == 0)
        {
            frame = Queue(frame, 391, arena + FileOffset, 2, inputs);
            frame = Queue(frame, 392, arena + FileOffset, 2, inputs);
            frame = Queue(frame, 393, arena + FileOffset, 5, inputs);
        }
        else if (age is 31 or 237)
        {
            NativeBattleEffect65IoStep polled = inputs.Poll534270(frame);
            frame = polled.Frame;
            if (polled.Busy)
            {
                return new(frame, 0);
            }
        }
        else if (age == 166)
        {
            frame = Queue(frame, 394, arena + FileOffset, 5, inputs);
        }
        else if (age == 543)
        {
            frame = Queue(frame, 360, arena + FirstPacketOffset, 1, inputs);
            frame = Queue(frame, 361, arena + FirstPacketOffset, 1, inputs);
            frame = Queue(frame, 362, arena + FirstPacketOffset, 0, inputs);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect65IoAdvance534210());
        frame = AdvanceParticles(frame, resources, inputs);
        frame = NativeBattleEffect65Mesh.Advance6E4570(frame);
        if (age == 548)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect65StopVibration4A2940(unchecked((int)Read(frame, address).ReadDword(0x10))));
        }
        int sound = age switch { 0 => 0, 97 => 1, 167 => 2, 238 => 3, 342 => 4, 457 => 5, _ => -1 };
        if (sound >= 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect65Sound501330(resources.ReadSound(0x133AB6C + (uint)(sound * 4)), 0, 128));
        }
        if (age == 32)
        {
            frame = RestoreTargetPoints(frame, address, 10000, false, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect65WriteActorPoint8(caster, TaskView(Read(frame, address), 0x14, 8), false));
        }
        frame = AdvanceCamera(frame, age, inputs);
        if (age == 548)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect65Contacts506BA0((frame.Effect.Parameters ?? throw new NotSupportedException("Effect65 requires its current contact header.")).HeaderIndex7C));
        }
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        if (unchecked((short)next) >= 552)
        {
            NativeBattleEffect65IoCompletionStep io = inputs.Select534300(frame, 2);
            frame = io.Frame;
            if (io.Complete)
            {
                return new(frame, 2);
            }
            frame = Write(frame, address, Read(frame, address).WriteWord(12, 562));
        }
        return new(frame, 0);
    }

    private static NativeBattleEffect65Frame Queue(NativeBattleEffect65Frame frame, short file, uint destination, byte type, NativeBattleEffect65Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect65IoQueue5341D0(file, destination, type));
    private static NativeBattleEffect65Frame Fade(NativeBattleEffect65Frame frame, int age, int rise, int hold, int fall, NativeBattleEffect65Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect65DeliverBatch(NativeBattleModuleRenderCommands.Emit700150(age, 255, 255, 255, rise, hold, fall), 0x1D8E054));
    private static NativeBattleEffect65Frame RequestWhite(NativeBattleEffect65Frame frame, NativeBattleEffect65Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect65PcRenderRequest47CF50(0, 255, 255, 255, 63));

    private static NativeBattleEffect65Frame HideEnabledActors(NativeBattleEffect65Frame frame, int first, NativeBattleEffect65Inputs inputs)
    {
        for (int actor = first; actor < 7; actor++)
        {
            uint model = ModelAddress(actor);
            if ((inputs.ReadModel(frame, model).Flags0 & 2) != 0)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect65ModelFlags(model, 0xFFFF, 4));
            }
        }
        return frame;
    }

    private static NativeBattleEffect65Frame MoveCasterZ(NativeBattleEffect65Frame frame, int delta, NativeBattleEffect65Inputs inputs)
    {
        uint caster = frame.State.ReadDword(CasterGlobal);
        NativeBattleVector point = Vector(inputs.ReadPosition8(frame, caster));
        return inputs.Execute(frame, new NativeBattleEffect65WriteActorPositionXYZ(caster, V(point.X, point.Y, point.Z + delta), false));
    }

    private static NativeBattleEffect65Frame RestoreTargetPoints(NativeBattleEffect65Frame frame, uint main, int deltaZ, bool clearScale, NativeBattleEffect65Inputs inputs)
    {
        int count = Signed(Read(frame, main), 14);
        for (int index = 0; index < count; index++)
        {
            NativeBattleModulePoolTask task = Read(frame, main);
            uint model = task.ReadDword(0x24 + index * 12);
            NativeBattleEffectArena point = TaskView(task, 0x28 + index * 12, 8);
            point = point.Write(4, BitConverter.GetBytes(unchecked((short)(Vector(point).Z + deltaZ))));
            frame = inputs.Execute(frame, new NativeBattleEffect65WriteActorPoint8(model, point, false));
            if (clearScale)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect65SetActorScalePointer30(model, 0));
            }
        }
        return frame;
    }

    private static NativeBattleEffect65Frame RestoreMainActors(NativeBattleEffect65Frame frame, uint main, NativeBattleEffect65Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect65CameraRestoreCaptured503300());
        frame = frame with { State = frame.State.Write(0x2544FD2, [0, 0]) };
        frame = RestoreTargetPoints(frame, main, 0, true, inputs);
        uint caster = frame.State.ReadDword(CasterGlobal);
        frame = inputs.Execute(frame, new NativeBattleEffect65ModelFlags(caster, 0xFFF7, 0));
        frame = inputs.Execute(frame, new NativeBattleEffect65WriteActorPoint8(caster, TaskView(Read(frame, main), 0x14, 8), false));
        frame = inputs.Execute(frame, new NativeBattleEffect65SetActorScalePointer30(caster, 0));
        frame = inputs.Execute(frame, new NativeBattleEffect65StageFlags(0xFF, 2));
        frame = RestoreActorFlags6E2BD0(frame, inputs);
        return inputs.Execute(frame, new NativeBattleEffect65SetSequence505C00(caster, 1));
    }
}
