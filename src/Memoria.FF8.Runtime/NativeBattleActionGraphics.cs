using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>50BF90's copied globals; header and contact storage remain in their canonical physical arenas.</summary>
public sealed record NativeBattleActionGraphicsState([property: JsonRequired] int HeaderIndex, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] int FirstContactIndex, [property: JsonRequired] ushort TargetMask, [property: JsonRequired] ushort CoverMask, [property: JsonRequired] byte? CoverSource, [property: JsonRequired] byte? FirstTarget, [property: JsonRequired] byte HitFlags, [property: JsonRequired] NativeBattleActionDataReference? PendingData, [property: JsonRequired] uint Callback)
{
    [JsonRequired]
    public bool? UseActionCamera { get; init; }

    [JsonRequired]
    public ushort? ReflectionWatchdog4A { get; init; }
    public ushort? CastingWatchdog8E { get; init; }
    public NativeBattleGuardianServiceState? Guardian { get; init; }
}
public sealed record NativeBattleGuardianServiceState(byte HiddenPartyMask47, byte CameraSelector4E, ushort Control44, byte? Neighbor70 = null);
public sealed record NativeBattleActionServiceSelection(uint Callback, uint SetGraphicsFlags = 0, byte? Sequence = null);

public static class NativeBattleActionGraphics
{
    public static bool IsModelBusy(NativeBattleGraphicsModel model, ushort resourceMask)
    {
        return (model.Flags0 & 8) == 0 && ((model.Flags8 ?? throw new NotSupportedException("Native508540 requires its reached model DWORD8.")) & 26) == 0 && ((model.Kind5 ?? throw new NotSupportedException("Native508540 requires its reached model BYTE5.")) != 0 || ((model.ResourceFlags44 ?? throw new NotSupportedException("Native508540 requires its reached resource WORD44.")) & resourceMask) == 0);
    }

    public static NativeBattleActionGraphicsState Normalize(NativeBattleActionPacketsState packets, int headerIndex, NativeBattleActionGraphicsState? previous = null)
    {
        packets.Validate();
        if ((uint)headerIndex >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new InvalidDataException("Native50BF90 header pointer is outside its physical arena.");
        }
        NativeBattleActionHeaderSlot first = packets.Headers[headerIndex];
        byte actor = first.ReadByte(0);
        int firstContact = first.FirstContactIndex ?? throw new NotSupportedException("Native50BF90 requires its actual header contact pointer.");
        int groups = first.ReadByte(17) + 1;
        ushort targets = 0;
        ushort cover = 0;
        byte? source = previous?.CoverSource;
        byte flags = 0;
        for (int group = 0; group < groups; group++)
        {
            int index = checked(headerIndex + group);
            if (index >= NativeBattleActionPacketsState.HeaderCapacity)
            {
                throw new NotSupportedException("Native50BF90 repeated header crosses into an unowned adjacent arena.");
            }
            NativeBattleActionHeaderSlot header = packets.Headers[index];
            int count = header.ReadByte(16);
            if (count == 0)
            {
                continue;
            }
            int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native repeated header lacks its physical contact binding.");
            for (int target = 0; target < count; target++)
            {
                NativeBattleContactHalf half = packets.ReadContact(checked(contact + target)).Primary;
                targets |= (ushort)(1 << half.ActorIndex);
                if ((half.HitFlags3 & 8) != 0)
                {
                    source = (byte)(half.HitFlags3 >> 6);
                    cover |= (ushort)(1 << source.Value);
                }
                if ((half.Flags2 & 0x30) == 0x30)
                {
                    flags |= 2;
                }
            }
        }
        byte firstCount = first.ReadByte(16);
        byte? firstTarget = firstCount != 0 ? packets.ReadContact(firstContact).Primary.ActorIndex : previous?.FirstTarget;
        NativeBattleActionServiceSelection selection = SelectService(first);
        return new(headerIndex, actor, firstContact, targets, cover, source, firstTarget, flags, first.Data, selection.Callback);
    }

    public static NativeBattleActionServiceSelection SelectService(NativeBattleActionHeaderSlot header)
    {
        byte command = header.ReadByte(1);
        if (command == 0)
        {
            return new(header.ReadByte(16) != 0 ? 0x50BD80u : 0x50BD00u);
        }
        if (command is 0x26 or 0xF4 or 0xFE)
        {
            return Word(header, 4) is 70 or 15 ? new(0x50A9A0, 0x40000000, 11) : new(0x50B2A0);
        }
        uint special = command switch
        {
            0xEC or 0xF5 => 0x50B830,
            0xED or 0xEE => 0x50BEE0,
            0xF1 => 0x50BDC0,
            0xF7 => 0x50B0C0,
            0xFC => 0x50B190,
            _ => 0
        };
        if (special != 0)
        {
            return new(special);
        }
        uint setFlags = command == 0x1C ? 0x10000000u : 0;
        uint callback = Word(header, 4) == 65535 ? 0x50BC20u : Word(header, 6) == 0 ? 0x50BB00u : header.ReadByte(2) != 0 ? 0x50A9A0u : 0x50B190u;
        return new(callback, setFlags);
    }

    public static ushort Word(NativeBattleActionHeaderSlot header, int offset) => (ushort)(header.ReadByte(offset) | header.ReadByte(offset + 1) << 8);
}

public sealed record NativeBattleEffectBinding(ushort Effect, uint Entry, uint Initializer)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBlizzardResources? BlizzardResources { get; init; }
    public bool? InitializerIsRet { get; init; }
    public NativeBattleEffectInitializerAsset? InitializerAsset { get; init; }
    public NativeBattleEffectInitializerResource? InitializerResource { get; init; }
    public NativeBattleEffectProgramResources? ProgramResources { get; init; }
    public NativeBattleEffect3Resources? Effect3Resources { get; init; }
    public NativeBattleQuezacotlResources? NativeQuezacotlResources { get; init; }
    public NativeBattleCureResources? CureResources { get; init; }
    public NativeBattleCuraResources? CuraResources { get; init; }
    public NativeBattleCuragaResources? CuragaResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRestorationResources? RestorationResources { get; init; }
}

public sealed record NativeBattleEffectInitializerAsset(string Name, System.Collections.Immutable.ImmutableArray<byte> Data);
public sealed record NativeBattleEffectInitializerResource(string Name, uint GlobalAddress);

public sealed record NativeBattleEffectProgramResources(NativeBattleEffectHeapAllocation Animation, System.Collections.Immutable.ImmutableArray<NativeBattleModuleResource> Sprites, uint Sound);
