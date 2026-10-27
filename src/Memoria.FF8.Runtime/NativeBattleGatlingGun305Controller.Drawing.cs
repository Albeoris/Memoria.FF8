using static Memoria.FF8.Runtime.NativeBattleGatlingGun305Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGatlingGun305Controller
{
    private static (NativeBattleGatlingGun305Frame Frame, int Result) Mesh(NativeBattleGatlingGun305Frame frame, uint address, NativeBattleGatlingGun305Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool first = task.Callback == 0x5A5C50;
        bool third = task.Callback == 0x5A6750;
        int age = Signed(task, 12);
        short scale = Signed(task, 0x1C);
        NativeBattleVector position = new(Signed(task, 0x10), Signed(task, 0x12), Signed(task, 0x14));
        NativeBattleVector angles = first ? new(0, Signed(task, 0x18), Signed(task, 0x1A)) : third ? new(1024, Signed(task, 0x18), 0) : new(0, Signed(task, 0x18), 0);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(angles), scale, scale, scale) with { X = position.X, Y = position.Y, Z = position.Z };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        if (third)
        {
            NativeBattleActionMatrix additional = NativeBattleGfMatrices.Euler56CD50(new(0, Signed(task, 0x1A), 0));
            matrix = matrix with { Rotation = NativeBattleGfMatrices.Multiply56C090(matrix, additional).Rotation };
        }
        if (first)
        {
            frame = SetupSprite(frame, task, 0x10, 0, unchecked((short)-(scale >> 3)), inputs);
            frame = LoadMatrix(frame, matrix, false);
        }
        else
        {
            frame = LoadMatrix(frame, matrix, true);
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 88);
        NativeBattleEffectArena temporary = Bounded(allocation.Staging, allocation.Address, 88);
        uint source = first ? 0xCED698u : third ? 0xCEDC60u : 0xCED998u;
        int? fade = third ? age >= 1 ? (age - 1) << 10 : null : age >= 0 ? age << (first ? 11 : 9) : null;
        temporary = Dword(Dword(Dword(temporary, 0, source), 8, 0), 0x1C, fade.HasValue ? 0xF3u : 0x33u);
        if (fade is int value)
        {
            temporary = Dword(temporary, 12, unchecked((uint)value));
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 88) };
        frame = inputs.Execute(frame, new NativeBattleGatlingGun305DrawStatic88(allocation.Address, source));
        if (first)
        {
            frame = inputs.Execute(frame, new NativeBattleGatlingGun305DrawStatic88(allocation.Address, source));
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 88) };
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (!first)
        {
            short velocity = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 8)));
        }
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        int terminal = first ? 2 : third ? 5 : 9;
        return (Write(frame, address, task.WriteWord(12, next)), unchecked((short)next) >= terminal ? 2 : 0);
    }

    private static (NativeBattleGatlingGun305Frame Frame, int Result) Sprite(NativeBattleGatlingGun305Frame frame, uint address, NativeBattleGatlingGun305Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short scale = Signed(task, 0x1C);
        frame = SetupSprite(frame, task, 0x10, scale, unchecked((short)-(scale >> 3)), inputs);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 180);
        ushort phase = task.ReadWord(12);
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, 0xCED15C, 0).Write(allocation.Address + 4, BitConverter.GetBytes(phase));
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleGatlingGun305DrawSprite180(allocation.Address, 0xCED15C, phase));
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 180) };
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        return (Write(frame, address, Read(frame, address).WriteWord(12, next)), unchecked((short)next) >= 16 ? 2 : 0);
    }

    private static NativeBattleGatlingGun305Frame SetupSprite(NativeBattleGatlingGun305Frame frame, NativeBattleModulePoolTask task, int offset, short scale, short depth, NativeBattleGatlingGun305Inputs inputs)
    {
        NativeBattleEffectArena packed = TaskView(task, offset, 8);
        (byte[] bytes, bool[] known) = packed.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BitConverter.ToInt16(bytes) : null;
        NativeBattleVector position = new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), position, w, scale, depth) };
    }

    private static NativeBattleGatlingGun305Frame LoadMatrix(NativeBattleGatlingGun305Frame frame, NativeBattleActionMatrix matrix, bool translation) => frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), matrix, true, translation, null) } };
    private static NativeBattleFileStaging RequireStaging(NativeBattleGatlingGun305Frame frame) => frame.Staging ?? throw new NotSupportedException("Gatling Gun requires its actual physical temporary memory.");
    private static NativeBattleEffectArena Bounded(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, task.ReadRawPayloadSpan().Slice(offset - 12, length));
        if (task.ReadUnknownPayloadRanges() is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                int first = Math.Max(offset, range.Offset);
                int end = Math.Min(offset + length, range.Offset + range.Length);
                if (first < end)
                {
                    result = result.MarkUnknown(first - offset, end - first);
                }
            }
        }
        return result;
    }
}
