using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect187OpeningGridStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context120, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect187OpeningGrid
{
    public static NativeBattleEffect187OpeningGridStep Draw647B10(NativeBattleEffectMemory memory, NativeBattleEffectArena context, NativeBattleCameraProjection projection)
    {
        int size = Int(context, 0);
        int uvStep = Int(context, 4);
        int count = Int(context, 8);
        if (uvStep != 32 || (size, count) is not ((256, 32) or (512, 16)))
        {
            throw new NotSupportedException("Effect187 opening grid requires its reached native dimensions.");
        }
        int step = size / uvStep;
        int initialX = Int(context, 12) & 255;
        int initialY = Int(context, 16) & 255;
        int origin = unchecked(-(count / 2) * size);
        context = Put(Put(Put(Put(context, 0x18, initialY), 0x1C, unchecked(origin - initialX * step)), 0x20, unchecked(origin - initialY * step)), 0x24, step);
        for (int corner = 0; corner < 4; corner++)
        {
            context = context.Write(0x56 + corner * 8, BitConverter.GetBytes((ushort)0));
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect187 opening grid requires its actual geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect187 opening grid requires its actual arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect187 opening grid requires its current projection controls.");
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int row = 0; row < count; row++)
        {
            int currentY = Int(context, 0x18);
            int vStart = currentY & 255;
            int vEnd = vStart + uvStep;
            int vExtent = uvStep;
            if (vEnd >= 256)
            {
                vEnd = 255;
                vExtent = 256 - vStart;
            }
            context = Put(Put(Put(context, 0x38, vStart), 0x3C, vEnd), 0x40, vExtent);
            short top = unchecked((short)(step * currentY + Int(context, 0x20)));
            short bottom = unchecked((short)(step * (currentY + vExtent) + Int(context, 0x20)));
            context = context.Write(0x58, BitConverter.GetBytes(top)).Write(0x60, BitConverter.GetBytes(top)).Write(0x68, BitConverter.GetBytes(bottom)).Write(0x70, BitConverter.GetBytes(bottom));
            context = Put(Put(context, 0x18, unchecked(currentY + vExtent)), 0x14, initialX);
            for (int column = 0; column < count; column++)
            {
                int currentX = Int(context, 0x14);
                int uStart = currentX & 255;
                int uEnd = uStart + uvStep;
                int uExtent = uvStep;
                if (uEnd >= 256)
                {
                    uEnd = 255;
                    uExtent = 256 - uStart;
                }
                context = Put(Put(Put(context, 0x2C, uStart), 0x30, uEnd), 0x34, uExtent);
                short left = unchecked((short)(step * currentX + Int(context, 0x1C)));
                short right = unchecked((short)(step * (currentX + uExtent) + Int(context, 0x1C)));
                context = context.Write(0x54, BitConverter.GetBytes(left)).Write(0x64, BitConverter.GetBytes(left)).Write(0x5C, BitConverter.GetBytes(right)).Write(0x6C, BitConverter.GetBytes(right));
                context = Put(context, 0x14, unchecked(currentX + uExtent));
                for (int corner = 0; corner < 3; corner++)
                {
                    Load(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                SkinWrites packet = new(40, allocated);
                packet.Word(0, 0x09000000);
                packet.Word(4, unchecked((uint)Int(context, 0x74)) | 0x2E000000);
                packet.Word16(12, unchecked((ushort)(uStart | vStart << 8)));
                packet.Word16(14, 0x3FD4);
                packet.Word16(20, unchecked((ushort)(uEnd | vStart << 8)));
                packet.Word16(22, 0xB6);
                packet.Word16(28, unchecked((ushort)(uStart | vEnd << 8)));
                packet.Word16(36, unchecked((ushort)(uEnd | vEnd << 8)));
                uint flags = registers.Colors.Flags!.Value;
                context = Put(context, 0x50, unchecked((int)flags));
                if ((flags & 0x60000) != 0)
                {
                    attempts.Add(packet.Attempt(null));
                    continue;
                }
                uint[] screens = new uint[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    screens[corner] = registers.Geometry.Sxy[corner]!.Value;
                    packet.Word(8 + corner * 8, screens[corner]);
                }
                Load(3, 0);
                registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                screens[3] = registers.Geometry.Sxy[2]!.Value;
                packet.Word(32, screens[3]);
                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect187 opening grid requires its actual ZSF4."));
                if (screens.All(screen => (screen & 0xFFFF) > 0xA00) || screens.All(screen => (screen >> 16) > 0x6C0))
                {
                    attempts.Add(packet.Attempt(null));
                    continue;
                }
                uint depth = registers.Geometry.Otz!.Value;
                context = Put(context, 0x4C, unchecked((int)depth));
                ImmutableArray<int> depths = registers.Geometry.Sz.Select(value => unchecked((int)value!.Value)).ToImmutableArray();
                attempts.Add(packet.Attempt(new(unchecked(17 + ((int)depth >> 2)), packet.Words(), depths)));
                allocated = checked(allocated + 40);
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, context, new(attempts.ToImmutable(), allocated));

        void Load(int corner, int slot)
        {
            int offset = 0x54 + corner * 8;
            (byte[] bytes, bool[] known) = context.ReadSnapshot(offset, 8);
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(context.Read(offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(context.Read(offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(context.Read(offset + 4, 2)));
            short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
            registers = NativeBattleGeometry.LoadVector(registers, slot, point, w);
        }
    }

    private static int Int(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadInt32LittleEndian(context.Read(offset, 4));

    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, int value) => context.Write(offset, BitConverter.GetBytes(value));
}
