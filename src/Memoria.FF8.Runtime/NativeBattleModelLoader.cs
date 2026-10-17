using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleGraphicsResource
{
    public byte? BodyFileSelector129 { get; init; }
    public byte? LinkedFileSelector128 { get; init; }
    public ImmutableArray<NativeBattleModelDatSource>? ModelLoadSources { get; init; }
}

public sealed partial record NativeBattleGraphicsModel
{
    public byte? FileSelector128 { get; init; }
    public byte? FileSelector129 { get; init; }
    public int? ActiveResourceRow100 { get; init; }
    public int? OriginalResourceRow104 { get; init; }
}

public sealed record NativeBattleModelLoadTask(byte ModelId, byte PhysicalIndex, uint Argument, byte Selector, uint Callback, byte Phase)
{
    public NativeBattleModelFileSelection? File { get; init; }
    public NativeBattleModelTextureBinding? TextureBinding { get; init; }
    public NativeBattleEffectHeapAllocation? OriginalTexturePack { get; init; }
}
public sealed record NativeBattleModelFileSelection(short FileId, string Name);
public sealed record NativeBattleModelFileRequest(short FileId, string Name, uint CompletionCallback, int FileResult);
public sealed record NativeBattleModelResourceBind(NativeBattleModelLoadTask Task, NativeBattleModelDatSource PreparedDat, NativeBattleModelTextureBinding TextureBinding)
{
    public NativeBattleEffectHeapAllocation? OriginalTexturePack { get; init; }
}
public sealed record NativeBattleModelLoadStep(NativeBattleModelLoader State, NativeBattleFileStaging Staging, int Remaining, NativeBattleModelFileRequest? FileRequest, NativeBattleModelTextureLoad? TextureUpload, NativeBattleModelResourceBind? Bind);

/// <summary>507080/507070's global one-row20-byte list; the shared508470 signed result is an explicit external input.</summary>
public sealed record NativeBattleModelLoader(NativeBattleModelLoadTask? Task)
{
    public void Validate()
    {
        if (Task is not { } task)
        {
            return;
        }
        if (task.ModelId >= 16 || task.PhysicalIndex >= 3 || task.Callback is not (0x5077B0 or 0x5079B0 or 0x507BF0 or 0x507E20) || task.Phase > (task.Callback == 0x5079B0 ? 3 : 2) || task.Phase != 0 && task.File is null || task.Phase >= 2 && (task.TextureBinding is null || task.OriginalTexturePack is null))
        {
            throw new InvalidDataException("Native507070 requires its original bounded task and staging owners.");
        }
        if (task.File is { } file)
        {
            if (file.FileId < 0 || string.IsNullOrWhiteSpace(file.Name))
            {
                throw new InvalidDataException("Native507070 lost its selected original file metadata.");
            }
        }
    }

    public static NativeBattleModelLoader Queue507080(ushort request, byte physicalIndex, uint argument, byte linkedSelector128, byte bodySelector129)
    {
        if (physicalIndex >= 3)
        {
            throw new NotSupportedException("Native model reload currently requires a physical party CPU bank0..2.");
        }
        bool linked = request >= 0x1000;
        int model = linked ? request - 0x1000 : request;
        if (model >= 16)
        {
            throw new NotSupportedException("Native507080 enemy/8F reload requires its original resource-copy callback.");
        }
        uint callback = linked ? model is 1 or 9 ? 0x507E20u : 0x507BF0u : model == 7 ? 0x5079B0u : 0x5077B0u;
        //507080 reinitializes the one-slot list even when model7's linked request appends no callback.
        return new(Task: linked && model == 7 ? null : new((byte)model, physicalIndex, argument, linked ? linkedSelector128 : bodySelector129, callback, 0));
    }

    public NativeBattleModelLoadStep Poll507070(int signed508470Result, IReadOnlyList<NativeBattleModelDatSource> sources, NativeBattleFileStaging staging)
    {
        Validate();
        staging.Validate();
        if (Task is not { } task)
        {
            return new(this, staging, 0, null, null, null);
        }
        if (task.Phase == 0)
        {
            bool linked = task.Callback is 0x507BF0 or 0x507E20;
            NativeBattleModelDatSource[] matches = sources.Where(source => source.ModelId == task.ModelId && source.Linked == linked && source.Selector == task.Selector).ToArray();
            NativeBattleModelDatSource source = matches.FirstOrDefault() ?? throw new NotSupportedException("Native508480 lacks its observed BYTE128/129 file-table selection.");
            if (matches.Any(candidate => candidate.FileId != source.FileId || candidate.Name != source.Name || !candidate.Bytes.AsSpan().SequenceEqual(source.Bytes.AsSpan())))
            {
                throw new InvalidDataException("Native model file selection has conflicting immutable sources.");
            }
            _ = source.Section(0);
            NativeBattleModelLoadTask next = task with { Phase = 1, File = new(source.FileId, source.Name) };
            staging = staging.Write(NativeBattleFileStaging.Address, source.Bytes.AsSpan());
            return new(this with { Task = next }, staging, 1, new(source.FileId, source.Name, 0x508470, source.Bytes.Length), null, null);
        }
        if (task.Phase == 1)
        {
            if (signed508470Result < 0)
            {
                return new(this, staging, 1, null, null, null);
            }
            NativeBattleModelDatSource source = ReadCurrentStaging(task, staging);
            ushort mask = task.Callback is 0x5077B0 or 0x5079B0 ? (ushort)(3 << (2 * task.PhysicalIndex)) : (ushort)(1 << (task.PhysicalIndex + 12));
            NativeBattleEffectHeapAllocation pack = source.TexturePack(task.Callback);
            NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(pack, unchecked((ushort)~mask));
            NativeBattleModelDatSource relocated = source.Relocate507400(task.Callback, upload);
            staging = staging.Write(NativeBattleFileStaging.Address, relocated.Bytes.AsSpan());
            NativeBattleModelLoadTask next = task with { Phase = 2, TextureBinding = upload.Binding, OriginalTexturePack = pack };
            return new(this with { Task = next }, staging, 1, null, upload, null);
        }
        if (task.Phase == 2 && task.Callback == 0x5079B0)
        {
            //46C040 is a literal zero-return in this executable; model7 still advances its distinct phase2.
            return new(this with { Task = task with { Phase = 3 } }, staging, 1, null, null, null);
        }
        if (task.Phase == 2 || task.Phase == 3 && task.Callback == 0x5079B0)
        {
            NativeBattleModelDatSource source = ReadCurrentStaging(task, staging);
            NativeBattleModelTextureBinding binding = task.TextureBinding ?? throw new InvalidDataException("Native resource completion lost its executed507400 binding.");
            return new(new(Task: null), staging, 0, null, null, new(task, source, binding) { OriginalTexturePack = task.OriginalTexturePack ?? throw new InvalidDataException("Native resource completion lost its pre-relocation texture pack.") });
        }
        throw new InvalidDataException("Native model loader phase has no original callback branch.");
    }

    private static NativeBattleModelDatSource ReadCurrentStaging(NativeBattleModelLoadTask task, NativeBattleFileStaging staging)
    {
        NativeBattleModelFileSelection file = task.File ?? throw new NotSupportedException("Native model callback requires its actual selected file metadata; migrate an older task snapshot explicitly.");
        return new(task.ModelId, task.Callback is 0x507BF0 or 0x507E20, task.Selector, file.FileId, file.Name, staging.ReadDat(NativeBattleFileStaging.Address));
    }
}
