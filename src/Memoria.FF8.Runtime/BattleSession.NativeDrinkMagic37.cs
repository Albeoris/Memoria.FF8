using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeDrinkMagic37(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Drink Magic requires its selected original effect.");
        NativeBattleDrinkMagic37Resources resources = effect.Binding.DrinkMagic37Resources ?? throw new NotSupportedException("Drink Magic requires its original module resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Drink Magic requires its retained process owners.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Drink Magic requires its retained CRT random owner.");
        NativeBattleDrinkMagic37Frame frame;
        if (initialize)
        {
            frame = NativeBattleDrinkMagic37Controller.Initialize(memory, effect, random, nativeActionPackets, resources, memory.RequireDrinkMagic37());
            NativeEffectMemory = frame.Memory.WithDrinkMagic37(frame.State);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Drink Magic requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Drink Magic requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Drink Magic requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Drink Magic requires its current view matrix.");
        frame = new(memory, effect, random, memory.RequireDrinkMagic37());
        uint? spriteTemporary = null;
        NativeBattleDrinkMagic37Frame? anchorReadFrame = null;
        Func<int, NativeBattleGraphicsModel> readModel = index => graphics.Models[index];
        NativeBattleDrinkMagic37Inputs inputs = new(nativeActionPackets, view, readModel, PoseSource, Execute) { PackedCamera = camera.CapturePackedView1D97778(), CameraPadding18 = camera.CanonicalViewPadding };
        frame = NativeBattleDrinkMagic37Controller.Advance(frame, resources, inputs);
        NativeEffectMemory = frame.Memory.WithDrinkMagic37(frame.State);
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Drink Magic requires its actual posed model source.");

        NativeBattleVector ReadBone12(byte actor, short distance)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return model.Transform?.Position28 ?? throw new NotSupportedException("Drink Magic disabled anchor requires its retained position.");
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(actor), 12, distance);
            NativeBattleGeometryRegisters geometry = (anchorReadFrame ?? throw new InvalidOperationException("Drink Magic anchor requires its current callback frame.")).Memory.GeometryRegisters ?? throw new NotSupportedException("Drink Magic anchor requires its retained vector scratch.");
            short x = AnchorWord(0);
            short y = AnchorWord(2);
            NativeBattleFullMacVertex point = NativeBattleGfMatrices.TransformMac460840(producer.Matrix!, new(x, y, producer.Distance));
            return new(unchecked((short)point.X), unchecked((short)point.Y), unchecked((short)point.Z));

            short AnchorWord(int offset)
            {
                byte low = geometry.AnchorScratch8[offset] ?? throw new NotSupportedException("Drink Magic anchor requires its actual retained scratch coordinate.");
                byte high = geometry.AnchorScratch8[offset + 1] ?? throw new NotSupportedException("Drink Magic anchor requires its actual retained scratch coordinate.");
                return unchecked((short)(low | high << 8));
            }
        }
        short ReadYaw(byte actor) => graphics.Models[actor].Rotation14 ?? throw new NotSupportedException("Drink Magic requires the actor's actual rotation WORD14.");
        NativeBattleVector ReadPosition(byte actor) => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Drink Magic requires its actor position WORD28/30/32.");

        NativeBattleDrinkMagic37Frame Execute(NativeBattleDrinkMagic37Frame current, NativeBattleDrinkMagic37Operation operation)
        {
            switch (operation)
            {
                case NativeBattleDrinkMagic37Sound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleDrinkMagic37Contact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Drink Magic contact lost its canonical process memory.") };
                case NativeBattleDrinkMagic37GenericTask generic:
                    NativeBattleCuragaControllerInputs genericInputs = new(nativeActionPackets, view, readModel, PoseSource);
                    NativeBattleCuragaLeafStep advanced = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(current.ToGeneric(), generic.Address, resources.GenericNodes(), genericInputs, new(AdvanceNodes));
                    return current.WithGeneric(advanced.Frame);

                    NativeBattleCuragaFrame AdvanceNodes(NativeBattleCuragaFrame nodes, uint address)
                    {
                        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Drink Magic requires its current ordering table.");
                        NativeBattleCuragaNodePresentationStep drawn = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(nodes, address, current.Random, readModel, projection, camera.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
                        current = current with { Random = drawn.Random };
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = drawn.Ordering } };
                        return drawn.Frame;
                    }
                case NativeBattleDrinkMagic37ParticleTask particle:
                    anchorReadFrame = current;
                    NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
                    NativeBattleDrinkMagic37ParticleInputs particleInputs = new(current.Effect.Parameters!.Actor78, (pointer, group, target) => NativeBattleCureController.SelectActors(current.Effect.Parameters!, nativeActionPackets, pointer, group, target), ReadBone12, ReadYaw, task =>
                    {
                        NativeBattleModulePoolTask first = hierarchy.Read(task.ReadDword(0x14));
                        return task.WriteDword(0x1C, first.ReadDword(0x38)).WriteDword(0x20, first.ReadDword(0x3C));
                    });
                    NativeBattleDrinkMagic37ParticleStep stepped = NativeBattleDrinkMagic37Particles.Advance(hierarchy, particle.Address, current.Random, current.State.CapturedAnchor8, particleInputs);
                    current = current with { Effect = current.Effect with { Pools = stepped.Hierarchy.Pools }, Random = stepped.Random, State = current.State with { CapturedAnchor8 = stepped.CapturedAnchor8 } };
                    NativeBattleModulePoolTask particleTask = stepped.Hierarchy.Read(particle.Address);
                    ushort? padding = particleTask.Callback is 0x85BD50 or 0x85E930 ? NativeBattleDrawStock15Particles.ReadPadding(particleTask, 0x72) : null;
                    foreach (NativeBattleDrawStock15ParticleOperation instruction in stepped.Operations)
                    {
                        current = ExecuteParticle(current, instruction, padding);
                    }
                    return current;
                case NativeBattleDrinkMagic37OrbTask orb:
                    anchorReadFrame = current;
                    NativeBattleDrinkMagic37OrbStep advancedOrb = NativeBattleDrinkMagic37OrbController.Advance(new(current.Effect.Pools), orb.Address, new(nativeActionPackets, current.Effect.Parameters!, ReadBone12, ReadYaw, ReadPosition));
                    current = current with { Effect = current.Effect with { Pools = advancedOrb.Hierarchy.Pools } };
                    foreach (NativeBattleDrawStock15ParticleOperation instruction in advancedOrb.Operations)
                    {
                        current = ExecuteParticle(current, instruction);
                    }
                    return current;
                default:
                    throw new NotSupportedException("Drink Magic operation requires its original immediate owner.");
            }
        }

        NativeBattleDrinkMagic37Frame ExecuteParticle(NativeBattleDrinkMagic37Frame current, NativeBattleDrawStock15ParticleOperation operation, ushort? transformPadding = null)
        {
            switch (operation)
            {
                case NativeBattleDrawStock15Transform transform:
                    NativeBattleGeometryRegisterStep transformed = new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Drink Magic transformation requires its geometry controls."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Drink Magic transformation requires its data registers."));
                    transformed = transformed with { Geometry = NativeBattleGeometry.LoadMatrix(transformed.Geometry, transform.Matrix, true, false, transformPadding is { } word ? unchecked((short)word) : null) };
                    transformed = NativeBattleGeometry.LoadVector(transformed, 0, transform.Point, null);
                    transformed = NativeBattleGeometry.Transform(transformed, false);
                    return current with { Memory = current.Memory with { GeometryRegisters = transformed.Geometry, ScriptedColorRegisters = transformed.Colors } };
                case NativeBattleDrinkMagic37Anchor anchor:
                    NativeBattleGraphicsModel anchorModel = graphics.Models[anchor.Actor];
                    NativeBattleBoneAnchorProducer producer = (anchorModel.Flags0 & 2) == 0
                        ? new(null, 0, anchorModel.Transform?.Position28 ?? throw new NotSupportedException("Drink Magic disabled anchor requires its retained position."))
                        : NativeBattlePoseMatrices.ResolveBoneAnchor502170(anchorModel, PoseSource(anchor.Actor), 12, anchor.Distance);
                    return current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer) };
                case NativeBattleDrawStock15Rotate rotate:
                    NativeBattleGeometryRegisterStep step = ObserveNativeGeometryRotation(new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Drink Magic rotation requires its retained geometry controls."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Drink Magic rotation requires its retained data registers.")), rotate.Matrix, rotate.Axis, rotate.Angle, rotate.Padding18);
                    return current with { Memory = current.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
                case NativeBattleDrawStock15LookBasis look:
                    NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Drink Magic basis requires its retained temporary memory."), 32);
                    NativeBattleGeometryRegisterStep cross = NativeBattleGeometry.LoadDiagonal45DC70(new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Drink Magic basis requires its retained geometry controls."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Drink Magic basis requires its retained data registers.")), look.Up);
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
                    NativeBattleDrawStock15RibbonStep drawn = NativeBattleDrinkMagic37Rendering.Ribbon(ribbon.Task, view, projection, camera.CanonicalViewPadding);
                    foreach (NativeBattleDrawStock15ParticleOperation instruction in drawn.Operations)
                    {
                        current = ExecuteParticle(current, instruction, NativeBattleDrawStock15Particles.ReadPadding(ribbon.Task, 0x72));
                    }
                    return Append(current, drawn.Batch);
                case NativeBattleDrinkMagic37OrbDraw orb:
                    return DrawOrb(current, orb.Task);
                case NativeBattleDrinkMagic37OrbBounds:
                    _ = NativeBattleDrinkMagic37OrbWave.Bounds(current.State.OrbMesh);
                    return current;
                default:
                    return current with { Memory = ExecuteNativeDrawStock15Geometry(current.Memory, operation, spriteTemporary) };
            }
        }

        NativeBattleDrinkMagic37Frame DrawOrb(NativeBattleDrinkMagic37Frame current, NativeBattleModulePoolTask task)
        {
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            current = ExecuteParticle(current, new NativeBattleDrawStock15Rotate(identity, 'Z', unchecked((short)task.ReadWord(0x58))));
            NativeBattleDrinkMagic37OrbWaveStep wave = NativeBattleDrinkMagic37OrbWave.Transform(resources.OrbShape, current.State.OrbMesh, unchecked((short)task.ReadWord(0x54)), unchecked((short)task.ReadWord(0x58)), new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Drink Magic orb wave requires its geometry controls."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Drink Magic orb wave requires its geometry data.")));
            current = current with { State = current.State with { OrbMesh = wave.Mesh }, Memory = current.Memory with { GeometryRegisters = wave.Registers.Geometry, ScriptedColorRegisters = wave.Registers.Colors } };
            NativeBattleDrinkMagic37OrbPreparation matrix = NativeBattleDrinkMagic37OrbRendering.Prepare(task, view, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), camera.CanonicalViewPadding);
            foreach (NativeBattleDrawStock15ParticleOperation instruction in matrix.Operations)
            {
                current = ExecuteParticle(current, instruction);
            }
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Drink Magic orb requires its retained temporary memory."), 104);
            NativeFileStaging = allocation.Staging;
            NativeBattleEffectArena physical = NativeBattleTemporaryMemory.ReadView(NativeFileStaging, allocation.Address, 104);
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(physical, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), 0, 104);
            NativeBattleDrinkMagic37OrbPreparation prepared = NativeBattleDrinkMagic37OrbRendering.Prepare(task, view, context, camera.CanonicalViewPadding);
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Drink Magic orb requires its actual common packet owner.");
            NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Drink Magic orb requires its actual projection controls.");
            NativeBattleActorWaveMeshStep emitted = NativeBattleDrinkMagic37OrbRendering.EmitBatch(current.State.OrbMesh, prepared, projection, new(current.Memory.GeometryRegisters!, current.Memory.ScriptedColorRegisters!), process.ModelRendererRegisters ?? throw new NotSupportedException("Drink Magic orb requires its retained far-color controls."), controls.Dqa, controls.Dqb, controls.Zsf4 ?? throw new NotSupportedException("Drink Magic orb requires its actual ZSF4."), definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Drink Magic orb requires its original geometry constants."));
            NativeFileStaging = NativeBattleTemporaryMemory.PublishView(NativeFileStaging, allocation.Address, emitted.Context104, 104);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging, 104);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Drink Magic orb requires its current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process with { ModelRendererRegisters = emitted.FarControls }, primitive.Pending, emitted.Batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process, GeometryRegisters = emitted.Registers.Geometry, ScriptedColorRegisters = emitted.Registers.Colors } };
        }

        NativeBattleDrinkMagic37Frame DrawSprite(NativeBattleDrinkMagic37Frame current, NativeBattleModulePoolTask task, bool head)
        {
            NativeBattleTemporaryAllocation? allocation = null;
            if (head)
            {
                allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Drink Magic sprite requires its retained temporary memory."), 180);
                NativeFileStaging = allocation.Staging;
            }
            foreach (NativeBattleDrawStock15ParticleOperation instruction in NativeBattleDrinkMagic37Rendering.SpritePreparation(task, view, camera.CanonicalViewPadding))
            {
                current = ExecuteParticle(current, instruction);
            }
            if (!head)
            {
                allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Drink Magic sprite requires its retained temporary memory."), 180);
                NativeFileStaging = allocation.Staging;
            }
            NativeBattleTemporaryAllocation active = allocation ?? throw new InvalidOperationException("Drink Magic sprite did not allocate its original frame.");
            NativeBattlePackedMatrix32 captured = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Drink Magic sprite requires its prepared matrix controls."));
            NativeBattleDrawStock15SpriteTemporaryResult emitted = NativeBattleDrinkMagic37Rendering.Emit180(NativeFileStaging!, active.Address, task, resources, view, projection, camera.Roll, captured);
            NativeFileStaging = emitted.Staging;
            spriteTemporary = active.Address;
            foreach (NativeBattleDrawStock15ParticleOperation instruction in emitted.Operations)
            {
                current = ExecuteParticle(current, instruction);
            }
            spriteTemporary = null;
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Drink Magic sprite requires its ordering table.");
            int cursor = current.State.Nodes!.PrimaryDrawCursor ?? throw new NotSupportedException("Drink Magic sprite requires its original packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, emitted.Batch, NativeBattleEffectGouraudWrites.FullDword);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging!, 180);
            return current with { Memory = written.Memory, State = current.State with { Nodes = current.State.Nodes! with { PrimaryDrawCursor = written.Cursor } } };
        }

        NativeBattleDrinkMagic37Frame Append(NativeBattleDrinkMagic37Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Drink Magic ribbon requires its ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.State.Nodes!.PrimaryDrawCursor ?? throw new NotSupportedException("Drink Magic ribbon requires its original packet cursor."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State with { Nodes = current.State.Nodes! with { PrimaryDrawCursor = written.Cursor } } };
        }
    }
}
