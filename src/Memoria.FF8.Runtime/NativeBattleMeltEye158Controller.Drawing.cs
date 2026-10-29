using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleMeltEye158Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMeltEye158Controller
{
    public static (NativeBattleMeltEye158Frame Frame, int Result) AdvanceImpactSprite(NativeBattleMeltEye158Frame frame, uint address, NativeBattleMeltEye158Resources resources, NativeBattleMeltEye158Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short scale = DrawingWord(task, 0x1C);
        (frame, uint temporary) = DrawingAllocate(frame, 180);
        frame = DrawingBeginSprite(frame, temporary, resources.SpriteA.Address);
        frame = inputs.Execute(frame, new NativeBattleMeltEye158DrawSprite180(temporary, resources.SpriteA.Address, TaskVector(task, 0x10), DrawingOptionalWord(task, 0x16), scale, unchecked((short)-(scale >> 3)), DrawingWord(task, 12)) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(158, task) with { ResourceGeneration = resources.SpriteA.Address } });
        frame = DrawingRelease(frame, 180);
        if (DrawingPaused(inputs))
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(DrawingWord(task, 12) + 1)));
        frame = Write(frame, address, task);
        short feedback = BinaryPrimitives.ReadInt16LittleEndian(frame.Staging!.Read(temporary + 0x28, 2));
        return (frame, feedback < 0 ? 2 : 0);
    }

    public static (NativeBattleMeltEye158Frame Frame, int Result) AdvanceImpactMesh(NativeBattleMeltEye158Frame frame, uint address, NativeBattleMeltEye158Resources resources, NativeBattleMeltEye158Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short scale = DrawingWord(task, 0x1C);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(1024, DrawingWord(task, 0x18), 0));
        local = local with { Rotation = NativeBattlePoseMatrices.ScaleRotation56BEF0(local.Rotation, new(scale, scale, scale)), X = DrawingWord(task, 0x10), Y = DrawingWord(task, 0x12), Z = DrawingWord(task, 0x14) };
        frame = DrawingLoad(frame, inputs, local);
        int age = DrawingWord(task, 12);
        frame = DrawingStatic(frame, resources.MeshB.Address, age < 6 ? 0x33u : 0xF3u, age < 6 ? null : (age - 6) << 9, 2, inputs, NativeBattlePilotVisualLifetimes.Identity(158, task) with { ResourceGeneration = resources.MeshB.Address });
        if (DrawingPaused(inputs))
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        short angularVelocity = DrawingWord(task, 0x1A);
        short growth = DrawingWord(task, 0x1E);
        task = task.WriteWord(0x18, unchecked((ushort)(DrawingWord(task, 0x18) + angularVelocity))).WriteWord(0x1A, unchecked((ushort)(angularVelocity - (angularVelocity >> 4)))).WriteWord(0x1C, unchecked((ushort)(DrawingWord(task, 0x1C) + growth))).WriteWord(0x1E, unchecked((ushort)(growth - growth / 8))).WriteWord(12, unchecked((ushort)(age + 1)));
        return (Write(frame, address, task), DrawingWord(task, 12) >= 14 ? 2 : 0);
    }

    public static (NativeBattleMeltEye158Frame Frame, int Result) AdvanceHeadMesh(NativeBattleMeltEye158Frame frame, uint address, NativeBattleMeltEye158Resources resources, NativeBattleMeltEye158Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint curve = DrawingCurve(DrawingWord(task, 14));
        NativeBattleVector end = DrawingStateVector(frame.State, curve + 0x38);
        NativeBattleVector position = TaskVector(task, 0x10);
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(end.X - position.X, end.Y - position.Y, end.Z - position.Z);
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, normalized.X, normalized.Y, normalized.Z);
        NativeBattleActionMatrix aligned = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
        NativeBattleActionMatrix identity = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        short scale = DrawingWord(task, 0x1C);
        identity = identity with { Rotation = NativeBattlePoseMatrices.ScaleRotation56BEF0(identity.Rotation, new(scale, scale, scale)) };
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Compose56C2F0(aligned, identity);
        NativeBattleVector origin = DrawingStateVector(frame.State, curve);
        local = local with { X = origin.X, Y = origin.Y, Z = origin.Z };
        frame = DrawingLoad(frame, inputs, local);
        int age = DrawingWord(task, 12);
        frame = DrawingStatic(frame, resources.MeshA.Address, age < 8 ? 0x33u : 0xF3u, age < 8 ? null : (age - 8) * 682, 30, inputs, NativeBattlePilotVisualLifetimes.Identity(158, task) with { ResourceGeneration = resources.MeshA.Address });
        if (DrawingPaused(inputs))
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        short shrink = DrawingWord(task, 0x1E);
        task = task.WriteWord(0x1C, unchecked((ushort)(DrawingWord(task, 0x1C) - shrink))).WriteWord(0x1E, unchecked((ushort)(shrink - shrink / 8))).WriteWord(12, unchecked((ushort)(age + 1)));
        return (Write(frame, address, task), DrawingWord(task, 12) >= 14 ? 2 : 0);
    }

    private static NativeBattleMeltEye158Frame DrawingLoad(NativeBattleMeltEye158Frame frame, NativeBattleMeltEye158Inputs inputs, NativeBattleActionMatrix local)
    {
        NativeBattleActionMatrix combined = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), local);
        return LoadGteMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(combined, null));
    }

    private static NativeBattleMeltEye158Frame DrawingStatic(NativeBattleMeltEye158Frame frame, uint source, uint flags, int? cue, byte shift, NativeBattleMeltEye158Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        (frame, uint temporary) = DrawingAllocate(frame, 88);
        NativeBattleFileStaging staging = frame.Staging!.Write(temporary, BitConverter.GetBytes(source)).Write(temporary + 8, new byte[4]).Write(temporary + 0x1C, BitConverter.GetBytes(flags));
        if (cue is int value)
        {
            staging = staging.Write(temporary + 12, BitConverter.GetBytes(value));
        }
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleMeltEye158DrawStatic88(temporary, source, shift) { PilotIdentity = pilotIdentity });
        return DrawingRelease(frame, 88);
    }

    private static (NativeBattleMeltEye158Frame Frame, uint Address) DrawingAllocate(NativeBattleMeltEye158Frame frame, int bytes)
    {
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Melt-Eye requires its actual temporary memory."), bytes);
        return (frame with { Staging = allocated.Staging }, allocated.Address);
    }

    private static NativeBattleMeltEye158Frame DrawingBeginSprite(NativeBattleMeltEye158Frame frame, uint temporary, uint source) => frame with { Staging = NativeBattleModuleSpriteTemporary.Begin571C80(frame.Staging!, temporary, source, 0) };
    private static NativeBattleMeltEye158Frame DrawingRelease(NativeBattleMeltEye158Frame frame, int bytes) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, bytes) };
    private static short DrawingWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static bool DrawingPaused(NativeBattleMeltEye158Inputs inputs) => (inputs.GraphicsFlags & 0x201) != 0;
    private static uint DrawingCurve(int ordinal) => ordinal is >= 0 and < CurveCount ? checked(CurveBank + (uint)(ordinal * CurveStride)) : throw new NotSupportedException("Melt-Eye curve ordinal leaves its original records.");
    private static NativeBattleVector DrawingStateVector(NativeBattleMeltEye158State state, uint address) => new(unchecked((short)state.ReadWord(address)), unchecked((short)state.ReadWord(address + 2)), unchecked((short)state.ReadWord(address + 4)));
    private static short? DrawingOptionalWord(NativeBattleModulePoolTask task, int offset)
    {
        if (task.ReadUnknownPayloadRanges() is { } ranges && ranges.Any(range => range.Offset < offset + 2 && range.Offset + range.Length > offset))
        {
            return null;
        }
        return DrawingWord(task, offset);
    }
}
