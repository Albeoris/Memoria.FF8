using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleScanByteOwner ReadNativeScanDescriptor208(BattleModelOwner owner)
    {
        if (hostedBattlePresentation)
        {
            return (hostedFrameServices?.ReadScanDescriptor ?? throw new InvalidOperationException("Hosted Scan requires its current canonical descriptor reader."))(owner);
        }
        return CaptureNativeScanDescriptor208(GraphicsActor(RequireNativeGraphics(), owner));
    }

    private static NativeBattleScanByteOwner CaptureNativeScanDescriptor208(BattleCombatant actor)
    {
        NativeBattleScanByteOwner row = new(new byte[208].ToImmutableArray(), new bool[208].ToImmutableArray());
        row = row.WriteDword(0x18, unchecked((uint)actor.CurrentHp)).WriteDword(0x1C, unchecked((uint)actor.MaximumHp)).WriteDword(0x7C, actor.NativeCombatantFlags).WriteByte(0xBB, checked((byte)NativeModelId(actor))).WriteByte(0xBC, checked((byte)actor.Level));
        if (actor.ElementDefenses.Count != 8)
        {
            throw new NotSupportedException("Native Scan requires all eight allocated element-defense WORDs.");
        }
        for (int element = 0; element < 8; element++)
        {
            row = row.WriteWord(0x44 + element * 2, checked((ushort)actor.ElementDefenses[element]));
        }
        return row.WriteByte(0xBD, checked((byte)EffectiveStat(actor, BattleStatId.Strength))).WriteByte(0xBE, checked((byte)EffectiveStat(actor, BattleStatId.Vitality))).WriteByte(0xBF, checked((byte)actor.Magic)).WriteByte(0xC0, checked((byte)EffectiveStat(actor, BattleStatId.Spirit))).WriteByte(0xC1, checked((byte)actor.Speed)).WriteByte(0xC3, checked((byte)actor.Evasion));
    }

    private byte[] ReadNativeScanEnemyInformation(BattleModelOwner owner)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (owner is not BattleEnemyActorModel)
        {
            throw new NotSupportedException("Native Scan enemy information requires its actual allocated enemy model.");
        }
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
        int originalRow = model.OriginalResourceRow104 ?? throw new NotSupportedException("Native Scan enemy information requires its original resource-row identity.");
        if (model.ActiveStagedResourceRow100 is not null || model.ActiveResourceRow100 != originalRow)
        {
            throw new NotSupportedException("Native Scan enemy information requires an established participant-to-information binding after resource replacement.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native Scan enemy information requires its loaded model CPU resources.");
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new NotSupportedException("Native Scan enemy information requires its original resource directory.");
        int modelId = hostedBattlePresentation
            ? (hostedFrameServices ?? throw new InvalidOperationException("Hosted Scan requires its current original actor identity.")).Facts[owner].OriginalModelId
            : GraphicsActor(graphics, owner).ModelId;
        if ((uint)originalRow >= cpu.Rows.Length || (uint)originalRow >= cache.Rows.Length || cache.Rows[originalRow].ModelId != checked(modelId + 16))
        {
            throw new NotSupportedException("Native Scan enemy information does not match its original participant resource identity.");
        }
        NativeBattleModelCpuResourceRow source = cpu.Rows[originalRow];
        int length = source.Lengths.TryGetValue(28, out int size) ? size : throw new NotSupportedException("Native Scan enemy information requires its actual DAT information extent.");
        if (length < 0xF8)
        {
            throw new InvalidDataException("Native Scan enemy information lacks its original flags BYTE F7.");
        }
        return NativeBattleModelEnemyHeap.Read(cpu, NativeFileStaging ?? throw new NotSupportedException("Native Scan enemy information requires its retained shared file bytes."), source.Require(28), length);
    }
    private NativeBattleScanByteOwner ReadNativeScanExtra71(BattleModelOwner owner)
    {
        if (hostedBattlePresentation)
        {
            return (hostedFrameServices?.ReadScanExtra ?? throw new InvalidOperationException("Hosted Scan requires its current canonical extra-text reader."))(owner);
        }
        return ReadNativeScanExtra71(GraphicsActor(RequireNativeGraphics(), owner));
    }

    private static NativeBattleScanByteOwner ReadNativeScanExtra71(BattleCombatant actor)
    {
        NativeBattleScanByteOwner bytes = new(new byte[71].ToImmutableArray(), new bool[71].ToImmutableArray());
        if (actor.NativeDisplayNameIndex is not int selected)
        {
            return bytes.WriteByte(0, 0);
        }
        byte[] name = actor.NativeAi?.NameBuffers[selected] ?? throw new NotSupportedException("Native Scan extra text requires its actual AI name buffer.");
        return bytes.Write(0, name);
    }

    private NativeBattleScanByteOwner ReadNativeScanName24(BattleModelOwner owner)
    {
        byte[] bytes = owner.Side == BattleSide.Enemies ? ReadNativeScanEnemyInformation(owner)[..24] : definition.NativeScanActorNames.GetValueOrDefault(RequireCanonicalGraphicsActorId(owner)) ?? throw new NotSupportedException("Native Scan requires its allocated player name bytes.");
        if (bytes.Length != 24)
        {
            throw new InvalidDataException("Native Scan actor name must retain its actual twenty-four-byte extent.");
        }
        return new(bytes.ToImmutableArray(), Enumerable.Repeat(true, 24).ToImmutableArray());
    }

    public NativeBattleScanTextExpansionServices CreateNativeScanTextExpansionServices()
    {
        return new(ReadNativeScanTextByte, _ => throw new NotSupportedException("Native Scan names are supplied by their canonical encoded content owner."))
        {
            ReadNameBytes = operand => definition.NativeScanNames.TryGetValue(operand, out byte[]? bytes) ? bytes.ToImmutableArray() : throw new NotSupportedException($"Native Scan replacement name{operand:X2} has no canonical content owner.")
        };
    }

    private byte ReadNativeScanTextByte(uint address)
    {
        if (address == NativeBattleEffect68Profile.ResultEmptyAddress)
        {
            return (NativeEffectMemory?.Effect68 ?? throw new NotSupportedException("Effect68 text requires its retained result header.")).Read(address, 1)[0];
        }
        NativeBattleScanProcess process = NativeEffectMemory?.Scan ?? throw new NotSupportedException("Native Scan text requires its retained process.");
        if (NativeBattleScanResults.TryReadBoundExtraByte(process.Results, address, ReadNativeScanExtra71, out byte value))
        {
            return value;
        }
        return NativeBattleScanResults.ReadByte(process.Results, address, ReadExternal);

        byte ReadExternal(uint pointer)
        {
            if (pointer >= 0x269AA78 && pointer < 0x269AA90)
            {
                return process.Presentation.Name269AA78.ReadByte((int)(pointer - 0x269AA78));
            }
            if (pointer >= NativeBattleScanResults.EnemyExtraAddress && pointer < NativeBattleScanResults.EnemyExtraAddress + 71 * 4)
            {
                int offset = (int)(pointer - NativeBattleScanResults.EnemyExtraAddress);
                NativeBattleGraphicsState graphics = RequireNativeGraphics();
                BattleModelOwner owner = graphics.NativeBindings.DecodeLiteral(checked((byte)(3 + offset / 71)), graphics.ActorModels);
                return ReadNativeScanExtra71(owner).ReadByte(offset % 71);
            }
            NativeBattleScanTextResources resources = NativeGraphics?.OriginalEffect?.Binding.ScanTextResources ?? throw new NotSupportedException("Native Scan text requires its original description bank.");
            return resources.ReadDescriptionByte(pointer);
        }
    }
}
