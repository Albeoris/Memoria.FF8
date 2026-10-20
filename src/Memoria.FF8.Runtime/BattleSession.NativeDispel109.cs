using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDispel109(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Dispel requires its selected effect.");
        NativeBattleDispel109Resources resources = effect.Binding.Dispel109Resources ?? throw new NotSupportedException("Native Dispel requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Dispel requires its retained process memory.");
        if (((effect.Parameters ?? throw new NotSupportedException("Native Dispel requires its actual action parameters.")).Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = (memory.Textures ?? throw new NotSupportedException("Native Dispel requires its retained texture queue.")).Register5099A0(resources.Camera);
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Dispel requires its current camera.")).RegisterControlResource(textures.Animation);
            NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Dispel requires its actual allocating TIM initializer.");
            (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
            memory = memory with { Textures = textures };
        }
        NativeBattleDispel109Frame frame = NativeBattleDispel109Controller.Initialize6CF4D0(new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Native Dispel requires its CRT stream.")));
        NativeEffectMemory = frame.Memory;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeDispel109(NativeBattleGraphicsState graphics, Func<(NativeBattleStackBytes Bytes, int Offset)> readScaledOperand, Action<NativeBattleStackBytes> publishScaledOperand)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Dispel requires its selected effect.");
        NativeBattleDispel109Resources resources = effect.Binding.Dispel109Resources ?? throw new NotSupportedException("Native Dispel requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Dispel requires its actual camera projection.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Dispel requires its original color conversion operands.");
        NativeBattleDispel109Inputs inputs = new(nativeActionPackets, Anchor, actor => unchecked((ushort)(graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Dispel requires its target WORD38.")).Scale38), Execute, DrawModel, DrawSprite)
        {
            ObserveTargetInitialize6CF687 = () =>
            {
                (NativeBattleStackBytes bytes, int offset) = readScaledOperand();
                publishScaledOperand(bytes.StoreUInt32(offset, 0x6CF68C, 0x6CF687, "Dispel target workspace initializer"));
            }
        };
        NativeBattleDispel109Frame frame = NativeBattleDispel109Controller.Advance6CF540(new(NativeEffectMemory ?? throw new NotSupportedException("Native Dispel requires its retained process memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Dispel requires its CRT stream.")), resources, inputs);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleModuleAnchor Anchor(byte actor)
        {
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return anchor with { W = (graphics.Models[actor].Transform ?? throw new NotSupportedException("Native571400 requires the actual actor WORD36.")).Angle36 };
        }

        NativeBattleDispel109Frame Execute(NativeBattleDispel109Frame current, NativeBattleDispel109Operation operation)
        {
            switch (operation.Kind)
            {
                case NativeBattleDispel109OperationKind.Flash:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Dispel requires its current stage flash owner.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)operation.Value), ColorFlags = 0 }).ToImmutableArray() };
                    break;
                case NativeBattleDispel109OperationKind.Preset:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 1, unchecked((short)operation.Value)) };
                    break;
                case NativeBattleDispel109OperationKind.Sound:
                    PublishNativeModuleSound(new(resources.Sound, unchecked((uint)operation.Value), operation.Anchor ?? throw new InvalidDataException("Native Dispel sound lost its freshly read target anchor.")));
                    break;
                case NativeBattleDispel109OperationKind.Contact:
                    int headerIndex = checked(current.Effect.Parameters!.HeaderIndex7C + operation.Value);
                    int contact = nativeActionPackets.Headers[headerIndex].FirstContactIndex ?? throw new InvalidDataException("Native Dispel requires its actual group contact pointer.");
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    current = current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Dispel contact lost its retained process memory.") };
                    break;
                default:
                    throw new NotSupportedException("Native Dispel selected an unsupported side effect.");
            }
            return current;
        }

        NativeBattleDispel109Frame DrawModel(NativeBattleDispel109Frame current, NativeBattleModulePoolTask task, NativeBattleGfAnimationNode node)
        {
            if (!NativeBattleDispel109Geometry.Visible(node))
            {
                return current;
            }
            (NativeBattleStackBytes bytes, int offset) = readScaledOperand();
            bool scaled = bytes.IsNonzero(offset);
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Native Dispel requires its retained temporary owner."), 88);
            NativeBattleDispel109Draw drawn = NativeBattleDispel109Geometry.Draw6CF9A0(current, task, node, scaled, resources, projection, colors, temporary.Staging, temporary.Address);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging ?? throw new InvalidDataException("Native Dispel drawing lost its physical temporary bytes."), 88);
            current = drawn.Frame;
            if (node.Scalar36 != 0)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Dispel requires its retained renderer registers.");
                NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
                current = current with { Memory = current.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(node.Color32) } } };
            }
            return Append(current, drawn.Packets);
        }

        NativeBattleDispel109Frame DrawSprite(NativeBattleDispel109Frame current, NativeBattleModulePoolTask task)
        {
            uint address = task.ReadDword(24);
            NativeBattleModuleResource resource = address == resources.Spark.Address ? resources.Spark : address == resources.Swirl.Address ? resources.Swirl : throw new NotSupportedException("Native Dispel requires the actual selected sprite bank.");
            ushort phase = task.ReadWord(12);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
            {
                Resource = resource,
                SpriteFrame = resource.ProjectedFrame571C80(phase),
                Phase = phase,
                Anchor = new(unchecked((short)task.ReadWord(16)), unchecked((short)task.ReadWord(18)), unchecked((short)task.ReadWord(20)), 0),
                Scale = unchecked((short)task.ReadWord(22)),
                DepthOffset = -2048,
                SpriteFlags = 0
            };
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Native Dispel requires its retained temporary owner."), 180);
            NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, resource.Address, 0).Write(temporary.Address + 4, BitConverter.GetBytes(phase));
            NativeBattleModuleSpriteTemporaryResult drawn = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary.Address, draw, projection, NativeCamera!.Roll, 17, 2);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 180);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Dispel lost its current private packet cursor."), graphics.PrimitiveFrame!.Pending, drawn.Batch, NativeBattleEffectGouraudWrites.FullDword);
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }

        NativeBattleDispel109Frame Append(NativeBattleDispel109Frame current, NativeBattleGfGeometryBatch packets)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Dispel requires its actual ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Dispel lost its current private packet cursor."), primitive.Pending, packets);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }
}
