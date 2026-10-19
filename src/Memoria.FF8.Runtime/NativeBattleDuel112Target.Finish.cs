using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDuel112Target
{
    public static NativeBattleDuel112TaskStep Finish6CBF90(NativeBattleDuel112Frame frame, int slot, NativeBattleDuel112ActorServices services)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.DescriptorAddress != 0x2521950 || task.Callback != 0x6CBF90)
        {
            throw new InvalidDataException("Native Duel target completion requires its actual task owner.");
        }
        int actor = frame.Memory.Duel112?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Duel target completion requires its actual actor.");
        short phase = unchecked((short)task.ReadWord(12));
        NativeBattleGraphicsModel model = services.ReadModel(actor);
        int fadeOut = phase - 137;
        int fadeIn = phase - 145;
        if ((uint)fadeOut <= 6 || (uint)fadeIn <= 6)
        {
            uint black = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2521918, 4));
            bool outgoing = (uint)fadeOut <= 6;
            int weight = outgoing ? fadeOut >= 4 ? 4096 : fadeOut << 10 : (fadeIn << 12) / 6;
            uint color40 = Color(task.ReadDword(48), 0x02000000);
            uint color44 = Color(task.ReadDword(52), 0x32000000);
            model = model with { Color40 = color40, Color44 = color44, Flags0 = (ushort)(model.Flags0 | 0x800), Alpha7 = 0 };

            uint Color(uint saved, uint alpha) => NativeBattleDuelSharedPackets.InterpolateColor56CC00(outgoing ? saved : black, outgoing ? black : saved, 4096 - weight, weight, alpha);
        }
        if (phase == 143)
        {
            model = model with { Flags0 = (ushort)(model.Flags0 | 0xC) };
        }
        else if (phase == 145)
        {
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel restore requires its actual actor position fields.");
            model = model with { Flags0 = (ushort)(model.Flags0 & 0xFFF3), Transform = transform with { Position28 = new(unchecked((short)task.ReadWord(60)), unchecked((short)task.ReadWord(62)), unchecked((short)task.ReadWord(64))), Vertical34 = unchecked((short)task.ReadWord(66)) } };
        }
        else if (phase == 152)
        {
            model = model with { Color40 = task.ReadDword(48) & 0xFFFFFF, Color44 = (task.ReadDword(52) & 0xFFFFFF) | 0x32000000, Alpha7 = task.ReadByte(55), Flags0 = (ushort)(model.Flags0 & 0xF7FF) };
        }
        if (phase == 137)
        {
            NativeBattleEffectTaskPool particles = frame.Effect.Pools[4];
            if (particles.DescriptorAddress != 0x2521920 || particles.Stride != 48)
            {
                throw new InvalidDataException("Native Duel texture callback requires its actual shared debris bank.");
            }
            if (particles.Slots.Any(candidate => (candidate.Flags & 1) == 0))
            {
                (particles, int textureSlot) = particles.Append(0x6CD870);
                particles = particles with { Slots = particles.Slots.SetItem(textureSlot, particles.Slots[textureSlot] with { Payload = new byte[36].ToImmutableArray(), ResourcePointers = null, UnknownPayloadRanges = null }) };
            }
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(4, particles) } };
        }
        int result;
        if (phase >= 152)
        {
            model = model with { Flags0 = (ushort)(model.Flags0 & 0xEFFF) };
            result = 2;
        }
        else
        {
            task = task.WriteWord(12, unchecked((ushort)(phase + 1)));
            result = 0;
        }
        services.WriteModel(actor, model);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        return new(frame, pool, result);
    }

    public static NativeBattleDuel112TaskStep Advance6CD870(NativeBattleDuel112Frame frame, int slot, Func<NativeBattleDuel112Frame, uint, uint, NativeBattleDuel112Frame> appendCommonTexture508630)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[4];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.DescriptorAddress != 0x2521920 || task.Callback != 0x6CD870)
        {
            throw new InvalidDataException("Native Duel texture task requires its actual physical bank.");
        }
        if (task.ReadWord(12) == 0)
        {
            uint arena = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x25218E8, 4));
            frame = appendCommonTexture508630(frame, unchecked(arena + 0x1E88), checked(pool.BankAddress + (uint)(slot * pool.Stride) + 14));
            pool = frame.Effect.Pools[4];
            task = pool.Slots[slot];
        }
        int result = task.ReadWord(14) != 0 ? 2 : 0;
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(4, pool) } };
        return new(frame, pool, result);
    }
}
