namespace Memoria.FF8.Runtime;

/// <summary>Owned fields of one156-byte native graphics model, distinct from the208-byte battle participant.</summary>
public sealed partial record NativeBattleGraphicsModel(int Index, ushort Flags0)
{
    public byte? ModelId4 { get; init; }
    public byte? Kind5 { get; init; }
    public byte? Phase6 { get; init; }
    public byte? Alpha7 { get; init; }
    public byte? EffectCounter97 { get; init; }
    public uint? Color40 { get; init; }
    public uint? Color44 { get; init; }
    public NativeBattleEffectLocalBounds? GeometryBounds52 { get; init; }
    public short? MinimumY146 { get; init; }
    public byte? CameraSequence130 { get; init; }
    public byte? CameraAccumulator131 { get; init; }
    public bool? HasLinkedModelRing140 { get; init; }
    public byte? BlinkCountdown144 { get; init; }
    public byte? SequenceByte145 { get; init; }
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
    public ushort? TextureAllocationMask { get; init; }
    public NativeBattleModelTextureBinding? BodyTextureSlots { get; init; }
    public int? TextureResourceIndex { get; init; }

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

    public static NativeBattleGraphicsModel CompleteLoad(NativeBattleGraphicsModel model, byte modelId, uint projectedStatusFlags, ref NativeGlobalRandomState random, byte? information246 = null)
    {
        //5027D0 initializes BYTE145 from model identity and48B9F0's original information BYTE246.
        byte sequenceByte145 = modelId < 16 ? (byte)0 : InitializeSequenceByte145(information246 ?? throw new InvalidDataException("Native5027D0 requires the enemy's original information BYTE246."));
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
        //5027D0 copies resourceDWORD100 to originalDWORD104, ending any temporary magic-resource override.
        return model with { Flags0 = 3, Flags2 = 4, ModelId4 = modelId, Kind5 = 0, Phase6 = null, Alpha7 = null, MinimumY146 = 0, CameraSequence130 = (byte)(frame % 6), CameraAccumulator131 = (byte)appearance, HasLinkedModelRing140 = false, LinkedModelIndex140 = null, BlinkCountdown144 = (byte)(blink % 45 + 15), SequenceByte145 = sequenceByte145, Flags8 = projectedStatusFlags, Flags8KnownMask = 0, Flags8KnownBits = 0, StatusTimers = timers, Rotation12 = 0, Rotation14 = model.Index >= 3 ? (short)2048 : (short)0, Rotation16 = 0, ResourceFlags44 = 1, ResourceMagicId = null, Sequence = 8, SequenceFrame = 0 };
    }

    private static byte InitializeSequenceByte145(byte information246)
    {
        if ((information246 & 8) != 0)
        {
            return 5;
        }
        if ((information246 & 4) != 0)
        {
            return 4;
        }
        return unchecked((byte)(3 - information246));
    }

    public static NativeBattleGraphicsModel SelectSequence(NativeBattleGraphicsModel model, byte sequence, bool overrideStatus = false)
    {
        if ((model.Flags0 & 2) == 0 || model.Flags8 is not uint flags)
        {
            throw new InvalidOperationException("Selecting a native graphics sequence requires its loaded model owner.");
        }
        if (sequence == 0 || model.ModelId4 == 0x8F || !overrideStatus && (flags & 0x18) != 0)
        {
            return model;
        }
        //505C00 strips the1000 override before505BC0; it bypasses only DWORD8's18 gate.
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
            kind = 13;
        }
        if (kind == 1 && ((model.Flags8 ?? throw new NotSupportedException("Native50C090 kind1 requires its model status DWORD8.")) & 0x100000) != 0)
        {
            kind = 10;
        }
        if (kind == 3 && ((model.Flags8 ?? throw new NotSupportedException("Native50C090 kind3 requires its actual model status DWORD8.")) & 0x100000) != 0)
        {
            kind = 11;
        }
        return model with { Kind5 = kind, Phase6 = 0, Flags0 = kind is 2 or 12 ? (ushort)(model.Flags0 & ~4) : model.Flags0, Flags2 = kind is 2 or 12 ? (ushort)(flags2 & ~0x80) : flags2 };
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

    public static NativeBattleGraphicsModel AdvanceAppearance(NativeBattleGraphicsModel model, bool graphicsFrozen, NativeBattleSummonColors colors)
    {
        if (model.Kind5 is not (1 or 2))
        {
            return model;
        }
        byte phase = unchecked((byte)((model.Phase6 ?? throw new InvalidDataException("Native appearance lacks phaseBYTE6.")) + 1));
        bool complete = phase >= 15;
        byte alpha = complete ? (byte)0 : (byte)(255 * ((phase << 12) / 15) >> 12);
        if (complete)
        {
            model = model with { Color44 = (colors.Normal & 0xFFFFFFu) | 0x32000000u };
        }
        else
        {
            int progress = (phase << 12) / 15;
            uint current = model.Color40 ?? throw new NotSupportedException("Native50C230 requires its retained model color DWORD40.");
            model = model with { Color40 = (colors.Interpolate(0, NativeBattleCameraMath.Sine(progress / 4), current) & 0xFFFFFFu) | 0x02000000u, Color44 = (colors.Interpolate(0, progress, colors.Normal) & 0xFFFFFFu) | 0x32000000u };
        }
        return model with { Kind5 = complete ? (byte)0 : model.Kind5, Alpha7 = alpha, Phase6 = graphicsFrozen ? phase : unchecked((byte)(phase + 1)) };
    }

    public static NativeBattleGraphicsRemoval AdvanceRemoval(NativeBattleGraphicsModel model, bool graphicsFrozen, bool soundBusy, byte cameraTaskMask, NativeBattleSummonColors? colors = null)
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
            else if (!soundBusy && cameraTaskMask == 0)
            {
                return new(model with { Flags0 = 0, Kind5 = 0 }, false);
            }
            //50C110 case5 deliberately falls through50C410 while cleanup remains blocked.
        }
        NativeBattleSummonColors fadeColors = colors ?? throw new NotSupportedException("Native removal fade requires its authenticated current color producers.");
        model = model.Kind5 == 7 ? NativeBattleModelFade.Advance50C560(model, graphicsFrozen, fadeColors) : NativeBattleModelFade.Advance50C410(model, graphicsFrozen, fadeColors);
        if (model.Kind5 == 0)
        {
            model = StartService(model, 5);
        }
        return new(model, release);
    }
}
