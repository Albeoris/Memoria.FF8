using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDegenerator306Controller
{
    private static (NativeBattleDegenerator306Frame Frame, int Result) Child(NativeBattleDegenerator306Frame frame, uint address, NativeBattleDegenerator306Resources resources, NativeBattleDegenerator306Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            MainCallback => Main(frame, address, resources, inputs),
            0x5A3490 or 0x5A35D0 or 0x5A3840 or 0x5A3970 or 0x5A53B0 => Mesh(frame, address, resources, inputs),
            0x5A3710 => CameraSprite(frame, address, inputs),
            0x5A47C0 => Body(frame, address, inputs),
            0x5A4D80 => NativeBattleDegenerator306Slots.Advance(frame, address, resources, inputs),
            0x5A5070 or 0x5A5270 or 0x5A5540 => NativeBattleDegenerator306Actors.Advance(frame, address, inputs),
            _ => throw new NotSupportedException("Degenerator reached an unsupported original child callback.")
        };
    }

    private static (NativeBattleDegenerator306Frame Frame, int Result) Mesh(NativeBattleDegenerator306Frame frame, uint address, NativeBattleDegenerator306Resources resources, NativeBattleDegenerator306Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint callback = task.Callback;
        int age = Signed(task, 12);
        short angle = Signed(task, 0x18);
        short scale = Signed(task, 0x1C);
        bool custom = callback == 0x5A3970;
        bool camera = callback == 0x5A3840;
        bool billboard = callback == 0x5A53B0;
        NativeBattleVector angles = custom ? new(0, angle, Signed(task, 0x20)) : camera || billboard ? new(0, 0, angle) : new(0, angle, 0);
        NativeBattleActionMatrix local = custom ? NativeBattleGfMatrices.Euler56CD50(angles) : NativeBattleGfMatrices.Euler56CE30(angles);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        NativeBattleActionMatrix matrix;
        if (camera)
        {
            matrix = local with { X = 0, Y = 0, Z = inputs.ReadFocalShadow() };
        }
        else
        {
            NativeBattleActionMatrix view = inputs.ReadView(frame).ReadMatrix();
            NativeBattleVector position = new(Signed(task, 0x10), Signed(task, 0x12), Signed(task, 0x14));
            if (billboard)
            {
                (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, position.X, position.Y, position.Z);
                matrix = local with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
            }
            else
            {
                matrix = NativeBattleGfMatrices.Compose56C2F0(view, local with { X = position.X, Y = position.Y, Z = position.Z });
            }
        }
        frame = LoadMatrix(frame, matrix);
        int length = custom ? 108 : 88;
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        NativeBattleEffectArena temporary = Bounded(allocation.Staging, allocation.Address, length);
        uint source;
        int? fade;
        int terminal;
        switch (callback)
        {
            case 0x5A3490:
                source = 0xCEAB94;
                fade = age < 4 ? (4 - age) << 10 : age >= 90 ? (age - 90) << 9 : null;
                terminal = 98;
                break;
            case 0x5A35D0:
                source = 0xCEAF2C;
                fade = age >= 2 ? (age - 2) << 9 : null;
                terminal = 10;
                break;
            case 0x5A3840:
                source = 0xCE643C;
                fade = age < 8 ? (8 - age) << 9 : age >= 50 ? (age - 50) << 9 : null;
                terminal = 58;
                break;
            case 0x5A3970:
                source = 0xCE7A2C;
                fade = age < 4 ? (4 - age) << 10 : null;
                terminal = 40;
                break;
            case 0x5A53B0:
                source = 0xCEA2E4;
                fade = age < 6 ? 4096 - age * 682 : age >= 22 ? (age - 22) * 682 : null;
                terminal = 28;
                break;
            default:
                throw new NotSupportedException("Degenerator selected an unsupported mesh callback.");
        }
        temporary = Dword(Dword(Dword(temporary, 0, source), 8, 0), 0x1C, fade.HasValue ? 0xF3u : 0x33u);
        if (fade is int value)
        {
            temporary = Dword(temporary, 12, unchecked((uint)value));
        }
        if (custom)
        {
            int material = age % 12;
            if (material < 0)
            {
                throw new InvalidDataException("Degenerator material selector leaves its original twelve-step cycle.");
            }
            temporary = Dword(temporary, 0x20, resources.MaterialCycle[material]);
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, length) };
        frame = inputs.Execute(frame, custom ? new NativeBattleDegenerator306DrawCustom108(allocation.Address) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(306, Read(frame, address)) with { ResourceGeneration = source } : null } : new NativeBattleDegenerator306DrawStatic88(allocation.Address, source, camera ? 4122 : 17, camera ? 14 : 2) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(306, Read(frame, address)) with { ResourceGeneration = source } : null });
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (callback == 0x5A3490)
        {
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) - task.ReadWord(0x1E))));
        }
        else if (callback == 0x5A35D0)
        {
            short velocity = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 16)));
        }
        else
        {
            task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A))));
            if (custom)
            {
                task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) - task.ReadWord(0x1E))));
            }
            else if (billboard)
            {
                task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x1E))));
            }
        }
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        return (Write(frame, address, task.WriteWord(12, next)), unchecked((short)next) >= terminal ? 2 : 0);
    }

    private static (NativeBattleDegenerator306Frame Frame, int Result) CameraSprite(NativeBattleDegenerator306Frame frame, uint address, NativeBattleDegenerator306Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if ((age & 1) != 0)
        {
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0));
            local = NativeBattlePoseMatrices.Scale56BEF0(local, 256, 256, 256) with { X = 0, Y = 0, Z = inputs.ReadFocalShadow() };
            frame = LoadMatrix(frame, local);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 180);
            NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, 0xCE60C0, 0).Write(allocation.Address + 4, BitConverter.GetBytes((ushort)0));
            frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleDegenerator306DrawSprite180(allocation.Address, 0xCE60C0, 0, 14) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(306, Read(frame, address)) with { ResourceGeneration = 0xCE60C0 } : null });
            frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 180) };
        }
        return Age(frame, address, 30, inputs);
    }

    private static (NativeBattleDegenerator306Frame Frame, int Result) Body(NativeBattleDegenerator306Frame frame, uint address, NativeBattleDegenerator306Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        byte actor = checked((byte)frame.State.ReadDword(TargetGlobal));
        if (age < 30)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorFlags(actor, 0, 4));
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 136);
            NativeBattleEffectArena temporary = Dword(Bounded(allocation.Staging, allocation.Address, 136), 0x2C, unchecked((uint)((age << 12) / 30)));
            temporary = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 0x10, 8), 0, temporary, 0x60, 8);
            frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 136) };
            frame = inputs.Execute(frame, new NativeBattleDegenerator306DrawBody136(allocation.Address, actor) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(306, Read(frame, address)) with { ResourceGeneration = 0 } : null });
            frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 136) };
        }
        return Age(frame, address, 31, inputs);
    }

    private static (NativeBattleDegenerator306Frame Frame, int Result) Age(NativeBattleDegenerator306Frame frame, uint address, int terminal, NativeBattleDegenerator306Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        return (Write(frame, address, Read(frame, address).WriteWord(12, next)), unchecked((short)next) >= terminal ? 2 : 0);
    }
    private static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        return task.BorrowPayloadArena(offset, length);
    }
    private static NativeBattleDegenerator306Frame LoadMatrix(NativeBattleDegenerator306Frame frame, NativeBattleActionMatrix matrix) => frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null) } };
    private static NativeBattleFileStaging RequireStaging(NativeBattleDegenerator306Frame frame) => frame.Staging ?? throw new NotSupportedException("Degenerator requires its physical temporary memory.");
    private static NativeBattleEffectArena Bounded(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
}
