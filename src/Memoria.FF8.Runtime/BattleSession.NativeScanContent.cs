using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleScanByteOwner ReadNativeScanDescriptor208(int index)
    {
        BattleCombatant actor = combatants.SingleOrDefault(candidate => candidate.NativeIndex == index) ?? throw new NotSupportedException("Native Scan requires its allocated physical battle participant.");
        NativeBattleScanByteOwner row = new(new byte[208].ToImmutableArray(), new bool[208].ToImmutableArray());
        row = row.WriteDword(0x18, unchecked((uint)actor.CurrentHp)).WriteDword(0x1C, unchecked((uint)actor.MaximumHp)).WriteDword(0x7C, actor.NativeCombatantFlags).WriteByte(0xBB, checked((byte)actor.ModelId)).WriteByte(0xBC, checked((byte)actor.Level));
        if (actor.ElementDefenses.Count != 8)
        {
            throw new NotSupportedException("Native Scan requires all eight allocated element-defense WORDs.");
        }
        for (int element = 0; element < 8; element++)
        {
            row = row.WriteWord(0x44 + element * 2, checked((ushort)actor.ElementDefenses[element]));
        }
        return row.WriteByte(0xBD, checked((byte)actor.Attack)).WriteByte(0xBE, checked((byte)actor.Defense)).WriteByte(0xBF, checked((byte)actor.Magic)).WriteByte(0xC0, checked((byte)actor.Spirit)).WriteByte(0xC1, checked((byte)actor.Speed)).WriteByte(0xC3, checked((byte)actor.Evasion));
    }

    private byte[] ReadNativeScanEnemyInformation(int index)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (index is < 3 or >= 7 || graphics.ActorIds[index] is not string actorId)
        {
            throw new NotSupportedException("Native Scan enemy information requires its actual allocated enemy model.");
        }
        NativeBattleGraphicsModel model = graphics.Models[index];
        int originalRow = model.OriginalResourceRow104 ?? throw new NotSupportedException("Native Scan enemy information requires its original resource-row identity.");
        if (model.ActiveStagedResourceRow100 is not null || model.ActiveResourceRow100 != originalRow)
        {
            throw new NotSupportedException("Native Scan enemy information requires an established participant-to-information binding after resource replacement.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native Scan enemy information requires its loaded model CPU resources.");
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new NotSupportedException("Native Scan enemy information requires its original resource directory.");
        BattleCombatant actor = GetActor(actorId);
        if ((uint)originalRow >= cpu.Rows.Length || (uint)originalRow >= cache.Rows.Length || cache.Rows[originalRow].ModelId != checked(actor.ModelId + 16))
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
    private NativeBattleScanByteOwner ReadNativeScanExtra71(int index)
    {
        BattleCombatant actor = combatants.Single(candidate => candidate.NativeIndex == index);
        NativeBattleScanByteOwner bytes = new(new byte[71].ToImmutableArray(), new bool[71].ToImmutableArray());
        if (actor.NativeDisplayNameIndex is not int selected)
        {
            return bytes.WriteByte(0, 0);
        }
        byte[] name = actor.NativeAi?.NameBuffers[selected] ?? throw new NotSupportedException("Native Scan extra text requires its actual AI name buffer.");
        return bytes.Write(0, name);
    }

    private NativeBattleScanByteOwner ReadNativeScanName24(int index)
    {
        byte[] bytes = index >= 3 ? ReadNativeScanEnemyInformation(index)[..24] : definition.NativeScanActorNames.GetValueOrDefault(combatants.Single(actor => actor.NativeIndex == index).Id) ?? throw new NotSupportedException("Native Scan requires its allocated player name bytes.");
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
                return ReadNativeScanExtra71(3 + offset / 71).ReadByte(offset % 71);
            }
            NativeBattleScanTextResources resources = NativeGraphics?.OriginalEffect?.Binding.ScanTextResources ?? throw new NotSupportedException("Native Scan text requires its original description bank.");
            return resources.ReadDescriptionByte(pointer);
        }
    }
}
