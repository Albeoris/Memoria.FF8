"""Generate an original room GLB and optionally bind it to a captured native field.

blender --background --python field_scene_fixture.py -- room.glb
blender --background --python field_scene_fixture.py -- room.glb --reference slot0/reference.json --reference slot1/reference.json --package Mods/Memoria/field-scene-fixture
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path
from statistics import median

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from field_scene import export_scene


def material(name, color):
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*color, 1)
    if result.node_tree is None:
        result.use_nodes = True
    result.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (*color, 1)
    return result


def block(name, location, dimensions, surface):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    obj.data.materials.append(surface)
    return obj


def camera(name, position, target):
    lens = bpy.data.cameras.new(name)
    lens.lens = 26
    lens.clip_start = 0.05
    obj = bpy.data.objects.new(name, lens)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector(target) - Vector(position)).to_track_quat("-Z", "Y").to_euler()
    return obj


def room():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Synthetic room"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.fps = 24
    floor = material("floor", (0.28, 0.39, 0.38))
    wall = material("wall", (0.20, 0.33, 0.49))
    glow = material("unlit-window", (0.30, 0.73, 0.90))
    barrier = material("shadow-caster", (0.69, 0.32, 0.14))
    proxy = material("occlusion-proxy", (1.0, 0.05, 0.8))
    block("Floor", (0, 0, -0.075), (12, 12, 0.15), floor)
    block("Wall north", (0, 5.9, 1.35), (12, 0.20, 2.7), wall)
    block("Wall east", (5.9, 0, 1.35), (0.20, 12, 2.7), wall)
    block("Window", (0, 5.78, 1.65), (5.5, 0.06, 1.1), glow)
    block("Low cover", (-0.9, 0, 0.38), (0.36, 1.8, 0.76), barrier)
    block("Occluder proxy", (-0.9, 0, 0.42), (0.34, 1.76, 0.84), proxy)
    vane = block("Moving vane", (0.8, 1.8, 1.1), (1.25, 0.15, 0.42), barrier)
    for frame, angle in ((1, 0.0), (25, 0.8), (49, 0.0)):
        vane.rotation_euler.z = angle
        vane.keyframe_insert(data_path="rotation_euler", frame=frame)
    vane.animation_data.action.name = "Vane"
    scene.camera = camera("Camera A", (6.5, -8.0, 7.0), (0, 0.6, 0.5))
    camera("Camera B", (-8.0, -5.2, 6.2), (0.2, 0.8, 0.45))
    return scene


def package(path, captures, glb):
    reference = captures[0]
    field = reference["field"]["name"]
    if any(item["field"]["name"] != field for item in captures):
        raise ValueError("Use two reference exports from the same native field")
    slots = [item["capture"]["cameraSlot"] for item in captures]
    if len(slots) != 2 or len(set(slots)) != 2:
        raise ValueError(f"{field} requires reference captures of two distinct native slots")
    points = [point["field"] for point in reference["calibration"]["landmarks"]]
    if not points:
        raise ValueError(f"{field} must provide walkmesh landmarks")
    xs, ys, zs = list(zip(*points))
    x, y, z = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, median(zs)
    scale = max(max(xs) - min(xs), max(ys) - min(ys), 1) / 8
    basis = [scale, 0, 0, 0, 0, 0, scale, 0, 0, -scale, 0, 0, x, y, z, 1]
    content = glb.read_bytes()
    digest = hashlib.sha256(content).hexdigest()
    materials = [
        {"materialId": "materials:floor", "shading": "lit", "receiveShadows": True},
        {"materialId": "materials:wall", "shading": "lit", "receiveShadows": True},
        {"materialId": "materials:unlit-window", "shading": "unlit"},
        {"materialId": "materials:shadow-caster", "shading": "lit", "castShadows": True},
        {"materialId": "materials:occlusion-proxy", "role": "occluder"},
    ]
    instance = {"id": "room", "assetId": "room", "assetToField": basis, "materials": materials,
                "playback": {"clipId": "animations:Vane"}}
    views = []
    for item, label in zip(captures, "AB"):
        capture = item["capture"]
        slot = capture["cameraSlot"]
        states = {str(tile["animationId"]): capture["backgroundStates"].get(str(tile["animationId"]), -1)
                  for tile in item["backgroundTiles"] if tile["animationId"] != 255}
        x, y = capture["actorScroll"]
        views.append({"id": f"room-{slot}", "cameraSlot": slot, "background": "replace", "nativeStates": states,
                      "projection": "authored", "cameraInstance": "room", "cameraId": f"cameras:Camera {label}",
                      "frameFit": "contain", "cameraMotion": "captureRelative", "scrollBaseline": {"x": x, "y": y},
                      "instances": [instance]})
    manifest = {"schemaVersion": 1, "field": field, "assets": [{"id": "room", "sha256": digest}],
                "views": views}
    path.mkdir(parents=True, exist_ok=False)
    (path / "scene3d" / "assets").mkdir(parents=True)
    (path / "scene3d" / "fields").mkdir()
    (path / "mod.ini").write_text("[Mod]\nenabled=0\n", encoding="utf-8")
    (path / "mod.json").write_text(json.dumps({"id": "field-scene-fixture", "name": "Synthetic 3D field room",
                                              "package": "Synthetic 3D field reference fixture",
                                              "description": {"en": "An original two-camera room for field-scene validation."}}), encoding="utf-8")
    (path / "scene3d" / "component.json").write_text('{"schemaVersion":1}', encoding="utf-8")
    (path / "scene3d" / "fields" / f"{field}.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    (path / "scene3d" / "assets" / f"{digest}.glb").write_bytes(content)
    lighting = {"schemaVersion": 1, "fieldName": field, "primaryLightId": "room-sun",
                "lights": [{"id": "room-sun", "name": "Room sunlight", "enabled": True, "type": "sunlight",
                            "direction": [0.48, 0.36, -0.8], "color": [1, 0.93, 0.84], "intensity": 1.2}],
                "groundShadow": {"opacity": 0.72, "softness": 1}}
    (path / "lighting" / "fields").mkdir(parents=True)
    (path / "lighting" / "component.json").write_text('{"schemaVersion":1}', encoding="utf-8")
    (path / "lighting" / "fields" / f"{field}.json").write_text(json.dumps(lighting, indent=2), encoding="utf-8")
    print(f"Fixture package for {field}, slots {slots[:2]}: {path}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("glb", type=Path)
    parser.add_argument("--reference", action="append", type=Path)
    parser.add_argument("--package", type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if bool(args.reference) != bool(args.package):
        parser.error("--reference and --package are used together")
    scene = room()
    export_scene(args.glb.resolve(), scene.name, None, None)
    if args.reference:
        captures = [json.loads(path.read_text(encoding="utf-8")) for path in args.reference]
        package(args.package.resolve(), captures, args.glb.resolve())


if __name__ == "__main__":
    main()
