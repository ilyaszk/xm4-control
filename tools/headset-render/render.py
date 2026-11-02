# Rendu du casque avec Blender (Cycles) : une vue de trois quarts, fond transparent, PNG (lisible par WinUI sans codec WebP).
# Usage : blender -b -P tools/headset-render/render.py -- <scene.gltf> <image.png> [--angle <degrés>]
import math
import os
import sys

import bpy
from mathutils import Vector

# Affichée à ~440 px de large : 960 px reste net jusqu'à 200 %.
WIDTH, HEIGHT = 960, 840

args = sys.argv[sys.argv.index("--") + 1:]
model_path, out_path = os.path.abspath(args[0]), os.path.abspath(args[1])
angle = float(args[args.index("--angle") + 1]) if "--angle" in args else 55.0

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# Import et normalisation : modèle centré sur l'origine, plus grande dimension = 1.
bpy.ops.import_scene.gltf(filepath=model_path)
meshes = [o for o in scene.objects if o.type == "MESH"]
corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
size = max(hi - lo)

pivot = bpy.data.objects.new("Pivot", None)
scene.collection.objects.link(pivot)
roots = [o for o in scene.objects if o.parent is None and o is not pivot]
for o in roots:
    o.parent = pivot
    o.matrix_parent_inverse = pivot.matrix_world.inverted()
    o.location -= (lo + hi) / 2
pivot.scale = (1 / size,) * 3


def look_at(obj, target=Vector((0, 0, 0))):
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def area_light(name, location, energy, color, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.color, data.size = energy, color, size
    obj = bpy.data.objects.new(name, data)
    obj.location = location
    scene.collection.objects.link(obj)
    look_at(obj)


# Studio : lumière principale douce, contre-jour bleu (accent de l'appli), débouchage léger.
area_light("Key", (1.6, -2.2, 1.8), 220, (1, 0.98, 0.95), 1.6)
area_light("Rim", (-1.8, 2.0, 1.2), 260, (0.55, 0.75, 1.0), 1.2)
area_light("Fill", (-2.4, -1.4, -0.4), 40, (1, 1, 1), 2.5)

world = bpy.data.worlds.new("World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.02, 0.025, 0.03, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
scene.world = world

cam_data = bpy.data.cameras.new("Camera")
cam_data.lens = 70
cam = bpy.data.objects.new("Camera", cam_data)
cam.location = (0, -2.25, 0.36)
scene.collection.objects.link(cam)
look_at(cam, Vector((0, 0, -0.02)))
scene.camera = cam

scene.render.engine = "CYCLES"
scene.cycles.samples = 256
scene.cycles.use_denoising = True
prefs = bpy.context.preferences.addons["cycles"].preferences
for backend in ("OPTIX", "CUDA", "HIP", "ONEAPI"):
    try:
        prefs.compute_device_type = backend
        prefs.get_devices()
        if any(d.type == backend for d in prefs.devices):
            for d in prefs.devices:
                d.use = d.type == backend
            scene.cycles.device = "GPU"
            break
    except TypeError:
        continue
scene.view_settings.view_transform = "AgX"
scene.render.film_transparent = True
scene.render.resolution_x, scene.render.resolution_y = WIDTH, HEIGHT
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.compression = 100
scene.render.image_settings.color_depth = "8"


pivot.rotation_euler = (0, 0, math.radians(angle))
scene.render.filepath = out_path
bpy.ops.render.render(write_still=True)
print(f"Rendu terminé ({scene.cycles.device}) : {out_path}")
