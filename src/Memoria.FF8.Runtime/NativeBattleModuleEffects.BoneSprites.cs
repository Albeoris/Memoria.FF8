using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModuleState
{
    private NativeBattleModuleCallback AdvanceBoneSpriteSeed(int slot, NativeBattleModuleInputs inputs, int scaleMultiplier = 1)
    {
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, []);
        }
        NativeBattleModuleTask task = Slots[slot];
        byte[] payload = NativeBattleTelemetry.Materialize(1054, task.Payload.AsSpan());
        short phase = S16(task.Payload, 0);
        int model = ModelIndex(U32(task.Payload, 20));
        NativeBattleModuleState state = this;
        ImmutableArray<NativeBattleModuleDraw>.Builder draws = ImmutableArray.CreateBuilder<NativeBattleModuleDraw>();
        if (phase == 0)
        {
            NativeBattleModuleAnchor anchor = (inputs.ReadModelAnchor ?? throw new NotSupportedException("Native573560 requires its current571400 model anchor."))(model);
            WriteAnchor(payload.AsSpan(4), anchor);
            draws.Add(new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = anchor, Sound = 19 });
            draws.Add(new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = anchor, Sound = 20 });
        }
        NativeCrtRandomState? random = inputs.CrtRandom;
        if (phase is >= 1 and <= 25)
        {
            byte bones = (inputs.ReadModelBoneCount ?? throw new NotSupportedException("Native573560 requires its current skeleton bone-count BYTE."))(model);
            if (bones == 0)
            {
                throw new InvalidDataException("Native573560 cannot divide its CRT value by a zero skeleton bone count.");
            }
            (state, int child) = state.Append(NativeBattleModuleKind.BoneSprite573640);
            byte[] childPayload = NativeBattleTelemetry.Materialize(1055, state.Slots[child].Payload.AsSpan());
            W(childPayload, 0, 0);
            NativeCrtRandomState crt = random ?? throw new NotSupportedException("Native573560 requires the existing CRT random owner.");
            short fraction = checked((short)(Draw(ref crt) % 4096));
            byte bone = checked((byte)(Draw(ref crt) % bones));
            NativeBattleVector point = (inputs.ReadModelBone ?? throw new NotSupportedException("Native573560 requires its original502170 model bone projection."))(model, bone, fraction);
            //502170 writes XYZ only; the retained fourth WORD in the reused task is untouched.
            W(childPayload, 4, point.X);
            W(childPayload, 6, point.Y);
            W(childPayload, 8, point.Z);
            //5736E0 differs only here: LEA edx,[edx+edx+1400h] instead of573560's ADD edx,A00h.
            W(childPayload, 16, (Draw(ref crt) % 1152 + 2560) * scaleMultiplier);
            random = crt;
            state = state.StoreCastingPayload(child, childPayload);
        }
        short next = unchecked((short)(phase + 1));
        W(payload, 0, next);
        return new(state.StoreCastingPayload(slot, payload), next > 26, draws.ToImmutable()) { CrtRandomAfter = random };
    }

    private NativeBattleModuleCallback AdvanceBoneSprite(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        ushort phase = unchecked((ushort)S16(task.Payload, 0));
        NativeBattleModuleResource resource = inputs.Resources.SingleOrDefault(resource => resource.Id == 5) ?? throw new NotSupportedException("Native573640 requires original5710B0 sprite resource5.");
        NativeBattleModuleSpriteFrame frame = resource.SpriteFrame(phase);
        short scale = S16(task.Payload, 16);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = ReadAnchor(task.Payload, 4), Resource = resource, SpriteFrame = frame, Phase = phase, Scale = scale, DepthOffset = -(scale >> 3), SpriteFlags = 0x10, SpritePaletteOffsets = [17, 0, 0, 0] };
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = NativeBattleTelemetry.Materialize(1056, task.Payload.AsSpan());
        W(payload, 0, unchecked((short)(phase + 1)));
        return new(StoreCastingPayload(slot, payload), frame.NextOffset < 0, [draw]);
    }
}
