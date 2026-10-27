using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;
using M = Memoria.FF8.Runtime.NativeBattleApocalypse221BreakupMemory;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeApocalypse221Breakup(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleActionMatrix? matrix = null;
        NativeBattleApocalypse221BreakupTriangleServices triangles = new(current =>
        {
            NativeBattleVector anchor = NativeBattleScriptedMeshPreparation.Vector(ReadSlot(current, 0x94, 6));
            NativeBattleCinematicScaledBasisResult prepared = NativeBattleCinematicScaledBasis.Prepare(current, resourceLength, anchor);
            matrix = prepared.Matrix;
            return prepared.Frame;
        }, Project, (current, material) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedMaterialStep step = NativeBattleScriptedMaterial.Prepare(current, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Apocalypse material requires its actual color registers."), material);
            NativeEffectMemory = memory with { ScriptedLighting = step.Lighting, ScriptedColorRegisters = step.Registers };
            return step.Frame;
        }, (current, face) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Apocalypse face shading requires its actual normal-color registers.");
            registers = NativeBattleCinematicRibbonInput.Capture(current, current.TemporaryFrame.Address + 0xE0, registers);
            uint material = BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(face, 4), 4));
            registers = NativeBattleScriptedNormalColor.Transform4601B0(memory.ScriptedLighting ?? throw new NotSupportedException("Native Apocalypse face shading requires its current lighting controls."), NativeBattleScriptedMeshPreparation.Vector(current.ReadTemporary(0xE0, 6)), material, registers);
            NativeEffectMemory = memory with { ScriptedColorRegisters = registers };
            return new(current, registers.Rgb[2] ?? throw new NotSupportedException("Native Apocalypse shading requires its actual RGB FIFO output."));
        }, (current, record) =>
        {
            M m = new(current);
            NativeBattleVector angles = new((short)(m.Byte(record + 0x18) << 4), (short)(m.Byte(record + 0x19) << 4), (short)(m.Byte(record + 0x1A) << 4));
            m.Word(m.Temporary + 0xB0, angles.X);
            m.Word(m.Temporary + 0xB2, angles.Y);
            m.Word(m.Temporary + 0xB4, angles.Z);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(angles);
            StoreRotation(m, 0xC0, local);
            m.Word(m.Temporary + 0xD2, 0);
            int scale = m.Word(record + 0x20);
            m.Dword(m.Temporary + 0xB0, (uint)scale);
            m.Dword(m.Temporary + 0xB4, (uint)scale);
            m.Dword(m.Temporary + 0xB8, (uint)scale);
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
            StoreRotation(m, 0xC0, local);
            int phase = m.Signed(record + 0x12);
            int distance = m.Signed(record + 0xC);
            int x = unchecked(NativeBattleCameraMath.Sine(phase) * distance) >> 12;
            int z = unchecked(NativeBattleCameraMath.Cosine(phase) * distance) >> 12;
            m.Dword(m.Temporary + 0xD4, unchecked((uint)x));
            m.Dword(m.Temporary + 0xD8, 0);
            m.Dword(m.Temporary + 0xDC, unchecked((uint)z));
            local = local with { X = x, Y = 0, Z = z };
            NativeBattleActionMatrix parent = ReadMatrix(m, 0x40);
            matrix = NativeBattleGfMatrices.Compose56C2F0(parent, local);
            StoreRotation(m, 0xC0, matrix);
            m.Dword(m.Temporary + 0xD4, unchecked((uint)matrix.X));
            m.Dword(m.Temporary + 0xD8, unchecked((uint)matrix.Y));
            m.Dword(m.Temporary + 0xDC, unchecked((uint)matrix.Z));
            return m.Frame;
        }, current =>
        {
            NativeBattleActionMatrix basis = matrix ?? throw new InvalidOperationException("Native Apocalypse triangle requires its prepared detached basis.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Apocalypse triangle requires its current camera projection.");
            ImmutableArray<uint>.Builder screens = ImmutableArray.CreateBuilder<uint>(3);
            ushort depth = 0;
            for (int index = 0; index < 3; index++)
            {
                NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(basis, NativeBattleScriptedMeshPreparation.Vector(current.ReadTemporary(0xC0 + index * 8, 6)), projection);
                screens.Add(projected.Screen);
                depth = projected.Depth;
            }
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Apocalypse triangle requires its retained input registers.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, current.TemporaryFrame.Address + 0xC0, registers) };
            return new(current, screens.MoveToImmutable(), depth);
        }, index => BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(unchecked(0x186D510u + (uint)(4 * index)), 4)), (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleApocalypse221Breakup50Services services = new(Resolve, (current, index) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, index).Selected, (current, source, destination, count) =>
        {
            M m = new(current);
            (byte[] raw, bool[] known) = current.Core.ReadSnapshot(0x2797968, 32);
            m.Frame = m.Frame.WriteTemporary(0xC0, raw);
            m.Frame.TemporaryFrame.SetKnownness(0xC0, known);
            NativeBattleActionMatrix seed = ReadMatrix(m, 0xC0);
            int sx = m.Signed(m.Slot + 0x8C) << 4;
            int sy = m.Signed(m.Slot + 0x8E) << 4;
            int sz = m.Signed(m.Slot + 0x90) << 4;
            m.Dword(m.Temporary + 0xE0, unchecked((uint)sx));
            m.Dword(m.Temporary + 0xE4, unchecked((uint)sy));
            m.Dword(m.Temporary + 0xE8, unchecked((uint)sz));
            NativeBattleActionMatrix initial = NativeBattlePoseMatrices.Scale56BEF0(seed, sx, sy, sz);
            StoreRotation(m, 0xC0, initial);
            initial = initial with { X = m.Signed(m.Slot + 0x94), Y = m.Signed(m.Slot + 0x96), Z = m.Signed(m.Slot + 0x98) };
            for (int index = 0; index < count; index++)
            {
                uint address = unchecked(source + (uint)(index * 8));
                NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(initial, NativeBattleScriptedMeshPreparation.Vector(m.Read(address, 6)));
                uint output = unchecked(destination + (uint)(index * 8));
                m.Word(output, transformed.X);
                m.Word(output + 2, transformed.Y);
                m.Word(output + 4, transformed.Z);
                m.Dword(m.Temporary + 0xFC, transformed.Flags);
                NativeBattleEffectMemory memory = RequireWaterMemory();
                NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Apocalypse world vertices require their actual input registers.");
                NativeEffectMemory = memory with { ScriptedColorRegisters = TransformRegisters(NativeBattleCinematicRibbonInput.Capture(m.Frame, address, registers), transformed) };
            }
            return m.Frame;
        }, (current, state, vertices) => NativeBattleApocalypse221BreakupTriangles.Draw(current, state, vertices, triangles));
        return (NativeBattleApocalypse221Breakup50.Draw(frame, services), graphics);

        uint Resolve(NativeBattleWaterExecutionFrame current, short selector)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int found = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, found));
        }

        NativeBattleApocalypse221BreakupProjection Project(NativeBattleWaterExecutionFrame current, uint source)
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native Apocalypse vertex projection requires its actual depth-cue controls.");
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FAA0(matrix ?? throw new InvalidOperationException("Native Apocalypse vertex projection requires its prepared basis."), NativeBattleScriptedMeshPreparation.Vector(current.Arena.Read(ArenaOffset(source, 6), 6)), NativeCamera?.Projection ?? throw new NotSupportedException("Native Apocalypse requires its current camera projection."), controls.Dqa, controls.Dqb);
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Apocalypse vertex projection requires its actual input registers.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, source, registers) };
            return new(current, projected.Screen, projected.Depth);
        }
    }

    private static NativeBattleActionMatrix ReadApocalypseBreakupMatrix(NativeBattleWaterExecutionFrame frame, int offset)
    {
        byte[] rotation = frame.ReadTemporary(offset, 18);
        return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)frame.ReadTemporaryDword(offset + 20)), unchecked((int)frame.ReadTemporaryDword(offset + 24)), unchecked((int)frame.ReadTemporaryDword(offset + 28)), 4096);
    }

    private static NativeBattleActionMatrix ReadMatrix(M memory, int offset) => ReadApocalypseBreakupMatrix(memory.Frame, offset);

    private static void StoreRotation(M memory, int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            memory.Word(memory.Temporary + (uint)(offset + index * 2), matrix.Rotation[index]);
        }
    }
}
