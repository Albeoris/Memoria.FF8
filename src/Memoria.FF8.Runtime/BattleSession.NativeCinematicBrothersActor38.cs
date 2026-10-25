using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicBrothersActor38(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsState graphics, bool scaledActor = false)
    {
        NativeBattleCinematicBrothersModelServices ringServices = new(_ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, address) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Brothers fan requires its canonical V0 owner.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, address, registers) };
            return current;
        }, (current, matrix, address) =>
        {
            NativeBattleScriptedGraphicsMemory controls = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native Brothers fan requires its retained depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Brothers fan requires its current projection controls.");
            int offset = checked((int)(address - current.TemporaryFrame.Address));
            NativeBattleVector point = NativeBattleScriptedMeshPreparation.Vector(current.ReadTemporary(offset, 6));
            NativeBattleModuleDepthCueVertex result = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, point, projection, controls.Dqa, controls.Dqb);
            return new(current, result.Projected.Screen, result.Cue, result.Projected.Flags);
        }, () => 0, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleCinematicBrothersActor38Services services = new((_, address) => graphics.Models[ActorIndex(address)], (current, address, actor) =>
        {
            graphics = graphics with { Models = graphics.Models.SetItem(ActorIndex(address), actor) };
            return current;
        }, Restore, (current, address, basis, position) =>
        {
            int index = ActorIndex(address);
            graphics = graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Flags0 = (ushort)(graphics.Models[index].Flags0 | 0x20) }) };
            NativeBattleCinematicBonePose pose = Restore(current, address);
            current = pose.Frame;
            graphics = graphics with { Models = graphics.Models.SetItem(index, NativeBattleBodyBoneMatrices.Compose5095B0(graphics.Models[index], basis)) };
            ImmutableArray<NativeBattleActionMatrix> bones = graphics.Models[index].CurrentBodyBoneMatrices!.Matrices;
            current = NativeBattleCinematicBrothersModel3.DrawPreparedRing(current, bones.Length, bones[0].X, bones[0].Z, (_, bone) => graphics.Models[index].CurrentBodyBoneMatrices!.Matrices[bone], (_, bone) => BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(0x12 + 48 * bone, 2)), position, -1, 0, 0, NativeBattleCameraMath.Cosine(512), _ => graphics.Models[index].Color44 ?? throw new NotSupportedException("Native Brothers actor fan requires its current packed physical model color."), ringServices, !scaledActor);
            return Restore(current, address).Frame;
        }, ringServices.PublishInput, CreateNativeActorAnchorServices());
        if (scaledActor)
        {
            NativeBattleCinematicScaledActor72Services scaledServices = new(services.ReadActor, services.WriteActor, services.RestorePose, services.DrawShadow, services.PublishInput, CreateNativeActorAnchorServices());
            return (NativeBattleCinematicScaledActor72.DrawAE44E0(frame, scaledServices), graphics);
        }
        return (NativeBattleCinematicBrothersActor38.DrawAF59D0(frame, services), graphics);

        int ActorIndex(uint address)
        {
            if (address < 0x1D972C0 || (address - 0x1D972C0) % 156 != 0 || (address - 0x1D972C0) / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Native Brothers actor positioning requires its actual physical model pointer.");
            }
            return checked((int)((address - 0x1D972C0) / 156));
        }

        NativeBattleCinematicBonePose Restore(NativeBattleWaterExecutionFrame current, uint address)
        {
            int index = ActorIndex(address);
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Brothers actor positioning requires its current physical rig source.");
            NativeBattleGraphicsModel actor = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[index], source);
            graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
            return new(current, source.Skeleton, NativeBattleBodyBoneMatrices.Require(actor, source), null);
        }
    }
}
