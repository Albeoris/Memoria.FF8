using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel212Stage(int Intensity) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212Contacts : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212Program(uint Address, int Offset, int Size, NativeBattleActionMatrix Basis, string Windows, string? DepthFlags, string? EmitFlags, short Depth, short SecondaryDepth, short VerticalOffset, bool Initialize) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212RestoreModels : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212GroundBirths(NativeBattleVector Origin) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212BoneEmission(byte First, byte Second, uint PointAddress) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212Pulse(int Phase) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212RingProgram(short Tick) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212BodyProgramTransition(short Tick) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212RestoreTargetVisibility : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212DrawTargets(short Tick) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212DrawEcho : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212CaptureEcho : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212Sound(uint Descriptor) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212LateBirths(NativeBattleVector Origin) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212Camera(short Tick) : NativeBattleDuel212Operation;
public sealed record NativeBattleDuel212SceneInputs(NativeBattleDuel212Inputs Drawing, Func<int, byte, short, NativeBattleVector> ReadBone502170, Func<int, NativeBattleModuleAnchor> ReadAnchor571400);

public static partial class NativeBattleDuel212Controller
{
    public static NativeBattleDuel212Step Advance6A1660(NativeBattleDuel212Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleDuel212SceneInputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x6A1660)
        {
            throw new InvalidDataException("Native Burning Rave requires its actual scene callback.");
        }
        short tick = unchecked((short)(task.ReadWord(12) + 1));
        task = task.WriteWord(12, unchecked((ushort)tick));
        if (tick == 182)
        {
            return new(frame, pool.WriteSlot(slot, task), 2);
        }
        if (tick == 174)
        {
            Execute(new NativeBattleDuel212Contacts());
            Execute(new NativeBattleDuel212RestoreModels());
            Execute(new NativeBattleDuel212Stage(0));
            return Finish();
        }
        if (tick > 174)
        {
            return Finish();
        }
        Execute(new NativeBattleDuel212Stage(tick < 12 ? 2300 * tick / 12 : tick < 170 ? 2300 : 2300 * (182 - tick) / 12));
        int actor = frame.Memory.Duel212?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Burning Rave requires its entered actor.");
        if ((uint)(tick - 2) < 56)
        {
            if (tick == 2)
            {
                NativeBattleModuleAnchor anchor = inputs.ReadAnchor571400(actor);
                task = task.WriteWord(312, unchecked((ushort)anchor.X)).WriteWord(314, unchecked((ushort)anchor.Y)).WriteWord(316, unchecked((ushort)anchor.Z)).WriteWord(318, unchecked((ushort)anchor.W));
            }
            Program(0x11883AC, 1496, 400, NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = Word(task, 312), Z = Word(task, 316) }, "100", null, null, -128, 0, unchecked((short)(32 * (tick - 2))));
            if ((uint)(tick - 2) < 40)
            {
                Execute(new NativeBattleDuel212GroundBirths(new(Word(task, 312), 0, Word(task, 316))));
            }
        }
        if ((uint)(tick - 17) < 28)
        {
            byte first = ((tick - 17) & 1) != 0 ? (byte)13 : (byte)14;
            byte second = ((tick - 17) & 1) != 0 ? (byte)8 : (byte)10;
            Execute(new NativeBattleDuel212BoneEmission(first, second, ((tick - 17) & 1) != 0 ? 0x11AC240u : 0x11AC248u));
        }
        if ((uint)(tick - 81) < 24)
        {
            NativeBattleVector point = inputs.ReadBone502170(actor, 22, 0);
            Program(0x1187F2C, 2652, 68, NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = point.X, Y = point.Y, Z = point.Z }, "0", null, null, -128, 0, null);
        }
        if ((uint)(tick - 81) < 12)
        {
            Execute(new NativeBattleDuel212Pulse(tick - 81));
        }
        if ((uint)(tick - 84) < 26)
        {
            Execute(new NativeBattleDuel212RingProgram(tick));
        }
        if ((uint)(tick - 94) < 70)
        {
            int phase = tick - 94;
            Execute(new NativeBattleDuel212BodyProgramTransition(tick));
            NativeBattleActionMatrix basis = NativeBattleGfMatrices.Euler56CE30(new(0, 2048, 0)) with { X = Word(task, phase < 15 ? 320 : 328), Z = Word(task, phase < 15 ? 324 : 332) };
            Program(0x118BA10, 1496, 1156, basis, "000000000", "110000000", phase < 15 ? "000000000" : "110000000", 0, 384, null);
        }
        else if (tick == 164)
        {
            Execute(new NativeBattleDuel212RestoreTargetVisibility());
        }
        if ((uint)(tick - 109) < 25 || (uint)(tick - 134) <= 40)
        {
            Execute(new NativeBattleDuel212DrawTargets(tick));
        }
        if (tick > 64 && tick <= 80)
        {
            Execute(new NativeBattleDuel212DrawEcho());
        }
        if (tick >= 64 && tick <= 74)
        {
            Execute(new NativeBattleDuel212CaptureEcho());
        }
        if (tick is 5 or 67)
        {
            Execute(new NativeBattleDuel212Sound(Resources(frame).Dword(tick == 5 ? 0x1181070u : 0x1181074u)));
        }
        if ((uint)(tick - 134) < 10)
        {
            Execute(new NativeBattleDuel212LateBirths(new(Word(task, 328), 0, Word(task, 332))));
        }
        Execute(new NativeBattleDuel212Camera(tick));
        return Finish();

        NativeBattleDuel212Step Finish() => new(frame, pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback)), 0);
        void Execute(NativeBattleDuel212Operation operation)
        {
            pool = pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback));
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(active => active.DescriptorAddress == pool.DescriptorAddress ? pool : active).ToImmutableArray() } };
            frame = inputs.Drawing.Execute(frame, operation);
            pool = frame.Effect.Pools.Single(active => active.DescriptorAddress == pool.DescriptorAddress);
            task = pool.Slots[slot];
        }
        void Program(uint address, int offset, int size, NativeBattleActionMatrix basis, string windows, string? depthFlags, string? emitFlags, short depth, short secondary, short? vertical)
        {
            Execute(new NativeBattleDuel212Program(address, offset, size, NativeBattleGfMatrices.Compose56C2F0(inputs.Drawing.View, basis), windows, depthFlags, emitFlags, depth, secondary, vertical ?? 0, address == 0x11883AC ? tick == 2 : address == 0x1187F2C ? tick == 81 : address == 0x1184ACC ? tick == 84 : tick is 94 or 109));
        }
    }

}
