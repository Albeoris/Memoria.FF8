namespace Memoria.FF8.Runtime;

/// <summary>Owned fields of one156-byte native graphics model, distinct from the208-byte battle participant.</summary>
public sealed record NativeBattleGraphicsModel(int Index, ushort Flags0)
{
    public byte? ModelId4 { get; init; }
    public byte? Kind5 { get; init; }
    public byte? Phase6 { get; init; }
    public byte? Alpha7 { get; init; }
    public byte? AppearanceFrame129 { get; init; }
    public byte? AppearancePhase130 { get; init; }
    public byte? BlinkCountdown144 { get; init; }
    public ushort? Flags2 { get; init; }
    public uint? Flags8 { get; init; }
    public short? Rotation12 { get; init; }
    public short? Rotation14 { get; init; }
    public short? Rotation16 { get; init; }
    public ushort? ResourceMagicId { get; init; }
    public NativeBattleModelTransform? Transform { get; init; }
    public System.Collections.Immutable.ImmutableArray<byte?> StatusTimers { get; init; } = System.Collections.Immutable.ImmutableArray.CreateRange(new byte?[32]);
    public ushort? ResourceFlags44 { get; init; }
    public byte? Sequence { get; init; }
    public int SequenceFrame { get; init; }
    public NativeBattleSequenceState? SequenceState { get; init; }

    public NativeBattleGraphicsModelCompletion CompletionInputs()
    {
        if ((Flags0 & 2) == 0)
        {
            return new(Flags0, 0, 0, null);
        }
        return new(Flags0, Kind5 ?? throw new InvalidDataException("Loaded graphics model lacks native Kind5."), Flags8 ?? throw new InvalidDataException("Loaded graphics model lacks native Flags8."), ResourceFlags44);
    }
}

public sealed record NativeBattleGraphicsRemoval(NativeBattleGraphicsModel Model, bool ReleaseParticipant);

public static class NativeBattleGraphicsModels
{
    public static NativeBattleGraphicsModel[] ClearBank()
    {
        //502340 clears only Flags0 of each of seven records. Unknown retained fields must not be supplied as zeros.
        return Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToArray();
    }

    public static NativeBattleGraphicsModel CompleteLoad(NativeBattleGraphicsModel model, byte modelId, uint projectedStatusFlags, ref NativeGlobalRandomState random)
    {
        //5027D0 establishes Flags0=3,Flags2=4,Kind5=0,Flags8=0 then50A070 applies saved status projection.
        //505BC0(8) selects the original entry sequence and writes resourceWORD44=1.
        random = random.Draw(out ushort frame);
        random = random.Draw(out ushort appearance);
        random = random.Draw(out ushort blink);
        System.Collections.Immutable.ImmutableArray<byte?> timers = model.StatusTimers;
        for (int bit = 0; bit < 32; bit++)
        {
            if ((projectedStatusFlags & (1u << bit)) != 0)
            {
                timers = timers.SetItem(bit, 0);
            }
        }
        return model with { Flags0 = 3, Flags2 = 4, ModelId4 = modelId, Kind5 = 0, Phase6 = null, Alpha7 = null, AppearanceFrame129 = (byte)(frame % 6), AppearancePhase130 = (byte)appearance, BlinkCountdown144 = (byte)(blink % 45 + 15), Flags8 = projectedStatusFlags, StatusTimers = timers, Rotation12 = 0, Rotation14 = model.Index >= 3 ? (short)2048 : (short)0, Rotation16 = 0, ResourceFlags44 = 1, Sequence = 8, SequenceFrame = 0 };
    }

    public static NativeBattleGraphicsModel SelectSequence(NativeBattleGraphicsModel model, byte sequence)
    {
        if ((model.Flags0 & 2) == 0 || model.Flags8 is not uint flags)
        {
            throw new InvalidOperationException("Selecting a native graphics sequence requires its loaded model owner.");
        }
        if (sequence == 0 || model.ModelId4 == 0x8F || (flags & 0x18) != 0)
        {
            return model;
        }
        //505C00 ->505BC0, without a linked-model ring or1000override.
        return model with { Sequence = sequence, SequenceFrame = 0, ResourceFlags44 = 1, SequenceState = model.SequenceState is null ? null : model.SequenceState with { Offset46 = 0, Pending3 = 0, Callback42 = 0 } };
    }

    public static NativeBattleGraphicsModel StartRemoval(NativeBattleGraphicsModel model, byte kind)
    {
        return StartService(model, kind);
    }

    public static NativeBattleGraphicsModel StartService(NativeBattleGraphicsModel model, byte kind)
    {
        if ((model.Flags0 & 2) == 0 || model.Flags2 is not ushort flags2)
        {
            throw new InvalidOperationException("Native graphics service requires its loaded model owner.");
        }
        //50C090's kind6 is redirected to13 when Flags2&10; that different service has its own effect producer.
        if (kind == 6 && (flags2 & 0x10) != 0)
        {
            throw new NotSupportedException("Native kind13 removal requires the50C4D0 effect owner.");
        }
        if (kind == 1 && (model.Flags8 & 0x100000) != 0)
        {
            throw new NotSupportedException("Native kind10 appearance requires the50C330 effect owner.");
        }
        if (kind is not (1 or 2 or 5 or 6 or 7 or 9))
        {
            throw new NotSupportedException($"Native graphics removal kind{kind} requires its own original service.");
        }
        return model with { Kind5 = kind, Phase6 = 0, Flags0 = kind == 2 ? (ushort)(model.Flags0 & ~4) : model.Flags0, Flags2 = kind == 2 ? (ushort)(flags2 & ~0x80) : flags2 };
    }

    public static NativeBattleGraphicsModel AdvanceCover(NativeBattleGraphicsModel model)
    {
        if (model.Kind5 != 9)
        {
            return model;
        }
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native50C6D0 Cover movement requires the original model positionsWORD24/30/32.");
        byte phase = model.Phase6 ?? throw new InvalidDataException("Native Cover service lacks its BYTE6 phase.");
        NativeBattleVector position = transform.Position28;
        byte kind = 9;
        if (phase >= 15)
        {
            int step = phase - 15;
            if (step < 2)
            {
                int progress = (step + 1) << 12;
                short z = unchecked((short)(transform.Initial20.Z + (160 * (4 * (progress / -2) - progress / 2) >> 12) + 800));
                short y = unchecked((short)(-100 * NativeBattleCameraMath.Sine(progress / 2 / 2) >> 12));
                position = position with { Y = y, Z = z };
            }
            else
            {
                position = position with { Z = transform.Initial20.Z };
                kind = 0;
            }
        }
        else if (phase == 0)
        {
            position = position with { Z = unchecked((short)(transform.Initial20.Z + 800)) };
        }
        //50C6D0 advances even under graphics pause bit1, unlike the fade services.
        return model with { Kind5 = kind, Phase6 = unchecked((byte)(phase + 1)), Transform = transform with { Position28 = position } };
    }

    public static NativeBattleGraphicsModel AdvanceAppearance(NativeBattleGraphicsModel model, bool graphicsFrozen)
    {
        if (model.Kind5 is not (1 or 2))
        {
            return model;
        }
        byte phase = unchecked((byte)((model.Phase6 ?? throw new InvalidDataException("Native appearance lacks phaseBYTE6.")) + 1));
        bool complete = phase >= 15;
        byte alpha = complete ? (byte)0 : (byte)(255 * ((phase << 12) / 15) >> 12);
        return model with { Kind5 = complete ? (byte)0 : model.Kind5, Alpha7 = alpha, Phase6 = graphicsFrozen ? phase : unchecked((byte)(phase + 1)) };
    }

    public static NativeBattleGraphicsRemoval AdvanceRemoval(NativeBattleGraphicsModel model, bool graphicsFrozen, bool modelLoadActive, byte cameraTaskMask)
    {
        if (model.Kind5 is not (5 or 6 or 7))
        {
            return new(model, false);
        }
        byte phase = model.Phase6 ?? throw new InvalidDataException("Native removal service lacks its owned phaseBYTE6.");
        bool release = false;
        if (model.Kind5 == 5)
        {
            if (phase == 0)
            {
                //50C5F0 releases the battle allocation before waiting for graphical cleanup.
                bool immediate = model.ModelId4 == 0x8F;
                if (immediate)
                {
                    return new(model with { Phase6 = 1, Flags0 = 0, Kind5 = 0 }, true);
                }
                model = model with { Phase6 = 1 };
                phase = 1;
                release = true;
            }
            else if (!modelLoadActive && cameraTaskMask == 0)
            {
                return new(model with { Flags0 = 0, Kind5 = 0 }, false);
            }
            //50C110 case5 deliberately falls through50C410 while cleanup remains blocked.
        }
        if (phase >= 15)
        {
            ushort flags2 = model.Flags2 ?? throw new InvalidDataException("Native removal service lacks its model Flags2.");
            //50C410/50C560 completion clears Kind5, then50C110 selects kind5 and resets phase6.
            return new(model with { Flags0 = (ushort)(model.Flags0 | 4), Flags2 = (ushort)(flags2 | 0x80), Kind5 = 5, Phase6 = 0 }, release);
        }
        byte? alpha = model.Alpha7;
        if (model.Kind5 is 5 or 6)
        {
            int progress = (phase << 12) / 15;
            //50C410 writes unsignedBYTE7 after its signedDWORD shift; scale/color services do not change this lifetime.
            alpha = unchecked((byte)((-255 * progress >> 12) - 1));
        }
        return new(model with { Alpha7 = alpha, Phase6 = graphicsFrozen ? phase : (byte)(phase + 1) }, release);
    }
}
