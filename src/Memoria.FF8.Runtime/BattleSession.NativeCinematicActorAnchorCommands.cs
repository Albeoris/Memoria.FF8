using System.Security.Cryptography;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) ExecuteNativeCinematicActorAnchorCommand(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleGraphicsState graphics, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> update)
    {
        NativeBattleCinematicActorAnchorServices services = new((current, address, restore, linked) =>
        {
            if (address < 0x1D972C0 || (address - 0x1D972C0) % 156 != 0 || (address - 0x1D972C0) / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Native actor anchor requires its actual physical model pointer.");
            }
            int index = checked((int)((address - 0x1D972C0) / 156));
            NativeBattleGraphicsModel actor = graphics.Models[index];
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
            NativeBattlePoseSource source = (linked ? resource.LinkedPoseSource : resource.PoseSource) ?? throw new NotSupportedException("Native actor anchor requires its selected actual rig source.");
            NativeBattleBodyBoneMatrices publication;
            if (linked)
            {
                if (restore)
                {
                    NativeBattleSequenceState sequence = actor.SequenceState ?? throw new NotSupportedException("Native linked anchor requires its current decoded pose owner.");
                    publication = new(Convert.ToHexString(SHA256.HashData(source.Skeleton.AsSpan())), NativeBattlePoseMatrices.StageBoneMatrices(source, NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, source)));
                    actor = actor with { CurrentLinkedBoneMatrices = publication };
                }
                else
                {
                    publication = actor.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Native linked anchor requires its actual current matrix publication.");
                }
            }
            else
            {
                if (restore)
                {
                    actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
                }
                publication = actor.CurrentBodyBoneMatrices ?? throw new NotSupportedException("Native actor anchor requires its actual current matrix publication.");
            }
            publication.Validate();
            if (!StringComparer.OrdinalIgnoreCase.Equals(publication.SkeletonSha256, Convert.ToHexString(SHA256.HashData(source.Skeleton.AsSpan()))) || publication.Matrices.Length != source.BoneCount)
            {
                throw new InvalidDataException("Native actor anchor matrix publication belongs to a different rig.");
            }
            graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
            return new NativeBattleCinematicBonePose(current, source.Skeleton, publication.Matrices, actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native actor anchor requires the caller's actual world matrix."));
        }, update, (current, address) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, address, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor anchor requires its retained packed input registers.")) };
            return current;
        }, CreateNativeActorAnchorServices())
        {
            ReadScale38 = pointer =>
            {
                if (pointer < 0x1D972C0 || (pointer - 0x1D972C0) % 156 != 0 || (pointer - 0x1D972C0) / 156 >= graphics.Models.Length)
                {
                    throw new NotSupportedException("Native radial anchor requires its actual physical actor pointer.");
                }
                return graphics.Models[checked((int)((pointer - 0x1D972C0) / 156))].Transform?.Scale38 ?? throw new NotSupportedException("Native radial anchor requires the actual actor scale WORD38.");
            }
        };
        return (NativeBattleCinematicActorAnchorCommands.Execute(frame, program, services), graphics);
    }
}
