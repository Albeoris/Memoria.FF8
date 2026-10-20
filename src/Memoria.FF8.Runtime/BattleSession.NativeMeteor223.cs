using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleWaterExecutionFrame ExecuteNativeMeteor223Texture(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicResources resources)
    {
        uint magic = RequireWaterMemory().MagicTextureBufferAddress1D99A88 ?? throw new NotSupportedException("Native Meteor texture commands require the actual loaded magic buffer pointer.");
        return NativeBattleMeteor223Commands.Execute(frame, program, resources, new(magic, (current, rectangle, address, readback) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Meteor texture commands require their current texture queue.");
            textures = readback ? textures.EnqueueArenaReadback505E70(rectangle, ArenaOffset(address, 0)) : textures.EnqueueArenaRectangle505DF0(rectangle, current.Arena, ArenaOffset(address, 0));
            NativeEffectMemory = memory with { Textures = textures };
            return current;
        }));
    }

    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeMeteor223Grid(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleActionMatrix? basis = null;
        NativeBattleMeteor223GridServices services = new(Resolve, (current, selector) =>
        {
            uint reference = Resolve(current, selector);
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            uint matrix = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, SlotWord(current, 0x9C));
            byte[] rotation = current.Core.Read(matrix, 18);
            NativeBattleActionMatrix parent = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(2 * index))).ToImmutableArray(), unchecked((int)CoreDword(current, matrix + 20)), unchecked((int)CoreDword(current, matrix + 24)), unchecked((int)CoreDword(current, matrix + 28)), 4096);
            byte[] position = current.Arena.Read(ArenaOffset(reference + 0x94, 6), 6);
            NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(parent, NativeBattleScriptedMeshPreparation.Vector(position));
            basis = parent with { X = translated.X, Y = translated.Y, Z = translated.Z };
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Meteor basis requires its actual packed input registers.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = TransformRegisters(NativeBattleCinematicRibbonInput.Capture(current, reference + 0x94, registers), translated) };
            return current;
        }, (current, texture) =>
        {
            NativeBattleCinematicTextureSelection selection = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(current, resources, texture);
            int width = selection.Rectangle.Width;
            if ((selection.SourceMapByte & 0x80) != 0)
            {
                width = unchecked(width * 2);
            }
            uint window = 0xE2000000u | (uint)((selection.Rectangle.Y & 0xF8) << 12) | (uint)((selection.Rectangle.X & 0x38) << 7) | (uint)((-width >> 3) & 31) | (uint)((-selection.Rectangle.Height * 4) & 0x3E0);
            return current.WriteTemporaryDword(0xF0, selection.RectangleAddress).WriteTemporaryDword(0xF4, selection.TexturePage!.Value).WriteTemporaryDword(0xF8, window).WriteTemporaryDword(0xFC, selection.SourceAddress);
        }, current =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native Meteor projection requires its actual depth-cue controls.");
            byte[] position = current.ReadTemporary(0xC0, 6);
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(basis ?? throw new InvalidOperationException("Native Meteor projection requires its selected parent basis."), NativeBattleScriptedMeshPreparation.Vector(position), NativeCamera?.Projection ?? throw new NotSupportedException("Native Meteor requires its current camera projection."), controls.Dqa, controls.Dqb);
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Meteor projection requires its packed input registers.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = registers with { NormalInput = [.. position, 0, 0], NormalInputKnown = null } };
            short x = unchecked((short)projected.Projected.Screen);
            short y = unchecked((short)(projected.Projected.Screen >> 16));
            uint screen = unchecked((ushort)(x >> 3) | ((uint)(ushort)(y >> 3) << 16));
            return new(current, screen, projected.Projected.Depth >> 2, projected.Projected.Flags);
        }, (current, output, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, null, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        }, (address, length) => resources.ReadStatic(address, length).ToArray());
        return (NativeBattleMeteor223Grid.Draw(frame, services), graphics);

        uint Resolve(NativeBattleWaterExecutionFrame current, short selector)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), unchecked((ushort)selector));
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }
    }
}
