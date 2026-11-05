using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePartyLoadPart(uint Callback, byte Selector, NativeBattleModelFileSelection File, string Sha256, int DatLength, int CopiedLength)
{
    internal void Validate()
    {
        if (File is null || File.FileId < 0 || string.IsNullOrWhiteSpace(File.Name) || Sha256 is not { Length: 64 } || !Sha256.All(Uri.IsHexDigit) || DatLength < 0 || CopiedLength < 0 || CopiedLength > DatLength)
        {
            throw new InvalidDataException("Party load planning requires its selected file fingerprint and copied extent.");
        }
    }
}

/// <summary>Selected file metadata and extents for one complete body/linked load; no resource bytes are retained here.</summary>
public sealed record NativeBattlePartyLoadPlan(byte ModelId, byte PhysicalIndex, NativeBattlePartyCpuLayout Layout, NativeBattlePartyLoadPart Body, NativeBattlePartyLoadPart? Linked)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PreferResourceStorage { get; init; }
    [JsonIgnore]
    public bool UseResourceStorage => RequiresResourceStorage || PreferResourceStorage;
    [JsonIgnore]
    public int RequiredCapacity => checked(Body.CopiedLength + (Linked?.CopiedLength ?? 0));
    [JsonIgnore]
    public bool RequiresResourceStorage => RequiredCapacity > Layout.BankLength;

    internal static NativeBattlePartyLoadPlan Create(byte model, byte physicalIndex, byte bodySelector129, byte linkedSelector128, NativeBattlePartyCpuLayout layout, IReadOnlyList<NativeBattleModelDatSource> sources)
    {
        NativeBattleModelLoadTask body = NativeBattleModelLoader.Queue507080(model, physicalIndex, 0, linkedSelector128, bodySelector129).Task ?? throw new InvalidDataException("A party body load requires its selected callback.");
        NativeBattleModelLoadTask? linked = NativeBattleModelLoader.Queue507080(checked((ushort)(model + 0x1000)), physicalIndex, 0, linkedSelector128, bodySelector129).Task;
        NativeBattlePartyLoadPlan plan = new(model, physicalIndex, layout, Part(body), linked is null ? null : Part(linked));
        plan.Validate();
        return plan;

        NativeBattlePartyLoadPart Part(NativeBattleModelLoadTask task)
        {
            NativeBattleModelDatSource source = NativeBattleModelLoader.SelectSource(task, sources);
            return new(task.Callback, task.Selector, new(source.FileId, source.Name), Convert.ToHexString(SHA256.HashData(source.Bytes.AsSpan())), source.Bytes.Length, CopyLength(source, task.Callback));
        }
    }

    internal void Validate()
    {
        if (ModelId >= 16 || PhysicalIndex >= 3 || Layout is null || Body is null)
        {
            throw new InvalidDataException("Party load planning requires its physical party slot and selected body.");
        }
        Layout.Validate();
        Body.Validate();
        Linked?.Validate();
        if (Body.Callback != (ModelId == 7 ? 0x5079B0u : 0x5077B0u) || (ModelId == 7) != (Linked is null) || Linked is { } linked && linked.Callback != (ModelId is 1 or 9 ? 0x507E20u : 0x507BF0u) || Body.CopiedLength > int.MaxValue - (Linked?.CopiedLength ?? 0))
        {
            throw new InvalidDataException("Party load planning disagrees with its native body/linked copy callbacks.");
        }
    }

    internal NativeBattlePartyLoadPart Part(NativeBattleModelLoadTask task)
    {
        Validate();
        NativeBattlePartyLoadPart selected = task.Callback == Body.Callback ? Body : Linked is { } linked && task.Callback == linked.Callback ? linked : throw new InvalidDataException("The model callback does not belong to its selected party load plan.");
        if (task.ModelId != ModelId || task.PhysicalIndex != PhysicalIndex || task.Selector != selected.Selector || task.File is { } file && file != selected.File)
        {
            throw new InvalidDataException("The model task no longer matches its selected complete party load.");
        }
        return selected;
    }

    internal void RequireSource(NativeBattleModelLoadTask task, NativeBattleModelDatSource source, bool originalBytes)
    {
        NativeBattlePartyLoadPart selected = Part(task);
        bool linked = task.Callback is 0x507BF0 or 0x507E20;
        if (source.ModelId != ModelId || source.Linked != linked || source.Selector != selected.Selector || source.FileId != selected.File.FileId || source.Name != selected.File.Name || source.Bytes.Length != selected.DatLength || CopyLength(source, task.Callback) != selected.CopiedLength || originalBytes && !StringComparer.OrdinalIgnoreCase.Equals(selected.Sha256, Convert.ToHexString(SHA256.HashData(source.Bytes.AsSpan()))))
        {
            throw new InvalidDataException("The selected model source differs from its complete party load plan.");
        }
    }

    internal static int CopyLength(NativeBattleModelDatSource source, uint callback)
    {
        int end = callback switch { 0x5077B0 or 0x507BF0 => 5, 0x5079B0 => 7, 0x507E20 => 3, _ => throw new InvalidDataException("Party load planning requires an original party copy callback.") };
        return checked(source.Section(end) - source.Section(0));
    }
}
