using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicStageShatter49(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicStageShatter49Services services = new((current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int index = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, index));
        }, (_, group) => NativeBattleCinematicStageShatter49.RestoreStageGroup508C90(group), current =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            uint address = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, 0);
            byte[] bytes = current.Core.Read(address, 32);
            NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20)), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28)), 4096);
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native stage shatter requires its current camera owner.");
            NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native stage shatter requires its current projection registers.");
            short focal = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(18));
            if (focal == 0)
            {
                focal = camera.FocalLength ?? throw new NotSupportedException("Native stage shatter requires its current global focal WORD.");
            }
            projection = projection with { FocalLength = unchecked((ushort)focal) };
            NativeCamera = camera with { Projection = projection };
            return new(current, address, matrix, projection);
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleCinematicStageShatter49Result result = NativeBattleCinematicStageShatter49.Draw(frame, NativeStage ?? throw new NotSupportedException("Native stage shatter requires its retained stage descriptors."), RequireNativeStageGeometry(), services);
        NativeStage = result.Stage;
        if (result.BackColor is uint color)
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? throw new NotSupportedException("Native stage shatter requires its current lighting registers.");
            NativeEffectMemory = memory with { ScriptedLighting = lighting with { BackColor = new((byte)color * 16, (byte)(color >> 8) * 16, (byte)(color >> 16) * 16) } };
        }
        return (result.Frame, graphics);
    }
}
