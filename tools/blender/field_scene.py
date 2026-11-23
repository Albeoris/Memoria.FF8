"""Build a local .blend reference or export the active author scene as a field GLB.

blender --background --python field_scene.py -- reference reference.json output.blend
blender --background input.blend --python field_scene.py -- export output.glb --scene Scene
"""
import argparse
import json
import math
import struct
import sys
from pathlib import Path

import bpy


def select_scene(name):
    scene = bpy.data.scenes.get(name) if name else bpy.context.scene
    if scene is None:
        raise ValueError(f"Unknown scene: {name}")
    bpy.context.window.scene = scene
    return scene


def collection(scene, name, reference=False):
    result = bpy.data.collections.new(name)
    scene.collection.children.link(result)
    result["memoria_reference_only"] = reference
    return result


def import_reference(source, destination):
    with source.open(encoding="utf-8") as stream:
        manifest = json.load(stream)
    if manifest["schemaVersion"] != 1:
        raise ValueError("Unsupported Memoria field reference version")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    native = collection(scene, "Memoria Native Reference", True)
    cameras = collection(scene, "Memoria Reference Cameras", True)
    collection(scene, "Memoria Author Geometry")
    collection(scene, "Memoria Author Cameras")
    collection(scene, "Memoria Author Proxies")
    bpy.ops.import_scene.gltf(filepath=str(source.with_name(manifest["referenceGlb"])))
    capture = manifest["capture"]
    scene.render.resolution_x = capture["width"]
    scene.render.resolution_y = capture["height"]
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Standard"
    for obj in list(scene.objects):
        for existing in list(obj.users_collection):
            existing.objects.unlink(obj)
        obj["memoria_reference_only"] = True
        if obj.type != "CAMERA":
            native.objects.link(obj)
            obj.hide_select = True
            obj.hide_render = True
            obj.display_type = "WIRE"
            continue
        cameras.objects.link(obj)
        obj.hide_select = True
        slot = obj.get("memoria_camera_slot")
        record = next(record for record in manifest["cameras"] if record["slot"] == slot)
        projection = record.get("projection")
        if projection is None:
            continue
        width, height = capture["width"], capture["height"]
        fx, fy = projection["focalPixels"]
        px, py = projection["principalPixels"]
        obj.data.sensor_fit = "HORIZONTAL"
        obj.data.lens = fx * obj.data.sensor_width / width
        obj.data.shift_x = (width / 2 - px) / width
        obj.data.shift_y = (py - height / 2) * fx / (fy * width)
        if slot != capture["cameraSlot"]:
            continue
        scene.camera = obj
        pixel_ratio = fx / fy
        scene.render.pixel_aspect_x = max(1, 1 / pixel_ratio)
        scene.render.pixel_aspect_y = max(1, pixel_ratio)
        if capture.get("background"):
            background = obj.data.background_images.new()
            background.image = bpy.data.images.load(str(source.with_name(capture["background"])), check_existing=True)
            background.image.pack()
            background.display_depth = "BACK"
            background.frame_method = "STRETCH"
            background.alpha = 1.0
            obj.data.show_background_images = True
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D" and scene.camera:
                area.spaces.active.region_3d.view_perspective = "CAMERA"
    scene["memoria_field_reference"] = manifest["field"]["name"]
    scene["memoria_asset_to_field"] = manifest["calibration"]["glbToField"]
    bpy.ops.wm.save_as_mainfile(filepath=str(destination), compress=True)


def author_objects(scene, requested):
    references = set()

    def find_references(parent, reference=False):
        reference = reference or parent.get("memoria_reference_only", False)
        if reference:
            references.update(parent.objects)
        for child in parent.children:
            find_references(child, reference)

    find_references(scene.collection)
    if requested:
        unknown = set(requested).difference(obj.name for obj in scene.objects)
        if unknown:
            raise ValueError(f"Objects absent from scene: {', '.join(sorted(unknown))}")
    chosen = {obj for obj in scene.objects
              if obj not in references and not obj.get("memoria_reference_only", False)
              and (not requested or obj.name in requested)}
    pending = list(chosen)
    while pending:
        obj = pending.pop()
        dependencies = [obj.parent]
        dependencies.extend(modifier.object for modifier in obj.modifiers if modifier.type == "ARMATURE")
        for dependency in dependencies:
            if dependency is None or dependency in chosen:
                continue
            if dependency in references or dependency.get("memoria_reference_only", False):
                raise ValueError(f"{obj.name} depends on reference-only object {dependency.name}")
            chosen.add(dependency)
            pending.append(dependency)
    return chosen


def effective_camera(scene, obj):
    render = scene.render
    width = max(1, render.resolution_x * render.resolution_percentage // 100)
    height = max(1, render.resolution_y * render.resolution_percentage // 100)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    camera = obj.evaluated_get(depsgraph)
    matrix = camera.calc_matrix_camera(depsgraph, x=width, y=height, scale_x=render.pixel_aspect_x, scale_y=render.pixel_aspect_y)
    perspective = camera.data.type == "PERSP"
    if not perspective and camera.data.type != "ORTHO":
        raise ValueError(f"Unsupported {camera.data.type} camera: {obj.name}")
    offset_x = -matrix[0][2] if perspective else matrix[0][3]
    offset_y = -matrix[1][2] if perspective else matrix[1][3]
    return {
        "width": width,
        "height": height,
        "pixelAspect": [render.pixel_aspect_x, render.pixel_aspect_y],
        "sensorFit": camera.data.sensor_fit if camera.data.sensor_fit != "AUTO" else
                     ("HORIZONTAL" if width * render.pixel_aspect_x >= height * render.pixel_aspect_y else "VERTICAL"),
        "principalPoint": [(1 + offset_x) / 2, (1 - offset_y) / 2],
        "projectionMatrix": [list(row) for row in matrix],
        "framing": "contain",
    }


def read_glb(path):
    data = path.read_bytes()
    magic, version, length, json_size, json_type = struct.unpack_from("<5I", data)
    if (magic, version, length, json_type) != (0x46546C67, 2, len(data), 0x4E4F534A):
        raise ValueError("Invalid Blender GLB output")
    return json.loads(data[20:20 + json_size]), data[20 + json_size:]


def write_glb(path, document, binary):
    encoded = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    encoded += b" " * (-len(encoded) % 4)
    path.write_bytes(struct.pack("<5I", 0x46546C67, 2, 20 + len(encoded) + len(binary), len(encoded), 0x4E4F534A)
                     + encoded + binary)


def reachable_shader_nodes(tree):
    found = set()
    pending = [node for node in tree.nodes if node.type == "OUTPUT_MATERIAL" and node.is_active_output]
    while pending:
        node = pending.pop()
        if node in found:
            continue
        found.add(node)
        pending.extend(link.from_node for socket in node.inputs for link in socket.links)
    return found


def export_scene(destination, scene_name, object_names, preset):
    scene = select_scene(scene_name)
    chosen = author_objects(scene, object_names)
    view_layer = bpy.context.view_layer
    for obj in view_layer.objects:
        obj.select_set(obj in chosen)
    unavailable = chosen.difference(obj for obj in view_layer.objects if obj.select_get())
    if unavailable:
        raise ValueError(f"Objects unavailable to the active view layer: {', '.join(sorted(obj.name for obj in unavailable))}")
    if not any(obj.type == "MESH" for obj in chosen):
        raise ValueError("The selected author scene contains no mesh")
    camera_frames = {obj.name: effective_camera(scene, obj) for obj in chosen if obj.type == "CAMERA"}
    warnings = []
    materials = {slot.material for obj in chosen if obj.type == "MESH" for slot in obj.material_slots if slot.material}
    for material in materials:
        if not material.node_tree:
            continue
        nodes = reachable_shader_nodes(material.node_tree)
        images = [node.image for node in nodes if node.type == "TEX_IMAGE" and node.image]
        if len(images) != len(set(images)):
            warnings.append(f"{material.name}: multiple shader nodes sample one image; verify the exported sampler")
        for image in images:
            if not image.packed_file and not image.has_data and not Path(bpy.path.abspath(image.filepath)).is_file():
                raise ValueError(f"Image used by {material.name} is unavailable: {image.filepath}")
    for obj in chosen:
        for modifier in obj.modifiers:
            if modifier.type != "ARMATURE" and modifier.show_render:
                warnings.append(f"{obj.name}: {modifier.type} modifier is not applied")
            if modifier.type == "ARMATURE" and modifier.object and obj.type == "MESH":
                deform_bones = {bone.name for bone in modifier.object.data.bones if bone.use_deform}
                deform_groups = {group.index for group in obj.vertex_groups if group.name in deform_bones}
                if any(sum(group.weight > 0 and group.group in deform_groups for group in vertex.groups) > 4
                       for vertex in obj.data.vertices):
                    raise ValueError(f"{obj.name} has more than four positive deform weights per vertex")
        if obj.type == "LIGHT":
            warnings.append(f"{obj.name}: Blender light is omitted; configure field lighting separately")
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(f".{destination.stem}.export.glb")
    try:
        bpy.ops.export_scene.gltf(
            filepath=str(temporary), export_format="GLB", use_selection=True, use_active_scene=True,
            export_cameras=True, export_extras=True, export_animations=True, export_animation_mode="ACTIONS",
            export_force_sampling=True, export_frame_range=False, export_frame_step=1,
            export_anim_slide_to_zero=True, export_optimize_animation_size=False, export_apply=False,
            export_anim_single_armature=False,
            export_all_influences=False, export_yup=True)
        document, binary = read_glb(temporary)
        for prefix in ("scenes", "nodes", "materials", "animations", "cameras"):
            for index, item in enumerate(document.get(prefix, [])):
                stable_id = item.get("extras", {}).get("memoria_scene_id", f"{prefix}:{item.get('name', index)}")
                item["extras"] = {"memoria_scene_id": stable_id}
        for camera in document.get("cameras", []):
            frame = camera_frames.get(camera.get("name"))
            if frame is None:
                node = next((node for node in document["nodes"] if node.get("camera") == document["cameras"].index(camera)), None)
                frame = camera_frames.get(node.get("name")) if node else None
            if frame is None:
                raise ValueError(f"Cannot resolve framing metadata for camera {camera.get('name')}")
            p = frame["projectionMatrix"]
            if camera["type"] == "perspective":
                camera["perspective"]["yfov"] = 2 * math.atan(1 / abs(p[1][1]))
                camera["perspective"]["aspectRatio"] = abs(p[1][1] / p[0][0])
            else:
                camera["orthographic"]["xmag"] = 1 / abs(p[0][0])
                camera["orthographic"]["ymag"] = 1 / abs(p[1][1])
            camera["extras"]["memoria_projection"] = frame
            if any(abs(value - 0.5) > 1e-6 for value in frame["principalPoint"]):
                warnings.append(f"{camera['name']}: GLB viewers may ignore the recorded off-center projection")
        presets = {}
        if preset == "unlitShadowReceiver":
            for material in document.get("materials", []):
                material.setdefault("extensions", {}).setdefault("KHR_materials_unlit", {})
                if "KHR_materials_unlit" not in document.setdefault("extensionsUsed", []):
                    document["extensionsUsed"].append("KHR_materials_unlit")
                presets[material["extras"]["memoria_scene_id"]] = {
                    "lighting": preset, "castShadows": False, "receiveShadows": True,
                }
        summary = {
            "schemaVersion": 1, "blenderVersion": bpy.app.version_string, "generator": document["asset"].get("generator"),
            "scene": scene.name, "objects": sorted(obj.name for obj in chosen), "cameras": camera_frames,
            "images": [image.get("name") for image in document.get("images", [])],
            "meshes": len(document.get("meshes", [])), "skins": len(document.get("skins", [])),
            "materials": [{"name": mat.get("name"), "alphaMode": mat.get("alphaMode", "OPAQUE"),
                           "doubleSided": mat.get("doubleSided", False)} for mat in document.get("materials", [])],
            "clips": [{"name": animation.get("name"), "duration": max(
                (document["accessors"][sample["input"]]["max"][0] for sample in animation["samplers"]), default=0)}
                for animation in document.get("animations", [])],
            "samplesPerSecond": scene.render.fps / scene.render.fps_base,
            "materialPresets": presets, "warnings": warnings,
        }
        document["asset"].setdefault("extras", {})["memoria_material_presets"] = presets
        write_glb(temporary, document, binary)
        temporary.replace(destination)
        destination.with_suffix(".json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(summary, indent=2))
    finally:
        temporary.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    reference = subparsers.add_parser("reference")
    reference.add_argument("source", type=Path)
    reference.add_argument("destination", type=Path)
    export = subparsers.add_parser("export")
    export.add_argument("destination", type=Path)
    export.add_argument("--scene")
    export.add_argument("--object", action="append")
    export.add_argument("--material-preset", choices=["unlitShadowReceiver"])
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if args.command == "reference":
        import_reference(args.source.resolve(), args.destination.resolve())
    else:
        export_scene(args.destination.resolve(), args.scene, args.object, args.material_preset)


if __name__ == "__main__":
    main()
