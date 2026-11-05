using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeFieldRetainedActor(int ObjectId, int ModelId, int FirstScript, int LastScript);
public sealed record NativeFieldRetainedPresentation(int ObjectId, byte Animation, ushort? Frame, bool AutoAnimation, byte BaseAnimation, bool Visible, byte RendererMode, short RootTranslationZ, byte Red, byte Green, byte Blue, bool AdditionalControlDisabled, bool PrimitiveTransparency, uint EyeMode)
{
    public byte? EyeBlinkParameter611 { get; init; }
}

public sealed record NativeBattleFieldResourceReloadResult(NativeBattleRetainedMemory Memory, NativeCrtRandomState? CrtRandomBefore, NativeCrtRandomState? CrtRandomAfter);
public sealed record NativeFieldRegisteredTexture(byte Mode, short X, short Y, short WidthWords, short Height, short PaletteX, short PaletteY, ushort TexturePage, ushort Clut)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public uint? PlaneIdentity { get; init; }
}
public sealed record NativeFieldRegisteredInputSource([property: System.Text.Json.Serialization.JsonRequired] int GeometryLength, [property: System.Text.Json.Serialization.JsonRequired] string AnimationSourceHash, [property: System.Text.Json.Serialization.JsonRequired] int AnimationSourceOffset, [property: System.Text.Json.Serialization.JsonRequired] int AnimationLength);
public sealed record NativeFieldRegisteredResource(int ModelId, int Index, uint Address, uint? GeometryAddress, uint? AnimationAddress, byte Scale, string SourceHash, int SourceOffset, ImmutableArray<NativeFieldRegisteredTexture> Textures)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? ResourceLength { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldRegisteredInputSource? Input { get; init; }

    internal NativeFieldResourceCursor Resolve(NativeBattleResourcePointer? receipt)
    {
        if (ResourceLength is null)
        {
            if (receipt?.FieldStorage is not null)
            {
                throw new InvalidDataException("Guest field descriptor cannot select a logical resource receipt.");
            }
            return NativeFieldResourceCursor.Guest(Address);
        }
        if (ResourceLength < 96 || receipt?.StagingAddress != Address || receipt.FieldStorage is not { Resource: { } owner } field || field.ResourceIndex != Index || owner.Length != ResourceLength || !receipt.HasValidOwner())
        {
            throw new InvalidDataException("Field resource descriptor disagrees with its numeric table receipt and local owner.");
        }
        return new(owner, 0);
    }
}
public sealed record NativeFieldRegisteredInstance(int ObjectId, int ModelId, uint Address, ushort? EmptyTerminal)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldInstanceResourceReference? ResourceReference { get; init; }
}
public sealed record NativeFieldRegistrationState(int Version, int FieldId, string ProgramHash, string OneHash, uint PreModelAddress, uint? RelocatedSourceAddress, int? RelocatedSourceLength, uint EndAddress, ImmutableArray<NativeFieldRegisteredResource> Resources, ImmutableArray<NativeFieldRegisteredInstance> Instances)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldRegistrationLayout? Layout { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? InputsOwned { get; init; }

    // An omitted layout describes the allocation actually used by the original snapshot schema.
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeFieldRegistrationLayout MemoryLayout => Layout ?? NativeFieldRegistrationLayout.ForImage(NativeFieldRuntimeProfile.FF8EnExecutableHash, NativeFieldRegistrationEntry.BattleReturn);

    public void RequireRuntimeProfile(string executableHash)
    {
        NativeFieldRegistrationLayout layout = MemoryLayout;
        layout.Validate();
        if (layout.ExecutableHash != executableHash)
        {
            throw new NotSupportedException("Saved field registration uses another runtime allocation profile and requires explicit migration.");
        }
    }

    public void Validate(NativeBattleRetainedMemory retained) => Validate(retained, null);

    internal void Validate(NativeBattleRetainedMemory retained, NativeFieldRegistrationMemory? scope)
    {
        NativeFieldRegistrationLayout layout = MemoryLayout;
        layout.Validate();
        retained.Validate();
        NativeBattleFileStaging memory = retained.FileStaging ?? throw new InvalidDataException("Native field registration lost its physical shared memory.");
        if (Version != 1 || FieldId is < 72 or > ushort.MaxValue || ProgramHash is not { Length: 64 } || OneHash is not { Length: 64 } || PreModelAddress < layout.ArenaAddress || EndAddress < PreModelAddress || EndAddress > layout.Limit || Resources.IsDefault || Instances.IsDefault || Resources.Any(resource => resource is null) || Instances.Any(instance => instance is null) || Resources.Length > 64 || Instances.Length > 32 || Resources.Select(resource => resource.ModelId).Distinct().Count() != Resources.Length || Instances.Select(instance => instance.ObjectId).Distinct().Count() != Instances.Length)
        {
            throw new InvalidDataException("Native field registration has invalid source identities, allocation layout or retained bindings.");
        }
        if (InputsOwned == true)
        {
            if (RelocatedSourceAddress is not null || RelocatedSourceLength is not null || Resources.Any(resource => resource.Input is null || resource.GeometryAddress is not null || resource.AnimationAddress is not null))
            {
                throw new InvalidDataException("Owned field inputs cannot claim a guest source placement.");
            }
        }
        else if (RelocatedSourceAddress is not uint relocated || RelocatedSourceLength is not int length || relocated < PreModelAddress || length < 4 || (ulong)relocated + (uint)length > layout.Limit || Resources.Any(resource => resource.Input is not null || resource.GeometryAddress is null || resource.AnimationAddress is null))
        {
            throw new InvalidDataException("Guest field input metadata has an invalid retained source placement.");
        }
        if (retained.FieldTextureAnimations is { } animations && (animations.OneHash != OneHash || !animations.Textures.Select(animation => animation.Texture).SequenceEqual(Resources.SelectMany(resource => resource.Textures).DistinctBy(texture => (texture.X, texture.Y)))))
        {
            throw new InvalidDataException("Field texture copy history does not belong to the retained resource registration.");
        }
        foreach (NativeFieldRegisteredResource resource in Resources)
        {
            if (resource.Input is { } input && (input.GeometryLength < 64 || input.AnimationLength < 2 || input.AnimationSourceOffset < 0 || input.AnimationSourceHash is not { Length: 64 } || !input.AnimationSourceHash.All(Uri.IsHexDigit) || resource.SourceHash is not { Length: 64 } || !resource.SourceHash.All(Uri.IsHexDigit) || resource.SourceOffset < 0))
            {
                throw new InvalidDataException("Field resource input coordinates exceed their explicit source spans.");
            }
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0x1DCB0F0 + (uint)resource.Index * 4), 4));
            uint tableAddress = checked(0x1DCB0F0 + (uint)resource.Index * 4);
            NativeBattleResourcePointer? receipt = scope is null ? memory.ReadResourcePointer(tableAddress) : scope.ReadResourcePointer(tableAddress);
            uint marker = Read32(resource.Resolve(receipt) + 76);
            if (resource.ModelId is < 0 or >= 64 || resource.Index < 0 || resource.Index >= Resources.Length || resource.Address < PreModelAddress || resource.Address >= EndAddress || pointer != resource.Address || marker != 0x12345678 || memory.Read(0x1DCAF28 + (uint)resource.ModelId, 1)[0] != resource.Index)
            {
                throw new InvalidDataException("Native field resource registration disagrees with its actual physical pointer/header.");
            }
        }
        for (int index = 0; index < Instances.Length; index++)
        {
            NativeFieldRegisteredInstance instance = Instances[index];
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0x1DCB340 + (uint)index * 4), 4));
            uint resource = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(instance.Address, 4));
            uint expected = instance.ModelId < 0 ? uint.MaxValue : Resources.Single(item => item.ModelId == instance.ModelId).Address;
            NativeFieldRegisteredResource? selected = instance.ModelId < 0 ? null : Resources.Single(item => item.ModelId == instance.ModelId);
            if ((selected?.ResourceLength is not null) != (instance.ResourceReference is not null))
            {
                throw new InvalidDataException("Field instance and resource must retain the same cursor domain.");
            }
            if (instance.ResourceReference is { } reference)
            {
                NativeBattleResourcePointer? receipt = scope is null ? memory.ReadResourcePointer(checked(0x1DCB340 + (uint)index * 4)) : scope.ReadResourcePointer(checked(0x1DCB340 + (uint)index * 4));
                if (reference.InstanceIndex != index || reference.ResourceIndex != selected!.Index || receipt?.StagingAddress != instance.Address || receipt.FieldStorage is not { InstanceBindings: { } bindings } field || field.ResourceIndex != selected.Index || !receipt.HasValidOwner())
                {
                    throw new InvalidDataException("Field instance has invalid selected resource cursor storage.");
                }
                foreach (uint offset in new uint[] { 8, 104, 108, 148 })
                {
                    if (Read32(new(bindings, offset)) > selected.ResourceLength || memory.ReadSnapshot(instance.Address + offset, 4).Known.Any(value => value))
                    {
                        throw new InvalidDataException("Field instance logical cursor is out of bounds or has conflicting guest bytes.");
                    }
                }
            }
            if (instance.Address < PreModelAddress || instance.Address >= EndAddress || pointer != instance.Address || resource != expected)
            {
                throw new InvalidDataException("Native field instance registration disagrees with its actual retained model pointer.");
            }
        }

        uint Read32(NativeFieldResourceCursor cursor)
        {
            if (scope is not null)
            {
                return scope.Read32(cursor);
            }
            NativeBattleFileStaging selected = cursor.Owner?.Storage ?? memory;
            return BinaryPrimitives.ReadUInt32LittleEndian(selected.Read(cursor.Offset, 4));
        }
    }
}
public sealed record NativeFieldRegistrationStep(NativeFieldRegistrationState State, NativeBattleRetainedMemory Memory, ImmutableArray<NativeBattleEffectVramUpload> Uploads);

/// <summary>Retained52BC00 instruction binding followed by532A40's actual field model registration.</summary>
public static partial class NativeFieldRegistrationMachine
{
    private const uint ProgramSource = 0x19AB018;
    private const uint CacheRows = 0x1DCA328;
    private const uint ResourcePointers = 0x1DCB0F0;
    private const uint InstancePointers = 0x1DCB340;
    private const uint ModelMap = 0x1DCAF28;
    private const uint TexturePages = 0x1DC3CEC;
    private const uint Cluts = 0x1DC6014;
    private const uint UvOffsets = 0x1DCAF90;

    private sealed class SelectedModel(int modelId, int index, NativeFieldOneModelEntry entry)
    {
        internal readonly int ModelId = modelId;
        internal readonly int Index = index;
        internal readonly NativeFieldOneModelEntry Entry = entry;
        internal readonly List<SelectedTexture> Textures = [];
        internal readonly List<NativeFieldRegisteredTexture> RegisteredTextures = [];
        internal int UploadedTextures;
        internal NativeFieldResourceCursor Geometry;
        internal NativeFieldResourceCursor Animation;
        internal string AnimationSourceHash = string.Empty;
        internal int AnimationSourceOffset;
        internal string SourceHash = string.Empty;
        internal int SourceOffset;
        internal byte Scale = (entry.Flag >> 28) == 0xD ? (byte)(entry.Flag >> 16) : (byte)16;
    }

    private readonly record struct SelectedTexture(NativeFieldResourceCursor? Source, uint? AliasFlag);

    private sealed class Context(NativeFieldRegistrationMemory memory, NativeBattleEffectMemory effects, NativeFieldTextureAnimations? animations = null)
    {
        internal readonly NativeFieldRegistrationMemory Memory = memory;
        internal NativeBattleEffectMemory Effects = effects;
        internal NativeFieldTextureAnimations? Animations = animations;
        internal readonly List<NativeBattleEffectVramUpload> Uploads = [];

        internal void Upload(short x, short y, short width, short height, byte[] bytes)
        {
            NativeBattleEffectTextureState textures = Effects.Textures ?? throw new NotSupportedException("Native field texture registration requires the process VRAM owner.");
            NativeBattlePrimitiveProcess? process = Effects.PrimitiveProcess;
            if (process?.TextureWindowCache is { } cache)
            {
                process = process with { TextureWindowCache = cache.Invalidate464850(x, y, x + width - 1, y + height - 1, textures) };
            }
            Effects = Effects with { Textures = textures.Upload45BD30(x, y, width, height, bytes), PrimitiveProcess = process };
            Uploads.Add(new(x, y, width, height, bytes.ToImmutableArray()));
        }
    }

    public static NativeFieldRegistrationStep Register(NativeFieldRegistrationEntry registrationEntry, NativeFieldRegistrationResources source, IReadOnlyList<NativeFieldRetainedActor> actors, NativeBattleRetainedMemory retained)
    {
        source.Validate();
        NativeFieldRegistrationLayout layout = NativeFieldRegistrationLayout.ForImage(source.ExecutableHash, registrationEntry);
        retained = NativeFieldEyeProcess.EnsureOwnedMemory(retained);
        retained.Validate();
        if (source.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash)
        {
            if (retained.FieldTextureAnimations is { Planes: null } || retained.FieldTextureAnimations is null && retained.FieldTexturePlaneCacheFresh != true)
            {
                throw new NotSupportedException("Remastered registration requires retained native plane state or an explicitly fresh cold renderer cache; older state requires migration before registration reload.");
            }
        }
        NativeFieldRegistrationLoaderInputs loader = source.Loader ?? throw new NotSupportedException("Native kind3 requires its original JSM, explicit PCB presence, INF selector and initialized placement tables.");
        if (actors.Count > 32 || actors.Select(actor => actor.ObjectId).Distinct().Count() != actors.Count || actors.Any(actor => actor.ModelId < -1 || actor.ModelId >= 64 || actor.ModelId >= source.Directory.Length || actor.FirstScript < 0 || actor.LastScript < actor.FirstScript))
        {
            throw new InvalidDataException("Native field registration requires retained physical actor order and bounded original models/script ranges.");
        }
        NativeBattleFileStaging staging = retained.FileStaging ?? NativeBattleFileStaging.Unknown();
        if (retained.ModelCpuResources is { } cpu && cpu.Cpu.EffectiveLayout != NativeBattlePartyCpuLayout.ForImage(source.ExecutableHash))
        {
            throw new NotSupportedException("Field registration requires the edition's retained party CPU allocation; migrate this save explicitly.");
        }
        using NativeFieldRegistrationMemory memory = new(staging, layout.MemoryStart, layout.MemoryEnd);
        memory.ObserveCpuBanks(retained.ModelCpuResources);
        Context context = new(memory, retained.EffectMemory ?? throw new NotSupportedException("Native kind3 requires the retained shared VRAM and texture process."));
        memory.Fill(CacheRows, 0xC00, 0);
        memory.Fill(ResourcePointers, 0x100, 0);
        memory.Fill(InstancePointers, 0x80, 0);
        memory.Fill(0x1DC308C, 64, 0);
        memory.Fill(ModelMap, 64, 255);
        uint cursor = RebindProgram(memory, loader, actors, layout.ArenaAddress);
        if (loader.Colors is { } colors)
        {
            memory.BindResource(cursor, colors.Data, 0, colors.Data.Length);
            cursor = checked(cursor + (uint)colors.Data.Length);
        }
        uint preModel = cursor;
        List<SelectedModel> selected = [];
        foreach (int model in actors.Where(actor => actor.ModelId >= 0).Select(actor => actor.ModelId).Distinct())
        {
            if (selected.Count == 64)
            {
                throw new NotSupportedException("Native field registration exceeds its64 resource slots.");
            }
            NativeFieldOneModelEntry entry = source.Directory[model];
            if (entry.SourceWord8 is null || entry.RawName.IsDefault || entry.RawName.Length != 8)
            {
                throw new NotSupportedException("Native ONE registration requires all original directory DWORDs and raw name/color bytes.");
            }
            SelectedModel item = new(model, selected.Count, entry);
            item.SourceHash = source.One.Sha256;
            item.SourceOffset = checked(entry.Offset + entry.ModelOffset);
            NativeFieldResourceOwner inputOwner = NativeFieldResourceOwner.Borrow(source.One.Data, entry.Offset, entry.Size);
            NativeFieldResourceCursor beginning = new(inputOwner, 0);
            if ((entry.Flag >> 28) == 0xD)
            {
                item.Animation = beginning;
                item.AnimationSourceHash = source.One.Sha256;
                item.AnimationSourceOffset = entry.Offset;
                memory.Write8(0x1DC308C + (uint)item.Index, 13);
            }
            else
            {
                item.Geometry = beginning + entry.ModelOffset;
                item.Animation = item.Geometry + memory.Read32(item.Geometry + 56);
                item.AnimationSourceHash = source.One.Sha256;
                item.AnimationSourceOffset = checked(entry.Offset + (int)item.Animation.Offset);
                memory.Write8(0x1DC308C + (uint)item.Index, 255);
                if ((entry.Flag >> 28) == 0xA)
                {
                    item.Textures.Add(new(null, entry.Flag));
                }
                else
                {
                    item.Textures.AddRange(entry.RawTextureWords.Select(word => new SelectedTexture(beginning + (word & 0x0FFFFFFF), null)));
                }
            }
            selected.Add(item);
            if (item.Textures.Count > 6)
            {
                throw new NotSupportedException("Native model registration exceeds its six texture slots.");
            }
        }
        foreach (SelectedModel item in selected.Where(item => (item.Entry.Flag >> 28) == 0xD))
        {
            string member = "/" + item.Entry.Name[..4] + ".mch";
            NativeFieldModelSource main = source.MainCharacters.Single(model => model.Member.EndsWith(member, StringComparison.OrdinalIgnoreCase));
            NativeFieldResourceOwner inputOwner = NativeFieldResourceOwner.Borrow(main.Data, 0, main.Data.Length);
            NativeFieldResourceCursor beginning = new(inputOwner, 0);
            NativeFieldResourceCursor pointer = beginning;
            while (unchecked((int)memory.Read32(pointer)) >= 0)
            {
                if (item.Textures.Count == 6)
                {
                    throw new NotSupportedException("Native model registration exceeds its six texture slots.");
                }
                item.Textures.Add(new(beginning + (memory.Read32(pointer) & 0x0FFFFFFF), null));
                pointer += 4;
            }
            item.Geometry = checked(beginning + (memory.Read32(pointer + 4) & 0x0FFFFFFF));
            item.SourceHash = main.Sha256;
            item.SourceOffset = checked((int)item.Geometry.Offset);
        }
        int uploadIndex = 0;
        short?[] lastX = new short?[6];
        short?[] lastY = new short?[6];
        List<NativeFieldRegisteredResource> resources = [];
        foreach (SelectedModel item in selected)
        {
            uint row = checked(CacheRows + (uint)item.Index * 48);
            memory.Write32(row, checked((uint)item.Entry.Offset));
            memory.Write32(row + 4, checked((uint)item.Entry.Size));
            memory.Write32(row + 8, item.Entry.SourceWord8!.Value);
            memory.Write32(row + 12, (item.Entry.Flag >> 28) == 0xD ? item.Entry.Flag & 0xFFFF : uint.MaxValue);
            memory.Fill(row + 16, 24, 255);
            memory.ProjectInputPointer(row + 40, item.Geometry);
            memory.ProjectInputPointer(row + 44, item.Animation);
            memory.Write8(ModelMap + (uint)item.ModelId, (byte)item.Index);
            for (int ordinal = 0; ordinal < item.Textures.Count; ordinal++)
            {
                SelectedTexture texture = item.Textures[ordinal];
                NativeFieldRegisteredTexture registration;
                if (texture.AliasFlag is uint aliasFlag)
                {
                    memory.Write32(row + 16 + (uint)ordinal * 4, aliasFlag);
                    int alias = (byte)(aliasFlag >> 20);
                    SelectedModel prior = selected.Take(item.Index).SingleOrDefault(model => model.ModelId == alias) ?? throw new NotSupportedException("Native texture alias requires an already registered original model.");
                    if (ordinal >= prior.RegisteredTextures.Count)
                    {
                        throw new InvalidDataException("Native texture alias has no previously registered matching ordinal.");
                    }
                    registration = prior.RegisteredTextures[ordinal];
                }
                else
                {
                    NativeFieldResourceCursor textureSource = texture.Source ?? throw new InvalidDataException("Field texture has no selected input owner.");
                    memory.ProjectInputPointer(row + 16 + (uint)ordinal * 4, textureSource);
                    registration = RegisterTexture(context, loader, item.Entry, textureSource, uploadIndex++);
                    item.UploadedTextures++;
                    if (source.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash)
                    {
                        registration = registration with { PlaneIdentity = NativeFieldTexturePlanes.SourceIdentity(item.Entry.RawName.AsSpan(), item.UploadedTextures) };
                    }
                    lastX[ordinal] = registration.X;
                    lastY[ordinal] = registration.Y;
                    ushort offset = unchecked((ushort)((registration.Y & 255) << 8 | (registration.X & 63) << (2 - registration.Mode)));
                    memory.Write16(UvOffsets + (uint)ordinal * 2, offset);
                }
                item.RegisteredTextures.Add(registration);
                memory.Write16(TexturePages + (uint)(item.Index * 6 + ordinal) * 2, registration.TexturePage);
                memory.Write16(Cluts + (uint)(item.Index * 6 + ordinal) * 2, registration.Clut);
            }
            uint address = cursor;
            if (address > layout.Limit || layout.Limit - address < 96)
            {
                throw new NotSupportedException("Field resource header projection exceeds the selected native allocation.");
            }
            NativeFieldResourceOwner owner = NativeFieldResourceOwner.Create(ResourceLength(memory, item));
            NativeFieldResourceCursor resourceCursor = new(owner, 0);
            int seedLength = checked((int)Math.Min((uint)owner.Length, layout.Limit - address));
            memory.Copy(address, resourceCursor, seedLength);
            if (RegisterResource5318D0(memory, item, resourceCursor) != owner.Length)
            {
                throw new InvalidDataException("Converted field resource length disagrees with its bounded owner.");
            }
            RebaseEyes(memory, resourceCursor, lastX[0], lastY[0]);
            memory.ProjectResourceHeader(resourceCursor, address);
            memory.WriteResourcePointer(ResourcePointers + (uint)item.Index * 4, new(null, address) { FieldStorage = new(item.Index) { Resource = owner } });
            cursor = checked(cursor + 96);
            resources.Add(new(item.ModelId, item.Index, address, null, null, item.Scale == 0 ? (byte)16 : item.Scale, item.SourceHash, item.SourceOffset, item.RegisteredTextures.ToImmutableArray())
            {
                ResourceLength = owner.Length,
                Input = new(checked(item.Geometry.Owner!.Length - (int)item.Geometry.Offset), item.AnimationSourceHash, item.AnimationSourceOffset, checked(item.Animation.Owner!.Length - (int)item.Animation.Offset))
            });
        }
        List<NativeFieldRegisteredInstance> instances = [];
        for (int index = 0; index < actors.Count; index++)
        {
            NativeFieldRetainedActor actor = actors[index];
            uint address = cursor;
            NativeFieldInstanceResourceReference? reference = null;
            if (actor.ModelId < 0)
            {
                cursor = RegisterEmpty531D40(memory, index, actor.ModelId, cursor);
            }
            else
            {
                NativeFieldRegisteredResource resource = resources.Single(resource => resource.ModelId == actor.ModelId);
                NativeFieldResourceOwner bindings = NativeFieldResourceOwner.Create(152);
                cursor = RegisterInstance531980(memory, index, resource, bindings, cursor);
                memory.WriteResourcePointer(InstancePointers + (uint)index * 4, new(null, address) { FieldStorage = new(resource.Index) { InstanceBindings = bindings } });
                reference = new(resource.Index, index);
            }
            instances.Add(new(actor.ObjectId, actor.ModelId, address, actor.ModelId < 0 ? memory.ReadOptional16(address + 12) : null) { ResourceReference = reference });
        }
        //532A40 constructs into the same arena after relocating its input. Consumed source bytes can be reused.
        if (cursor > layout.Limit)
        {
            throw new NotSupportedException("Constructed field resources exceed the selected native allocation.");
        }
        NativeFieldRegistrationState state = new(1, source.FieldId, source.ProgramHash, source.One.Sha256, preModel, null, null, cursor, resources.ToImmutableArray(), instances.ToImmutableArray()) { Layout = layout, InputsOwned = true };
        NativeBattleRetainedMemory result = retained with { FileStaging = memory.Prepare(), EffectMemory = context.Effects, ModelCpuResources = memory.ReconcileCpuBanks(retained.ModelCpuResources), FieldTextureAnimations = NativeFieldTextureAnimations.Register(source.One.Sha256, resources.SelectMany(resource => resource.Textures), source.ExecutableHash) };
        if (source.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash)
        {
            result = result with { FieldTexturePlaneCacheFresh = false, FieldTextureAnimations = result.FieldTextureAnimations! with { Planes = retained.FieldTextureAnimations?.Planes ?? result.FieldTextureAnimations!.Planes } };
        }
        state.Validate(result, memory);
        ImmutableArray<NativeBattleEffectVramUpload> uploads = context.Uploads.ToImmutableArray();
        result = result with { FileStaging = memory.Finish() };
        return new(state, result, uploads);
    }

    private static uint RebindProgram(NativeFieldRegistrationMemory memory, NativeFieldRegistrationLoaderInputs loader, IReadOnlyList<NativeFieldRetainedActor> actors, uint arenaAddress)
    {
        ReadOnlySpan<byte> program = loader.Program.Data.AsSpan();
        if (program.Length < 8)
        {
            throw new InvalidDataException("Native retained JSM has no original header.");
        }
        int table = BinaryPrimitives.ReadUInt16LittleEndian(program[4..]);
        int code = BinaryPrimitives.ReadUInt16LittleEndian(program[6..]);
        if (table > short.MaxValue || code > short.MaxValue || code < table + 4 || code > program.Length || (code - table) % 2 != 0)
        {
            throw new NotSupportedException("Native retained JSM pointer table is outside its original signed offset domain.");
        }
        memory.BindResource(ProgramSource, loader.Program.Data, 0, loader.Program.Data.Length);
        uint tableAddress = checked(arenaAddress + (uint)loader.AllocationPrefixBytes);
        int pointers = (code - table) / 2;
        uint operations = checked((tableAddress + (uint)pointers * 2 + 3) & ~3u);
        int copied = 0;
        for (int index = 0; index < pointers; index++)
        {
            ushort raw = BinaryPrimitives.ReadUInt16LittleEndian(program.Slice(table + index * 2, 2));
            memory.Write16(tableAddress + (uint)index * 2, (ushort)(raw & 0x7FFF));
        }
        for (int index = 0; index < pointers - 1; index++)
        {
            int raw = BinaryPrimitives.ReadUInt16LittleEndian(program.Slice(table + index * 2, 2));
            bool admitted = (raw & 0x8000) != 0 || actors.Any(actor => index >= actor.FirstScript && index <= actor.LastScript);
            if (!admitted)
            {
                memory.Write16(tableAddress + (uint)index * 2, 0);
                continue;
            }
            int first = raw & 0x7FFF;
            int last = BinaryPrimitives.ReadUInt16LittleEndian(program.Slice(table + index * 2 + 2, 2)) & 0x7FFF;
            memory.Write16(tableAddress + (uint)index * 2, checked((ushort)copied));
            for (int count = Math.Max(1, last - first); count > 0; count--)
            {
                memory.Copy(checked(ProgramSource + (uint)code + (uint)first++ * 4), checked(operations + (uint)copied++ * 4), 4);
            }
        }
        memory.Write32(0x1D9D0E4, tableAddress);
        memory.Write32(0x1D9CF50, operations);
        memory.Write32(0x1D9CDF8, checked((uint)copied));
        return checked(operations + (uint)copied * 4);
    }

    private static NativeFieldRegisteredTexture RegisterTexture(Context context, NativeFieldRegistrationLoaderInputs loader, NativeFieldOneModelEntry entry, NativeFieldResourceCursor texture, int index)
    {
        NativeFieldRegistrationMemory memory = context.Memory;
        if ((uint)index >= loader.ImageCoordinates.Length || (uint)index >= loader.PaletteCoordinates.Length || memory.Read32(texture) != 0x10)
        {
            throw new NotSupportedException("Native field texture registration requires a reached original TIM and available initialized placement slots.");
        }
        uint mode = memory.Read32(texture + 4);
        if ((mode & 8) == 0 || (mode & 7) > 2)
        {
            throw new NotSupportedException("Native field registration requires its original indexed TIM palette contract.");
        }
        NativeFieldResourceCursor paletteBlock = texture + 8;
        NativeFieldResourceCursor imageBlock = paletteBlock + memory.Read32(paletteBlock);
        short width = memory.ReadSigned16(imageBlock + 8);
        short height = memory.ReadSigned16(imageBlock + 10);
        uint imagePosition = loader.ImageCoordinates[index];
        uint palettePosition = loader.PaletteCoordinates[index];
        short x = (short)imagePosition;
        short y = (short)(imagePosition >> 16);
        short paletteX = (short)palettePosition;
        short paletteY = (short)(palettePosition >> 16);
        context.Upload(x, y, width, height, memory.Read(imageBlock + 12, checked(2 * width * height)));
        byte[] palette = ProjectPalette532A40(memory.Read(paletteBlock + 12, 512), entry.RawName.AsSpan(), loader.Colors);
        memory.Write(paletteBlock + 12, palette);
        context.Upload(paletteX, paletteY, 256, 1, palette);
        ushort tpage = unchecked((ushort)(((y & 0x100 | x >> 2 & 0xF0) >> 4) | 4 * (y & 0x200 | 8 * (1 | 4 * ((int)mode & 3)))));
        ushort clut = unchecked((ushort)(paletteY << 6 | paletteX >> 4 & 63));
        return new((byte)(mode & 3), x, y, width, height, paletteX, paletteY, tpage, clut);
    }

    private static void RebaseEyes(NativeFieldRegistrationMemory memory, NativeFieldResourceCursor resource, short? x, short? y)
    {
        if ((memory.Read8(resource + 95) & 1) == 0)
        {
            return;
        }
        if (x is not short imageX || y is not short imageY)
        {
            throw new NotSupportedException("Native eye rebasing requires texture0's actual reached image placement.");
        }
        NativeFieldResourceCursor eyes = memory.ReadPointer(resource + 20);
        int divisor = 16 / memory.Read8(eyes + 2);
        memory.Write16(eyes + 6, unchecked((ushort)(imageX + memory.Read16(eyes + 6) / divisor)));
        memory.Write16(eyes + 8, unchecked((ushort)(imageY + memory.Read16(eyes + 8))));
        memory.Write8(eyes + 3, unchecked((byte)(memory.Read8(eyes + 3) / divisor)));
        for (int frame = 0; frame < memory.Read8(eyes + 5); frame++)
        {
            NativeFieldResourceCursor position = eyes + 12 + (uint)frame * 4;
            memory.Write16(position, unchecked((ushort)(imageX + memory.Read16(position) / divisor)));
            memory.Write16(position + 2, unchecked((ushort)(imageY + memory.Read16(position + 2))));
        }
    }
}
