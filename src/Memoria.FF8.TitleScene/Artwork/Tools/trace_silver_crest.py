"""Trace the design reference into bounded contour geometry; never used at runtime."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np


def trace(mask: np.ndarray, minimum_area: float = 8.0) -> list[list[list[float]]]:
    contours, _ = cv2.findContours(mask.astype(np.uint8), cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
    result = []
    for contour in contours:
        if abs(cv2.contourArea(contour)) < minimum_area:
            continue
        polygon = cv2.approxPolyDP(contour, 0.72, True).reshape(-1, 2)
        if len(polygon) >= 3:
            result.append(polygon.astype(float).tolist())
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--levels", type=int, default=40)
    args = parser.parse_args()
    source = cv2.imread(str(args.source), cv2.IMREAD_UNCHANGED)
    alpha = source[:, :, 3] > 150
    gray = cv2.cvtColor(source[:, :, :3], cv2.COLOR_BGR2GRAY).astype(np.float32)
    # Preserve the sculpted ridges while discarding photographic brushed microtexture.
    gray[~alpha] = 145
    gray = cv2.bilateralFilter(gray, 9, 26, 5)
    weights = cv2.GaussianBlur(alpha.astype(np.float32), (0, 0), 1.05)
    gray = cv2.GaussianBlur(gray * alpha, (0, 0), 1.05) / np.maximum(weights, 0.0001)
    bins = np.minimum((gray * args.levels / 256).astype(np.int32), args.levels - 1)
    silhouette = trace(alpha, 20)
    layers = []
    for level in range(args.levels):
        contours = trace((bins >= level) & alpha)
        if contours:
            value = int((level + 0.5) * 256 / args.levels)
            layers.append({"value": value, "contours": contours})
    result = {"format": "silver-crest-contours-v1", "source_sha256": hashlib.sha256(args.source.read_bytes()).hexdigest(), "width": int(source.shape[1]), "height": int(source.shape[0]), "tone_levels": args.levels, "simplification_pixels": 0.72, "silhouette": silhouette, "layers": layers}
    args.output.write_text(json.dumps(result, separators=(",", ":")), encoding="utf-8")
    vertex_count = 0
    for layer in layers:
        edges = np.concatenate([np.concatenate([np.asarray(contour), np.roll(np.asarray(contour), 1, axis=0)], axis=1) for contour in layer["contours"]])
        rows = np.unique(edges[:, [1, 3]])
        for y in (rows[:-1] + rows[1:]) / 2:
            crossings = np.count_nonzero(((edges[:, 1] < y) & (edges[:, 3] > y)) | ((edges[:, 3] < y) & (edges[:, 1] > y)))
            vertex_count += int(crossings // 2 * 6)
    print(json.dumps({"layers": len(layers), "silhouette_points": sum(map(len, silhouette)), "contours": sum(len(layer["contours"]) for layer in layers), "points": sum(sum(map(len, layer["contours"])) for layer in layers), "triangle_vertices": vertex_count, "bytes": args.output.stat().st_size}))


if __name__ == "__main__":
    main()
