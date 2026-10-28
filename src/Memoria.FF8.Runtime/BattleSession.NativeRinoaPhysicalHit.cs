using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleModuleCallback Callback) AdvanceNativeRinoaPhysicalHit(NativeBattleGraphicsState graphics, NativeBattleModuleState modules, int slot, NativeBattleModuleInputs inputs)
    {
        inputs = inputs with { ReadModelAnchor = index => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native571400 requires its angleWORD36.")).Angle36 } };
        NativeBattleRinoaPhysicalHitInputs services = new(inputs, Aliases, actor => graphics.Models[actor].Flags0, Sound, () => NativeBattleActionGraphics.ReadCameraFlag50A6E0(nativeActionPackets) != 0, () => graphics = ApplyNativeNextSequenceContact(graphics), Sprite);
        NativeBattleModuleCallback result;
        uint projectileTemporary = 0;
        switch (modules.Slots[slot].Callback)
        {
            case 0x56F9C0:
                result = NativeBattleRinoaPhysicalHit.AdvanceRoot(modules, slot, services);
                break;
            case 0x56FCD0:
                result = NativeBattleRinoaPhysicalHit.AdvanceSpark(modules, slot, services);
                break;
            case 0x56FE10:
                NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(Staging(), 248);
                NativeFileStaging = temporary.Staging;
                projectileTemporary = temporary.Address;
                result = NativeBattleRinoaPhysicalHit.AdvanceProjectile(modules, slot, services, Camera().Projection!.View!, DrawProjectile, PublishCurveWork, PublishMatrices);
                NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(Staging(), 248);
                break;
            default:
                throw new NotSupportedException("Native Rinoa physical hit requires its actual shared callback.");
        }
        return (graphics, result);

        NativeBattleFileStaging Staging() => NativeFileStaging ?? throw new NotSupportedException("Native Rinoa physical hit requires its retained temporary memory.");
        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Native Rinoa physical hit requires its current camera.");
        NativeBattleEffectMemory Memory() => NativeEffectMemory ?? throw new NotSupportedException("Native Rinoa physical hit requires its retained process memory.");
        (byte Actor, byte Target) Aliases()
        {
            NativeBattleCameraAliases aliases = Memory().CameraAliases ?? throw new NotSupportedException("Native56F9C0 requires its actual actor and target aliases.");
            return (aliases.Actor1D99B95 ?? throw new NotSupportedException("Native56F9C0 requires its caster alias."), aliases.Target1D99AAA ?? throw new NotSupportedException("Native56F9C0 requires its target alias."));
        }
        void Sound(byte actor, int number, NativeBattleModuleAnchor anchor)
        {
            byte model = graphics.Models[actor].ModelId4 ?? throw new NotSupportedException("Native501640 requires its actual model identity.");
            ImmutableArray<uint> sounds = definition.NativeGraphics?.Rules.ActorSounds1120 ?? throw new NotSupportedException("Native501640 requires its actor sound table.");
            PublishNativeModuleSound(sounds[checked(model * 7 + number)], 0, anchor, graphics.ActorIds[actor]);
        }
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Rinoa physical hit requires its current ordering table.");
            NativeBattleEffectMemory memory = Memory();
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(memory.PrimitiveProcess ?? throw new NotSupportedException("Native Rinoa physical hit requires its COMMON packet owner."), primitive.Pending, batch);
            NativeEffectMemory = memory with { PrimitiveProcess = written.Process };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        }
        void Sprite(NativeBattleModuleDraw draw)
        {
            NativeBattleCameraState camera = Camera();
            NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native56FCD0 requires its current projection.");
            NativeBattleModuleAnchor point = draw.Anchor!;
            NativeEffectMemory = NativeBattleModuleSpriteSetup.Setup571BC0(Memory(), camera.CapturePackedView1D97778(), point, checked((short)draw.Scale), checked((short)draw.DepthOffset));
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(Staging(), 180);
            NativeBattleEffectArena context = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 180).Write(0, BitConverter.GetBytes(draw.Resource!.Address)).Write(4, BitConverter.GetBytes(draw.Phase)).Write(32, [4]).Write(36, BitConverter.GetBytes(draw.SpriteFlags));
            if ((draw.SpriteFlags & 4) != 0)
            {
                context = context.Write(28, BitConverter.GetBytes(draw.SpriteColor).AsSpan(0, 3));
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(Memory(), context, address => definition.NativeModuleResources.Single(resource => resource.Address == address), projection, camera.Roll, 17, 2, allowFirstPaletteOverride: true);
            NativeEffectMemory = drawn.Memory;
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary180, 180), 180);
            Append(drawn.Batch);
        }
        void PublishCurveWork(NativeBattleEffectArena work)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(Staging(), 400);
            NativeBattleFileStaging staging = allocation.Staging;
            (byte[] bytes, bool[] known) = work.ReadSnapshot(0, 400);
            for (int index = 0; index < known.Length; index++)
            {
                if (known[index])
                {
                    staging = staging.Write(checked(allocation.Address + (uint)index), bytes.AsSpan(index, 1));
                }
            }
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 400);
        }
        void PublishMatrices(NativeBattleActionMatrix basis, NativeBattleActionMatrix cameraCurve)
        {
            WriteMatrix(184, basis);
            WriteMatrix(216, cameraCurve);
        }
        void WriteMatrix(uint offset, NativeBattleActionMatrix matrix)
        {
            NativeBattleFileStaging staging = Staging();
            for (int lane = 0; lane < 9; lane++)
            {
                staging = staging.Write(projectileTemporary + offset + (uint)lane * 2, BitConverter.GetBytes(matrix.Rotation[lane]));
            }
            NativeFileStaging = staging.Write(projectileTemporary + offset + 20, BitConverter.GetBytes(matrix.X)).Write(projectileTemporary + offset + 24, BitConverter.GetBytes(matrix.Y)).Write(projectileTemporary + offset + 28, BitConverter.GetBytes(matrix.Z));
        }
        NativeBattleModuleState DrawProjectile(NativeBattleModuleState current, NativeBattleActionMatrix cameraCurve, int first, int next)
        {
            NativeBattleRinoaPhysicalHitState hit = current.RinoaPhysicalHit ?? throw new NotSupportedException("Native56FE10 requires its current physical curve.");
            uint temporaryAddress = projectileTemporary;
            NativeBattleGraphicsModel actor = graphics.Models[hit.Actor];
            if (next < 64)
            {
                NativeBattleVector start = hit.Curve[first];
                NativeBattleVector end = hit.Curve[next];
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(end.X - start.X, 0, end.Z - start.Z);
                NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
                NativeBattleActionMatrix local = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), start.X, 0, start.Z, 4096);
                NativeBattleActionMatrix body = NativeBattleGfMatrices.Compose56C2F0(cameraCurve, local);
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, hit.Actor).PoseSource ?? throw new NotSupportedException("Native5717D0 requires its actual caster rig.");
                short scale = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(2));
                WriteMatrix(184, body);
                body = NativeBattlePoseMatrices.Scale56BEF0(body, scale, scale, scale);
                NativeBattleBodyBoneMatrices bones = actor.CurrentBodyBoneMatrices ?? NativeBattleBodyBoneMatrices.ReadLoadedSkeleton(source);
                actor = actor with { Flags0 = (ushort)(actor.Flags0 | 0x200), CurrentBodyBoneMatrices = bones with { Matrices = Enumerable.Repeat(body, source.BoneCount).ToImmutableArray() } };
                graphics = graphics with { Models = graphics.Models.SetItem(hit.Actor, actor) };
                NativeBattleTemporaryAllocation context = NativeBattleTemporaryMemory.Allocate5082B0(Staging(), 44);
                uint scratch = checked(0x196B018u - (uint)(NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native56FE10 requires its projected CPU workspace.")) * 8);
                NativeFileStaging = context.Staging.Write(context.Address + 4, BitConverter.GetBytes(scratch)).Write(context.Address + 20, new byte[4]).Write(context.Address + 24, BitConverter.GetBytes((short)320)).Write(context.Address + 26, BitConverter.GetBytes((short)216)).Write(context.Address + 28, BitConverter.GetBytes(actor.Color40 ?? throw new NotSupportedException("Native56FE10 requires its caster color."))).Write(context.Address + 32, BitConverter.GetBytes(uint.MaxValue)).Write(context.Address + 36, new byte[2]);
                graphics = DrawNativeEffectModel5099D0(graphics, hit.Actor, body, Camera().Projection!, 17, 4, depthCue: 0, prepareModel: false, drawShadow: false, drawLinked: false, maskOverride: uint.MaxValue);
                NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(Staging(), 44);
            }
            else
            {
                ushort saved = BinaryPrimitives.ReadUInt16LittleEndian(current.Slots[slot].Payload.AsSpan().Slice(2));
                graphics = graphics with { Models = graphics.Models.SetItem(hit.Actor, actor with { Flags0 = (ushort)((actor.Flags0 & ~0x200) | saved) }) };
            }
            NativeBattleRinoaPhysicalRibbonDraw ribbon = NativeBattleRinoaPhysicalRibbon.Draw(Memory(), Staging(), temporaryAddress, hit.Curve, cameraCurve, first, next, Camera().Projection!, current);
            NativeEffectMemory = ribbon.Memory;
            NativeFileStaging = ribbon.Staging;
            Append(ribbon.Batch);
            return current;
        }
    }
}
