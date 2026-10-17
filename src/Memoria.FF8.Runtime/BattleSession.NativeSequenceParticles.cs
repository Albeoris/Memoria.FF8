namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, int Offset) ScheduleNativeSequenceParticle(NativeBattleGraphicsState graphics, int currentIndex, NativeBattleAuxiliaryProgram program, bool contact)
    {
        int target = currentIndex;
        bool suppressed = false;
        if (contact)
        {
            NativeBattleActionGraphicsState action = graphics.ActionGraphics ?? throw new NotSupportedException("Native B4 requires its50BF90 active contact owner.");
            NativeBattleContactHalf half = nativeActionPackets.ReadActiveContact().Primary;
            graphics = ApplyNativeSequenceCover5065B0(graphics, half);
            suppressed = (half.HitFlags3 & 4) != 0;
            target = action.FirstTarget ?? throw new NotSupportedException("Native B4 requires its retained first target BYTE1D99A48.");
        }
        NativeBattleCameraPose camera = NativeCamera?.CurrentPose ?? throw new NotSupportedException("Native505900 requires its actual camera eye and target WORDs.");
        int angle = NativeWorldScriptedPathMath.NativeAngle(camera.Eye.X - camera.Target.X, camera.Eye.Z - camera.Target.Z);
        NativeBattleSequenceParticle particle = NativeBattleSequenceParticles.Read505900(program, angle, () =>
        {
            int count = (NativeGraphicsPoseResource(graphics, target).PoseSource ?? throw new NotSupportedException("Native505900 requires its target skeleton bone count.")).BoneCount;
            if (count <= 1)
            {
                throw new InvalidDataException("Native505900 random bone requires at least two skeleton entries.");
            }
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native505900 requires its retained previous-bone owner.");
            byte previous = memory.SequenceParticlePreviousBone ?? throw new NotSupportedException("Native505900 requires its retained previous-bone BYTE1D98208; migrate the standalone snapshot explicitly.");
            byte selected = checked((byte)((NextNativeGlobalRandom() % (count - 1) + previous + 1) % count));
            NativeEffectMemory = memory with { SequenceParticlePreviousBone = selected };
            return selected;
        }, suppressed);
        if (!suppressed)
        {
            NativeBattleModuleResource resource = RequireNativeModuleResource((byte)particle.PackedParameters);
            NativeBattleModuleState modules = particle.Placement switch
            {
                1 => graphics.ModuleEffects.AppendModelResource(target, resource, particle.PackedParameters),
                3 => graphics.ModuleEffects.AppendBoneParticle(target, particle.Bone, particle.Fraction, resource, particle.PackedParameters),
                4 => graphics.ModuleEffects.AppendPositionResource(target, particle.Position ?? throw new InvalidDataException("Native505900 placement4 lacks its local position."), resource, particle.PackedParameters),
                _ => throw new InvalidDataException("Native505900 produced an invalid placement.")
            };
            graphics = graphics with { ModuleEffects = modules };
        }
        return (graphics, particle.NextOffset);
    }

    private NativeBattleGraphicsState ApplyNativeNextSequenceContact(NativeBattleGraphicsState graphics)
    {
        NativeBattleContactPacket previous = nativeActionPackets.ReadActiveContact();
        nativeActionPackets = nativeActionPackets.AdvanceContact50A690();
        var pending = graphics.PendingContacts;
        graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [previous] });
        NativeBattleActionGraphicsState context = graphics.ActionGraphics ?? throw new NotSupportedException("Native50A690 requires its retained action globals.");
        return graphics with { PendingContacts = pending, ActionGraphics = context with { FirstTarget = nativeActionPackets.ReadActiveContact().Primary.ActorIndex } };
    }

    private NativeBattleGraphicsState ApplyNativeSequenceCover5065B0(NativeBattleGraphicsState graphics, NativeBattleContactHalf half)
    {
        if ((graphics.Flags & 0x10) != 0 || (half.HitFlags3 & 8) == 0)
        {
            return graphics;
        }
        int target = half.ActorIndex;
        int sourceIndex = half.HitFlags3 >> 6;
        NativeBattleGraphicsModel source = graphics.Models[sourceIndex];
        NativeBattleGraphicsModel recipient = graphics.Models[target];
        NativeBattleVector position = source.Transform?.Initial20 ?? throw new NotSupportedException("Native Cover source requires its initial position WORD20/24.");
        NativeBattleModelTransform transform = recipient.Transform ?? throw new NotSupportedException("Native Cover recipient requires its current position WORD28/32.");
        recipient = recipient with { Transform = transform with { Position28 = transform.Position28 with { X = position.X, Z = position.Z } } };
        graphics = graphics with { Models = graphics.Models.SetItem(target, recipient) };
        graphics = SelectNativeHeaderSequence(graphics, checked((byte)target), 22);
        source = graphics.Models[sourceIndex];
        return graphics with { Models = graphics.Models.SetItem(sourceIndex, NativeBattleGraphicsModels.StartService(source, 9)) };
    }
}
