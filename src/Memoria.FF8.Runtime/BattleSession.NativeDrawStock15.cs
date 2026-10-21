using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeDrawStock15(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Draw Stock requires its selected original effect.");
        NativeBattleDrawStock15Resources resources = effect.Binding.DrawStock15Resources ?? throw new NotSupportedException("Draw Stock requires its original module resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Draw Stock requires its retained process owners.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Draw Stock requires its retained CRT random owner.");
        NativeBattleDrawStock15Frame frame;
        if (initialize)
        {
            frame = NativeBattleDrawStock15Controller.Initialize(memory, effect, random, nativeActionPackets, resources);
            NativeEffectMemory = frame.Memory;
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Draw Stock requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect with { DrawStock15 = frame.State } };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Draw Stock requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Draw Stock requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Draw Stock requires its current view matrix.");
        frame = new(memory, effect, random, effect.DrawStock15 ?? throw new NotSupportedException("Draw Stock requires its executed entry state."));
        uint? spriteTemporary = null;
        Func<int, NativeBattleGraphicsModel> readModel = index => graphics.Models[index];
        NativeBattleDrawStock15Inputs inputs = new(nativeActionPackets, view, readModel, PoseSource, ReadBone241, Execute) { PackedCamera = camera.CapturePackedView1D97778(), CameraPadding18 = camera.CanonicalViewPadding };
        frame = NativeBattleDrawStock15Controller.Advance(frame, resources, inputs);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect with { DrawStock15 = frame.State } };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Draw Stock requires its actual posed model source.");

        NativeBattleModuleAnchor ReadBone241(byte actor)
        {
            NativeBattleVector point = NativeBattlePoseMatrices.ReadBone(graphics.Models[actor], PoseSource(actor), 241, 0);
            return new(point.X, point.Y, point.Z, 0);
        }

        NativeBattleDrawStock15Frame Execute(NativeBattleDrawStock15Frame current, NativeBattleDrawStock15Operation operation)
        {
            switch (operation)
            {
                case NativeBattleDrawStock15Sound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleDrawStock15Contact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Draw Stock contact lost its canonical process memory.") };
                case NativeBattleDrawStock15GenericTask generic:
                    NativeBattleCuragaControllerInputs genericInputs = new(nativeActionPackets, view, readModel, PoseSource);
                    NativeBattleCuragaLeafStep advanced = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(current.ToGeneric(), generic.Address, resources.GenericNodes(), genericInputs, new(AdvanceNodes));
                    return current.WithGeneric(advanced.Frame);

                    NativeBattleCuragaFrame AdvanceNodes(NativeBattleCuragaFrame nodes, uint address)
                    {
                        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Draw Stock requires its current ordering table.");
                        NativeBattleCuragaNodePresentationStep drawn = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(nodes, address, current.Random, readModel, projection, camera.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
                        current = current with { Random = drawn.Random };
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = drawn.Ordering } };
                        return drawn.Frame;
                    }
                case NativeBattleDrawStock15Particle particle:
                    return ExecuteParticle(current, particle.Operation);
                default:
                    throw new NotSupportedException("Draw Stock operation requires its original immediate owner.");
            }
        }

        NativeBattleDrawStock15Frame ExecuteParticle(NativeBattleDrawStock15Frame current, NativeBattleDrawStock15ParticleOperation operation)
        {
            switch (operation)
            {
                case NativeBattleDrawStock15Transform:
                    return current;
                case NativeBattleDrawStock15Anchor anchor:
                    NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[anchor.Actor], PoseSource(anchor.Actor), 241, 0);
                    return current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer) };
                case NativeBattleDrawStock15Rotate rotate:
                    NativeBattleGeometryRegisterStep step = ObserveNativeGeometryRotation(new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Draw Stock rotation requires its retained geometry controls."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Draw Stock rotation requires its retained data registers.")), rotate.Matrix, rotate.Axis, rotate.Angle, rotate.Padding18);
                    return current with { Memory = current.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
                case NativeBattleDrawStock15LookBasis look:
                    NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Draw Stock basis requires its retained temporary memory."), 32);
                    NativeBattleGeometryRegisterStep cross = NativeBattleGeometry.LoadDiagonal45DC70(new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Draw Stock basis requires its retained geometry controls."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Draw Stock basis requires its retained data registers.")), look.Up);
                    cross = NativeBattleGeometry.LoadSignedIr45DC40(cross, look.Direction);
                    cross = NativeBattleGeometry.OuterProduct45E820(cross);
                    byte[] vectors = new byte[18];
                    for (int index = 0; index < 3; index++)
                    {
                        BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(index * 2), unchecked((short)cross.Colors.Ir[index]!.Value));
                    }
                    short[] source = [look.Up.X, look.Up.Y, look.Up.Z, look.Direction.X, look.Direction.Y, look.Direction.Z];
                    for (int index = 0; index < source.Length; index++)
                    {
                        BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(6 + index * 2), source[index]);
                    }
                    NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(allocation.Staging.Write(allocation.Address, vectors), 32);
                    return current with { Memory = current.Memory with { GeometryRegisters = cross.Geometry, ScriptedColorRegisters = cross.Colors } };
                case NativeBattleDrawStock15Billboard billboard:
                    return current with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(current.Memory, camera.CapturePackedView1D97778(), billboard.Anchor, billboard.Scale, billboard.Depth) };
                case NativeBattleDrawStock15HeadSprite head:
                    return DrawSprite(current, head.Task, true);
                case NativeBattleDrawStock15SparkleSprite sparkle:
                    return DrawSprite(current, sparkle.Task, false);
                case NativeBattleDrawStock15Ribbon ribbon:
                    NativeBattleDrawStock15RibbonStep drawn = NativeBattleDrawStock15Rendering.Ribbon8B5F30(ribbon.Task, view, projection, camera.CanonicalViewPadding);
                    foreach (NativeBattleDrawStock15ParticleOperation instruction in drawn.Operations)
                    {
                        current = ExecuteParticle(current, instruction);
                    }
                    return Append(current, drawn.Batch);
                default:
                    return current with { Memory = ExecuteNativeDrawStock15Geometry(current.Memory, operation, spriteTemporary) };
            }
        }

        NativeBattleDrawStock15Frame DrawSprite(NativeBattleDrawStock15Frame current, NativeBattleModulePoolTask task, bool head)
        {
            NativeBattleTemporaryAllocation? allocation = null;
            if (head)
            {
                allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Draw Stock sprite requires its retained temporary memory."), 180);
                NativeFileStaging = allocation.Staging;
            }
            foreach (NativeBattleDrawStock15ParticleOperation instruction in NativeBattleDrawStock15SpriteRendering.Preparation(task, view, camera.CanonicalViewPadding))
            {
                current = ExecuteParticle(current, instruction);
            }
            if (!head)
            {
                allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Draw Stock sprite requires its retained temporary memory."), 180);
                NativeFileStaging = allocation.Staging;
            }
            NativeBattleTemporaryAllocation active = allocation ?? throw new InvalidOperationException("Draw Stock sprite did not allocate its original frame.");
            NativeBattlePackedMatrix32 captured = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Draw Stock sprite requires its prepared matrix controls."));
            NativeBattleDrawStock15SpriteTemporaryResult emitted = NativeBattleDrawStock15SpriteRendering.Emit180(NativeFileStaging!, active.Address, task, resources.Particles, view, projection, camera.Roll, captured);
            NativeFileStaging = emitted.Staging;
            spriteTemporary = active.Address;
            foreach (NativeBattleDrawStock15ParticleOperation instruction in emitted.Operations)
            {
                current = ExecuteParticle(current, instruction);
            }
            spriteTemporary = null;
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Draw Stock sprite requires its ordering table.");
            int cursor = current.State.Nodes.PrimaryDrawCursor ?? throw new NotSupportedException("Draw Stock sprite requires its original packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, emitted.Batch, NativeBattleEffectGouraudWrites.FullDword);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging!, 180);
            return current with { Memory = written.Memory, State = current.State with { Nodes = current.State.Nodes with { PrimaryDrawCursor = written.Cursor } } };
        }

        NativeBattleDrawStock15Frame Append(NativeBattleDrawStock15Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Draw Stock ribbon requires its ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.State.Nodes.PrimaryDrawCursor ?? throw new NotSupportedException("Draw Stock ribbon requires its original packet cursor."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State with { Nodes = current.State.Nodes with { PrimaryDrawCursor = written.Cursor } } };
        }
    }
}
