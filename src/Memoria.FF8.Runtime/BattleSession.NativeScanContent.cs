using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleScanByteOwner ReadNativeScanDescriptor208(int index)
    {
        BattleCombatant actor = combatants.SingleOrDefault(candidate => candidate.NativeIndex == index) ?? throw new NotSupportedException("Native Scan requires its allocated physical battle participant.");
        NativeBattleScanByteOwner row = new(new byte[208].ToImmutableArray(), new bool[208].ToImmutableArray());
        row = row.WriteDword(0x18, unchecked((uint)actor.CurrentHp)).WriteDword(0x1C, unchecked((uint)actor.MaximumHp)).WriteDword(0x7C, actor.NativeCombatantFlags).WriteByte(0xBC, checked((byte)actor.Level));
        if (actor.ElementDefenses.Count != 8)
        {
            throw new NotSupportedException("Native Scan requires all eight allocated element-defense WORDs.");
        }
        for (int element = 0; element < 8; element++)
        {
            row = row.WriteWord(0x44 + element * 2, checked((ushort)actor.ElementDefenses[element]));
        }
        return row;
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
}
