using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoaPhysicalRibbonDraw(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Batch);

/// <summary>56FE10's paired G4 ribbon and retained 248-byte caller scratch.</summary>
public static class NativeBattleRinoaPhysicalRibbon
{
    public static NativeBattleRinoaPhysicalRibbonDraw Draw(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, uint temporary248, ImmutableArray<NativeBattleVector> curve, NativeBattleActionMatrix cameraCurveMatrix, int i, int j, NativeBattleCameraProjection projection, NativeBattleModuleState modules)
    {
        if (curve.IsDefault || curve.Length != 64)
        {
            throw new InvalidDataException("Rinoa physical ribbon requires its complete current curve.");
        }
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(staging, temporary248, 248);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Rinoa physical ribbon requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Rinoa physical ribbon requires its current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Rinoa physical ribbon requires its current projection controls.");
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        Put(0, unchecked((uint)i));
        Put(4, unchecked((uint)j));
        Put(40, 0x3A309090);
        Put(44, 0x3A286060);
        Put(120, 0);
        Put(124, 0);
        Put(128, unchecked((uint)-4096));
        Put(168, unchecked((uint)-4096));
        Put(172, 0);
        Put(176, 0);
        Put(8, 100);
        for (int k = i; k >= Math.Max(i - 5, 0); k--)
        {
            Copy(96, 72, 24);
            Copy(60, 48, 12);
            Copy(40, 32, 8);
            Put(40, unchecked(U32(40) - 0x71414));
            Put(44, unchecked(U32(44) - 0x61010));
            if (k >= 64)
            {
                continue;
            }
            NativeBattleVector before = curve[k >= 1 ? k - 1 : 0];
            NativeBattleVector after = k == 63 ? ReadCurve(64) : curve[k >= 1 ? k + 1 : 2];
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(after.X - before.X, 0, after.Z - before.Z);
            Put(136, unchecked((uint)direction.X));
            Put(140, unchecked((uint)direction.Y));
            Put(144, unchecked((uint)direction.Z));
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
            Put(0, unchecked((uint)alignment.Angle));
            NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
            for (int word = 0; word < 9; word++)
            {
                Word(184 + word * 2, rotation.Rotation[word]);
            }
            (int axisX, int axisY, int axisZ) = NativeBattleGfMatrices.RotateLong56C600(rotation, -4096, 0, 0);
            Put(152, unchecked((uint)axisX));
            Put(156, unchecked((uint)axisY));
            Put(160, unchecked((uint)axisZ));
            NativeBattleVector center = curve[k];
            NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, modules.ReadPhysicalModuleBytes(checked(0x209D7C0u + (uint)k * 8), 8));
            temporary = NativeBattleEffectNodeContextMemory.Copy(row, 0, temporary, 96, 8);
            Word(96, center.X);
            Word(98, center.Y);
            Word(100, center.Z);
            Word(104, unchecked((short)(center.X + (unchecked(100 * axisX) >> 12))));
            Word(106, 0);
            Word(108, unchecked((short)(center.Z + (unchecked(100 * axisZ) >> 12))));
            Word(112, unchecked((short)(center.X + (unchecked(-100 * axisX) >> 12))));
            Word(114, 0);
            Word(116, unchecked((short)(center.Z + (unchecked(-100 * axisZ) >> 12))));
            (byte[] paddingBytes, bool[] paddingKnown) = temporary.ReadSnapshot(234, 2);
            short? padding = paddingKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(paddingBytes) : null;
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, cameraCurveMatrix, true, true, padding) };
            for (int slot = 0; slot < 3; slot++)
            {
                (byte[] bytes, bool[] known) = temporary.ReadSnapshot(96 + slot * 8, 8);
                NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
                short? fourth = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
                registers = NativeBattleGeometry.LoadVector(registers, slot, vector, fourth);
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            registers = NativeBattleGeometry.Clip45EE10(registers);
            for (int slot = 0; slot < 3; slot++)
            {
                Put(60 + slot * 4, registers.Geometry.Sxy[slot] ?? throw new NotSupportedException("Rinoa physical ribbon requires its reached projected coordinates."));
            }
            registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Rinoa physical ribbon requires its current ZSF3."));
            Put(24, registers.Geometry.Otz ?? throw new NotSupportedException("Rinoa physical ribbon requires its reached OTZ."));
            if (k == i || k == 63)
            {
                continue;
            }
            int bucket = unchecked(17 + (unchecked((int)U32(24)) >> 2));
            Emit([0x08000000, U32(44), U32(60), U32(40), U32(64), U32(36), U32(48), U32(32), U32(52)], bucket);
            Emit([0x08000000, U32(40), U32(68), U32(44), U32(60), U32(32), U32(56), U32(36), U32(48)], bucket);
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, NativeBattleTemporaryMemory.PublishView(staging, temporary248, temporary, 248), new(attempts.ToImmutable(), checked(allocated + 36)) { NativeUnlinkedAllocation = true });

        void Put(int offset, uint value) => temporary = temporary.Write(offset, BitConverter.GetBytes(value));
        void Word(int offset, short value) => temporary = temporary.Write(offset, BitConverter.GetBytes(value));
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(offset, 4));
        void Copy(int source, int destination, int length) => temporary = NativeBattleEffectNodeContextMemory.Copy(temporary, source, temporary, destination, length);
        NativeBattleVector ReadCurve(int index)
        {
            uint address = checked(0x209D7C0u + (uint)index * 8);
            byte[] bytes = modules.ReadPhysicalModuleBytes(address, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }
        void Emit(ImmutableArray<uint> words, int bucket)
        {
            byte[] bytes = new byte[36];
            for (int word = 0; word < words.Length; word++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), words[word]);
            }
            ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Rinoa physical ribbon requires all four current depth FIFO WORDs.")))).ToImmutableArray();
            attempts.Add(new(allocated, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 36).ToImmutableArray(), new(bucket, words, payload)));
            allocated = checked(allocated + 36);
        }
    }
}
