using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWhiteWind226;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeWhiteWind226(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleWhiteWind226Resources resources = effect.Binding.WhiteWind226Resources ?? throw new NotSupportedException("Native White Wind requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures!;
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native White Wind requires its original TIM allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        textures = textures.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2508790, 0x25087B0, 20, 1);
        (root, _) = root.Append(0x6945D0);
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = 0x2508790, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeWhiteWind226(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleWhiteWind226Resources resources = effect.Binding.WhiteWind226Resources ?? throw new NotSupportedException("Native White Wind requires its original resources.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        if (pools[0].DescriptorAddress != 0x2508790 || root.Callback != 0x6945D0)
        {
            throw new InvalidDataException("Native White Wind lost its original task banks.");
        }
        effect = effect with { DrawArenaOffset = root.ReadDword(16) != 0 ? 0xECC : 0x10ECC };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        if (WindWord(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            root = root.WriteByte(15, 1);
            pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x2508780, 0x20DFAB8 + 0x7B0, 0x33C, 1), NativeBattleEffectTaskPool.Initialize(0x25087A0, 0x20DFAB8, 0x290, 3)];
            (pools[1], int slot) = pools[1].Append(0x6946D0);
            Put(1, slot, pools[1].Slots[slot].WriteWord(12, 0));
        }
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleActionMatrix moduleCamera = NativeBattleGfMatrices.Normalize67AAE0(view);
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Native White Wind requires its original target contacts.");
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            survivors = 0;
            for (int bank = 1; bank < 3; bank++)
            {
                int current = bank;
                NativeBattleEffectTaskStep step = pools[bank].Advance((pool, slot) =>
                {
                    pools[current] = pool;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    int result = task.Callback switch
                    {
                        0x6946D0 => Scene(ref task),
                        0x694D40 => Target(ref task),
                        _ => throw new NotSupportedException("Native White Wind selected an unrelated callback.")
                    };
                    task = task with { Next = pools[current].Slots[slot].Next };
                    Put(current, slot, task);
                    return new(pools[current], result);
                });
                pools[bank] = step.Pool;
                survivors += step.Survivors;
            }
        }
        Put(0, 0, root);
        if (survivors == 0)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        else
        {
            root = root.WriteWord(12, unchecked((ushort)(WindWord(root, 12) + 1)));
            Put(0, 0, root);
        }
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = survivors == 0 ? 0u : 0x2508790u } };

        int Scene(ref NativeBattleModulePoolTask task)
        {
            int frame = WindWord(task, 12);
            int caster = effect.Parameters.Actor78;
            if (frame < 22)
            {
                if (frame == 0)
                {
                    task = WindStore(task, 16, Anchor(caster));
                }
                NativeBattleActionMatrix matrix = Billboard673810(view, WindVector(task, 16));
                int scale = SizeScale(graphics.Models[caster].Transform!.Scale38);
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale);
                Play(ref task, 0x18, 0x188, resources.Caster, matrix, frame == 0);
            }
            if (frame is >= 7 and < 29)
            {
                if (frame == 7)
                {
                    short z = 0;
                    int count = header.ReadByte(16);
                    for (int index = 0; index < count; index++)
                    {
                        int actor = nativeActionPackets.ReadContact(firstContact + index).Primary.ActorIndex;
                        z = unchecked((short)(z + graphics.Models[actor].Transform!.Position28.Z));
                    }
                    if (count == 0)
                    {
                        throw new InvalidDataException("Native White Wind requires a nonempty original target list.");
                    }
                    task = WindStore(task, 0x1A0, new(0, Height(caster), (short)(z / count)));
                }
                NativeBattleVector point = WindVector(task, 0x1A0);
                NativeBattleActionMatrix world = new([4096, 0, 0, 0, 2048, 0, 0, 0, 2048], point.X, point.Y, point.Z, 4096);
                Play(ref task, 0x1A8, 0x194, resources.Sweep, NativeBattleGfMatrices.Compose56C2F0(view, world), frame == 7);
            }
            if (frame == 1)
            {
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128 });
            }
            int targetIndex = frame - 19;
            if ((uint)targetIndex < 12 && (targetIndex & 3) == 0 && targetIndex / 4 < header.ReadByte(16) && pools[2].Slots.Any(candidate => (candidate.Flags & 1) == 0))
            {
                (pools[2], int slot) = pools[2].Append(0x694D40);
                int actor = nativeActionPackets.ReadContact(firstContact + targetIndex / 4).Primary.ActorIndex;
                NativeBattleModulePoolTask target = pools[2].Slots[slot].WriteDword(12, checked(0x1D972C0u + (uint)actor * 156));
                target = WindStore(target, 16, Anchor(actor));
                NativeBattleGfAnimationWorkspace workspace = new(target.Payload.Slice(12, 0x278));
                workspace = workspace.Initialize7016B0(resources.Target, 0x278);
                Put(2, slot, PublishWorkspace(target, 0x18, workspace));
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame + 1 >= 29 ? 2 : 0;
        }

        int Target(ref NativeBattleModulePoolTask task)
        {
            uint offset = unchecked(task.ReadDword(12) - 0x1D972C0u);
            if (offset % 156 != 0 || offset / 156 >= 7)
            {
                throw new InvalidDataException("Native White Wind target pointer leaves its physical model bank.");
            }
            int actor = (int)(offset / 156);
            int scale = SizeScale(graphics.Models[actor].Transform!.Scale38);
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(Billboard673810(view, WindVector(task, 16)), scale, scale, scale);
            int remaining = Play(ref task, 0x18, 0x278, resources.Target, matrix, false);
            if (remaining != 0)
            {
                return 0;
            }
            for (int index = 0; index < header.ReadByte(16); index++)
            {
                if (nativeActionPackets.ReadContact(firstContact + index).Primary.ActorIndex == actor)
                {
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(firstContact + index)] });
                }
            }
            return 2;
        }

        int Play(ref NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleActionMatrix context, bool initialize)
        {
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(offset - 12, size));
            if (initialize)
            {
                workspace = workspace.Initialize7016B0(source, size);
            }
            NativeBattleGfAnimationAdvance advance = workspace.Advance701970(source, 0, (current, node) =>
            {
                if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
                {
                    return current;
                }
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
                ImmutableArray<byte> vertices;
                if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
                {
                    int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                    vertices = source.Data.Slice(geometry + 12 + shape * count * 8, count * 8);
                }
                else
                {
                    if (count > 124)
                    {
                        throw new InvalidDataException("Native White Wind exceeds its original morph scratch.");
                    }
                    NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                    vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(0xAEC, 0x3E0).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(0xAEC, vertices.AsSpan()) };
                    vertices = vertices.Slice(0, count * 8);
                }
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose6C53A0(node, context, moduleCamera);
                bool cue = node.Scalar36 != 0;
                NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, vertices, matrix, projection, new(cue ? 0x20F0u : 0x2030u, cue ? node.Color32 : 0, node.Scalar36, null, null, -128, 17, 2), definition.NativeGraphics!.Rules.SummonColors);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
                NativeEffectMemory = written.Memory;
                effect = effect with { DrawArenaOffset = written.Cursor };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
                return current;
            });
            task = PublishWorkspace(task, offset, advance.Workspace);
            return advance.Remaining;
        }

        short Height(int actor) => AnchorY(graphics.Models[actor].MinimumY146 ?? throw new NotSupportedException("Native White Wind requires actual model WORD146."), graphics.Models[actor].Transform!.Angle36);
        NativeBattleVector Anchor(int actor)
        {
            NativeBattleVector point = ReadNativeDefaultPosition571400(graphics, actor);
            return point with { Y = Height(actor) };
        }
        void Put(int bank, int slot, NativeBattleModulePoolTask task) => pools[bank] = pools[bank] with { Slots = pools[bank].Slots.SetItem(slot, task) };
    }

    private static NativeBattleModulePoolTask PublishWorkspace(NativeBattleModulePoolTask task, int offset, NativeBattleGfAnimationWorkspace workspace)
    {
        byte[] payload = task.Payload.ToArray();
        workspace.Data.AsSpan().CopyTo(payload.AsSpan(offset - 12));
        return task with { Payload = payload.ToImmutableArray() };
    }
    private static short WindWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector WindVector(NativeBattleModulePoolTask task, int offset) => new(WindWord(task, offset), WindWord(task, offset + 2), WindWord(task, offset + 4));
    private static NativeBattleModulePoolTask WindStore(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
}
