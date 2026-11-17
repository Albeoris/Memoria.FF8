using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private static NativeBattleEffectArena CaptureNativeEffect301Model(NativeBattleGraphicsModel model)
    {
        NativeBattleEffectArena bytes = CaptureNativeEffect210Model(model);
        if (model.Color40 is uint color40)
        {
            bytes = bytes.Write(0x28, BitConverter.GetBytes(color40));
        }
        if (model.Color44 is uint color44)
        {
            bytes = bytes.Write(0x2C, BitConverter.GetBytes(color44));
        }
        if (model.SequenceState is { } sequence)
        {
            bytes = bytes.Write(0x7C, BitConverter.GetBytes(sequence.MeshMask124));
        }
        return bytes;
    }

    private static NativeBattleGraphicsModel ApplyNativeEffect301ModelBytes(NativeBattleGraphicsModel model, int offset, NativeBattleEffectArena data, int length)
    {
        if (offset == 0x2C && length == 3)
        {
            uint color = model.Color44 ?? throw new NotSupportedException("Effect301 requires its current packed model color before its RGB store.");
            byte[] bytes = data.Read(0, 3);
            uint rgb = bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16);
            return model with { Color44 = (color & 0xFF000000) | rgb };
        }
        if (offset == 0x2F && length == 1)
        {
            uint color = model.Color44 ?? throw new NotSupportedException("Effect301 requires its current packed model color before its code-byte store.");
            return model with { Color44 = (color & 0xFFFFFF) | ((uint)data.Read(0, 1)[0] << 24) };
        }
        if (offset == 0 && length == 1)
        {
            return model with { Flags0 = unchecked((ushort)((model.Flags0 & 0xFF00) | data.Read(0, 1)[0])) };
        }
        return ApplyNativeEffect174ModelBytes(model, offset, data, length);
    }

    private static NativeBattleEffect301Frame ApplyNativeEffect301PartyVisibility(ref NativeBattleGraphicsState graphics, NativeBattleEffect301Frame frame, bool restore)
    {
        bool original = graphics.Effects.StorageContract == BattleContactTaskStorageContract.OriginalArena;
        if (!restore)
        {
            BattleActorModels models = graphics.ActorModels;
            frame = frame with { Effect = frame.Effect with { Effect301SavedPartyFlags = models.Roster.Party.ToImmutableDictionary(actor => (BattleModelOwner)actor, actor => models.Get(actor).Flags0) } };
            if (original)
            {
                for (byte operand = 0; operand < 3; operand++)
                {
                    NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(operand);
                    ushort flags = graphics.NativeBindings.ReadOriginalCell(slot, models).Flags0;
                    frame.State.WriteDword(SavedPartyFlagsAddress + (uint)slot.Offset * 4, flags);
                }
            }
        }
        if (original)
        {
            for (byte operand = 0; operand < 3; operand++)
            {
                NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(operand);
                NativeBattleGraphicsModel before = graphics.NativeBindings.ReadOriginalCell(slot, graphics.ActorModels);
                ushort saved = restore ? unchecked((ushort)frame.State.ReadDword(SavedPartyFlagsAddress + (uint)slot.Offset * 4)) : (ushort)4;
                NativeBattleGraphicsModel after = before with { Flags0 = VisibilityFlags(before.Flags0, saved) };
                graphics = graphics.NativeBindings.FindOriginalOwner(slot, graphics.ActorModels) is { } owner
                    ? graphics with { ActorModels = graphics.ActorModels.Set(owner, after) }
                    : graphics with { NativeBindings = graphics.NativeBindings.WriteUnboundCell(slot, after) };
            }
        }
        else
        {
            foreach (BattlePartyActorModel owner in graphics.ActorModels.Roster.Party)
            {
                if (!frame.Effect.Effect301SavedPartyFlags.TryGetValue(owner, out ushort saved))
                {
                    throw new InvalidDataException("Effect301 party visibility requires its captured model owner.");
                }
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(owner, model with { Flags0 = VisibilityFlags(model.Flags0, restore ? saved : (ushort)4) }) };
            }
        }
        return frame;

        static ushort VisibilityFlags(ushort current, ushort saved) => (ushort)(current ^ ((current ^ saved) & 4));
    }
}
