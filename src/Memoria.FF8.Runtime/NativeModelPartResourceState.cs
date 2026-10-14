using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The sole mutable32-byte part rows for one selected ONE resource slot.</summary>
public sealed record NativeModelPartResourceState([property: JsonRequired] string SourceHash, [property: JsonRequired] int SourceOffset, [property: JsonRequired] short[][] Parts)
{
    public static NativeModelPartResourceState Create(NativeModelPartsDefinition definition)
    {
        definition.Validate();
        short[][] rows = definition.Parts.Select(part => part.Words.ToArray()).ToArray();
        foreach (short[] row in rows)
        {
            //5318D0 preserves copied MCH counter/start/target/duration and clears WORD14 only.
            row[7] = 0;
        }
        return new NativeModelPartResourceState(definition.SourceHash, definition.SourceOffset, rows);
    }

    public void Validate(NativeModelPartsDefinition definition)
    {
        definition.Validate();
        if (SourceHash != definition.SourceHash || SourceOffset != definition.SourceOffset || Parts is null || Parts.Length != definition.Parts.Count)
        {
            throw new InvalidDataException("Native model-part state does not match its original MCH resource.");
        }
        for (int part = 0; part < Parts.Length; part++)
        {
            short[] row = Parts[part];
            if (row is null || row.Length != 16 || new[] { 0, 1, 3, 8, 9, 10, 11 }.Any(word => row[word] != definition.Parts[part].Words[word]) || row[7] is not (0 or 1 or 4) || row[7] == 1 && row[15] == 0)
            {
                throw new InvalidDataException("Native model-part physical WORDs are malformed or contain an unsupported writer.");
            }
            if (row[7] == 0 && Enumerable.Range(0, 16).Where(word => word != 7).Any(word => row[word] != definition.Parts[part].Words[word]))
            {
                throw new InvalidDataException("Inactive native model part has no writer for changed copied MCH WORDs.");
            }
        }
        if (Parts.Any(row => (row[7] & 5) != 0))
        {
            definition.RequireDisjointTranslationGeometry();
        }
    }

    public NativeModelPartResourceState StartTranslation(int partIndex, short x, short y, short z, short duration)
    {
        if ((uint)partIndex >= Parts.Length || Parts[partIndex].Length != 16)
        {
            throw new NotSupportedException("Native model-part command requires its registered original part.");
        }
        short[] current = Parts[partIndex];
        if ((current[7] & 3) != 0)
        {
            //532180 returns-1;359/360 still consume their selector and return2.
            return this;
        }
        if (duration == 0)
        {
            throw new NotSupportedException("Native animated part translation cannot divide by zero.");
        }
        short[][] rows = Parts.Select(row => row.ToArray()).ToArray();
        short[] target = rows[partIndex];
        target[7] = 1;
        target[15] = duration;
        target[2] = 1;
        for (int axis = 0; axis < 3; axis++)
        {
            target[4 + axis] = target[12 + axis];
        }
        target[12] = unchecked((short)(target[12] + x));
        target[13] = unchecked((short)(target[13] + y));
        target[14] = unchecked((short)(target[14] + z));
        return this with { Parts = rows };
    }

    /// <summary>One actual533CD0 enabled-instance visit, preserving per-skin signed WORD arithmetic and ordering.</summary>
    public NativeModelPartStep Step(NativeModelPartsDefinition definition)
    {
        Validate(definition);
        if (!Parts.Any(row => (row[7] & 5) != 0))
        {
            return new NativeModelPartStep(this, new Dictionary<int, Vector3>());
        }
        short[][] rows = Parts.Select(row => row.ToArray()).ToArray();
        Dictionary<int, Vector3> translations = [];
        for (int part = 0; part < rows.Length; part++)
        {
            short[] row = rows[part];
            int first = row[0];
            int last = first + row[1];
            for (int skin = first; skin < last; skin++)
            {
                if ((row[7] & 1) != 0)
                {
                    short[] translated = new short[3];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        //530C30 narrows the interpolated delta before separately adding the start vector.
                        short delta = unchecked((short)(row[2] * (row[12 + axis] - row[4 + axis]) / row[15]));
                        translated[axis] = delta;
                    }
                    translations[skin] = new Vector3(translated[0], translated[1], translated[2]) + new Vector3(row[4], row[5], row[6]);
                    row[2] = unchecked((short)(row[2] + 1));
                    if (row[15] < row[2])
                    {
                        row[7] = unchecked((short)((row[7] ^ 1) | 4));
                    }
                }
                else if ((row[7] & 4) != 0)
                {
                    translations[skin] = new Vector3(row[12], row[13], row[14]);
                }
            }
        }
        return new NativeModelPartStep(this with { Parts = rows }, translations);
    }
}

public sealed record NativeModelPartStep(NativeModelPartResourceState State, IReadOnlyDictionary<int, Vector3> SkinTranslations);
