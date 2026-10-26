using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect187ColoredMesh108Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context108, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect187ColoredMesh108
{
    public static NativeBattleEffect187ColoredMesh108Step Draw64C9A0(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context108, NativeBattleSummonColors colors, byte depthShift = 3)
    {
        uint flags = Dword(context108, 0x1C);
        if (Dword(context108, 0) != mesh.Address || mesh.Address is not (0xE41788 or 0xE413E8 or 0xE3D3A8) || flags != 0xF3 || depthShift != 3)
        {
            throw new NotSupportedException("Effect187 colored mesh requires its selected native context.");
        }
        uint material = Dword(context108, 0x20);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect187 colored mesh requires its actual geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect187 colored mesh requires its actual arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect187 colored mesh requires its current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect187 colored mesh requires its actual primitive process.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Effect187 colored mesh requires its current far color owner.");
        byte[] rgb = context108.Read(8, 3);
        far = far with { FarRed = (uint)rgb[0] << 4, FarGreen = (uint)rgb[1] << 4, FarBlue = (uint)rgb[2] << 4 };
        uint farColor = (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
        context108 = Put(Put(context108, 4, mesh.Address + 8), 0x18, 0);
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < 8; group++)
        {
            if (cursor < 0 || cursor > mesh.Bytes.Length - 4)
            {
                throw new InvalidDataException("Effect187 colored mesh leaves its actual polygon owner.");
            }
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            cursor = checked(cursor + 4);
            context108 = Put(context108, 0x24, checked(mesh.Address + (uint)cursor));
            if (count < 0 || count != 0 && group != 3 || count > (mesh.Bytes.Length - cursor) / 24)
            {
                throw new NotSupportedException("Effect187 colored mesh requires its reached plain FT4 group.");
            }
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = mesh.Bytes.AsSpan(cursor, 24).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    Load(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint firstFlags = registers.Colors.Flags!.Value;
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                NativeBattleMeshPacketVariant variant = new(material, null, false, true)
                {
                    ObserveFlags = value => context108 = Put(context108, 0x34, value),
                    ObserveArea = _ =>
                    {
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        context108 = Put(context108, 0x28, registers.Geometry.Mac0!.Value);
                    }
                };
                NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, new Corners(ReadCorner), 3, new(flags, farColor, unchecked((int)Dword(context108, 12)), null, null, 0, 0, 17, depthShift), colors, allocated, variant: variant);
                if (attempt.Primitive is not null)
                {
                    context108 = Put(context108, 0x30, registers.Geometry.Otz!.Value);
                    uint command = (material | 0x2C000000) | 0x02000000;
                    registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, command, Dword(context108, 12), far, colors).Registers };
                    allocated = checked(allocated + attempt.PacketLength);
                }
                attempts.Add(attempt);
                cursor = checked(cursor + 24);
                context108 = Put(context108, 0x24, checked(mesh.Address + (uint)cursor));

                void Load(int corner, int slot)
                {
                    int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                    ReadOnlySpan<byte> packed = mesh.Bytes.AsSpan(offset, 8);
                    registers = NativeBattleGeometry.LoadVector(registers, slot, new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed[2..]), BinaryPrimitives.ReadInt16LittleEndian(packed[4..])), BinaryPrimitives.ReadInt16LittleEndian(packed[6..]));
                }

                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner == 3)
                    {
                        Load(3, 0);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect187 colored mesh requires its actual ZSF4."));
                    }
                    return projected[corner];
                }
            }
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } };
        return new(memory, context108, new(attempts.ToImmutable(), allocated));
    }

    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));

    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));

    private sealed class Corners(Func<int, NativeBattleModuleProjectedVertex> read) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        private readonly Dictionary<int, NativeBattleModuleProjectedVertex> values = [];
        public int Count => 4;
        public NativeBattleModuleProjectedVertex this[int index]
        {
            get
            {
                if (!values.TryGetValue(index, out NativeBattleModuleProjectedVertex value))
                {
                    value = read(index);
                    values.Add(index, value);
                }
                return value;
            }
        }
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
