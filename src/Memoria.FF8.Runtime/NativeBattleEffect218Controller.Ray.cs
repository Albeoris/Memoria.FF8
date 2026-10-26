using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleEffect218Frame PrepareBillboard633440(NativeBattleEffect218Frame frame, NativeBattleVector position, short? sourceW, short scale, int depth, NativeBattlePackedMatrix32 currentView, NativeBattleCameraProjection projection, short normalDepth)
    {
        frame = Word(Word(Word(frame, 0x24F35F8, scale), 0x24F3600, scale), 0x24F3608, scale);
        frame = LoadMatrix(frame, currentView);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect218 billboard requires its current projection controls.");
        NativeBattleGeometryRegisterStep step = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        step = NativeBattleGeometry.LoadVector(step, 0, position, sourceW);
        step = NativeBattleGeometry.ProjectSingle(step, projection, controls.Dqa, controls.Dqb);
        uint screen = step.Geometry.Sxy[2] ?? throw new InvalidDataException("Effect218 billboard projection did not publish SXY.");
        int x = unchecked((short)screen) / 8;
        int y = unchecked((short)(screen >> 16)) / 8;
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
        frame = Dword(Dword(Dword(frame, 0x24F360C, unchecked((uint)(x - 160))), 0x24F3610, unchecked((uint)(y - 108))), 0x24F3614, unchecked((uint)(normalDepth + depth)));
        (byte[] bytes, bool[] known) = frame.State.ReadView(0x24F35F8, 32).ReadSnapshot(0, 32);
        return LoadMatrix(frame, new(bytes.ToImmutableArray(), known.ToImmutableArray()));
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceRay633200(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleEffect218Resources resources, NativeBattleVector cameraEye, NativeBattlePackedMatrix32 currentView, NativeBattleCameraProjection projection, short cameraRoll, short normalDepth, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleVector origin = new(Signed(task, 16), Signed(task, 18), Signed(task, 20));
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(cameraEye.X - origin.X, cameraEye.Y - origin.Y, cameraEye.Z - origin.Z);
        NativeBattleModuleAxisAngle angle = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(angle.Angle, angle.Axis), 0, 0, 0, 4096);
        (frame, uint temporary) = Allocate(frame, 180);
        short age = Signed(task, 12);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xE19C14u)).Write(0x24, new byte[2]);
        if (age < 2 || age >= 16)
        {
            int intensity = age < 2 ? age * 64 : (24 - age) * 16;
            uint rgb = unchecked((uint)((intensity << 8 | intensity) << 8));
            context = context.Write(0x24, BitConverter.GetBytes((ushort)4)).Write(0x1C, BitConverter.GetBytes(rgb));
        }
        int xFactor = unchecked(-512 - age * 800);
        int yFactor = unchecked(-256 + age * 400);
        int zFactor = unchecked(-3584 - age * 25);
        for (int index = 0; index < 8; index++)
        {
            ReadOnlySpan<byte> row = resources.RayOrigins.AsSpan().Slice(index * 8, 8);
            short coefficient = BinaryPrimitives.ReadInt16LittleEndian(row[4..]);
            int normalized = unchecked(coefficient * 2200) >> 12;
            NativeBattleVector local = new(unchecked((short)(unchecked(normalized * xFactor) >> 12)), unchecked((short)(unchecked(normalized * yFactor) >> 12)), unchecked((short)(unchecked(normalized * zFactor) >> 12)));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(rotation, local);
            NativeBattleVector position = new(unchecked((short)(rotated.X + origin.X)), unchecked((short)(rotated.Y + origin.Y)), unchecked((short)(rotated.Z + origin.Z)));
            short scale = BinaryPrimitives.ReadInt16LittleEndian(row[2..]);
            ushort spriteFrame = BinaryPrimitives.ReadUInt16LittleEndian(row);
            frame = PrepareBillboard633440(frame, position, null, scale, 0, currentView, projection, normalDepth);
            context = context.Write(4, BitConverter.GetBytes(spriteFrame));
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, projection, cameraRoll);
            context = drawn.Temporary180;
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, context, 180);
            frame = deliver(frame, drawn.Batch);
        }
        frame = Release(frame, 180);
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 24 ? 2 : 0);
    }
}
