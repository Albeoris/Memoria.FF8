using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceFlash64B130(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleCameraPose camera = inputs.ReadCameraPose(frame);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(camera.Target.X - camera.Eye.X, camera.Target.Y - camera.Eye.Y, camera.Target.Z - camera.Eye.Z);
        NativeBattleVector point = new(unchecked((short)(camera.Target.X + (unchecked(direction.X * 200) >> 12))), unchecked((short)(camera.Target.Y + (unchecked(direction.Y * 200) >> 12))), unchecked((short)(camera.Target.Z + (unchecked(direction.Z * 200) >> 12))));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, point, inputs.ReadTargetPadding(frame));
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect187 flash requires its actual projection controls.");
        registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
        frame = Publish(frame, registers);
        int depth = unchecked((int)(registers.Geometry.Otz ?? throw new NotSupportedException("Effect187 flash requires its retained ordering depth.")));
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        int timing = Signed(task, 14);
        int intensity = Signed(task, 0x1C);
        int rise = timing & 15;
        int hold = (timing >> 4) & 15;
        int fall = (timing >> 8) & 15;
        int lane;
        if (age < rise)
        {
            lane = unchecked(intensity / rise * age);
        }
        else if (age - rise < hold)
        {
            lane = intensity;
        }
        else if (age - rise - hold < fall)
        {
            lane = unchecked(intensity - intensity / fall * (age - rise - hold));
        }
        else
        {
            lane = -1;
        }
        if (lane >= 0)
        {
            uint rgb = unchecked((uint)(byte)lane);
            uint color = rgb | (rgb << 8) | (rgb << 16) | 0x62000000;
            ImmutableArray<uint> words = [0x03000000, color, 0, 0x00D80140];
            SkinWrites packet = new(16, 0);
            for (int index = 0; index < words.Length; index++)
            {
                packet.Word(index * 4, words[index]);
            }
            NativeBattleGeometryPrimitive primitive = new(unchecked(17 + depth), words, [0, 0, 0, 0]);
            if (inputs.CapturePresentation)
            {
                NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(187, task, unchecked((int)(words[1] >> 24))) with { ResourceGeneration = task.Callback };
                System.Numerics.Vector3 origin = new(unchecked((short)words[2]), unchecked((short)(words[2] >> 16)), 0);
                int width = unchecked((ushort)words[3]);
                int height = unchecked((ushort)(words[3] >> 16));
                primitive = primitive with { PilotGeometry = new(identity, origin, origin + new System.Numerics.Vector3(width, 0, 0), origin + new System.Numerics.Vector3(0, height, 0), origin + new System.Numerics.Vector3(width, height, 0), 4, false) { ScreenSpace = true, ScreenScale = 8, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } };
            }
            frame = inputs.Execute(frame, new NativeBattleEffect187DeliverBatch(new([packet.Attempt(primitive)], 16), 0x1D8E054, 1));
        }
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return new(frame, lane < 0 ? 2 : 0);
    }
}
