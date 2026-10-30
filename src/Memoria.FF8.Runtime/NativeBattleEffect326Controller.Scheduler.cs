using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Frame Advance(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.State.FixedBanks[RootBank] is not null || frame.State.FixedBanks[ChildBank] is not null)
        {
            throw new InvalidDataException("Effect326 requires its exclusively active physical task banks.");
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        if (survivors != 0)
        {
            return frame;
        }
        NativeBattleEffect326State state = frame.State;
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            state = state.AttachBank(pool.BankAddress, NativeBattleEffectTaskMemory.CaptureBank(pool));
        }
        return frame with { State = state, Effect = frame.Effect with { Pools = [] } };
    }

    private static (NativeBattleEffect326Frame Frame, int Survivors) AdvancePool(NativeBattleEffect326Frame frame, int index, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleEffect326Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            NativeBattleEffect326Inputs selectedInputs = inputs.CapturePresentation ? inputs with { PresentationTask = NativeBattlePilotVisualLifetimes.Identity(326, pool.Slots[slot]) } : inputs;
            NativeBattleEffect326Step result = pool.Slots[slot].Callback switch
            {
                RootCallback => AdvanceRoot6326B0(current, address, resources, selectedInputs),
                MainCallback => AdvanceMain62A9C0(current, address, resources, selectedInputs),
                0x63E9C0 => AdvanceCamera63E9C0(current, address, selectedInputs),
                0x62B080 or 0x62B4F0 => AdvanceOpening(current, address, selectedInputs),
                0x62B630 => AdvanceFalling62B630(current, address, resources, selectedInputs),
                0x62BB40 => AdvanceTexture62BB40(current, address, resources, selectedInputs),
                0x62BC70 => AdvanceScrollMesh62BC70(current, address, resources, selectedInputs),
                0x62BDA0 => AdvanceFocalMesh62BDA0(current, address, resources, selectedInputs),
                0x62BF40 or 0x62C070 => AdvanceClosingGrid(current, address, selectedInputs),
                0x62C2F0 => AdvanceClosingMesh62C2F0(current, address, resources, selectedInputs),
                0x62C430 => AdvanceClosingCenter62C430(current, address, resources, selectedInputs),
                0x62C550 => AdvanceEmbeddedTrail62C550(current, address, resources, selectedInputs),
                0x62C8F0 => AdvanceEmbeddedAttachment62C8F0(current, address, resources, selectedInputs),
                0x62CA80 or 0x62CC00 => AdvanceFinalOwnedModel(current, address, resources, selectedInputs),
                0x62CD40 => AdvanceFinalTint62CD40(current, address, selectedInputs),
                0x62CEA0 => AdvanceTextureRelease62CEA0(current, address, selectedInputs),
                0x62CEE0 => AdvanceModel62CEE0(current, address, resources, selectedInputs),
                0x62E4F0 => AdvanceRadial62E4F0(current, address, resources, selectedInputs),
                0x62E8C0 or 0x62EA10 => AdvanceRingMesh(current, address, resources, selectedInputs),
                0x62EB50 or 0x62EE30 => AdvanceLateParticles(current, address, resources, selectedInputs),
                0x62F220 => AdvanceTint62F220(current, address, selectedInputs),
                0x62F2B0 => AdvanceFlash62F2B0(current, address, selectedInputs),
                0x62F400 => AdvanceMorphMesh62F400(current, address, resources, selectedInputs),
                0x62F740 or 0x62FC90 => AdvanceBoneParticles(current, address, resources, selectedInputs),
                0x630160 => AdvanceLateBase630160(current, address, resources, selectedInputs),
                0x630430 => AdvanceRibbon630430(current, address, resources, selectedInputs),
                0x630870 => AdvanceLateBurst630870(current, address, resources, selectedInputs),
                0x630AB0 => AdvanceHistoryBurst630AB0(current, address, resources, selectedInputs),
                0x630D30 => AdvanceLatePair630D30(current, address, resources, selectedInputs),
                0x6312F0 or 0x6321A0 or 0x632350 or 0x632500 => AdvanceTargetTrail326(current, address, resources, selectedInputs),
                uint callback => throw new NotSupportedException($"Effect326 reached unsupported native callback{callback:X8}.")
            };
            current = result.Frame;
            return new(current.Effect.Pools[index], result.Result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect326Step AdvanceRoot6326B0(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }
}
