# FFVII read-only formats

Independent .NET 10 readers for LGP archives, raw/length-framed LZSS, PC field section directories, HRC hierarchies and PC field animation frames. No game data is included. No resources are exported or modified.

Inner PC field readers preserve script entity entry offsets and native text bytes, model/light descriptors, camera matrices, walkmesh identities/adjacency, gateways/triggers, palettes and all four background layers with indexed or direct-color texture pages. RSD/P/TEX readers preserve model geometry, material state and native pixels. Kernel gzip sections and scene formation/reward records remain separate from mutable gameplay state. Movie camera tracks and map lists are decoded without choosing a gameplay route.

`FieldText` preserves glyph IDs and typed controls, including bounded wait/string operands. `TimTexture` retains indexed font pixels/palettes and the advisory image-size field while checking actual dimensions against the input. `KernelInitialData` preserves original character records, party/inventory/materia slots and recorded sentinels without computing derived gameplay values.

`LgpArchive` accepts a seekable stream or file path and preserves archive directory identity, including duplicate paths. `Find` returns every matching entry. `ReadEntry` requires an entry from that exact directory and an optional byte limit. Archive streams must remain unchanged while open.

`Lzss.DecodeRaw` and `DecodeLengthFramed` have an explicit maximum decompressed length. `PcFieldFile` retains section offsets and payloads without claiming to decode every section's internal format. Unknown execution contracts belong in the separate game interpreter.

`HrcHierarchy.Read` preserves names, parent references, lengths and resource descriptor identities. `PcFieldAnimation.Read` preserves header fields and root/bone frame vectors. Both apply configured count/byte limits and reject malformed input. They perform no coordinate conversion, resource extraction or character retargeting.

`DeclaredBoneCount` distinguishes native zero-bone HRC root attachments from animated skeletal bones. `Bones` retains the attachment record; `IsRootOnly` identifies this form. Animation compatibility compares the declared count. Geometry readers retain native winding and coordinate conventions for the renderer adapter to interpret explicitly.
