#!/usr/bin/env python3
"""Export the title meteor's reusable object-space solid as a closed OBJ mesh.

Regenerate Artwork/Meteor.obj whenever MeteorDistance, hash or noise changes in
src/Memoria.FF8.TitleScene/Shaders/MeteorScene.fx. This is a standard-library
translation of that implicit model, independent of combustion and animation.
Python evaluates the shared equations in double precision; GPU noise detail can
vary with backend floating-point arithmetic. The long axis is X, incoming face -X.
"""

import argparse
from collections import Counter
import math
from pathlib import Path


def frac(value):
    return value - math.floor(value)


def hash_value(point):
    x, y, z = (frac(value * 0.1031) for value in point)
    offset = x * (y + 33.33) + y * (z + 33.33) + z * (x + 33.33)
    x, y, z = x + offset, y + offset, z + offset
    return frac((x + y) * z)


def lerp(a, b, amount):
    return a + (b - a) * amount


def noise(point):
    integer = tuple(math.floor(value) for value in point)
    f = tuple(frac(value) for value in point)
    x, y, z = (value * value * (3 - 2 * value) for value in f)

    def corner(dx, dy, dz):
        return hash_value((integer[0] + dx, integer[1] + dy, integer[2] + dz))

    return lerp(
        lerp(lerp(corner(0, 0, 0), corner(1, 0, 0), x), lerp(corner(0, 1, 0), corner(1, 1, 0), x), y),
        lerp(lerp(corner(0, 0, 1), corner(1, 0, 1), x), lerp(corner(0, 1, 1), corner(1, 1, 1), x), y),
        z,
    )


def meteor_distance(point):
    q = tuple(value * scale for value, scale in zip(point, (0.88, 1.05, 1.0)))
    relief = noise(tuple(value * 3.7 + 4.1 for value in q)) * 0.18
    relief += noise(tuple(value * 8.3 for value in q)) * 0.065
    relief += abs(noise(tuple(value * 26 for value in q)) - 0.5) * 0.022
    return (math.sqrt(sum(value * value for value in q)) - 0.84 - relief) * 0.72


def normalize(vector):
    length = math.sqrt(sum(value * value for value in vector))
    if length < 1e-12:
        raise ValueError("Cannot normalize a zero vector.")
    return tuple(value / length for value in vector)


def normal(point):
    # Finite differences of the implicit solid, rather than averaged mesh faces.
    epsilon = 0.0001
    gradient = []
    for axis in range(3):
        plus, minus = list(point), list(point)
        plus[axis] += epsilon
        minus[axis] -= epsilon
        gradient.append(meteor_distance(plus) - meteor_distance(minus))
    return normalize(gradient)


def surface(direction):
    low, high = 0.0, 1.5
    if meteor_distance(tuple(value * high for value in direction)) <= 0:
        raise ValueError("Surface root is outside the extraction radius.")
    for _ in range(40):
        middle = (low + high) * 0.5
        if meteor_distance(tuple(value * middle for value in direction)) < 0:
            low = middle
        else:
            high = middle
    radius = (low + high) * 0.5
    # Validate the rounded coordinates actually written to the OBJ.
    return tuple(round(value * radius, 7) for value in direction)


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def subtract(a, b):
    return tuple(x - y for x, y in zip(a, b))


def build_mesh(longitudes, latitudes):
    vertices = [surface((0, 1, 0))]
    for latitude in range(1, latitudes):
        theta = math.pi * latitude / latitudes
        for longitude in range(longitudes):
            phi = math.tau * longitude / longitudes
            vertices.append(surface((math.sin(theta) * math.cos(phi), math.cos(theta), math.sin(theta) * math.sin(phi))))
    vertices.append(surface((0, -1, 0)))
    bottom = len(vertices) - 1

    def ring(latitude, longitude):
        return 1 + latitude * longitudes + longitude % longitudes

    faces = []
    for longitude in range(longitudes):
        faces.append((0, ring(0, longitude + 1), ring(0, longitude)))
        for latitude in range(latitudes - 2):
            a, b = ring(latitude, longitude), ring(latitude, longitude + 1)
            c, d = ring(latitude + 1, longitude), ring(latitude + 1, longitude + 1)
            faces.extend(((a, b, c), (b, d, c)))
        faces.append((ring(latitudes - 2, longitude), ring(latitudes - 2, longitude + 1), bottom))
    return vertices, faces


def validate(vertices, faces):
    edges, directed = Counter(), Counter()
    for face in faces:
        a, b, c = (vertices[index] for index in face)
        area = cross(subtract(b, a), subtract(c, a))
        if sum(value * value for value in area) < 1e-20:
            raise ValueError("Degenerate triangle.")
        center = tuple((x + y + z) / 3 for x, y, z in zip(a, b, c))
        if sum(x * y for x, y in zip(area, center)) <= 0:
            raise ValueError("Triangle winding is not outward.")
        for first, second in zip(face, face[1:] + face[:1]):
            edges[tuple(sorted((first, second)))] += 1
            directed[(first, second)] += 1
    if any(count != 2 for count in edges.values()):
        raise ValueError("Every undirected edge must have two incident faces.")
    if any(directed[(a, b)] != 1 or directed[(b, a)] != 1 for a, b in edges):
        raise ValueError("Adjacent triangles must have consistent winding.")
    if len(vertices) - len(edges) + len(faces) != 2:
        raise ValueError("Expected a closed spherical topology.")
    maximum_distance = max(abs(meteor_distance(vertex)) for vertex in vertices)
    if maximum_distance > 0.000002:
        raise ValueError(f"Vertices exceed surface tolerance: {maximum_distance:.8g}.")
    return len(edges), maximum_distance


def bounded_integer(minimum, maximum):
    def parse(value):
        result = int(value)
        if not minimum <= result <= maximum:
            raise argparse.ArgumentTypeError(f"Expected {minimum}..{maximum}.")
        return result
    return parse


def main():
    product = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=product / "src/Memoria.FF8.TitleScene/Artwork/Meteor.obj")
    parser.add_argument("--longitudes", type=bounded_integer(8, 192), default=96)
    parser.add_argument("--latitudes", type=bounded_integer(4, 96), default=48)
    args = parser.parse_args()
    vertices, faces = build_mesh(args.longitudes, args.latitudes)
    edges, maximum_distance = validate(vertices, faces)
    lines = ["# MeteorDistance object-space solid; regenerate with tools/Export-MeteorModel.py.", "# X is the long axis; incoming face is -X. No animation or combustion is baked.", "o Meteor"]
    lines.extend("v " + " ".join(f"{value:.7f}" for value in vertex) for vertex in vertices)
    lines.extend("vn " + " ".join(f"{value:.7f}" for value in normal(vertex)) for vertex in vertices)
    lines.append("s 1")
    lines.extend("f " + " ".join(f"{index + 1}//{index + 1}" for index in face) for face in faces)
    payload = ("\n".join(lines) + "\n").encode("ascii")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(payload)
    print(f"Meteor OBJ: {len(vertices)} vertices, {len(faces)} triangles, {edges} edges, {len(payload)} bytes; closed/outward/nondegenerate; max |distance|={maximum_distance:.3g}")


if __name__ == "__main__":
    main()
