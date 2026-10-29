using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect166Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect166Lower
{
    private static NativeBattleEffect166Step AdvanceSprites(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        (frame, uint scratchAddress) = Allocate(frame, 72);
        NativeBattleModulePoolTask task = Read(frame, address);
        if (!Paused(frame, inputs) && Signed(task, 12) <= 16)
        {
            for (int birth = 0; birth < 2; birth++)
            {
                int free = 0;
                while (free < 100 && frame.State.ReadDword(checked(0x23BF080u + (uint)(free * 24))) != 0)
                {
                    free++;
                }
                if (free == 100)
                {
                    break;
                }
                uint location = checked(0x23BF080u + (uint)(free * 24));
                NativeBattleEffectArena row = frame.State.ReadView(location, 24);
                row = Put(Word(row, 4, 0), 0, 1);
                row = Word(row, 6, Roll(ref frame) % 3072 + 2560);
                row = Copy(row, 8, TaskView(task, 16, 8), 0, 8);
                row = Word(row, 8, S(row, 8) + Roll(ref frame) % 4800 - 2400);
                row = Word(row, 10, S(row, 10) - 400 - Roll(ref frame) % 2200);
                row = Word(row, 12, S(row, 12) + Roll(ref frame) % 2400 - 1200);
                frame = frame with { State = frame.State.WriteView(location, row, 24) };
                if (inputs.CapturePresentation)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, 0x23BF080, free);
                }
            }
        }
        (frame, uint spriteAddress) = Allocate(frame, 180);
        NativeBattleEffectArena scratch = Copy(Context(frame, scratchAddress, 72), 8, PackedArena(inputs.ReadView(frame)), 0, 32);
        NativeBattleEffectArena sprite = Word(Put(Context(frame, spriteAddress, 180), 0, 0xD718AC), 0x24, 0);
        frame = PublishContext(PublishContext(frame, scratchAddress, scratch, 72), spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, inputs.ReadView(frame)), inputs.ReadView(frame)) };
        int survivors = 0;
        for (int index = 0; index < 100; index++)
        {
            uint location = checked(0x23BF080u + (uint)(index * 24));
            NativeBattleEffectArena row = frame.State.ReadView(location, 24);
            if ((U(row, 0) & 1) == 0)
            {
                continue;
            }
            (byte[] bytes, bool[] known) = row.ReadSnapshot(14, 2);
            short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
            frame = Publish(frame, NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(row, 8), w));
            frame = frame with { Memory = NativeBattleEffect315Gte.SetDiagonal64DE00(NativeBattleEffect315Gte.TransformAlternate4607E0(frame.Memory), unchecked((ushort)S(row, 6))) };
            sprite = Word(Context(frame, spriteAddress, 180), 4, S(row, 4));
            frame = PublishContext(frame, spriteAddress, sprite, 180);
            frame = frame with { Memory = NativeBattleEffect315Gte.MacToTranslation64DE40(frame.Memory) };
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, sprite, resources.Sprite, inputs.ReadProjection(frame), (inputs.ReadCameraRoll ?? throw new NotSupportedException("Effect166 sprites require their current camera roll."))(frame), 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(166, task, index) with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(166, frame.State.Storage, 0x23BF080, index).Generation } : null);
            frame = PublishContext(frame with { Memory = drawn.Memory }, spriteAddress, drawn.Temporary180, 180);
            frame = Deliver(frame, drawn.Batch, inputs);
            if (!Paused(frame, inputs))
            {
                row = Word(row, 4, S(row, 4) + 1);
                if (S(Context(frame, spriteAddress, 180), 0x28) < 0)
                {
                    row = Put(row, 0, 0);
                }
                else
                {
                    survivors++;
                }
                frame = frame with { State = frame.State.WriteView(location, row, 24) };

            }
        }
        frame = Release(Release(frame, 180), 72);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }
}
