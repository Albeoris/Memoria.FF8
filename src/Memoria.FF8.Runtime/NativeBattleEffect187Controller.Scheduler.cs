using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Frame Advance(NativeBattleEffect187Frame frame, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.State.FixedBanks[RootBank] is not null || frame.State.FixedBanks[ChildBank] is not null)
        {
            throw new InvalidDataException("Effect187 requires its exclusively active physical task banks.");
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        if (survivors != 0)
        {
            return frame;
        }
        NativeBattleEffect187State state = frame.State;
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            state = state.AttachBank(pool.BankAddress, NativeBattleEffectTaskMemory.CaptureBank(pool));
        }
        return frame with { State = state, Effect = frame.Effect with { Pools = [] } };
    }

    private static (NativeBattleEffect187Frame Frame, int Survivors) AdvancePool(NativeBattleEffect187Frame frame, int index, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleEffect187Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            NativeBattleEffect187Step result = pool.Slots[slot].Callback switch
            {
                RootCallback => AdvanceRoot64DD50(current, address, resources, inputs),
                MainCallback => AdvanceMain6474C0(current, address, resources, inputs),
                0x63E9C0 => AdvanceCamera63E9C0(current, address, inputs),
                0x6479D0 or 0x647E40 => AdvanceOpening(current, address, inputs),
                0x647F80 => AdvanceFalling647F80(current, address, resources, inputs),
                0x648AF0 => AdvanceActorOverlay648AF0(current, address, inputs),
                0x648490 => AdvanceTexture648490(current, address, resources, inputs),
                0x6485C0 => AdvanceScrollMesh6485C0(current, address, resources, inputs),
                0x6486F0 => AdvanceFocalMesh6486F0(current, address, resources, inputs),
                0x649CC0 => AdvanceTextureRelease649CC0(current, address, inputs),
                0x649D00 => AdvanceModel649D00(current, address, resources, inputs),
                0x64A9D0 or 0x64ACB0 => AdvanceLateParticles(current, address, resources, inputs),
                0x64B130 => AdvanceFlash64B130(current, address, inputs),
                0x64A370 => AdvanceRadial64A370(current, address, resources, inputs),
                0x64A740 or 0x64A890 => AdvanceRingMesh(current, address, resources, inputs),
                0x64B0A0 => AdvanceTint64B0A0(current, address, inputs),
                0x64B300 => AdvanceMorphMesh64B300(current, address, resources, inputs),
                0x64B640 or 0x64BB90 => AdvanceBoneParticles(current, address, resources, inputs),
                0x64C060 => AdvanceModelOverlay64C060(current, address, resources, inputs),
                0x64C330 => AdvanceRibbons64C330(current, address, resources, inputs),
                0x64C7F0 or 0x64D6A0 or 0x64D930 or 0x64DAD0 => AdvanceTrailMesh(current, address, resources, inputs),
                0x64DCF0 => AdvanceActorFlags64DCF0(current, address, inputs),
                uint callback => throw new NotSupportedException($"Effect187 reached unsupported native callback{callback:X8}.")
            };
            current = result.Frame;
            return new(current.Effect.Pools[index], result.Result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect187Step AdvanceRoot64DD50(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }
}
