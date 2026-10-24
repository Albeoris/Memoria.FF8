using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect166ModeMesh96Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context96, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect166ModeMesh96
{
    public static NativeBattleEffect166ModeMesh96Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context96, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2)
    {
        if (Dword(context96, 0) != mesh.Address || Dword(context96, 0x1C) != 0 || depthShift > 31)
        {
            throw new NotSupportedException("Effect166 mode mesh requires its selected flat triangle context.");
        }
        int countVertices = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        if (countVertices <= 0 || cursor != 8 + countVertices * 8)
        {
            throw new InvalidDataException("Effect166 mode mesh requires its packed vertex rows.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Mode mesh requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Mode mesh requires current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Mode mesh requires current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Mode mesh requires the current primitive owner.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Mode mesh requires current renderer registers.");
        byte[] rgb = context96.Read(8, 3);
        far = far with { FarRed = (uint)rgb[0] << 4, FarGreen = (uint)rgb[1] << 4, FarBlue = (uint)rgb[2] << 4 };
        context96 = Put(Put(Put(context96, 4, mesh.Address + 8), 0x18, 0), 0x20, checked(mesh.Address + (uint)cursor));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            cursor = checked(cursor + 4);
            context96 = Put(context96, 0x20, checked(mesh.Address + (uint)cursor));
            if (count < 0 || count != 0 && group != 2)
            {
                throw new NotSupportedException("Effect166 mode mesh leaves its selected triangle group.");
            }
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> face = mesh.Bytes.AsSpan().Slice(cursor, 20);
                for (int corner = 0; corner < 3; corner++)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(4 + corner * 2)) * 4;
                    if ((offset & 7) != 0 || offset >= countVertices * 8)
                    {
                        throw new InvalidDataException("Mode mesh selected an invalid vertex row.");
                    }
                    ReadOnlySpan<byte> vertex = mesh.Bytes.AsSpan().Slice(8 + offset, 6);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..])), null);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                SkinWrites packet = new(40, allocated);
                packet.Word(0, 0x09000000);
                packet.Word(8, BinaryPrimitives.ReadUInt32LittleEndian(face));
                packet.Word(16, BinaryPrimitives.ReadUInt32LittleEndian(face[12..]));
                packet.Word(24, BinaryPrimitives.ReadUInt32LittleEndian(face[16..]));
                packet.Word(32, BinaryPrimitives.ReadUInt16LittleEndian(face[10..]));
                uint flags = registers.Colors.Flags!.Value;
                context96 = Put(context96, 0x30, flags);
                bool accepted = false;
                if ((flags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    int area = unchecked((int)registers.Geometry.Mac0!.Value);
                    context96 = Put(context96, 0x24, unchecked((uint)area));
                    if (area > 0)
                    {
                        uint[] screens = registers.Geometry.Sxy.Select(value => value!.Value).ToArray();
                        for (int corner = 0; corner < 3; corner++)
                        {
                            packet.Word(12 + corner * 8, screens[corner]);
                        }
                        registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Mode mesh requires current ZSF3."));
                        if (!screens.All(value => unchecked((short)value) is < 0 or > 2560) && !screens.All(value => unchecked((short)(value >> 16)) is < 0 or > 1728))
                        {
                            uint depth = registers.Geometry.Otz!.Value;
                            context96 = Put(context96, 0x2C, depth);
                            packet.Word(4, Dword(context96, 0x58));
                            packet.Word(36, Dword(context96, 0x5C));
                            ImmutableArray<int> payload = [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0];
                            attempts.Add(packet.Attempt(new(tableBaseBucket + (unchecked((int)depth) >> depthShift), packet.Words(), payload)));
                            allocated += 40;
                            accepted = true;
                        }
                    }
                }
                if (!accepted)
                {
                    attempts.Add(packet.Attempt(null));
                }
                cursor += 20;
                context96 = Put(context96, 0x20, checked(mesh.Address + (uint)cursor));
            }
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Mode mesh leaves its selected source extent.");
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } };
        return new(memory, context96, new(attempts.ToImmutable(), allocated));
    }

    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
}
