"""Blender background GLB round trip: blender --background --python this.py -- input.glb output.glb.

Uses the built-in glTF add-on, custom properties, and NLA tracks so synchronized
multi-object clips retain one animation per stable track name.
"""
import bpy
import sys
import struct
import json
from fractions import Fraction
from math import lcm

source, destination = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
with open(source, "rb") as stream:
    content = stream.read()
length = struct.unpack_from("<I", content, 12)[0]
document = json.loads(content[20:20 + length])
manifest_text = next(node.get("extras", {}).get("memoria_ff8") for node in document["nodes"] if node.get("extras", {}).get("memoria_ff8"))
metadata = json.loads(manifest_text)
rates = [Fraction(clip["SecondsPerFrame"]).limit_denominator(600).denominator for clip in metadata["Clips"]]
bpy.context.scene.render.fps = lcm(*rates) if rates else 30
bpy.context.scene.render.fps_base = 1
bpy.ops.import_scene.gltf(filepath=source)
roots = [obj for obj in bpy.data.objects if "memoria_ff8" in obj]
assert len(roots) == 1, "The manifest root must survive import"
manifest = roots[0]["memoria_ff8"]
import json
metadata = json.loads(manifest)
print("MEMORIA imported objects", len(bpy.data.objects), "actions", len(bpy.data.actions))
for action in bpy.data.actions:
    print("MEMORIA action", action.name, "slots", len(action.slots))
# Imported glTF animations have named NLA tracks on every affected object.
# NLA_TRACKS joins matching tracks across armatures without merging distinct clips.
arguments = dict(filepath=destination, export_format="GLB", export_extras=True,
                 export_animations=True, export_animation_mode="NLA_TRACKS",
                 export_force_sampling=True, export_anim_slide_to_zero=True)
bpy.ops.export_scene.gltf(**arguments)
print("MEMORIA roundtrip", destination)
