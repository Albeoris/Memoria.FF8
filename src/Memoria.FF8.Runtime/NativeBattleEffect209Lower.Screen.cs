using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect209Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect209Lower
{
    private static NativeBattleEffect209Step AdvanceScreen(NativeBattleEffect209Frame frame, uint address, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        uint color = age < 8 ? resources.InitialColors[age] : resources.CycleColors[(age - 8) % 24];
        uint stage40 = 0;
        uint stage44 = 0;
        if (age < 100)
        {
            stage40 = frame.State.ReadDword(SavedColor40Global);
            stage44 = frame.State.ReadDword(SavedColor44Global);
            bool capture = false;
            if (age < 36)
            {
                frame = DrawTiling83B0(frame, color | 0x64000000, inputs);
                capture = true;
            }
            else if (age < 38)
            {
                frame = DrawTiling83B0(frame, color | 0x64000000, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect209Stage5713E0(4096, 0));
            }
            else if (age < 82)
            {
                if (age == 38)
                {
                    frame = InitializeGrid7F40(frame);
                }
                frame = DeformGrid80F0(frame, age - 38, 60, inputs);
                frame = DrawGrid8250(frame, inputs.ReadBuffer80(frame), color, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect209Stage5713E0(4096, 0));
            }
            else if (age < 98)
            {
                int cue = (age - 82) << 8;
                (frame, color) = Cue(frame, color | 0x02000000, 0, cue, inputs);
                (frame, stage40) = Cue(frame, stage40, frame.State.ReadDword(StageAfterColor40Global), null, inputs);
                (frame, stage44) = Cue(frame, stage44, frame.State.ReadDword(StageAfterColor44Global), null, inputs);
                frame = DeformGrid80F0(frame, age - 38, 60, inputs);
                frame = DrawGrid8250(frame, inputs.ReadBuffer80(frame), color, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect209Stage5713E0(unchecked(4096 - ((cue * 5 << 9) >> 12)), 0));
                capture = true;
            }
            else
            {
                stage40 = frame.State.ReadDword(StageAfterColor40Global);
                stage44 = frame.State.ReadDword(StageAfterColor44Global);
                frame = inputs.Execute(frame, new NativeBattleEffect209Stage5713E0(1536, 0));
                capture = true;
            }
            if (capture)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect209CaptureStage7D30());
            }
            frame = inputs.Execute(frame, new NativeBattleEffect209PublishColor(0xB8B7D8, stage40));
            frame = inputs.Execute(frame, new NativeBattleEffect209PublishNormalColor44(stage44));
        }
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        if (age == 0 || age == 100)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect209SetStageFlags2(age == 100));
        }
        short next = unchecked((short)(age + 1));
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, unchecked((ushort)next));
        return new(Write(frame, address, task), next > 100 ? 2 : 0);
    }

    public static NativeBattleEffect209Frame DrawTiling83B0(NativeBattleEffect209Frame frame, uint color, NativeBattleEffect209Inputs inputs)
    {
        for (int column = 0; column < 10; column++)
        {
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            for (int row = 0; row < 7; row++)
            {
                SkinWrites packet = new(20, row * 20);
                packet.Word(0, 0x04000000);
                packet.Word(4, color);
                packet.Word16(8, (ushort)(column * 32));
                packet.Word16(10, (ushort)(row * 32));
                packet.Byte(12, (byte)((column & 1) * 32));
                packet.Byte(13, (byte)(row * 32));
                packet.Word16(16, 32);
                packet.Word16(18, 32);
                attempts.Add(packet.Attempt(new(0x101A, packet.Words(), [])));
            }
            NativeBattleGfGeometryBatch tiles = NativeBattleCinematicCapturePresentation.Discrete(new(attempts.ToImmutable(), 140), frame.PresentationIdentity);
            frame = inputs.Execute(frame, new NativeBattleEffect209DrawBatch(tiles, CommonCursor, 1));
            SkinWrites mode = new(8, 0);
            mode.Word(0, 0x01000000);
            mode.Word(4, 0xE1000200 | unchecked((uint)((column / 2 + 313) & 0x9FF)));
            frame = inputs.Execute(frame, new NativeBattleEffect209DrawBatch(new([mode.Attempt(new(0x101A, mode.Words(), [0, 0, 0, 0]))], 8), CommonCursor));
        }
        return frame;
    }

    private static (NativeBattleEffect209Frame Frame, uint Color) Cue(NativeBattleEffect209Frame frame, uint color, uint farColor, int? cue, NativeBattleEffect209Inputs inputs)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect209 color interpolation requires its current primitive owner.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Effect209 color interpolation requires current far colors.")).PublishFarColor45DDA0((byte)farColor, (byte)(farColor >> 8), (byte)(farColor >> 16));
        NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect209 color interpolation requires its current arithmetic owner.");
        uint factor = cue is int value ? unchecked((uint)value) : registers.Ir0 ?? throw new NotSupportedException("Effect209 interpolation requires its retained IR0.");
        NativeBattleScriptedDpcsResult step = NativeBattleScriptedDpcs.Transform45F270(registers, color, factor, far, inputs.Colors);
        return (frame with { Memory = frame.Memory with { ScriptedColorRegisters = step.Registers, PrimitiveProcess = process with { ModelRendererRegisters = far } } }, step.PackedRgb);
    }
}
