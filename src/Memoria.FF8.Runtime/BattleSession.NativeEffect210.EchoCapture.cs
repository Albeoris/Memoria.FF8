using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect210Frame CaptureNativeEffect210Echo(NativeBattleGraphicsState graphics, NativeBattleEffect210Frame frame, NativeBattleEffect210CaptureEcho8200 operation)
    {
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect210 echo capture requires its current addressed memory.");
        NativeBattleGraphicsModel model = graphics.Models[operation.Actor];
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, operation.Actor).PoseSource ?? throw new NotSupportedException("Effect210 echo capture requires its actual current primary skeleton.");
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        uint taskAddress = checked(operation.HeaderAddress - 0x60);
        NativeBattleModulePoolTask task = NativeBattleEffect210Controller.Read(frame, taskAddress);
        task = NativeBattleDuelEcho.Capture8200(task, 0x60, matrices.Length, bone =>
        {
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
            return (packed.Bytes.ToArray(), packed.Known.ToArray());
        });
        return NativeBattleEffect210Controller.Write(frame, taskAddress, task);
    }
}
