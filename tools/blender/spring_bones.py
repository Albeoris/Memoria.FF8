"""Tune a skeleton-only Memoria spring profile using the VRM Add-on for Blender.

In Blender's Python Console, add this file's directory to sys.path, then run:
    import spring_bones
    spring_bones.load_profile(r"path/to/d025.springs.glb")

Select the armature before loading. Edit its Spring Bone 1.0 joint settings,
then run:
    spring_bones.save_profile(r"path/to/d025.springs.glb", r"edited/d025.springs.glb")

Loading replaces the armature's Spring Bone 1.0 chains. Saving uses the source
chain order, node hierarchy and transforms, including the terminal offsets.
"""

import json
import struct
from pathlib import Path
from tempfile import NamedTemporaryFile

import bpy
from mathutils import Vector


EXTENSION = "VRMC_springBone"
GLB_HEADER = struct.Struct("<4sIII4s")
JOINT_VALUES = (
    ("hitRadius", "hit_radius", 0.0),
    ("stiffness", "stiffness", 1.0),
    ("gravityPower", "gravity_power", 0.0),
    ("dragForce", "drag_force", 0.5),
)


def read_profile(path):
    content = Path(path).read_bytes()
    if len(content) < GLB_HEADER.size:
        raise ValueError("Select a skeleton-only glTF 2.0 GLB spring profile.")
    magic, version, length, chunk_length, chunk = GLB_HEADER.unpack_from(content)
    if (magic, version, length, chunk_length, chunk) != (b"glTF", 2, len(content), len(content) - GLB_HEADER.size, b"JSON"):
        raise ValueError("Select a skeleton-only glTF 2.0 GLB spring profile.")
    document = json.loads(content[GLB_HEADER.size:])
    if any(document.get(key) for key in ("animations", "buffers", "images", "meshes")):
        raise ValueError("Select a skeleton-only glTF 2.0 GLB spring profile.")
    extension = document.get("extensions", {}).get(EXTENSION, {})
    if extension.get("specVersion") != "1.0" or not extension.get("springs"):
        raise ValueError("The profile needs at least one VRMC_springBone 1.0 chain.")
    if extension.get("colliders") or extension.get("colliderGroups"):
        raise ValueError("Use the VRM add-on's avatar workflow to author collider groups.")
    return document, extension["springs"]


def get_armature(armature):
    if armature is None:
        armature = bpy.context.object
    if armature is None or armature.type != "ARMATURE" or armature != bpy.context.object or armature.mode != "OBJECT":
        raise ValueError("Make the matching armature active in Object Mode.")
    extension = getattr(armature.data, "vrm_addon_extension", None)
    if extension is None:
        raise RuntimeError("Enable the VRM Add-on for Blender before editing springs.")
    return armature, extension


def find_bone(armature, node):
    name = node.get("name")
    native_index = node.get("extras", {}).get("FF8_bone_index")
    if native_index is None and name and name.startswith("bone_") and name[5:].isdecimal():
        native_index = int(name[5:])
    if name:
        exact = armature.data.bones.get(name)
        if exact is not None:
            return exact.name
    if native_index is not None:
        matches = [bone.name for bone in armature.data.bones if bone.get("FF8_bone_index") == native_index]
        if len(matches) == 1:
            return matches[0]
    return None


def resolve_chains(armature, document, springs, add_tips=False):
    nodes = document["nodes"]
    chains = []
    tips = {}
    for spring in springs:
        joints = spring["joints"]
        if len(joints) < 2:
            raise ValueError("A spring chain needs at least one driven joint and a terminal joint.")
        bone_names = [find_bone(armature, nodes[joint["node"]]) for joint in joints]
        for name, joint in zip(bone_names[:-1], joints[:-1]):
            if name is None:
                raise ValueError(f"The armature has no bone for glTF node {joint['node']}.")
        if bone_names[-1] is None:
            parent = nodes[joints[-2]["node"]]
            terminal = nodes[joints[-1]["node"]]
            name = terminal.get("name")
            matrix = terminal.get("matrix")
            offset = Vector(matrix[12:15] if matrix is not None else terminal.get("translation", (0, 0, 0)))
            if not add_tips or not name or joints[-1]["node"] not in parent.get("children", []) or not offset.length:
                raise ValueError(f"The armature has no bone for terminal glTF node {joints[-1]['node']}.")
            tips[name] = (bone_names[-2], offset.length)
            bone_names[-1] = name
        center = find_bone(armature, nodes[spring["center"]]) if "center" in spring else None
        if "center" in spring and center is None:
            raise ValueError(f"The armature has no bone for center glTF node {spring['center']}.")
        chains.append((bone_names, center))
    if tips:
        bpy.ops.object.mode_set(mode="EDIT")
        try:
            for name, (parent_name, length) in tips.items():
                parent = armature.data.edit_bones[parent_name]
                direction = (parent.tail - parent.head).normalized()
                tail = armature.data.edit_bones.new(name)
                tail.head = parent.head + direction * length
                tail.tail = tail.head + direction * (length * 0.25)
                tail.parent = parent
                tail.use_deform = False
        finally:
            bpy.ops.object.mode_set(mode="OBJECT")
    return chains


def load_profile(path, armature=None):
    """Load a profile into the active armature's Spring Bone 1.0 settings."""
    armature, extension = get_armature(armature)
    document, springs = read_profile(path)
    chains = resolve_chains(armature, document, springs, add_tips=True)
    extension.spec_version = "1.0"
    settings = extension.spring_bone1
    settings.springs.clear()
    for source, (names, center) in zip(springs, chains):
        spring = settings.add_spring()
        spring.vrm_name = source.get("name", "")
        if center is not None:
            spring.center.bone_name = center
        for source_joint, name in zip(source["joints"], names):
            joint = spring.add_joint()
            joint.node.bone_name = name
            for key, attribute, default in JOINT_VALUES:
                setattr(joint, attribute, source_joint.get(key, default))
            x, y, z = source_joint.get("gravityDir", (0, -1, 0))
            joint.gravity_dir = (x, -z, y)
        spring.active_joint_index = 0
    settings.active_spring_index = 0


def save_profile(source, destination, armature=None):
    """Write edited spring tuning into a copy of the source GLB skeleton."""
    armature, extension = get_armature(armature)
    document, springs = read_profile(source)
    chains = resolve_chains(armature, document, springs)
    edited = extension.spring_bone1.springs
    if len(edited) != len(springs):
        raise ValueError("The number of chains must match the source spring profile.")
    for spring, original, (names, center) in zip(edited, springs, chains):
        if [joint.node.bone_name for joint in spring.joints] != names or (spring.center.bone_name or None) != center or spring.collider_groups:
            raise ValueError("Keep the profile's original joints, centers and collider groups when tuning it.")
        if spring.vrm_name or "name" in original:
            original["name"] = spring.vrm_name
        for joint, target in zip(list(spring.joints)[:-1], original["joints"][:-1]):
            for key, attribute, default in JOINT_VALUES:
                value = getattr(joint, attribute)
                if struct.unpack("f", struct.pack("f", target.get(key, default)))[0] != value:
                    target[key] = value
            x, y, z = joint.gravity_dir
            direction = [x, z, -y]
            if struct.unpack("3f", struct.pack("3f", *target.get("gravityDir", (0, -1, 0)))) != tuple(direction):
                target["gravityDir"] = direction
    payload = json.dumps(document, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")
    payload += b" " * (-len(payload) % 4)
    header = GLB_HEADER.pack(b"glTF", 2, GLB_HEADER.size + len(payload), len(payload), b"JSON")
    destination = Path(destination)
    with NamedTemporaryFile(dir=destination.parent, delete=False) as output:
        temporary = Path(output.name)
        try:
            output.write(header + payload)
            output.close()
            temporary.replace(destination)
        finally:
            temporary.unlink(missing_ok=True)
