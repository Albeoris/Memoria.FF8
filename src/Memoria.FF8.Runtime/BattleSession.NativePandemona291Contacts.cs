using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) AdvanceNativePandemona291Contacts(NativeBattlePandemona291Frame frame, uint address, int age, NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        int count = nativeActionPackets.Headers[frame.Effect.Parameters!.HeaderIndex7C].ReadByte(0x10);
        bool paused = (frame.Memory.Pandemona291!.Pause2556258 ?? throw new NotSupportedException("Native Pandemona contacts require their actual pause DWORD.")) != 0;
        if (age == 440)
        {
            for (int index = 0; index < count; index++)
            {
                int row = start + 0x14 + index * 32;
                int actor = Actor(row);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Pandemona contacts require their current actor position.");
                Store(row + 0x18, Word(row + 0x18) + 2048);
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = new(Word(row + 0xC), unchecked((short)(transform.Position28.Y - Word(row + 4))), Word(row + 0x10)) } }) };
            }
        }
        if (!paused && age is >= 440 and < 480)
        {
            int elapsed = age - 440;
            for (int index = 0; index < count; index++)
            {
                int row = start + 0x14 + index * 32;
                byte state = arena.Read(row + 0x1C, 1)[0];
                if (state != 0)
                {
                    arena = arena.Write(row + 0x1C, [unchecked((byte)(state + 1))]);
                    continue;
                }
                int actor = Actor(row);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Pandemona contacts require their current actor position.");
                Store(row + 0x16, Word(row + 0x16) + ((index & 1) != 0 ? 256 : -256));
                short y = unchecked((short)(transform.Position28.Y + 1024));
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = transform.Position28 with { Y = y } } }) };
                frame = frame with { Memory = frame.Memory with { Arena = arena } };
                if (y >= -Word(row + 4))
                {
                    frame = SpawnNativePandemona291Attached(frame, address, index, true);
                    arena = frame.Memory.Arena!;
                    model = graphics.Models[actor];
                    transform = model.Transform!;
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = transform.Position28 with { Y = unchecked((short)-Word(row + 4)) } } }) };
                    Store(row + 0x18, model.Rotation16 ?? throw new NotSupportedException("Native Pandemona impact requires its current Z rotation."));
                    Store(row + 0x16, (model.Rotation14 ?? throw new NotSupportedException("Native Pandemona impact requires its current Y rotation.")) + 2048);
                    ImmutableArray<NativeBattleContactPacket> contacts = nativeActionPackets.ReadHeaderContacts506BA0(frame.Effect.Parameters!.HeaderIndex7C).Where(contact => contact.Primary.ActorIndex == actor).ToImmutableArray();
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = contacts }) with { PendingContacts = pending };
                    bool ring = arena.Read(row + 0x1E, 1)[0] != 0;
                    foreach (int selected in ring ? NativeBattleModelRings.Walk(graphics.Models, actor) : new[] { actor })
                    {
                        NativeBattleGraphicsModel target = graphics.Models[selected];
                        if (!ring || (target.Flags0 & 2) != 0)
                        {
                            graphics = graphics with { Models = graphics.Models.SetItem(selected, target with { Flags0 = (ushort)(target.Flags0 & ~4) }) };
                        }
                    }
                    arena = arena.Write(row + 0x1C, [1]);
                    if (index == 0)
                    {
                        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Pandemona impact requires its current camera scratch.");
                        NativeBattleCameraViewTranslationScratch scratch = camera.ViewTranslationScratch ?? throw new NotSupportedException("Native Pandemona impact requires its current camera scratch.");
                        NativeCamera = camera with { ViewTranslationScratch = scratch with { Y = unchecked((short)((scratch.Y ?? throw new NotSupportedException("Native Pandemona impact requires its current Y scratch.")) + 1000)) } };
                    }
                }
                else if ((elapsed & 3) == 0)
                {
                    frame = SpawnNativePandemona291Attached(frame, address, index, false);
                    arena = frame.Memory.Arena!;
                }
            }
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        if (age is >= 440 and < 1920)
        {
            for (int index = 0; index < count; index++)
            {
                int row = start + 0x14 + index * 32;
                int actor = Actor(row);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                if (arena.Read(row + 0x1C, 1)[0] == 0 || ((model.ResourceFlags44 ?? throw new NotSupportedException("Native Pandemona requires its actual target resource flags.")) & 0x40) != 0 || (model.Flags0 & 2) == 0)
                {
                    continue;
                }
                var captured = CaptureNativePandemona291Bounds(graphics, frame.Memory, actor);
                graphics = captured.Graphics;
                frame = frame with { Memory = captured.Memory };
                arena = frame.Memory.Arena!;
                Store(row + 4, captured.Bounds.Maximum);
                Store(row + 6, captured.Bounds.Minimum);
                model = graphics.Models[actor];
                NativeBattleModelTransform transform = model.Transform!;
                short y = unchecked((short)-captured.Bounds.Maximum);
                NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Pandemona target capture requires its current world matrix.");
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = transform.Position28 with { Y = y } }, GeometryWorldMatrix = world with { Y = y } }) };
                frame = frame with { Memory = frame.Memory with { Arena = arena } };
            }
        }
        return (frame, graphics);

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        void Store(int offset, int value) => arena = arena.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
        int Actor(int row)
        {
            uint relative = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(row, 4)) - 0x1D972C0);
            if (relative % 156 != 0 || relative / 156 >= graphics.Models.Length)
            {
                throw new InvalidDataException("Native Pandemona contact target leaves its original actor rows.");
            }
            return checked((int)(relative / 156));
        }
    }

    private NativeBattlePandemona291Frame SpawnNativePandemona291Attached(NativeBattlePandemona291Frame frame, uint address, int target, bool impact)
    {
        uint descriptor = impact ? 0x2556268u : 0x2556278u;
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == descriptor);
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return frame;
        }
        (pool, int index) = pool.Append(impact ? 0x6F0D90u : 0x6F0E30u);
        uint row = unchecked(address + 0x14u + (uint)(target * 32));
        NativeBattleModulePoolTask task = pool.Slots[index].WriteDword(0xC, row);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        int start = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(pool.BankAddress + (uint)(index * pool.Stride)), pool.Stride);
        arena = arena.Write(start + 0xC, BitConverter.GetBytes(row));
        uint buffer = frame.Memory.Pandemona291!.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona attached source requires its current buffer.");
        uint sourceAddress = unchecked(buffer + (impact ? 0x2EAA0u : 0x30760u));
        NativeBattleEffectFileAsset file = frame.Effect.Binding.Pandemona291Resources!.Files.Single(file => file.FileId == 726);
        int relative = checked((int)(sourceAddress - unchecked(buffer + 0x23220)));
        int length = file.Data.Length - relative;
        NativeBattleGfAnimationSource source = NativeBattleGfAnimationSource.Borrow(sourceAddress, arena, NativeBattleWaterExecutionFields.ArenaOffset(sourceAddress, length), length);
        int workspaceLength = impact ? 0x180 : 0x8C;
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(arena, start + 0x10, workspaceLength);
        arena = workspace.Initialize7016B0(source, workspaceLength).CopyTo(arena, start + 0x10);
        pool = pool.WriteSlot(index, task);
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == descriptor ? pool : current).ToImmutableArray() } };
    }
}
