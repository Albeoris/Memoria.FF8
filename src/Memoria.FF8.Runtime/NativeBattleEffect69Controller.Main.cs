using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect69Controller
{
    public static NativeBattleEffect69Step AdvanceRoot(NativeBattleEffect69Frame frame, uint address, NativeBattleEffect69Resources resources, NativeBattleEffect69Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool alternate = task.ReadByte(0xE) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, unchecked(arena + (alternate ? 0x3F020u : 0x47020u))) };
        frame = Write(frame, address, task.WriteByte(0xE, alternate ? (byte)0 : (byte)1));
        if (task.ReadWord(0xC) == 1 && task.ReadByte(0xF) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(0xF, 1));
            frame = NativeBattleEffect69Particles.InitializeSparks6FE5A0(frame);
            frame = NativeBattleEffect69Particles.InitializeDebris6FE250(frame);
            frame = InitializePool(frame, 1);
            (frame, uint main) = AppendPool(frame, 1, MainCallback, 0x8E4);
            frame = inputs.Execute(frame, new NativeBattleEffect69StartVibration(resources.Vibration, main, 0x10));
            NativeBattleModelTransform caster = inputs.ReadModel(frame, Caster(frame)).Transform ?? throw new NotSupportedException("Effect69 requires its current caster position.");
            NativeBattleActionMatrix cpu = new([1024, 0, 0, 0, 2048, 0, 0, 0, 1024], -(caster.Position28.X / 4), 0, -(caster.Position28.Z / 4), 4096);
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(cpu, null);
            NativeBattleEffect69StageStep first = NativeBattleEffect69StageCapture.StageCapture6FE300(frame, 1, packed, checked(arena + 0x5A50), inputs);
            frame = NativeBattleEffect69StageCapture.StageCapture6FE300(first.Frame, 2, packed, first.NextMetric, inputs).Frame;
        }
        if (Read(frame, address).ReadByte(0xF) != 0)
        {
            frame = frame with { State = frame.State.WriteView(ViewAddress, NormalizedView69(inputs.ReadView(frame)), 32) };
            (frame, int survivors) = Scan(frame, 1, resources, inputs);
            if (survivors == 0)
            {
                return new(frame, 2);
            }
        }
        task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)))), 0);
    }

    public static NativeBattleEffect69Step AdvanceMain(NativeBattleEffect69Frame frame, uint address, NativeBattleEffect69Resources resources, NativeBattleEffect69Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = unchecked((short)task.ReadWord(0xC));
        int caster = Caster(frame);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        if (unchecked((uint)age) < 92)
        {
            frame = AdvanceProgramme(frame, address, age == 0, resources, inputs);
        }
        if (age == 55)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect69ModelFlags(caster, 0xFFF7, 0, true));
        }
        if (age is >= 55 and < 85)
        {
            uint scratch = (inputs.ReadScratchAddress ?? throw new NotSupportedException("Effect69 requires its current shared projected scratch pointer."))(frame);
            frame = inputs.Execute(frame, new NativeBattleEffect69DrawCaster(frame.State.ReadDword(CasterGlobal), 0, scratch));
            frame = inputs.Execute(frame, new NativeBattleEffect69ModelFlags(caster, 0xFFFF, 4, false));
        }
        else if (age == 85)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect69ModelFlags(caster, 0xFFFB, 0, true));
        }
        if (unchecked((uint)(age - 73)) < 35)
        {
            int radius = Math.Min(6000, unchecked((age - 73) * 560));
            ushort threshold = unchecked((ushort)(unchecked(radius * radius) >> 12));
            NativeBattleEffect69StageStep first = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(frame, 1, threshold, 63, checked(arena + 0x5A50), inputs, 4122, 16);
            frame = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(first.Frame, 2, threshold, 63, first.NextMetric, inputs, 17, 0).Frame;
            frame = inputs.Execute(frame, new NativeBattleEffect69StageFlags(1, 0xFD, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect69StageFlags(2, 0xFD, 0));
        }
        else
        {
            frame = inputs.Execute(frame, new NativeBattleEffect69StageFlags(1, 0xFF, 2));
            frame = inputs.Execute(frame, new NativeBattleEffect69StageFlags(2, 0xFF, 2));
        }
        if (age < 48)
        {
            frame = NativeBattleEffect69Particles.DrawSparks7001E0(frame, resources, inputs);
        }
        frame = NativeBattleEffect69Particles.DrawDebris6FF2F0(frame, inputs);
        if (unchecked((uint)(age - 94)) < 32)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect69DeliverBatch(NativeBattleModuleRenderCommands.Emit700150(age - 94, 255, 255, 255, 14, 4, 14), CursorGlobal));
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect69Sound501330(resources.Sound, 0, 128));
        }
        if (age == 122)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect69StopVibration(unchecked((int)Read(frame, address).ReadDword(0x10))));
        }
        if (age < 47)
        {
            frame = NativeBattleEffect69Particles.AdvanceSparks700370(frame);
        }
        frame = NativeBattleEffect69Particles.AdvanceDebris6FF5F0(frame);
        uint flags = inputs.ReadFlags(frame);
        if ((flags & 0x201) != 0 && ((flags & 1) != 0 || IoResult(frame, inputs) < 0))
        {
            return new(frame, 0);
        }
        if (age == 50)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect69QueueStage512AA0(0, checked(arena + 0x8020)));
        }
        else if (age == 110)
        {
            if (IoResult(frame, inputs) < 0)
            {
                return new(frame, 0);
            }
            frame = inputs.Execute(frame, new NativeBattleEffect69ReloadStage512AC0(0, checked(arena + 0x8020)));
            frame = inputs.Execute(frame, new NativeBattleEffect69ModelFlags(caster, 0xFFFF, 0x20, false));
            frame = inputs.Execute(frame, new NativeBattleEffect69ModelColors44(inputs.ReadColor(frame, 0xB8B9A8)));
        }
        if (unchecked((uint)(age - 94)) < 32)
        {
            short z = unchecked((short)(-2000 - unchecked((age - 94) * 1400) / 31));
            frame = inputs.Execute(frame, new NativeBattleEffect69ModelPositionZ(caster, z, true));
        }
        if (unchecked((uint)age) < 30)
        {
            frame = NativeBattleEffect69Particles.BirthSparks(frame, resources, inputs);
        }
        if (age >= 126)
        {
            return new(frame, 2);
        }
        task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)))), 0);
    }

    private static NativeBattleEffectArena NormalizedView69(NativeBattlePackedMatrix32 packed)
    {
        NativeBattleEffectArena result = PackedArena(packed);
        NativeBattleActionMatrix normalized = NativeBattleGfMatrices.Normalize67AAE0(packed.ReadMatrix());
        for (int index = 0; index < 9; index++)
        {
            result = result.Write(index * 2, BitConverter.GetBytes(normalized.Rotation[index]));
        }
        return result;
    }

    private static int IoResult(NativeBattleEffect69Frame frame, NativeBattleEffect69Inputs inputs) => (inputs.ReadIoResult ?? throw new NotSupportedException("Effect69 requires its current signed file IO result."))(frame);
}
