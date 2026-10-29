using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleDarkMist13;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDarkMist13(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleDarkMist13Resources resources = effect.Binding.DarkMist13Resources ?? throw new NotSupportedException("Native Dark Mist requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleDarkMist13State state = memory.DarkMist13 ?? throw new NotSupportedException("Native Dark Mist requires its retained process state.");
        NativeBattleEffectTextureState textures = memory.Textures!.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Dark Mist requires its original TIM allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        NativeEffectMemory = memory with { Textures = textures, DarkMist13 = state.Swirl is null ? state with { Swirl = resources.Swirl } : state };
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x2747720, 0x2747658, 100, 2), NativeBattleEffectTaskPool.Initialize(0x274E960, 0x274E970, 88, 4), NativeBattleEffectTaskPool.Initialize(0x274EAD8, 0x274EAE8, 48, 20), NativeBattleEffectTaskPool.Initialize(0x274C3C0, 0x27486D0, 104, 150), NativeBattleEffectTaskPool.Initialize(0x27437B0, 0x27428B0, 96, 40)]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x8BE2D0, 100);
        hierarchy = appended.Hierarchy;
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Dark Mist requires its actual contact pointer.");
        NativeBattleModulePoolTask root = hierarchy.Read(appended.Address).WriteDword(12, 0x1D99A78).WriteByte(0x2D, nativeActionPackets.ReadContact(first).Primary.ActorIndex).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        hierarchy = hierarchy.Write(appended.Address, root);
        return graphics with { OriginalEffect = effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x2747720, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeDarkMist13(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleDarkMist13Resources resources = effect.Binding.DarkMist13Resources ?? throw new NotSupportedException("Native Dark Mist requires its original resources.");
        NativeBattleDarkMist13State process = NativeEffectMemory!.DarkMist13 ?? throw new NotSupportedException("Native Dark Mist requires its retained executable mesh and overlay work.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Dark Mist requires its shared CRT stream.");
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        uint rootAddress = hierarchy.Address(0, hierarchy.Pools[0].Head ?? throw new InvalidDataException("Native Dark Mist requires its live root."));
        NativeBattleModulePoolTask root = hierarchy.Read(rootAddress);
        if (root.Callback != 0x8BE2D0 || hierarchy.Pools[0].DescriptorAddress != 0x2747720)
        {
            throw new InvalidDataException("Native Dark Mist lost its actual root bank.");
        }
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        effect = effect with { DrawArenaOffset = (Word(root, 0x5C) & 1) != 0 ? 0 : 0x4000 };
        root = NativeBattleEffectModelHelpers.UpdateCaster8DC740(root, effect.Parameters!, graphics.Models, ReadSource);
        hierarchy = hierarchy.Write(rootAddress, root);
        switch (root.ReadByte(0x29))
        {
            case 0:
                hierarchy = hierarchy.Write(rootAddress, Phase(root));
                break;
            case 1:
                hierarchy = hierarchy.Write(rootAddress, root.WriteByte(0x63, 1));
                Append(1, 0x8BE3E0, 88, rootAddress);
                hierarchy = hierarchy.Write(rootAddress, Phase(hierarchy.Read(rootAddress)));
                break;
            case 2:
                if (root.ReadByte(0x63) == 0)
                {
                    int action = unchecked((sbyte)root.ReadByte(0x2A));
                    root = action < Word(root, 0x58) ? root.WriteByte(0x2A, unchecked((byte)(action + 1))).WriteByte(0x29, 1) : Phase(root);
                    hierarchy = hierarchy.Write(rootAddress, root);
                }
                break;
            case 3:
                if (Word(root, 0x5E) == 0)
                {
                    hierarchy = hierarchy.Write(rootAddress, Done(root));
                }
                break;
        }
        int alive = 0;
        for (int bank = 1; bank < 5; bank++)
        {
            NativeBattleEffectHierarchyStep step = hierarchy.Advance(bank, (current, address) =>
            {
                hierarchy = current;
                NativeBattleModulePoolTask task = hierarchy.Read(address);
                task = task.Callback switch
                {
                    0x8BE3E0 => Emitter(address, task),
                    0x8BE960 => Control(address, task),
                    0x8BF260 or 0x8BF440 or 0x8BF640 => Spawner(address, task),
                    0x8BF880 => Overlay(task),
                    0x8BE4A0 or 0x8BEAD0 => Model(task),
                    0x8BEC80 or 0x8BEF30 or 0x8BF300 or 0x8BF4F0 or 0x8BF700 => Particle(task),
                    _ => throw new NotSupportedException("Native Dark Mist selected an unrelated task callback.")
                };
                NativeBattleModulePoolTask actual = hierarchy.Read(address);
                task = (task.UpdateHeader(task.Flags, task.State, actual.Next, task.Callback)).WriteByte(0x28, actual.ReadByte(0x28)).WriteWord(0x24, unchecked((ushort)(Word(task, 0x24) + 1)));
                hierarchy = hierarchy.Write(address, task);
                bool ended = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
                if (ended)
                {
                    hierarchy = hierarchy.Complete8DC530(address);
                }
                return new(hierarchy, ended ? 2 : 0);
            });
            hierarchy = step.Hierarchy;
            alive = unchecked((short)(alive + step.Survivors));
        }
        root = hierarchy.Read(rootAddress).WriteWord(0x5E, unchecked((ushort)alive));
        root = root.WriteWord(0x5C, unchecked((ushort)(Word(root, 0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(Word(root, 0x24) + 1)));
        hierarchy = hierarchy.Write(rootAddress, root);
        bool completed = (root.ReadByte(0x26) & 1) != 0 && root.ReadByte(0x28) == 0;
        if (completed)
        {
            hierarchy = hierarchy.Advance(0, (current, _) => new(current, 2)).Hierarchy;
        }
        NativeEffectMemory = NativeEffectMemory! with { DarkMist13 = process };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = hierarchy.Pools, ReturnedDescriptor = completed ? 0u : 0x2747720u } };

        NativeBattleModulePoolTask Emitter(uint address, NativeBattleModulePoolTask task)
        {
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, graphics.Models, ReadSource);
            int target = task.ReadByte(0x2D);
            NativeBattlePoseSource source = ReadSource(target);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[target].SequenceState!, source));
            hierarchy = hierarchy.Write(address, task);
            switch (task.ReadByte(0x29))
            {
                case 0:
                    Append(4, 0x8BE4A0, 96, address);
                    task = Phase(task);
                    break;
                case 1:
                    if (Word(task, 0x24) >= 25)
                    {
                        Append(2, 0x8BE960, 48, address);
                        task = Phase(task);
                    }
                    break;
                case 2:
                    if (Word(task, 0x24) >= 70)
                    {
                        int group = effect.Parameters!.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A));
                        int contact = nativeActionPackets.Headers[group].FirstContactIndex ?? throw new NotSupportedException("Native Dark Mist requires its selected action contact.");
                        graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
                        task = Done(task);
                    }
                    break;
            }
            if (Word(task, 0x24) == 0)
            {
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128 });
            }
            return task;
        }

        NativeBattleModulePoolTask Control(uint address, NativeBattleModulePoolTask task)
        {
            task = Store(task, 0x1C, ReadNativeGraphicsBone(graphics, effect.Parameters!.Actor78, 0xF0, 1024));
            hierarchy = hierarchy.Write(address, task);
            int counter = Word(task, 0x24);
            if (task.ReadByte(0x29) == 0)
            {
                task = Phase(task);
            }
            else if (task.ReadByte(0x29) == 1 && counter >= 16)
            {
                Append(2, 0x8BF880, 48, address);
                task = Phase(task);
            }
            else if (task.ReadByte(0x29) == 2 && counter > 30)
            {
                task = Done(task);
            }
            if (counter == 0)
            {
                Append(4, 0x8BEAD0, 96, address);
            }
            if (counter <= 12)
            {
                foreach (uint callback in new uint[] { 0x8BEC80, 0x8BEF30 })
                {
                    for (int variant = 0; variant < 2; variant++)
                    {
                        uint child = Append(3, callback, 104, address);
                        hierarchy = hierarchy.Write(child, hierarchy.Read(child).WriteWord(0x62, (ushort)variant));
                    }
                }
            }
            if (counter is 16 or 23)
            {
                Append(2, 0x8BF640, 48, address);
            }
            if (counter == 16)
            {
                Append(2, 0x8BF260, 48, address);
            }
            if (counter == 30)
            {
                Append(2, 0x8BF440, 48, address);
            }
            return task;
        }

        NativeBattleModulePoolTask Spawner(uint address, NativeBattleModulePoolTask task)
        {
            bool stream = task.Callback == 0x8BF640;
            bool first = task.Callback == 0x8BF260;
            if (stream)
            {
                task = Store(task, 0x1C, ReadNativeGraphicsBone(graphics, effect.Parameters!.Actor78, 0xF0, 1024));
                hierarchy = hierarchy.Write(address, task);
            }
            int counter = Word(task, 0x24);
            int phase = task.ReadByte(0x29);
            if (phase == 0)
            {
                if (counter <= (stream ? 9 : first ? 16 : 8))
                {
                    for (int birth = 0; birth < (stream || first ? 1 : 2); birth++)
                    {
                        uint child = Append(3, stream ? 0x8BF700u : first ? 0x8BF300u : 0x8BF4F0u, 104, address);
                        if (stream || first || birth == 1)
                        {
                            hierarchy = hierarchy.Write(child, hierarchy.Read(child).WriteWord(0x62, unchecked((ushort)counter)));
                        }
                    }
                }
                else
                {
                    task = stream ? Done(task) : Phase(task);
                }
            }
            else if (phase == 1 && !stream && counter > 16)
            {
                task = Done(task);
            }
            return task;
        }

        NativeBattleModulePoolTask Particle(NativeBattleModulePoolTask task)
        {
            NativeBattleVector anchor = Vector(hierarchy.Read(task.ReadDword(0x14)), 0x30);
            NativeBattleDarkMist13ParticleStep step = AdvanceParticle(task, anchor, resources, random);
            task = step.Task;
            random = step.Random;
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                uint spriteAddress = task.ReadDword(0x4C);
                NativeBattleModuleResource resource = resources.Sprites.Single(sprite => sprite.Address == spriteAddress);
                NativeBattleVector point = Vector(task, 0x1C);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(task.ReadWord(0x50)), Phase = task.ReadWord(0x50), Anchor = new(point.X, point.Y, point.Z, 0), Scale = 4096, DepthOffset = Word(task, 0x54), SpriteFlags = task.Callback == 0x8BEF30 ? (ushort)4 : (ushort)0, SpriteColor = task.ReadDword(0x48), PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(13, task) };
                NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.RgbBytes);
                NativeEffectMemory = written.Memory;
                effect = effect with { DrawArenaOffset = written.Cursor };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            }
            return task;
        }

        NativeBattleModulePoolTask Model(NativeBattleModulePoolTask task)
        {
            bool swirl = task.Callback == 0x8BE4A0;
            short yaw = graphics.Models[effect.Parameters!.Actor78].Rotation14 ?? throw new NotSupportedException("Native Dark Mist requires its caster yaw.");
            int phase = task.ReadByte(0x29);
            if (swirl)
            {
                task = Store(task.WriteWord(0x44, 256).WriteWord(0x46, unchecked((ushort)yaw)), 0x1C, ReadNativeGraphicsBone(graphics, effect.Parameters.Actor78, 0xF0, 1024));
                switch (phase)
                {
                    case 0:
                        task = Phase(task.WriteWord(0x54, 4096).WriteWord(0x56, 4096).WriteDword(0x40, 0).WriteDword(0x4C, 0x163257C));
                        break;
                    case 1:
                        task = task.WriteWord(0x54, unchecked((ushort)(Word(task, 0x54) - 1024))).WriteWord(0x56, unchecked((ushort)(Word(task, 0x56) - 512)));
                        if (Word(task, 0x54) <= 0)
                        {
                            task = Phase(task.WriteWord(0x54, 0).WriteWord(0x56, 2048));
                        }
                        break;
                    case 2:
                        if (Word(task, 0x24) >= 12)
                        {
                            task = Phase(task);
                        }
                        break;
                    case 3:
                        task = task.WriteWord(0x54, unchecked((ushort)(Word(task, 0x54) + 512))).WriteWord(0x56, unchecked((ushort)(Word(task, 0x56) + 256)));
                        if (Word(task, 0x54) >= 4096)
                        {
                            task = Done(task.WriteWord(0x54, 4096).WriteWord(0x56, 4096));
                        }
                        break;
                }
                task = task.WriteWord(0x48, (ushort)((Word(task, 0x48) - 32) & 4095)).WriteWord(0x58, (ushort)((Word(task, 0x58) + 128) & 4095));
                process = process with { Swirl = Warp8BE7C0(resources.Ring, Scroll8BE6B0(process.Swirl!.Value), Word(task, 0x58)) };
                task = task.WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x50, task.ReadWord(0x54));
                DrawModel(task, process.Swirl.Value, 0);
                task = task.WriteDword(0x30, 8192).WriteDword(0x34, 8192).WriteDword(0x38, 3072).WriteWord(0x50, task.ReadWord(0x56));
                DrawModel(task, process.Swirl.Value, 1);
            }
            else
            {
                if (phase == 0)
                {
                    task = Phase(task.WriteWord(0x44, 256).WriteWord(0x46, unchecked((ushort)yaw)).WriteWord(0x5A, 1024).WriteWord(0x5E, 704).WriteWord(0x5C, 2048).WriteDword(0x4C, 0x1633C24).WriteWord(0x50, 0).WriteDword(0x40, 0).WriteDword(0x30, 0).WriteDword(0x34, 0).WriteDword(0x38, 0));
                }
                else if (phase == 1)
                {
                    int fade = unchecked((short)(Word(task, 0x50) + Word(task, 0x5E)));
                    task = task.WriteWord(0x50, unchecked((ushort)fade)).WriteWord(0x5E, unchecked((ushort)Math.Max(64, Word(task, 0x5E) - 64)));
                    if (fade >= 4096)
                    {
                        task = Done(task.WriteWord(0x50, 4096));
                    }
                }
                int growth = Word(task, 0x5C);
                short nextGrowth = unchecked((short)(growth - 128));
                task = task.WriteDword(0x30, unchecked(task.ReadDword(0x30) + (uint)growth)).WriteDword(0x34, unchecked(task.ReadDword(0x34) + (uint)growth)).WriteDword(0x38, unchecked(task.ReadDword(0x38) + (uint)growth)).WriteWord(0x5C, nextGrowth < 0 ? (ushort)0 : unchecked((ushort)nextGrowth)).WriteWord(0x5A, unchecked((ushort)(Word(task, 0x5A) + 1024)));
                task = Store(task, 0x1C, ReadNativeGraphicsBone(graphics, effect.Parameters.Actor78, 0xF0, Word(task, 0x5A)));
                DrawModel(task, resources.Cloud, 0);
            }
            return task;
        }

        void DrawModel(NativeBattleModulePoolTask task, ImmutableArray<byte> model, int copy)
        {
            if ((task.ReadByte(0x26) & 4) != 0)
            {
                return;
            }
            NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
            matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, Word(task, 0x46));
            matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, Word(task, 0x44));
            matrix = NativeBattleCuragaMatrices.RotateZ8DD960(matrix, Word(task, 0x48));
            NativeBattleVector point = Vector(task, 0x1C);
            matrix = matrix with { X = point.X, Y = point.Y, Z = point.Z };
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
            matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
            NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(model, 0, default, matrix, projection, new(0xF0, task.ReadDword(0x40), Word(task, 0x50), null, null, 0, 17, 2), colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(13, task, copy));
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }

        NativeBattleModulePoolTask Overlay(NativeBattleModulePoolTask task)
        {
            int actor = task.ReadByte(0x2D);
            switch (task.ReadByte(0x29))
            {
                case 0:
                    process = process with { OverlayAmplitude = 0 };
                    task = Phase(task.WriteWord(0x26, (ushort)(task.ReadWord(0x26) | 8)));
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = (ushort)(graphics.Models[actor].Flags0 | 4) }) };
                    break;
                case 1:
                    process = process with { OverlayAmplitude = unchecked((short)(process.OverlayAmplitude + 512)) };
                    if (process.OverlayAmplitude >= 4096)
                    {
                        process = process with { OverlayAmplitude = 4096 };
                        task = Phase(task);
                    }
                    break;
                case 2:
                    if (Word(task, 0x24) >= 28)
                    {
                        task = Phase(task);
                    }
                    break;
                case 3:
                    process = process with { OverlayAmplitude = unchecked((short)(process.OverlayAmplitude - 256)) };
                    if (process.OverlayAmplitude <= 0)
                    {
                        graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = (ushort)(graphics.Models[actor].Flags0 & 0xFFFB) }) };
                        NativeBattleModulePoolTask owner = hierarchy.Read(task.ReadDword(0x10));
                        hierarchy = hierarchy.Write(task.ReadDword(0x10), owner.WriteByte(0x63, 0));
                        task = Done(task.WriteWord(0x26, (ushort)(task.ReadWord(0x26) & ~8)));
                    }
                    break;
            }
            process = process with { OverlayPhase = (short)((process.OverlayPhase + 160) & 4095) };
            if ((task.ReadByte(0x26) & 8) != 0)
            {
                DrawOverlay(actor, NativeBattlePilotVisualLifetimes.Identity(13, task));
            }
            return task;
        }

        void DrawOverlay(int index, NativeBattlePilotVisualIdentity identity)
        {
            NativeBattleGraphicsModel actor = graphics.Models[index];
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
            NativeBattlePoseSource source = resource.PoseSource!;
            actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
            NativeBattleActionMatrix main = NativeBattleGfMatrices.Compose56C2F0(view, actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Dark Mist requires its actual actor matrix."));
            if ((actor.Flags0 & 0x20) == 0)
            {
                graphics = SubmitNativeShadowPackets5088A0(graphics, actor, projection, 4112, false);
            }
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source);
            short zsf3 = NativeEffectMemory!.ScriptedGraphics!.Zsf3 ?? throw new NotSupportedException("Native Dark Mist requires its current ZSF3.");
            short zsf4 = NativeEffectMemory.ScriptedGraphics.Zsf4 ?? throw new NotSupportedException("Native Dark Mist requires its current ZSF4.");
            foreach (NativeBattleCureActorGeometry geometry in NativeBattleCureActorAssembly.SelectGeometry8D7110(actor, resource, actor.SequenceState!.MeshMask124))
            {
                if (geometry.Mesh.Vertices.Length * 16 > 0x2580)
                {
                    throw new InvalidDataException("Native Dark Mist exceeds its original overlay vertex work.");
                }
                NativeBattleBio22ActorDraw draw = NativeBattleBio22ActorPackets.Emit89D780(geometry.Mesh, bones, main, projection, actor.Color40!.Value, process.OverlayPhase, process.OverlayAmplitude, zsf3, zsf4, process.ActorWork, 0, resource.GeometryRelocated507550 ? null : geometry.Binding, identity with { Primitive = unchecked(index * 65537 + geometry.AuthoredMesh) });
                process = process with { ActorWork = draw.Work };
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeEffectMemory.PrimitiveProcess!, graphics.PrimitiveFrame!.Pending, draw.Packets);
                NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = written.Process };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            }
            actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
            graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
        }

        uint Append(int bank, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend result = hierarchy.Append8DC540(bank, callback, size, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(effect.Parameters!, nativeActionPackets, pointer, group, target));
            hierarchy = result.Hierarchy;
            if (result.Address == 0)
            {
                throw new NotSupportedException("Native Dark Mist dereferenced a failed physical child allocation.");
            }
            return result.Address;
        }
        NativeBattlePoseSource ReadSource(int index) => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Dark Mist requires its actual actor skeleton.");
    }
}
