using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The reached B5/B6/B8 routes through505AB0,5015A0 and501740.</summary>
public static class NativeBattleSequenceAudio
{
    public static NativeBattleModuleSound? Resolve505AB0(NativeBattleSequenceSound sound, int modelIndex, byte? firstTarget, Func<int, byte> readModelId, Func<int, NativeBattleVector> readBone241, Func<NativeBattleVector, byte> readPan, ImmutableArray<uint>? actorSounds1120, byte? contactFlags3 = null)
    {
        ushort mask = sound.Attributes;
        if ((mask & 0xFF) != 0 || (sound.Flags & 2) == 0 && mask != 0)
        {
            throw new InvalidDataException("Native505AB0 attributes require the optional BYTE shifted left8 exactly when operand bit2 is set.");
        }
        byte pan;
        NativeBattleModuleAnchor? anchor = null;
        if ((sound.Flags & 4) != 0)
        {
            pan = 128;
        }
        else
        {
            int source = (sound.Flags & 1) != 0 ? firstTarget ?? throw new NotSupportedException("Native505AB0 target-relative sound requires the retained1D99AAA recipient.") : modelIndex;
            NativeBattleVector position = readBone241(source);
            anchor = new(position.X, position.Y, position.Z, 0);
            pan = readPan(position);
        }
        bool overridden = sound.OverrideOnContact4 && ((contactFlags3 ?? throw new NotSupportedException("Native conditional sequence sound requires its current contact BYTE3.")) & 4) != 0;
        uint effect = overridden ? 31u : sound.SoundId;
        if (sound.ActorSound && !overridden)
        {
            byte identity = readModelId(modelIndex);
            //5015A0 returns without calling46B2A0 when either original operand is outside its table.
            if (identity >= 160 || sound.SoundId >= 7)
            {
                return null;
            }
            ImmutableArray<uint> table = actorSounds1120 ?? throw new NotSupportedException("Native5015A0 requires the authenticated B8A418 actor sound table.");
            if (table.IsDefault || table.Length != 160 * 7)
            {
                throw new InvalidDataException("Native5015A0 actor sound table requires1120 DWORDs.");
            }
            effect = table[identity * 7 + sound.SoundId];
        }
        //Zero is an original sound ID too:5015A0/501740 do not suppress its46B2A0 call.
        return new(effect, mask, anchor) { Pan = pan };
    }
}
