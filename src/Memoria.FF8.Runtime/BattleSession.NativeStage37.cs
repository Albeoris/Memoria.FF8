using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void InitializeNativeStage37(NativeBattleStageSource source)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native518A80 requires its initialized stage owner.");
        NativeBattleStageExtendedState extended = stage.Extended ?? throw new NotSupportedException("Native518A80 requires its retained process state.");
        if (source.Texture.Data.Length < 20 + 8192)
        {
            throw new InvalidDataException("Native518A80 requires its complete original stage CLUT.");
        }
        NativeBattleStage37State state = NativeBattleStage37.Initialize518A80(extended.Stage37) with { ClutSource = new(source.Texture.Identity + ":CLUT", source.Texture.Data.Slice(20, 8192)) };
        NativeStage = stage with { Extended = extended with { Stage37 = state } };
    }

    private void UpdateNativeStage37Target3(NativeBattleGraphicsState graphics)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native518A80 requires its initialized stage owner.");
        NativeBattleStageExtendedState extended = stage.Extended ?? throw new NotSupportedException("Native518A80 requires its retained process state.");
        for (int index = 3; index <= 6; index++)
        {
            NativeBattleGraphicsModel model = graphics.Models[index];
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            byte identity = model.ModelId4 ?? throw new NotSupportedException("Native518A80 requires the enabled actor identity byte.");
            if (identity != 0x8C)
            {
                continue;
            }
            BattleCombatant actor = GraphicsActor(graphics, index) ?? throw new NotSupportedException("Native518A80 requires the selected actor's current and maximum HP.");
            NativeStage = stage with { Extended = extended with { Stage37 = NativeBattleStage37.UpdateHp518A80(extended.Stage37, actor.CurrentHp, actor.MaximumHp) } };
            break;
        }
    }

    private NativeBattleGraphicsState DispatchNativeStage37(NativeBattleGraphicsState graphics, int command)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native518A80 requires its initialized stage owner.");
        NativeBattleStageExtendedState extended = stage.Extended ?? throw new NotSupportedException("Native518A80 requires its retained process state.");
        NativeStage = stage with { Extended = extended with { Stage37 = NativeBattleStage37.Dispatch518A80(extended.Stage37, command) }, RotationSpeed = command is 101 or 103 ? (short)8 : stage.RotationSpeed };
        return command == 101 ? SubmitNativeStage37Draw(graphics, new(false, 0, 32, 16, null)) : graphics;
    }

    private NativeBattleGraphicsState AdvanceNativeStage37(NativeBattleGraphicsState graphics, NativeBattleStageState stage, NativeBattleStageTask task)
    {
        NativeBattleStageExtendedState extended = stage.Extended ?? throw new NotSupportedException("Native518F50 requires its retained process state.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native518F50 requires its camera process.");
        short shake = camera.ScreenOffsetScratchY ?? (camera.ProjectionOffsetsCleared ? (short)0 : throw new NotSupportedException("Native518F50 requires its actual screen-offset scratch WORD."));
        NativeBattleStage37Step step = NativeBattleStage37.Advance518F50(extended.Stage37, task.Phase, stage.RotationSpeed, shake);
        NativeStage = stage with { Extended = extended with { Stage37 = step.State }, Task = task with { Phase = step.Phase }, RotationSpeed = step.RotationSpeed };
        if (step.CameraShake != shake)
        {
            NativeCamera = camera with { ScreenOffsetScratchY = step.CameraShake, ProjectionOffsetsCleared = false };
        }
        foreach (NativeBattleStage37Draw draw in step.Draws)
        {
            graphics = SubmitNativeStage37Draw(graphics, draw);
        }
        return task.Phase == 1 ? DrawNativeStandardStage(graphics) : graphics;
    }

    private NativeBattleGraphicsState SubmitNativeStage37Draw(NativeBattleGraphicsState graphics, NativeBattleStage37Draw draw)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native stage37 requires its effect process.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native stage37 requires its actual common packet arena.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native stage37 requires its framebuffer selector.");
        if (draw.Strips)
        {
            ImmutableArray<NativeBattleGeometryPrimitive>.Builder strips = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>(5);
            for (int index = 0; index < 5; index++)
            {
                uint[] words = [0x05000000, 0xE1000100u | unchecked((uint)(index + 5 * buffer)), 0x66808080, unchecked((uint)(index << 6)), 0, 0x00D80040];
                NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 24).ToArray();
                Array.Fill(mask, NativeBattleGfPacketByteState.Untouched, 18, 2);
                strips.Add(new(4, words.ToImmutableArray(), [0, 0, 0, 0]) { PhysicalWriteMask = mask.ToImmutableArray() });
            }
            return AppendNativePrimitivePackets(graphics, strips.MoveToImmutable());
        }
        if (draw.Rgb is uint rgb)
        {
            process = process.PublishFarColor45DDA0(rgb & 255, (rgb >> 8) & 255, (rgb >> 16) & 255);
        }
        NativeBattleScriptedDpcsResult color = NativeBattleScriptedDpcs.Transform45F270(memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native56CA00 requires its retained GTE color registers."), 0, unchecked((uint)draw.Intensity), process.ModelRendererRegisters ?? throw new NotSupportedException("Native56CA00 requires its retained far-color controls."), definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native45F270 requires its authenticated interpolation constants."));
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native518D30 requires its retained texture upload owner.");
        textures = textures.EnqueueRectangle505DF0(new(0, 240, 256, 16), NativeStage!.Extended!.Stage37.ClutSource ?? throw new NotSupportedException("Native518D30 requires its original stage CLUT."));
        NativeEffectMemory = memory with { PrimitiveProcess = process, ScriptedColorRegisters = color.Registers, Textures = textures };
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native500830 requires its actual draw environments.");
        ImmutableArray<byte> opposite = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        short x = BinaryPrimitives.ReadInt16LittleEndian(opposite.AsSpan());
        short y = BinaryPrimitives.ReadInt16LittleEndian(opposite.AsSpan().Slice(2));
        short width = BinaryPrimitives.ReadInt16LittleEndian(opposite.AsSpan().Slice(4));
        short height = BinaryPrimitives.ReadInt16LittleEndian(opposite.AsSpan().Slice(6));
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>(6);
        Add(NativeBattleDrawEnvironmentPackets.Area45C940(new byte[12].ToImmutableArray(), x, y, width, height), 3, 12);
        Add(NativeBattleDrawEnvironmentPackets.Offset45C9B0(new byte[12].ToImmutableArray(), x, y), 3, 8);
        uint[] rectangle = [0x03000000, (color.PackedRgb & 0xFFFFFF) | 0x62000000, 1, unchecked((uint)(ushort)draw.Height << 16) | 255];
        AddWords(rectangle.ToImmutableArray(), 0, 16);
        Add(NativeBattleDrawEnvironmentPackets.Area45C940(new byte[12].ToImmutableArray(), 0, 240, 256, 16), 3, 12);
        Add(NativeBattleDrawEnvironmentPackets.Offset45C9B0(new byte[12].ToImmutableArray(), 0, 240), 3, 8);
        AddWords([0x01000000, 0xE1000600u | (unchecked((uint)draw.DrawMode) & 0x9FF)], 3, 8);
        return AppendNativePrimitivePackets(graphics, packets.MoveToImmutable());

        void Add(ImmutableArray<byte> bytes, int firstWritten, int endWritten)
        {
            AddWords(Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(index * 4))).ToImmutableArray(), firstWritten, endWritten);
        }

        void AddWords(ImmutableArray<uint> words, int firstWritten, int endWritten)
        {
            NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, words.Length * 4).ToArray();
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, firstWritten, endWritten - firstWritten);
            packets.Add(new(4385, words, [0, 0, 0, 0]) { PhysicalWriteMask = mask.ToImmutableArray() });
        }
    }
}
