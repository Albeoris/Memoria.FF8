using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Controller
{
    private static NativeBattleEffect65Frame DrawCasterStrands(NativeBattleEffect65Frame frame, int relative, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        if (relative == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect65ModelFlags(frame.State.ReadDword(CasterGlobal), 0xFFF3, 0));
        }
        if (frame.State.ReadDword(ControlGlobal) == 0)
        {
            int delta = 0;
            if (relative is >= 13 and < 21)
            {
                delta = NativeBattleCameraMath.Sine((relative - 13) * 128) / 6;
            }
            else if (relative is >= 21 and < 29)
            {
                int nested = NativeBattleCameraMath.Sine(NativeBattleCameraMath.Sine((relative - 21) * 128) >> 2);
                delta = unchecked((4096 - nested) * (NativeBattleCameraMath.Sine(1024) / 6)) / 4096;
            }
            else if (relative is >= 29 and < 45)
            {
                delta = NativeBattleCameraMath.Sine(((relative - 29) << 11) / 20) / -18;
            }
            else if (relative >= 45)
            {
                delta = -170;
            }
            if (relative >= 13)
            {
                frame = MoveCasterZ(frame, delta, inputs);
            }
        }
        int waveY;
        int waveX;
        if (relative < 23)
        {
            waveY = NativeBattleCameraMath.Sine(((relative << 12) / 23) / 4) * 2;
            waveX = (waveY + (waveY < 0 ? 1 : 0)) >> 1;
        }
        else if (relative < 25)
        {
            waveY = 0x1556 - (relative - 23) * 2730;
            waveX = (waveY + (waveY < 0 ? 1 : 0)) >> 1;
        }
        else
        {
            int factor = NativeBattleCameraMath.Cosine(((relative - 25) << 12) / 192);
            factor = NativeBattleCameraMath.Cosine((4096 - factor) / 4);
            waveY = NativeBattleCameraMath.Sine((4096 - factor) / 4) * 2;
            waveX = 0;
        }
        NativeBattleVector saved = Vector(frame.State.ReadView(CasterPointAddress, 8));
        for (int side = 0; side < 2; side++)
        {
            NativeBattleEffect65PointOutputStep point = inputs.ReadCasterPoint6E2D30(frame, ReadSource(frame, resources, side == 0 ? 0x133ABB0u : 0x133ABB8u, 8), NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = point.Frame;
            int x = saved.X + (side == 0 ? 10000 : -10000);
            int horizontal = side == 0 ? waveX : -waveX;
            frame = NativeBattleEffect65Mesh.Draw6E4710(frame, point.Output, PointView(V(x, saved.Y - 10000, saved.Z - 2000)), horizontal, waveY, resources, inputs);
            frame = NativeBattleEffect65Mesh.Draw6E4710(frame, point.Output, PointView(V(x, saved.Y - 20000, saved.Z - 5000)), horizontal, waveY, resources, inputs);
        }
        return frame;
    }

    private static NativeBattleEffect65Frame DrawMiddleStrands(NativeBattleEffect65Frame frame, uint main, int relative, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, main);
        NativeBattleVector center = Vector(TaskView(task, 0x1C, 6));
        if (relative == 0)
        {
            int count = Signed(task, 14);
            for (int index = 0; index < count; index++)
            {
                task = Read(frame, main);
                uint actor = task.ReadDword(0x24 + index * 12);
                NativeBattleVector saved = Vector(TaskView(task, 0x28 + index * 12, 8));
                NativeBattleVector placed = V(center.X + (saved.X - center.X) / 2, center.Y + (saved.Y - center.Y) / 2, center.Z + (saved.Z - center.Z) / 2 + 2000);
                frame = inputs.Execute(frame, new NativeBattleEffect65WriteActorPositionXYZ(actor, placed, false));
                frame = inputs.Execute(frame, new NativeBattleEffect65SetActorScalePointer30(actor, 0x133AB88));
            }
            uint caster = frame.State.ReadDword(CasterGlobal);
            NativeBattleVector casterSaved = Vector(TaskView(Read(frame, main), 0x14, 8));
            NativeBattleVector casterPoint = V(center.X + (casterSaved.X - center.X) / 2, center.Y + (casterSaved.Y - center.Y) / 2, center.Z + (casterSaved.Z - center.Z) / 2 - 2000);
            frame = inputs.Execute(frame, new NativeBattleEffect65WriteActorPositionXYZ(caster, casterPoint, true));
            frame = inputs.Execute(frame, new NativeBattleEffect65SetActorScalePointer30(caster, 0x133AB88));
            frame = RestoreActorFlags6E2BD0(frame, inputs);
            frame = HideEnabledActors(frame, 3, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect65ModelFlags(caster, 0xFFF3, 0));
        }
        if (frame.State.ReadDword(ControlGlobal) == 0)
        {
            frame = MoveCasterZ(frame, -170, inputs);
        }
        NativeBattleEffectArena target = PointView(V(center.X, center.Y, center.Z + 2000));
        uint arena = frame.State.ReadDword(ArenaGlobal);
        for (int side = 0; side < 2; side++)
        {
            NativeBattleEffect65PointOutputStep point = inputs.ReadCasterPoint6E2D30(frame, ReadSource(frame, resources, side == 0 ? 0x133ABB0u : 0x133ABB8u, 8), NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = point.Frame;
            frame = NativeBattleEffect65Mesh.Draw6E48A0(frame, point.Output, target, side == 0 ? 0x133AC40u : 0x133AC68u, relative * 50, (relative << 12) / 60, side == 0 ? -1 : 1, arena + (side == 0 ? 0x36620u : 0x37558u), resources, inputs);
            frame = NativeBattleEffect65Mesh.Draw6E48A0(frame, point.Output, target, side == 0 ? 0x133AC54u : 0x133AC7Cu, relative * 50, (relative << 12) / 60, side == 0 ? 1 : -1, arena + (side == 0 ? 0x393C8u : 0x38490u), resources, inputs);
        }
        return frame;
    }

    private static NativeBattleEffect65Frame AdvanceGrowingStrips(NativeBattleEffect65Frame frame, uint main, int relative, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        frame = RequestWhite(frame, inputs);
        if (relative == 0)
        {
            frame = frame with { State = frame.State.Write(0x2544FD0, [1, 1]) };
            frame = NativeBattleEffect65Strips.Initialize6E3910(frame, 7, 4096, -2048);
            frame = NativeBattleEffect65Particles.Initialize6E3390(frame);
            frame = InitializeChildBank(frame);
        }
        NativeBattleVector baseCenter = Vector(TaskView(Read(frame, main), 0x1C, 6));
        NativeBattleVector center = V(baseCenter.X, baseCenter.Y, baseCenter.Z + 2000);
        NativeBattleEffectArena center8 = PointView(center);
        int phase = (relative << 12) / 68;
        int factor = phase < 1500 ? NativeBattleCameraMath.Sine(phase >> 2) : NativeBattleCameraMath.Sine(375) + unchecked((4096 - NativeBattleCameraMath.Sine(375)) * (phase - 1500)) / 2596;
        int radius = unchecked((4096 - factor) * 5000) / 4096 + 312;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        uint[] descriptors = [0x133AC90, 0x133ACB4, 0x133ACD8, 0x133ACFC];
        uint[] sources = [0x36620, 0x393C8, 0x38490, 0x37558];
        for (int index = 0; index < 4; index++)
        {
            frame = NativeBattleEffect65Mesh.Draw6E4BD0(frame, descriptors[index], center8, radius, arena + sources[index], relative >= 6, resources, inputs);
        }
        if (frame.State.ReadDword(ControlGlobal) != 0 || radius >= 1666)
        {
            return frame;
        }
        if (relative == 58)
        {
            frame = BirthChild(frame, center, 8192, 5, 0x1A0, resources);
        }
        if (radius < 833)
        {
            (frame, uint child) = Append(frame, ChildDescriptor, ChildCallback);
            if (child == 0)
            {
                return frame;
            }
            int[] xyz = [center.X, center.Y, center.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Random(frame, out int random);
                xyz[axis] += random * 10000 / 32768 - 5000;
            }
            (frame, int value) = NativeBattleEffect65Strips.Random6E3F10(frame);
            bool odd = (relative & 1) != 0;
            frame = SetChild(frame, child, V(xyz[0], xyz[1], xyz[2]), (value >> 4) + (odd ? 3072 : 2048));
            return InitializeProgramme(frame, child + 0x18, odd ? 0x1A0 : 0x78, odd ? 5 : 4, resources);
        }
        int angle = relative << 10;
        for (int count = 0; count < 8; count++)
        {
            (frame, uint record) = NativeBattleEffect65Particles.Allocate6E34F0(frame, inputs.CapturePresentation);
            if (record == 0)
            {
                break;
            }
            frame = Random(frame, out int random);
            int puffRadius = unchecked((random - 16384) * 2000) / 32768 + 2000;
            int x = unchecked(NativeBattleCameraMath.Sine(angle) * puffRadius) >> 12;
            int z = unchecked(NativeBattleCameraMath.Cosine(angle) * puffRadius) >> 12;
            frame = Random(frame, out int jitterX);
            frame = Random(frame, out int jitterY);
            frame = Random(frame, out int jitterZ);
            NativeBattleEffectArena point = ReadBytes(frame, record, 8);
            point = PutPoint(point, 0, V(x + center.X + (jitterX & 511) - 256, center.Y + (jitterY & 511) - 256, z + center.Z + (jitterZ & 511) - 256)).Write(6, BitConverter.GetBytes((ushort)8));
            frame = WriteBytes(frame, record, point, 8);
        }
        return frame;
    }

    private static NativeBattleEffect65Frame BirthChild(NativeBattleEffect65Frame frame, NativeBattleVector point, int scale, int source, int length, NativeBattleEffect65Resources resources)
    {
        (frame, uint child) = Append(frame, ChildDescriptor, ChildCallback);
        if (child == 0)
        {
            return frame;
        }
        frame = SetChild(frame, child, point, scale);
        return InitializeProgramme(frame, child + 0x18, length, source, resources);
    }

    private static NativeBattleEffect65Frame SetChild(NativeBattleEffect65Frame frame, uint child, NativeBattleVector point, int scale) => Write(frame, child, CopyTask(Read(frame, child), 12, PointView(point), 6).WriteWord(0x12, 0xF800).WriteDword(0x14, unchecked((uint)scale)));

    private static NativeBattleEffect65Frame Random(NativeBattleEffect65Frame frame, out int value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort output);
        value = output;
        return frame with { Random = random };
    }

    private static NativeBattleEffect65Frame DrawParticles(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        if (frame.State.Read(0x2544FD0, 1)[0] != 0)
        {
            frame = NativeBattleEffect65Strips.Draw6E3990(frame, resources, inputs);
        }
        if (frame.State.Read(0x2544FD1, 1)[0] != 0)
        {
            frame = NativeBattleEffect65Particles.Draw6E33C0(frame, resources, inputs);
        }
        else
        {
            if (frame.State.Read(0x2544FD2, 1)[0] != 0)
            {
                frame = NativeBattleEffect65Particles.Draw6E2ED0(frame, resources, inputs);
            }
            if (frame.State.Read(0x2544FD3, 1)[0] != 0)
            {
                frame = NativeBattleEffect65Particles.Draw6E3610(frame, resources, inputs);
            }
        }
        return frame;
    }

    private static NativeBattleEffect65Frame AdvanceParticles(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        if (frame.State.Read(0x2544FD0, 1)[0] != 0)
        {
            frame = NativeBattleEffect65Strips.Advance6E3F30(frame, resources, inputs);
        }
        if (frame.State.Read(0x2544FD1, 1)[0] != 0)
        {
            frame = NativeBattleEffect65Particles.Advance6E3550(frame);
        }
        else
        {
            if (frame.State.Read(0x2544FD2, 1)[0] != 0)
            {
                frame = NativeBattleEffect65Particles.Advance6E3330(frame);
            }
            if (frame.State.Read(0x2544FD3, 1)[0] != 0)
            {
                frame = NativeBattleEffect65Particles.Advance6E38C0(frame);
            }
        }
        return frame;
    }
}
