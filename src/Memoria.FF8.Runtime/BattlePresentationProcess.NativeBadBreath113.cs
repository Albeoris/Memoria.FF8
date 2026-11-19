using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBadBreath113;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private const uint BadBreath113Arena = 0x20DFAB8;

    private NativeBattleGraphicsState EnterNativeBadBreath113(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBadBreath113Resources resources = effect.Binding.BadBreath113Resources ?? throw new NotSupportedException("Native Bad Breath requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures!;
        NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Bad Breath requires its original TIM allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
        textures = textures.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25218C8, 0x2521880, 20, 1);
        (root, _) = root.Append(0x6CACD0);
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = root.DescriptorAddress, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeBadBreath113(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBadBreath113Resources resources = effect.Binding.BadBreath113Resources ?? throw new NotSupportedException("Native Bad Breath requires its original resources.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Bad Breath requires its shared CRT stream.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        if (pools[0].DescriptorAddress != 0x25218C8 || root.Callback != 0x6CACD0)
        {
            throw new InvalidDataException("Native Bad Breath lost its original task descriptor.");
        }
        bool expanded = graphics.Effects.StorageContract == BattleContactTaskStorageContract.ExpandableArena;
        if (!expanded)
        {
            effect = effect with { DrawArenaOffset = root.ReadDword(16) != 0 ? 0x14C8 : 0x114C8 };
            root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        }
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = this.resources.NativeGraphics!.Rules.SummonColors!;
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(effect.Parameters!.Header);
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        BattleModelTargets targets = effect.Parameters.TargetActors;
        ushort? originalTargetWord = aliases.EncodeTargetMask(targets);
        uint targetBank = BadBreath113Arena;
        uint particleBank = BadBreath113Arena + 0x2F4;
        ushort targetCapacity = 3;
        ushort particleCapacity = 80;
        BattleExecutionReference.OrderedContactRecipients? captured = null;
        if (expanded)
        {
            int programmes = graphics.ActorModels.Roster.Party.Count(targets.Contains);
            targetCapacity = checked((ushort)Math.Max(3, programmes));
            // The caster's three particle streams occupy at most 20 + 2*14 + 14 slots.
            // Each programmed recipient adds at most eleven births during its entire window.
            particleCapacity = checked((ushort)Math.Max(80, checked(62 + 11 * programmes)));
            if (BadWord(root, 12) == 1)
            {
                var recipients = nativeActionPackets.ReadHeaderContacts(effect.Parameters.Header)
                    .Where(packet => packet.Primary.Recipient.Side == BattleSide.Allies && targets.Contains(packet.Primary.Recipient))
                    .Select(packet => (packet.Primary.Recipient, packet.Occurrence ?? throw new InvalidDataException("Bad Breath requires a captured contact occurrence.")))
                    .ToImmutableArray();
                captured = new(recipients);
                NativeBattleEffectMemory memory = NativeEffectMemory!;
                if (targetCapacity > 3)
                {
                    (memory, targetBank) = NativeBattlePresentationTaskStorage.Reserve(memory, effect, nativeActionPackets.Roster, 0xFC, targetCapacity);
                }
                if (particleCapacity > 80)
                {
                    (memory, particleBank) = NativeBattlePresentationTaskStorage.Reserve(memory, effect, nativeActionPackets.Roster, 0x2C, particleCapacity);
                }
                NativeEffectMemory = memory;
            }
            else if (root.ReadByte(15) != 0)
            {
                if (pools.Length != 4)
                {
                    throw new InvalidDataException("Bad Breath requires its actual root and hosted child banks.");
                }
                ValidateBadBreath113Bank(pools[1], 0x25218B8, BadBreath113Arena + 0x10B4, 0x104, 1, 1);
                ValidateBadBreath113Bank(pools[2], 0x25218A8, particleBank, 0x2C, 80, particleCapacity);
                ValidateBadBreath113Bank(pools[3], 0x2521898, targetBank, 0xFC, 3, targetCapacity);
                if (pools[2].BankAddress < pools[3].BankAddress + (ulong)0xFC * targetCapacity && pools[3].BankAddress < pools[2].BankAddress + (ulong)0x2C * particleCapacity)
                {
                    throw new InvalidDataException("Bad Breath target and particle banks overlap.");
                }
                captured = pools[1].Slots[0].ReadExecutionReference<BattleExecutionReference.OrderedContactRecipients>(14, 1);
            }
        }
        if (expanded)
        {
            effect = effect with { DrawArenaOffset = root.ReadDword(16) != 0 ? 0x14C8 : 0x114C8 };
            root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        }
        if (BadWord(root, 12) == 1)
        {
            root = root.WriteByte(15, 1);
            pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x25218B8, BadBreath113Arena + 0x10B4, 0x104, 1), NativeBattleEffectTaskPool.Initialize(0x25218A8, particleBank, 0x2C, particleCapacity), NativeBattleEffectTaskPool.Initialize(0x2521898, targetBank, 0xFC, targetCapacity)];
            if (Allocate(1, 0x6CAE10, out int slot))
            {
                NativeBattleModulePoolTask main = Clear(1, slot);
                Put(1, slot, MirrorTargetByte(main, 0));
            }
        }
        NativeBattlePilotVisualIdentity? pilotIdentity = null;
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            survivors = 0;
            for (int bank = 1; bank < 4; bank++)
            {
                int current = bank;
                NativeBattleEffectTaskStep step = pools[bank].Advance((pool, slot) =>
                {
                    pools[current] = pool;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    pilotIdentity = NativeBattlePilotVisualLifetimes.Identity(113, task);
                    int result = task.Callback switch
                    {
                        0x6CAE10 => Main(ref task),
                        0x6CB260 => Particle(ref task),
                        0x6CB340 => Target(ref task),
                        _ => throw new NotSupportedException("Native Bad Breath selected an unrelated callback.")
                    };
                    task = task.UpdateHeader(task.Flags, task.State, pools[current].Slots[slot].Next, task.Callback);
                    Put(current, slot, task);
                    return new(pools[current], result);
                });
                pools[bank] = step.Pool;
                survivors += step.Survivors;
            }
        }
        if (survivors != 0)
        {
            root = root.WriteWord(12, unchecked((ushort)(BadWord(root, 12) + 1)));
        }
        pools[0] = pools[0].WriteSlot(0, root);
        if (survivors == 0)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
            if (expanded)
            {
                pools = pools.Where(pool => !NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, pool.Stride, pool.Slots.Length)).ToArray();
            }
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = survivors == 0 ? 0u : 0x25218C8u } };

        int Main(ref NativeBattleModulePoolTask task)
        {
            int frame = BadWord(task, 12);
            NativeBattleVector mouth = ReadNativeGraphicsBone(graphics, effect.Parameters.Source, 9, 0);
            if (frame <= 44)
            {
                if (frame == 0)
                {
                    long x = 0;
                    long y = 0;
                    long z = 0;
                    int count = 0;
                    foreach (BattleModelOwner actor in graphics.ActorModels.Roster.Party.Where(targets.Contains))
                    {
                        NativeBattleVector anchor = ReadNativeDefaultPosition571400(graphics, actor);
                        x += anchor.X;
                        y += anchor.Y;
                        z += anchor.Z;
                        if (!expanded)
                        {
                            x = unchecked((short)x);
                            y = unchecked((short)y);
                            z = unchecked((short)z);
                        }
                        count++;
                    }
                    if (count == 0)
                    {
                        throw new InvalidDataException("Native Bad Breath requires at least one selected party model.");
                    }
                    task = BadStore(task, 16, new((short)(x / count), (short)(y / count), unchecked((short)(z / count + 2000)))).WriteWord(22, 0xF000);
                }
                else if (frame is 10 or 16 or 26)
                {
                    byte role = frame == 10 ? (byte)0 : frame == 16 ? (byte)1 : (byte)2;
                    if (graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(role), graphics.ActorModels) is { } actor && targets.Contains(actor))
                    {
                        SpawnTarget(actor);
                    }
                    if (expanded && frame == 26)
                    {
                        foreach (BattleModelOwner extra in graphics.ActorModels.Roster.Party.Where(owner => targets.Contains(owner) && !aliases.TryEncodeExact(owner, out _)))
                        {
                            SpawnTarget(extra);
                        }
                    }
                    task = MirrorTargetByte(task, role + 1).WriteByte(15, (byte)(role + 1));
                }
                if (NewParticle(out int slot, out NativeBattleModulePoolTask puff))
                {
                    int aimX = NativeBattleCameraMath.Sine(BadWord(task, 22) >> 2) * 2000 >> 12;
                    task = task.WriteWord(22, unchecked((ushort)(BadWord(task, 22) + 0x174)));
                    NativeBattleVector aim = BadVector(task, 16);
                    NativeBattleVector velocity = new(unchecked((short)((Roll() & 63) + (aimX - mouth.X) / 16 - 32)), unchecked((short)((Roll() & 63) + (aim.Y - mouth.Y) / 16 - 32)), unchecked((short)((Roll() & 63) + (aim.Z - mouth.Z) / 16 - 32)));
                    puff = BadStore(BadStore(puff, 20, mouth), 28, velocity).WriteDword(16, 0x12C118C).WriteWord(14, 20).WriteWord(34, 2048).WriteWord(26, (ushort)((Roll() >> 4) + 3072));
                    Put(2, slot, BadStore(puff, 36, Decelerate(velocity, 5)));
                }
                short pitch = (short)((Roll() & 127) + 600);
                short yaw = graphics.ActorModels.Get(effect.Parameters.Source).Rotation14 ?? throw new NotSupportedException("Native Bad Breath requires its caster yaw WORD14.");
                short roll = (short)Roll();
                for (int birth = 0; birth < 2; birth++)
                {
                    if (!NewParticle(out int spraySlot, out NativeBattleModulePoolTask spray))
                    {
                        continue;
                    }
                    NativeBattleVector direction = Direction6CBB40(pitch, yaw, roll);
                    NativeBattleVector velocity = new((short)(direction.X >> 6), (short)(direction.Y >> 6), (short)(direction.Z >> 6));
                    spray = BadStore(BadStore(spray, 20, mouth), 28, velocity).WriteDword(16, 0x12C1418).WriteWord(14, 14).WriteWord(26, (ushort)((Roll() >> 4) + 2048));
                    Put(2, spraySlot, BadStore(spray, 36, Decelerate(velocity, 4)));
                    roll = unchecked((short)(roll + 2048));
                }
            }
            if (frame is >= 44 and <= 59 && NewParticle(out int fallingSlot, out NativeBattleModulePoolTask falling))
            {
                short x = (short)((Roll() & 31) - 16);
                short z = (short)((Roll() & 31) - 16);
                short y = (short)(-16 - (Roll() & 15));
                short scale = (short)((Roll() >> 4) + 2048);
                NativeBattleVector point = new(unchecked((short)(mouth.X + (x << 4))), mouth.Y, unchecked((short)(mouth.Z + (z << 4))));
                falling = BadStore(BadStore(falling, 28, new(x, y, z)), 20, point).WriteDword(16, 0x12C1418).WriteWord(14, 14).WriteWord(26, (ushort)scale).WriteWord(34, (ushort)scale);
                Put(2, fallingSlot, falling);
            }
            if (frame == 0)
            {
                NativeBattleVector anchor = ReadNativeDefaultPosition571400(graphics, effect.Parameters.Source);
                PublishNativeModuleSound(resources.Sound, 0x100, new(anchor.X, anchor.Y, anchor.Z, 0));
            }
            if (frame >= 59)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return 0;
        }

        NativeBattleModulePoolTask MirrorTargetByte(NativeBattleModulePoolTask task, int shift)
        {
            if (captured is not null)
            {
                task.WriteExecutionReference(14, 1, captured, originalTargetWord is ushort original ? [(byte)((byte)original >> shift)] : null);
                return task;
            }
            return originalTargetWord is ushort word ? task.WriteByte(14, (byte)((byte)word >> shift)) : task.MarkPayloadUnknown(14, 1);
        }

        void SpawnTarget(BattleModelOwner actor)
        {
            if (Allocate(3, 0x6CB340, out int targetSlot))
            {
                NativeBattleModulePoolTask targetTask = Clear(3, targetSlot);
                targetTask.WriteExecutionReference(14, 2, new BattleExecutionReference.Model(actor), aliases.EncodeOperand(actor, 2));
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(targetTask, 16, 0xEC);
                workspace = workspace.Initialize7016B0(resources.Cloud, 0xEC);
                Put(3, targetSlot, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(targetTask, 16, workspace));
            }
        }

        int Particle(ref NativeBattleModulePoolTask task)
        {
            int frame = BadWord(task, 12);
            uint resourceAddress = task.ReadDword(16);
            NativeBattleModuleResource resource = resources.Sprites.Single(sprite => sprite.Address == resourceAddress);
            NativeBattleVector point = BadVector(task, 20);
            int depth = resource.Address == 0x12C118C && frame < 4 ? 0 : -(BadWord(task, 34) >> 2);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(frame), Phase = task.ReadWord(12), Anchor = new(point.X, point.Y, point.Z, 0), Scale = BadWord(task, 26), DepthOffset = depth, Direction = 4096, SpriteFlags = 0, PilotIdentity = pilotIdentity is { } spriteIdentity ? spriteIdentity with { ResourceGeneration = resource.Address } : null };
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            frame++;
            task = task.WriteWord(12, unchecked((ushort)frame));
            if (frame >= BadWord(task, 14))
            {
                return 2;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                short velocity = unchecked((short)(BadWord(task, 28 + axis * 2) + BadWord(task, 36 + axis * 2)));
                task = task.WriteWord(28 + axis * 2, unchecked((ushort)velocity)).WriteWord(20 + axis * 2, unchecked((ushort)(BadWord(task, 20 + axis * 2) + velocity)));
            }
            return 0;
        }

        int Target(ref NativeBattleModulePoolTask task)
        {
            int frame = BadWord(task, 12);
            BattleModelOwner actor = task.ReadExecutionReference<BattleExecutionReference.Model>(14, 2).Owner;
            if (frame <= 10 && NewParticle(out int slot, out NativeBattleModulePoolTask particle))
            {
                NativeBattleVector anchor = ReadNativeDefaultPosition571400(graphics, actor);
                short x = (short)((Roll() & 64) - 32);
                short z = (short)((Roll() & 64) - 32);
                short y = (short)(-32 - (Roll() & 31));
                short height = unchecked((short)RiseHeight(Roll(), anchor.Y));
                short scale = (short)((Roll() >> 4) + 2048);
                NativeBattleVector point = new(unchecked((short)(anchor.X + (x << 2))), height, unchecked((short)(anchor.Z + (z << 2))));
                particle = BadStore(BadStore(particle, 20, point), 28, new(x, y, z)).WriteDword(16, 0x12C1014).WriteWord(14, 14).WriteWord(26, (ushort)scale).WriteWord(34, (ushort)scale);
                Put(2, slot, particle);
            }
            int step = frame - 10;
            if ((uint)step < 28)
            {
                int sine = NativeBattleCameraMath.Sine(step * 2048 / 28);
                DrawOverlay(actor, (short)(sine >> 2), unchecked((short)(step << 8)));
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor, graphics.ActorModels.Get(actor) with { Flags0 = (ushort)(graphics.ActorModels.Get(actor).Flags0 | 4) }) };
                PlayCloud(ref task, actor);
            }
            else
            {
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor, graphics.ActorModels.Get(actor) with { Flags0 = (ushort)(graphics.ActorModels.Get(actor).Flags0 & 0xFFFB) }) };
            }
            if (frame == 27)
            {
                if (expanded)
                {
                    foreach ((BattleModelOwner owner, BattleContactOccurrence occurrence) in captured!.Recipients)
                    {
                        if (owner == actor)
                        {
                            int relative = occurrence.Ordinal - header.FirstContact.Ordinal;
                            if ((uint)relative >= (uint)header.ReadContactCount())
                            {
                                throw new InvalidDataException("Bad Breath target occurrence left its selected header.");
                            }
                            NativeBattleContactPacket packet = nativeActionPackets.ReadContact(occurrence);
                            if (packet.Primary.Recipient != actor)
                            {
                                throw new InvalidDataException("Bad Breath target occurrence lost its captured model owner.");
                            }
                            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [packet] });
                        }
                    }
                }
                else
                {
                    for (int index = 0; index < header.ReadContactCount(); index++)
                    {
                        if (nativeActionPackets.ReadContact(header.FirstContact.Advance(index)).Primary.Recipient == actor)
                        {
                            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(header.FirstContact.Advance(index))] });
                        }
                    }
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame >= 38 ? 2 : 0;
        }

        void PlayCloud(ref NativeBattleModulePoolTask task, BattleModelOwner actor)
        {
            NativeBattleVector anchor = ReadNativeGraphicsBone(graphics, actor, 0xF1, 0);
            anchor = anchor with { Y = graphics.ActorModels.Get(actor).Transform!.Angle36 };
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (4), 0xEC);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Cloud, 0, (current, node) =>
            {
                if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
                {
                    return current;
                }
                ImmutableArray<byte> source = resources.Cloud.Data;
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(8 + node.GeometryIndex * 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(geometry + 4));
                ImmutableArray<byte> vertices;
                if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
                {
                    int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                    vertices = source.Slice(geometry + 12 + shape * count * 8, count * 8);
                }
                else
                {
                    NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                    vertices = NativeBattleGfMorphGeometry.Morph701390(source, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(0x11B8, 0x310).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(0x11B8, vertices.AsSpan()) };
                    vertices = vertices.Slice(0, count * 8);
                }
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(node.Vector16) with { X = node.Vector8.X + anchor.X, Y = node.Vector8.Y + anchor.Y, Z = node.Vector8.Z + anchor.Z };
                uint flag = resources.Flags[node.NodeIndex];
                if ((flag & 0x10000000) != 0)
                {
                    var translation = NativeBattleGfMatrices.RotateLong56C600(view, matrix.X, matrix.Y, matrix.Z);
                    matrix = matrix with { X = unchecked(translation.X + view.X), Y = unchecked(translation.Y + view.Y), Z = unchecked(translation.Z + view.Z) };
                }
                else
                {
                    matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
                }
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
                uint flags = (flag & 0xDFFF) | 0x2000u | (node.Scalar36 == 0 ? 0u : 0xC0u);
                Append(NativeBattleModulePackets.EmitBatch572200(source, geometry, vertices, matrix, projection, new(flags, node.Scalar36 == 0 ? 0u : node.Color32, node.Scalar36, null, null, null, 17, 2), colors, pilotIdentity: pilotIdentity is { } cloudIdentity ? cloudIdentity with { Primitive = node.ResourceOffset, ResourceGeneration = resources.Cloud.Address } : null));
                return current;
            });
            task = advanced.Workspace.CopyTo(task, 12 + (4), retainUnknown: true);
        }

        void DrawOverlay(BattleModelOwner actorIndex, short amplitude, short phase)
        {
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(actorIndex);
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actorIndex);
            NativeBattleActionMatrix main = NativeBattleGfMatrices.Compose56C2F0(view, actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Bad Breath requires its retained actor world matrix."));
            NativeBattleModuleProjectedVertex origin = NativeBattleModuleProjection.Project45FE10(main, default, projection);
            if (origin.Depth == 0)
            {
                return;
            }
            graphics = SubmitNativeShadowPackets5088A0(graphics, actorIndex, actor, projection, 4112, false);
            NativeBattlePoseSource source = resource.PoseSource!;
            actor = NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(actor, source, view);
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source);
            ImmutableArray<NativeBattleRawMesh> body = resource.Geometry!.Value;
            ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry!.Value;
            bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Bad Breath requires linked skeleton ownership."));
            NativeBattleModelTextureOwner? material = PrepareIntrinsicBodyMaterials(graphics, actorIndex, resource);
            for (int index = 0; index < body.Length; index++)
            {
                if ((actor.SequenceState!.MeshMask124 & (1u << (index & 31))) != 0)
                {
                    Submit(shared && index == 1 ? linked.Single() : body[index], bones, shared && index == 1 ? actor.LinkedTextureSlots! : actor.BodyTextureSlots!, index, shared && index == 1, material: material);
                }
            }
            if (resource.IntrinsicHands is { } hands && (actor.SequenceState!.MeshMask124 & 1) != 0)
            {
                var handMaterial = PrepareIntrinsicHandMaterial(actorIndex, hands);
                for (int index = 0; index < hands.Geometry.Length; index++)
                {
                    Submit(hands.Geometry[index], bones, handMaterial.Binding, 0x20000 + index, false, false, handMaterial.Material);
                }
            }
            actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
            if (!shared && !linked.IsEmpty)
            {
                ImmutableArray<NativeBattleActionMatrix> weaponBones = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(actor, resource.LinkedPoseSource!, view);
                for (int linkedIndex = 0; linkedIndex < linked.Length; linkedIndex++)
                {
                    Submit(linked[linkedIndex], weaponBones, actor.LinkedTextureSlots!, linkedIndex, true, material: material);
                }
            }
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(actorIndex, actor) };

            void Submit(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> matrices, NativeBattleModelTextureBinding binding, int meshIndex, bool linkedSource, bool? alreadyRelocated = null, NativeBattleModelTextureOwner? material = null)
            {
                ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Bad Breath requires its actual projected scratch capacity.");
                uint address = checked(0x196B018u - (uint)maximum * 8);
                byte[] output = (NativeFileStaging ?? throw new NotSupportedException("Native Bad Breath requires its retained shared projection bytes.")).Read(address, mesh.Vertices.Length * 8);
                NativeBattleProjectedVertex[] projected = new NativeBattleProjectedVertex[mesh.Vertices.Length];
                for (int index = 0; index < mesh.Vertices.Length; index++)
                {
                    NativeBattleRawVertex vertex = mesh.Vertices[index];
                    NativeBattleModuleDepthCueVertex point = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrices[vertex.Bone], vertex.Position, projection, 0, 0);
                    NativeBattleProjectedVertex retained = new(BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(index * 8)), BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(index * 8 + 4)));
                    projected[index] = Sway6CB7D0(point.Projected, point.Ir2, unchecked((short)origin.Screen), amplitude, phase, resources.Wave, retained);
                    BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(index * 8), projected[index].Screen);
                    BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(index * 8 + 4), projected[index].DepthAndClip);
                }
                NativeFileStaging = NativeFileStaging!.Write(address, output);
                NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.Emit6BCB50(mesh.Polygons, projected, actor.Color40!.Value, (alreadyRelocated ?? resource.GeometryRelocated507550) ? null : binding, pilotIdentity: pilotIdentity is { } bodyIdentity ? bodyIdentity with { ResourceGeneration = ((long)NativeModelResourceAddress100(actor) << 32) | ((linkedSource ? 1L : 0L) << 31) | (uint)meshIndex } : null);
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeEffectMemory!.PrimitiveProcess!, graphics.PrimitiveFrame!.Pending, BindNativeModelMaterial(batch, material));
                NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = written.Process };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            }
        }

        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        bool Allocate(int bank, uint callback, out int slot)
        {
            if (pools[bank].Slots.All(task => (task.Flags & 1) != 0))
            {
                if (expanded)
                {
                    throw new InvalidDataException("Bad Breath exhausted its reserved task bank.");
                }
                slot = -1;
                return false;
            }
            (pools[bank], slot) = pools[bank].Append(callback);
            return true;
        }
        bool NewParticle(out int slot, out NativeBattleModulePoolTask task)
        {
            if (!Allocate(2, 0x6CB260, out slot))
            {
                task = null!;
                return false;
            }
            task = Clear(2, slot);
            return true;
        }
        NativeBattleModulePoolTask Clear(int bank, int slot) => pools[bank].Slots[slot].ClearPayload(pools[bank].Slots[slot].PayloadLength);
        void Put(int bank, int slot, NativeBattleModulePoolTask task) => pools[bank] = pools[bank].WriteSlot(slot, task);
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        static NativeBattleVector Decelerate(NativeBattleVector velocity, int shift) => new((short)(-velocity.X >> shift), (short)(-velocity.Y >> shift), (short)(-velocity.Z >> shift));
    }

    private static void ValidateBadBreath113Bank(NativeBattleEffectTaskPool pool, uint descriptor, uint originalAddress, ushort stride, ushort originalCapacity, ushort capacity)
    {
        bool located = capacity == originalCapacity ? pool.BankAddress == originalAddress : NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, stride, capacity);
        if (!located || pool.DescriptorAddress != descriptor || pool.Stride != stride || pool.Slots.Length != capacity)
        {
            throw new InvalidDataException("Bad Breath task bank lost its actual physical descriptor.");
        }
    }

    private static short BadWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector BadVector(NativeBattleModulePoolTask task, int offset) => new(BadWord(task, offset), BadWord(task, offset + 2), BadWord(task, offset + 4));
    private static NativeBattleModulePoolTask BadStore(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
}
