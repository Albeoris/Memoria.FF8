using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicGrid28(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleActionMatrix? matrix = null;
        NativeBattleCinematicGrid28Services services = new((current, texture) => NativeBattleWaterTextureMetadata.SelectTPageB665C0(current, resources, texture), (current, texture) => new(current, NativeBattleWaterTextureMetadata.ReadClutB66640(current, resources, texture)), (current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }, current =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            uint address = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, SlotWord(current, 0x9C));
            (byte[] raw, bool[] known) = current.Core.ReadSnapshot(address, 32);
            byte[] rotation = current.Core.Read(address, 18);
            NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(current, address + 20)), unchecked((int)CoreDword(current, address + 24)), unchecked((int)CoreDword(current, address + 28)), 4096);
            NativeBattleVector global = new(unchecked((short)CoreWord(current, 0x27971E4)), unchecked((short)CoreWord(current, 0x27971E6)), unchecked((short)CoreWord(current, 0x27971E8)));
            NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(basis, global);
            int x = unchecked((short)SlotWord(current, 0x8C)) << 4;
            int y = unchecked((short)SlotWord(current, 0x8E)) << 4;
            int z = unchecked((short)SlotWord(current, 0x90)) << 4;
            current = current.WriteTemporaryDword(0xD4, unchecked((uint)x)).WriteTemporaryDword(0xD8, unchecked((uint)y)).WriteTemporaryDword(0xDC, unchecked((uint)z)).WriteTemporary(0xE0, raw);
            bool[] temporaryKnown = current.TemporaryFrame.Known.ToArray();
            known.CopyTo(temporaryKnown, 0xE0);
            current = current with { TemporaryFrame = current.TemporaryFrame with { Known = temporaryKnown.ToImmutableArray() } };
            NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(basis, x, y, z);
            for (int index = 0; index < 9; index++)
            {
                current = current.WriteTemporaryWord(0xE0 + index * 2, unchecked((ushort)scaled.Rotation[index]));
            }
            matrix = scaled with { X = translated.X, Y = translated.Y, Z = translated.Z };
            return current;
        }, (current, color) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic grid lighting requires its retained color registers.");
            NativeBattleScriptedMaterialStep prepared = NativeBattleScriptedMaterial.Prepare(current, registers, color);
            NativeEffectMemory = memory with { ScriptedLighting = prepared.Lighting, ScriptedColorRegisters = prepared.Registers };
            return prepared.Frame;
        }, (current, near, far, third) =>
        {
            int difference = far - near;
            if (difference == 0)
            {
                difference = 1;
            }
            int quotient = unchecked(far << 12) / difference;
            int coefficient = unchecked(-(unchecked(near * far) / difference) << 8);
            if (third != 0)
            {
                coefficient /= third;
            }
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory registers = memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic grid lighting requires its retained depth-cue controls.");
            NativeEffectMemory = memory with { ScriptedGraphics = registers with { Dqa = unchecked((short)coefficient), Dqb = unchecked(quotient << 12) } };
            return current;
        }, (current, color) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native cinematic grid lighting requires its canonical far-color owner.");
            NativeEffectMemory = memory with { PrimitiveProcess = process.PublishModelColor(color) };
            return current;
        }, (current, vertex, normal, material, screen, color, flags) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic grid projection requires its current depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic grid projection requires its current camera projection.");
            byte[] position = ReadNativeGridTemporary(current, vertex, 6);
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix ?? throw new InvalidOperationException("Native cinematic grid basis has not been prepared."), NativeBattleScriptedMeshPreparation.Vector(position), projection, controls.Dqa, controls.Dqb);
            current = StoreNativeGridDword(current, screen, projected.Projected.Screen);
            current = StoreNativeGridDword(current, flags, projected.Projected.Flags);
            byte[] rawNormal = ReadNativeGridTemporary(current, normal, 6);
            int normalOffset = checked((int)(normal - current.TemporaryFrame.Address));
            ImmutableArray<byte> packedNormal = current.TemporaryFrame.Bytes.AsSpan().Slice(normalOffset, 8).ToArray().ToImmutableArray();
            ImmutableArray<bool> normalKnown = current.TemporaryFrame.Known.AsSpan().Slice(normalOffset, 8).ToArray().ToImmutableArray();
            uint command = BinaryPrimitives.ReadUInt32LittleEndian(ReadNativeGridTemporary(current, material, 4));
            NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? throw new NotSupportedException("Native cinematic grid projection requires its current lighting controls.");
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic grid projection requires its retained normal-color registers.");
            registers = registers with { NormalInput = packedNormal, NormalInputKnown = normalKnown, Material = command };
            NativeBattleModelRendererRegisters distant = memory.PrimitiveProcess?.ModelRendererRegisters ?? throw new NotSupportedException("Native cinematic grid projection requires its current far-color controls.");
            registers = NativeBattleScriptedNormalDepthColor.Transform45EE80(lighting, NativeBattleScriptedMeshPreparation.Vector(rawNormal), command, unchecked((short)projected.Cue), distant, registers);
            NativeEffectMemory = memory with { ScriptedColorRegisters = registers };
            current = StoreNativeGridDword(current, color, registers.Rgb[2] ?? throw new NotSupportedException("Native cinematic grid projection did not publish its normal color."));
            return new(current, projected.Projected.Depth >> 2);
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        return (NativeBattleCinematicGrid28.Draw(frame, services), graphics);
    }

    private static byte[] ReadNativeGridTemporary(NativeBattleWaterExecutionFrame frame, uint address, int length)
        => frame.ReadTemporary(checked((int)(address - frame.TemporaryFrame.Address)), length);

    private static NativeBattleWaterExecutionFrame StoreNativeGridDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        if (address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address <= 0x17C)
        {
            return frame.WriteTemporaryDword(checked((int)(address - frame.TemporaryFrame.Address)), value);
        }
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        if (address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - 4)
        {
            return frame with { Core = frame.Core.Write(address, bytes) };
        }
        return frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), bytes) };
    }
}
