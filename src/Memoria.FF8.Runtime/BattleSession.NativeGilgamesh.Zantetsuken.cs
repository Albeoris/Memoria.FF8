namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGilgameshFrame ReadNativeZantetsukenAnchor(NativeBattleGraphicsState graphics, NativeBattleGilgameshFrame frame, uint taskAddress, byte actor)
    {
        if (actor >= graphics.Models.Length)
        {
            throw new InvalidDataException("Zantetsuken anchor leaves its actual physical models.");
        }
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Zantetsuken requires the retained model angle WORD.");
        NativeBattlePoseSource? source = (model.Flags0 & 2) != 0 ? NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Zantetsuken requires the current body's actual skeleton.") : null;
        NativeBattleVector first = ReadBone(0xF0);
        NativeBattleVector second = ReadBone(0xF1);
        NativeBattleModuleAnchor anchor = NativeBattleGilgameshZantetsukenAnchors.Average58E260(first, second, transform.Angle36);
        NativeBattleModulePoolTask task = NativeBattleZantetsukenTasks.Read(frame, taskAddress);
        return NativeBattleZantetsukenTasks.Write(frame, taskAddress, task.WriteWord(0x10, unchecked((ushort)anchor.X)).WriteWord(0x12, unchecked((ushort)anchor.Y)).WriteWord(0x14, unchecked((ushort)anchor.Z)).WriteWord(0x16, unchecked((ushort)anchor.W)));

        NativeBattleVector ReadBone(byte selector)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattleGilgameshZantetsukenAnchors.Resolve58E2E0(model, source, selector, 0);
            NativeBattleGilgameshZantetsukenAnchorStep step = NativeBattleGilgameshZantetsukenAnchors.Project58E2E0(frame.Memory, NativeFileStaging ?? throw new NotSupportedException("Zantetsuken anchor requires its retained physical temporary memory."), producer, null);
            frame = frame with { Memory = step.Memory, Staging = step.Staging };
            NativeFileStaging = step.Staging;
            return step.Position;
        }
    }
}
