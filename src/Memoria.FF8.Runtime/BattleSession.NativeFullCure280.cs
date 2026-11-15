using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeFullCure280(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFullCure280Resources resources = effect.Binding.FullCure280Resources ?? throw new NotSupportedException("Native Full-cure requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleFullCure280State process = memory.FullCure280 ?? throw new NotSupportedException("Native Full-cure requires its retained process state.");
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Full-cure requires its original TIM allocation.");
        (NativeBattleEffectTextureState textures, _) = memory.Textures!.EnqueueTim505E30(new(asset.Name, asset.Data));
        textures = textures.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        nativeCrtRandom = (nativeCrtRandom ?? throw new NotSupportedException("Native Full-cure requires its CRT process.")).Draw(out ushort random);
        NativeEffectMemory = memory with { Textures = textures, FullCure280 = process with { EntryRandom = random } };
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x2508018, 0x2508028, 20, 1);
        (roots, int slot) = roots.Append(0x67E7F0);
        roots = roots.WriteSlot(slot, roots.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        return graphics with { OriginalEffect = effect with { FullCure280 = new(0), Pools = [roots], ReturnedDescriptor = 0x2508018, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeFullCure280(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFullCure280Resources resources = effect.Binding.FullCure280Resources ?? throw new NotSupportedException("Native Full-cure requires its original resources.");
        NativeBattleFullCure280State process = NativeEffectMemory!.FullCure280 ?? throw new NotSupportedException("Native Full-cure requires its retained process state.");
        int buffer = effect.FullCure280?.BufferOffset ?? throw new NotSupportedException("Native Full-cure requires its executed initializer.");
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleModulePoolTask root = roots.Slots[0];
        NativeBattleEffectTaskPool? directors = effect.Pools.Length > 1 ? effect.Pools[1] : null;
        NativeBattleEffectTaskPool? targets = effect.Pools.Length > 2 ? effect.Pools[2] : null;
        NativeBattleEffectTaskPool? sparks = effect.Pools.Length > 3 ? effect.Pools[3] : null;
        bool expanded = graphics.Effects.StorageContract == BattleContactTaskStorageContract.ExpandableArena;
        uint originalTargets = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)(buffer + 320));
        uint originalSparks = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer);
        uint directorBank = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)(buffer + 7124));
        if (expanded)
        {
            ValidateFullCure280Bank(roots, 0x2508018, 0x2508028, 20, 1, 1);
            if (effect.Pools.Length == 4)
            {
                NativeBattleEffectTaskPool directorPool = effect.Pools[1];
                NativeBattleEffectTaskPool targetPool = effect.Pools[2];
                NativeBattleEffectTaskPool sparkPool = effect.Pools[3];
                ValidateFullCure280Bank(directorPool, 0x2508008, directorBank, 28, 1, 1);
                (ushort requiredTargets, ushort requiredSparks) = FullCure280Capacities(directorPool.Slots[0].ReadContactCount(14));
                ValidateFullCure280Bank(targetPool, 0x2507FF8, originalTargets, 2268, 3, requiredTargets);
                ValidateFullCure280Bank(sparkPool, 0x2507FE8, originalSparks, 20, 16, requiredSparks);
                ulong targetEnd = targetPool.BankAddress + (ulong)targetPool.Stride * requiredTargets;
                ulong sparkEnd = sparkPool.BankAddress + (ulong)sparkPool.Stride * requiredSparks;
                if (targetPool.BankAddress < sparkEnd && sparkPool.BankAddress < targetEnd)
                {
                    throw new InvalidDataException("Full-cure reserved target and spark banks overlap.");
                }
            }
            else if (effect.Pools.Length != 1)
            {
                throw new InvalidDataException("Full-cure requires its actual root and three child banks.");
            }
        }
        uint targetBank = originalTargets;
        uint sparkBank = originalSparks;
        ushort targetCapacity = 3;
        ushort sparkCapacity = 16;
        NativeBattleEffectMemory? allocatedMemory = null;
        NativeBattleActionHeaderSlot? expandedHeader = null;
        if (expanded && root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            expandedHeader = nativeActionPackets.ReadHeader(effect.Parameters!.Header);
            (targetCapacity, sparkCapacity) = FullCure280Capacities(expandedHeader.ReadContactCount());
            NativeBattleEffectMemory memory = NativeEffectMemory!;
            if (targetCapacity > 3)
            {
                (memory, targetBank) = NativeBattlePresentationTaskStorage.Reserve(memory, effect, nativeActionPackets.Roster, 2268, targetCapacity);
            }
            if (sparkCapacity > 16)
            {
                (memory, sparkBank) = NativeBattlePresentationTaskStorage.Reserve(memory, effect, nativeActionPackets.Roster, 20, sparkCapacity);
            }
            allocatedMemory = memory;
        }
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 7664 : 73200)) };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        int flash = 0;
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            root = root.WriteByte(15, 1);
            directors = NativeBattleEffectTaskPool.Initialize(0x2508008, directorBank, 28, 1);
            targets = NativeBattleEffectTaskPool.Initialize(0x2507FF8, targetBank, 2268, targetCapacity);
            sparks = NativeBattleEffectTaskPool.Initialize(0x2507FE8, sparkBank, 20, sparkCapacity);
            (directors, int slot) = directors.Append(0x67E980);
            NativeBattleActionHeaderSlot header = expandedHeader ?? nativeActionPackets.ReadHeader(effect.Parameters!.Header);
            int count = header.ReadContactCount();
            NativeBattleModulePoolTask director = directors.Slots[slot].WriteDword(12, 0).WriteDword(16, 0).WriteDword(20, 0).WriteDword(24, 0).WriteContactCount(14, count);
            var recipients = ImmutableArray.CreateBuilder<(BattleModelOwner Owner, BattleContactOccurrence Occurrence)>(count);
            ImmutableArray<byte>? originalFirst = [0, 0, 0, 0];
            for (int index = 0; index < count; index++)
            {
                NativeBattleContactPacket packet = nativeActionPackets.ReadContact(header.FirstContact.Advance(index));
                BattleModelOwner model = packet.Primary.Recipient;
                BattleContactOccurrence occurrence = packet.Occurrence ?? throw new InvalidDataException("Native Full-cure requires its captured recipient occurrence.");
                recipients.Add((model, occurrence));
                if (index == 0)
                {
                    originalFirst = nativeActionPackets.ModelAliases.EncodeAddress(model);
                }
                else if (index < 3)
                {
                    int offset = 16 + index * 4;
                    director = nativeActionPackets.ModelAliases.EncodeAddress(model) is { } encoded ? director.WriteBytes(offset, encoded.AsSpan()) : director.MarkPayloadUnknown(offset, 4);
                }
            }
            director.WriteExecutionReference(16, 4, new BattleExecutionReference.OrderedContactRecipients(recipients.MoveToImmutable()), originalFirst);
            directors = directors.WriteSlot(slot, director);
            if (allocatedMemory is not null)
            {
                NativeEffectMemory = allocatedMemory;
            }
        }
        int directorSurvivors = 1;
        int targetSurvivors = 1;
        int sparkSurvivors = 1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep directorStep = directors!.Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int age = WindWord(task, 12);
                flash = NativeBattleFullCure280Geometry.Flash((short)age);
                bool birth = expanded ? age is 0 or 20 or 40 : (uint)age < 63 && age % 20 == 0;
                if (birth)
                {
                    int first = age / 20;
                    int count = task.ReadContactCount(14);
                    int end = expanded && age == 40 ? count : Math.Min(count, first + 1);
                    for (int index = first; index < end; index++)
                    {
                        (targets, int targetSlot) = targets!.Append(0x67EA80);
                        var recipient = task.ReadExecutionReference<BattleExecutionReference.OrderedContactRecipients>(16, 4).Recipients[index];
                        BattleModelOwner actor = recipient.Owner;
                        NativeBattleModulePoolTask target = targets.Slots[targetSlot];
                        target.WriteExecutionReference(12, 4, new BattleExecutionReference.OrderedContactRecipients([recipient]), nativeActionPackets.ModelAliases.EncodeAddress(actor));
                        target = WindStore(target, 16, ReadNativeGraphicsBone(graphics, actor, 0xF1, 0)).WriteWord(22, 0);
                        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(target, 12 + (12), 2244);
                        workspace = workspace.Initialize7016B0(resources.Layout, 2244);
                        target = StoreWorkspace(target, workspace);
                        targets = targets.WriteSlot(targetSlot, target);
                        PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 });
                    }
                }
                task = task.WriteWord(12, unchecked((ushort)(age + 1)));
                bool complete = age + 1 >= 65;
                if (complete)
                {
                    flash = 0;
                }
                return new(pool.WriteSlot(slot, task), complete ? 2 : 0);
            });
            directors = directorStep.Pool;
            directorSurvivors = directorStep.Survivors;
            NativeBattleEffectTaskStep targetStep = targets!.Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (12), 2244);
                NativeBattleVector anchor = WindVector(task, 16);
                NativeBattlePilotVisualIdentity ownerIdentity = NativeBattleSlot280288Presentation.Task(280, task, resources.Layout.Address, 24);
                NativeBattleGfAnimationAdvance played = workspace.Advance701970(resources.Layout, 0, (current, node) =>
                {
                    DrawMesh(node, anchor, ownerIdentity with { Primitive = unchecked(ownerIdentity.Primitive * 397 + node.ResourceOffset) });
                    return current;
                });
                task = StoreWorkspace(task, played.Workspace);
                (BattleModelOwner actor, BattleContactOccurrence occurrence) = task.ReadExecutionReference<BattleExecutionReference.OrderedContactRecipients>(12, 4).Recipients.Single();
                int age = WindWord(task, 22);
                if (age >= 19 && (age & 1) == 0 && (expanded || sparks!.Slots.Any(candidate => (candidate.Flags & 1) == 0)))
                {
                    (sparks, int sparkSlot) = sparks!.Append(0x67EF50);
                    int boneCount = ReadNativeGraphicsBoneCount(graphics, actor);
                    NativeBattleModulePoolTask spark = WindStore(sparks.Slots[sparkSlot], 12, ReadNativeGraphicsBone(graphics, actor, checked((byte)(age % boneCount)), 0)).WriteWord(18, 0);
                    sparks = sparks.WriteSlot(sparkSlot, spark);
                }
                task = task.WriteWord(22, unchecked((ushort)(age + 1)));
                if (age + 1 >= 30)
                {
                    NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(effect.Parameters!.Header);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    if (expanded)
                    {
                        int relative = occurrence.Ordinal - header.FirstContact.Ordinal;
                        if ((uint)relative >= (uint)header.ReadContactCount())
                        {
                            throw new InvalidDataException("Full-cure target occurrence left its selected header.");
                        }
                        NativeBattleContactPacket contact = nativeActionPackets.ReadContact(occurrence);
                        if (contact.Primary.Recipient != actor)
                        {
                            throw new InvalidDataException("Full-cure target occurrence lost its captured model owner.");
                        }
                        graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [contact] });
                    }
                    else
                    {
                        BattleContactCursor first = header.FirstContact;
                        for (int index = 0; index < header.ReadContactCount(); index++)
                        {
                            NativeBattleContactPacket contact = nativeActionPackets.ReadContact(first.Advance(index));
                            if (contact.Primary.Recipient == actor)
                            {
                                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [contact] });
                            }
                        }
                    }
                    graphics = graphics with { PendingContacts = pending };
                }
                return new(pool.WriteSlot(slot, task), age + 1 >= 30 ? 2 : 0);
            });
            targets = targetStep.Pool;
            targetSurvivors = targetStep.Survivors;
            NativeBattleEffectTaskStep sparkStep = sparks!.Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int age = WindWord(task, 18);
                NativeBattleVector anchor = WindVector(task, 12);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Spark, SpriteFrame = resources.Spark.ProjectedFrame571C80(age), Phase = (ushort)age, Anchor = new(anchor.X, anchor.Y, anchor.Z, 0), Scale = 2048, DepthOffset = -512, SpriteFlags = 0, PilotIdentity = NativeBattleSlot280288Presentation.Task(280, task, resources.Spark.Address) };
                NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, 180);
                NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, resources.Spark.Address, 0).Write(temporary.Address + 4, BitConverter.GetBytes((ushort)age));
                NativeBattleModuleSpriteTemporaryResult drawn = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary.Address, draw, projection, NativeCamera.Roll, 17, 2);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, NativeBattleSlot280288Presentation.Depths(280, drawn.Batch), NativeBattleEffectGouraudWrites.FullDword);
                NativeEffectMemory = written.Memory;
                effect = effect with { DrawArenaOffset = written.Cursor };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
                NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 180);
                task = task.WriteWord(18, unchecked((ushort)(age + 1)));
                return new(pool.WriteSlot(slot, task), age + 1 >= 8 ? 2 : 0);
            });
            sparks = sparkStep.Pool;
            sparkSurvivors = sparkStep.Survivors;
        }
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Full-cure requires its current stage.");
        NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)flash), ColorFlags = 0 }).ToImmutableArray() };
        bool released = root.ReadByte(15) != 0 && directorSurvivors == 0 && sparkSurvivors == 0 && (!expanded || targetSurvivors == 0);
        if (!released)
        {
            root = root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)));
        }
        roots = roots.WriteSlot(0, root);
        if (released)
        {
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        ImmutableArray<NativeBattleEffectTaskPool> pools = directors is null ? [roots] : [roots, directors, targets!, sparks!];
        if (released && expanded)
        {
            pools = pools.Where(pool => pool.DescriptorAddress switch
            {
                0x2507FF8 => pool.BankAddress == originalTargets,
                0x2507FE8 => pool.BankAddress == originalSparks,
                _ => true
            }).ToImmutableArray();
        }
        return graphics with { OriginalEffect = effect with { Pools = pools, ReturnedDescriptor = released ? 0u : 0x2508018u } };

        NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
        {
            return workspace.CopyTo(task, 12 + (12), retainUnknown: true);
        }

        void DrawMesh(NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
            {
                return;
            }
            NativeBattleFullCure280Draw drawn = NativeBattleFullCure280Part.Draw(NativeEffectMemory!, resources.Layout, node, view, process.Billboard, anchor, buffer, projection, definition.NativeGraphics!.Rules.SummonColors, pilotIdentity, publishMorph: updated => NativeEffectMemory = updated);
            NativeBattleGfGeometryBatch batch = drawn.Batch;
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
    }

    private static (ushort Targets, ushort Sparks) FullCure280Capacities(int count)
    {
        // Targets emit every other callback and each spark survives eight callbacks.
        // The first two windows and the final cohort's window do not overlap.
        return (checked((ushort)Math.Max(3, count)), checked((ushort)Math.Max(16, checked(4 * Math.Max(1, count - 2)))));
    }

    private static void ValidateFullCure280Bank(NativeBattleEffectTaskPool pool, uint descriptor, uint originalAddress, ushort stride, ushort originalCapacity, ushort capacity)
    {
        bool located = capacity == originalCapacity ? pool.BankAddress == originalAddress : NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, stride, capacity);
        if (!located || pool.DescriptorAddress != descriptor || pool.Stride != stride || pool.Slots.Length != capacity)
        {
            throw new InvalidDataException("Full-cure task bank leaves its physical descriptor contract.");
        }
    }
}
