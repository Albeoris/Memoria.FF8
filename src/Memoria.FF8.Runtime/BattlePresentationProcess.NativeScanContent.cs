using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleScanByteOwner ReadNativeScanDescriptor208(BattleModelOwner owner)
    {
        return (hostedFrameServices?.ReadScanDescriptor ?? throw new InvalidOperationException("Hosted Scan requires its current canonical descriptor reader."))(owner);
    }

    private int ReadNativeScanLevel(BattleModelOwner owner)
    {
        return (hostedFrameServices?.ReadScanLevel ?? throw new InvalidOperationException("Hosted Scan requires its current combatant level reader."))(owner);
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
        int modelId = (hostedFrameServices ?? throw new InvalidOperationException("Hosted Scan requires its current original actor identity.")).Facts[owner].OriginalModelId;
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
        return (hostedFrameServices?.ReadScanExtra ?? throw new InvalidOperationException("Hosted Scan requires its current canonical extra-text reader."))(owner);
    }



    private NativeBattleScanByteOwner ReadNativeScanName24(BattleModelOwner owner)
    {
        byte[] bytes = owner.Side == BattleSide.Enemies ? ReadNativeScanEnemyInformation(owner)[..24] : this.resources.NativeScanActorNames.GetValueOrDefault(RequireCanonicalGraphicsActorId(owner)) ?? throw new NotSupportedException("Native Scan requires its allocated player name bytes.");
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
            ReadNameBytes = operand => this.resources.NativeScanNames.TryGetValue(operand, out byte[]? bytes) ? bytes.ToImmutableArray() : throw new NotSupportedException($"Native Scan replacement name{operand:X2} has no canonical content owner.")
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
