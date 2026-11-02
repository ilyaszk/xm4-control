# Export du modèle du casque pour la fenêtre principale (three.js) : un seul .glb, textures réduites en JPEG.
# Usage : blender -b -P tools/headset-render/export_web.py -- <scene.gltf> <headset.glb> [--size <px>]
import os
import sys

import bpy

args = sys.argv[sys.argv.index("--") + 1:]
model_path, out_path = os.path.abspath(args[0]), os.path.abspath(args[1])
size = int(args[args.index("--size") + 1]) if "--size" in args else 1024

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=model_path)

# Textures 4K d'origine (~17 Mo) : 1024 px suffisent pour un casque affiché à ~500 px.
for image in bpy.data.images:
    w, h = image.size
    if max(w, h) > size:
        image.scale(size, max(1, round(h * size / w)))

bpy.ops.export_scene.gltf(
    filepath=out_path,
    export_format="GLB",
    export_image_format="JPEG",
    export_jpeg_quality=85,
    export_animations=False,
    export_cameras=False,
    export_lights=False,
)
print(f"Export terminé : {out_path} ({os.path.getsize(out_path) / 1e6:.1f} Mo)")
