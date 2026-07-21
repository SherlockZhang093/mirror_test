import json
import struct
import sys
from pathlib import Path


def main() -> None:
    source = Path(sys.argv[1])
    output_dir = Path(sys.argv[2])
    output_dir.mkdir(parents=True, exist_ok=True)

    with source.open("rb") as stream:
        magic, version, _ = struct.unpack("<4sII", stream.read(12))
        if magic != b"glTF" or version != 2:
            raise ValueError("Expected a glTF 2.0 binary file")

        json_length, json_type = struct.unpack("<II", stream.read(8))
        if json_type != 0x4E4F534A:
            raise ValueError("GLB JSON chunk is missing")
        document = json.loads(stream.read(json_length).decode("utf-8").rstrip(" \0"))

        binary_length, binary_type = struct.unpack("<II", stream.read(8))
        if binary_type != 0x004E4942:
            raise ValueError("GLB binary chunk is missing")
        binary = stream.read(binary_length)

    semantic_names = {}
    material = document["materials"][0]
    pbr = material.get("pbrMetallicRoughness", {})
    if "baseColorTexture" in pbr:
        semantic_names[pbr["baseColorTexture"]["index"]] = "HunyuanTemple_BaseColor.png"
    if "metallicRoughnessTexture" in pbr:
        semantic_names[pbr["metallicRoughnessTexture"]["index"]] = "HunyuanTemple_MetallicRoughness.png"
    if "normalTexture" in material:
        semantic_names[material["normalTexture"]["index"]] = "HunyuanTemple_Normal.png"

    for texture_index, texture in enumerate(document.get("textures", [])):
        image = document["images"][texture["source"]]
        view = document["bufferViews"][image["bufferView"]]
        start = view.get("byteOffset", 0)
        end = start + view["byteLength"]
        filename = semantic_names.get(texture_index, f"HunyuanTemple_Texture_{texture_index}.png")
        (output_dir / filename).write_bytes(binary[start:end])
        print(f"Extracted {filename} ({view['byteLength']} bytes)")


if __name__ == "__main__":
    main()
