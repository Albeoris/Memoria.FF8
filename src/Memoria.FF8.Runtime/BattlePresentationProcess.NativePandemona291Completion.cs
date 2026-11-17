using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private (NativeBattleGraphicsModel Actor, byte State) ReadNativePandemona291FirstTarget(NativeBattlePandemona291Frame frame, NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x6ED900));
        int index = Enumerable.Range(0, main.Slots.Length).Single(index => (main.Slots[index].Flags & 1) != 0 && main.Slots[index].Callback == 0x6ED900);
        int row = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(main.BankAddress + (uint)(index * main.Stride) + 0x14), 32);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        BattleModelOwner actorIndex = NativeBattlePandemona291Construction.ReadTargetOwner(arena, row);
        return (graphics.ActorModels.Get(actorIndex), arena.Read(row + 0x1C, 1)[0]);
    }

    private bool NativePandemona291TargetsAwaitResource40(NativeBattlePandemona291Frame frame, NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x6ED900));
        int index = Enumerable.Range(0, main.Slots.Length).Single(index => (main.Slots[index].Flags & 1) != 0 && main.Slots[index].Callback == 0x6ED900);
        int start = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(main.BankAddress + (uint)(index * main.Stride)), main.Stride);
        int count = nativeActionPackets.ReadHeader(frame.Effect.Parameters!.Header).ReadContactCount();
        for (int target = 0; target < count; target++)
        {
            BattleModelOwner actorIndex = NativeBattlePandemona291Construction.ReadTargetOwner(frame.Memory.Arena!, start + 0x14 + target * 32);
            if (!NativeGraphicsActorAtStandingPose(graphics, actorIndex))
            {
                return true;
            }
        }
        return false;
    }

    private NativeBattlePandemona291Frame LoadNativePandemona291EndingTim(NativeBattlePandemona291Frame frame, short fileId)
    {
        NativeBattlePandemona291State state = frame.Memory.Pandemona291!;
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona ending TIM delivery requires its current buffer.");
        NativeBattleEffectFileAsset file = frame.Effect.Binding.Pandemona291Resources!.Files.Single(file => file.FileId == fileId);
        if (file.Data.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native Pandemona ending TIM requires its actual archive bytes.");
        }
        int destination = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(buffer + 0x31220), file.Data.Length);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(destination, file.Data.AsSpan());
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Pandemona ending TIM requires its current upload queue.");
        textures = textures.EnqueueArenaTim505E30(arena, destination).State;
        return frame with { Memory = frame.Memory with { Arena = arena, Textures = textures } };
    }

    private NativeBattlePandemona291Frame RestoreNativePandemona291Targets(NativeBattlePandemona291Frame frame, uint address, ref NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        int count = nativeActionPackets.ReadHeader(frame.Effect.Parameters!.Header).ReadContactCount();
        for (int index = 0; index < count; index++)
        {
            int row = start + 0x14 + index * 32;
            BattleModelOwner actor = NativeBattlePandemona291Construction.ReadTargetOwner(arena, row);
            NativeBattleGraphicsModel model = graphics.ActorModels.Get(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Pandemona completion requires its current actor position.");
            NativeBattleVector position = new(Word(row + 0xC), Word(row + 0xE), Word(row + 0x10));
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor, model with { Transform = transform with { Position28 = position } }) };
        }
        return frame;

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    }
}
