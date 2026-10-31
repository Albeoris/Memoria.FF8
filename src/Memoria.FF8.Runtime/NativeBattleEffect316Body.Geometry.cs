namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect316Body
{
    public static NativeBattleEffect316Frame Draw661550(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs, uint model, int x, int y, int z)
    {
        return DrawBody(frame, resources, inputs, 0, model, 0, x, y, z, false);
    }

    public static NativeBattleEffect316Frame Draw661CC0(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs, uint main, uint model, uint projected, int x, int y, int z)
    {
        return DrawBody(frame, resources, inputs, main, model, projected, x, y, z, true);
    }

    private static NativeBattleEffect316Frame DrawBody(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs, uint main, uint model, uint projected, int x, int y, int z, bool expanded)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect316Shadow5088A0(model, 4112, 16));
        int length = expanded ? 0xB0 : 0x84;
        NativeBattleEffect316AddressStep allocation = inputs.AllocateTemporary(frame, length);
        frame = allocation.Frame;
        uint temporary = allocation.Address;
        NativeBattleEffectArena context = inputs.ReadArena(frame, temporary, length);
        int position = expanded ? 0x8C : 0x6C;
        context = PutWord(PutWord(PutWord(context, position, x), position + 2, y), position + 4, z);
        if (expanded)
        {
            context = PutDword(context, 0xA4, main);
        }
        frame = Write(frame, inputs, temporary, context, length);
        frame = inputs.Execute(frame, new NativeBattleEffect316Prepare5095B0(model));
        if (!expanded)
        {
            projected = ReadDword(frame, inputs, ProjectedGlobal);
        }
        context = inputs.ReadArena(frame, temporary, length);
        context = PutDword(context, 0x24, projected);
        context = PutWord(PutWord(PutWord(PutWord(context, 0x2C, 0), 0x2E, 0), 0x30, 320), 0x32, 216);
        context = PutDword(PutDword(context, 0x34, ReadDword(frame, inputs, model + 0x28)), 0x38, ReadDword(frame, inputs, model + 0x7C));
        int cursors = expanded ? 0xA8 : 0x7C;
        context = PutDword(PutDword(context, cursors, ReadDword(frame, inputs, CommonCursorGlobal)), cursors + 4, frame.State.ReadDword(PrivateCursorGlobal));
        frame = Write(frame, inputs, temporary, context, length);
        frame = WalkObjects(frame, resources, inputs, model, inputs.ReadGeometryAddress(frame, model), temporary + 0x20, 17, 0, expanded);
        context = inputs.ReadArena(frame, temporary, length);
        frame = inputs.Execute(frame, new NativeBattleEffect316PublishCursors(Dword(context, cursors), Dword(context, cursors + 4)));
        frame = inputs.Execute(frame, new NativeBattleEffect316Restore508C90(model));
        return inputs.ReleaseTemporary(frame, length);
    }

    private static NativeBattleEffect316Frame WalkObjects(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs, uint model, uint source, uint contextAddress, int ordering, int shift, bool expanded)
    {
        int objects = unchecked((int)ReadDword(frame, inputs, source));
        int cursorOffset = expanded ? 0x74 : 0x54;
        int outputOffset = cursorOffset + 4;
        for (int index = 0; index < objects; index++)
        {
            uint entry = ReadDword(frame, inputs, source + 4 + unchecked((uint)index * 4));
            NativeBattleEffectArena context = inputs.ReadArena(frame, contextAddress, expanded ? 0x90 : 0x64);
            context = PutDword(context, cursorOffset, unchecked(source + entry));
            frame = Write(frame, inputs, contextAddress, context, expanded ? 0x90 : 0x64);
            if ((Dword(context, 0x18) & (1u << (index & 31))) == 0)
            {
                continue;
            }
            context = PutDword(context, outputOffset, Dword(context, 4));
            uint raw = Dword(context, cursorOffset);
            int groups = Word(inputs.ReadArena(frame, raw, 2), 0);
            context = PutDword(context, cursorOffset, raw + 2);
            frame = Write(frame, inputs, contextAddress, context, expanded ? 0x90 : 0x64);
            for (int group = 0; group < groups; group++)
            {
                context = inputs.ReadArena(frame, contextAddress, expanded ? 0x90 : 0x64);
                raw = Dword(context, cursorOffset);
                int bone = Word(inputs.ReadArena(frame, raw, 2), 0);
                uint matrix = inputs.ResolveBoneRecord(frame, model, bone);
                frame = LoadMatrix(frame, inputs.ReadArena(frame, matrix, 32));
                context = PutDword(context, cursorOffset, raw + 2);
                frame = Write(frame, inputs, contextAddress, context, expanded ? 0x90 : 0x64);
                frame = ProjectGroup(frame, inputs, contextAddress, expanded);
            }
            context = inputs.ReadArena(frame, contextAddress, expanded ? 0x90 : 0x64);
            raw = unchecked(Dword(context, cursorOffset) + 3) & 0xFFFFFFFC;
            NativeBattleEffectArena counts = inputs.ReadArena(frame, raw, 4);
            context = PutWord(PutWord(context, 8, Word(counts, 0)), 10, Word(counts, 2));
            context = PutDword(PutDword(context, 0, raw + 12), cursorOffset, raw + 12);
            frame = Write(frame, inputs, contextAddress, context, expanded ? 0x90 : 0x64);
            frame = RasterFaces(frame, resources, inputs, contextAddress, ordering, shift, expanded);
        }
        return frame;
    }

    public static NativeBattleEffect316Frame ProjectGroup663760(NativeBattleEffect316Frame frame, NativeBattleEffect316Inputs inputs, uint context) => ProjectGroup(frame, inputs, context, false);
    public static NativeBattleEffect316Frame ProjectGroup662630(NativeBattleEffect316Frame frame, NativeBattleEffect316Inputs inputs, uint context) => ProjectGroup(frame, inputs, context, true);

    private static NativeBattleEffect316Frame ProjectGroup(NativeBattleEffect316Frame frame, NativeBattleEffect316Inputs inputs, uint address, bool expanded)
    {
        int length = expanded ? 0x90 : 0x64;
        int cursorOffset = expanded ? 0x74 : 0x54;
        NativeBattleEffectArena context = inputs.ReadArena(frame, address, length);
        uint raw = Dword(context, cursorOffset);
        uint output = Dword(context, cursorOffset + 4);
        int count = Word(inputs.ReadArena(frame, raw, 2), 0);
        if (count < 0)
        {
            throw new NotSupportedException("The current geometry requires a finite nonnegative vertex count.");
        }
        raw += 2;
        int stride = expanded ? 16 : 8;
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        for (int index = 0; index < count; index++)
        {
            NativeBattleVector point = Vector(inputs.ReadArena(frame, raw, 6));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, point, 0));
            uint destination = output + unchecked((uint)(index * stride + (expanded ? 8 : 0)));
            NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear().MarkUnknown(0, 6);
            for (int axis = 0; axis < 3; axis++)
            {
                xyz = PutWord(xyz, axis * 2, registers.Colors.Ir[axis] ?? throw new NotSupportedException("The vertex requires its current transformed IR value."));
            }
            frame = Write(Publish(frame, registers), inputs, destination, xyz, 6);
            raw += 6;
        }
        frame = LoadMatrix(Publish(frame, registers), inputs.ReadView(frame));
        registers = Registers(frame);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("The body requires its current projection controls.");
        NativeBattleCameraProjection projection = inputs.ReadProjection(frame);
        for (int index = 0; index < count; index++)
        {
            uint rowAddress = output + unchecked((uint)(index * stride));
            NativeBattleEffectArena row = inputs.ReadArena(frame, rowAddress, stride);
            int pointOffset = expanded ? 8 : 0;
            NativeBattleVector point = Vector(row, pointOffset);
            registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, point, Padding(row, pointOffset + 6)), projection, controls.Dqa, controls.Dqb);
            if (frame.CapturePresentation)
            {
                NativeBattleActionMatrix matrix = NativeBattleGeometry.CaptureMatrix(registers.Geometry);
                System.Numerics.Vector3 world = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection);
                frame = frame with { PresentationWorld = frame.PresentationWorld.SetItem(rowAddress, world) };
            }
            context = PutDword(context, 0x1C, registers.Geometry.Sxy[2] ?? throw new NotSupportedException("The vertex requires its projected screen coordinates."));
            uint depth = unchecked((uint)(unchecked((int)(registers.Geometry.Sz[3] ?? throw new NotSupportedException("The vertex requires its projected depth."))) >> 2));
            context = PutDword(context, 0x20, depth);
            if (unchecked((short)depth) > 0)
            {
                byte gray = Gray(point.Y, Word(context, expanded ? 0x6C : 0x4C));
                row = PutDword(row, 0, Dword(context, 0x1C));
                if (expanded)
                {
                    row = PutWord(row, 4, unchecked((int)depth)).Write(6, [gray]);
                }
                else
                {
                    context = context.Write(0x22, [gray]);
                    row = Copy(context, 0x20, row, 4, 4);
                }
            }
            else
            {
                row = PutWord(row, 4, 0);
            }
            frame = Write(Publish(frame, registers), inputs, rowAddress, row, stride);
        }
        context = PutDword(PutDword(context, cursorOffset, raw), cursorOffset + 4, output + unchecked((uint)(count * stride)));
        return Write(Publish(frame, registers), inputs, address, context, length);
    }
}
