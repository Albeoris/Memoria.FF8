# FFVII read-only formats

Independent .NET 10 readers for LGP archives, raw/length-framed LZSS, PC field section directories, HRC hierarchies and PC field animation frames. No game data is included. No resources are exported or modified.

`LgpArchive` accepts a seekable stream or file path and preserves archive directory identity, including duplicate paths. `Find` returns every matching entry. `ReadEntry` requires an entry from that exact directory and an optional byte limit. Archive streams must remain unchanged while open.

`Lzss.DecodeRaw` and `DecodeLengthFramed` have an explicit maximum decompressed length. `PcFieldFile` retains section offsets and payloads without claiming to decode every section's internal format. Unknown execution contracts belong in the separate game interpreter.

`HrcHierarchy.Read` preserves names, parent references, lengths and resource descriptor identities. `PcFieldAnimation.Read` preserves header fields and root/bone frame vectors. Both apply configured count/byte limits and reject malformed input. They perform no coordinate conversion, resource extraction or character retargeting.
