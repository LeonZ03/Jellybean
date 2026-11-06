"""Run inside Blender with the original CC0 Cat.blend open; writes a 2D motion reference."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view


def export(destination):
    output = Path(destination)
    output.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    arm = bpy.data.objects['CatArmature']
    cat = bpy.data.objects['Cat']
    for track in arm.animation_data.nla_tracks:
        track.mute = True
    # The source already has Walking active; retain the author's curves and joint constraints.
    if arm.animation_data.action.name != 'Walking':
        raise ValueError('Open the original Cat.blend with Walking active.')
    cat.show_wire = False
    cat.show_all_edges = False
    camera_data = bpy.data.cameras.new('JellybeanMotionReference')
    camera = bpy.data.objects.new('JellybeanMotionReference', camera_data)
    scene.collection.objects.link(camera)
    camera.location = (0.4, 18, 2.25)
    camera.rotation_euler = (Vector((0.4, 0, 2.25)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 7.5
    scene.camera = camera
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = False
    scene.render.resolution_x = 384
    scene.render.resolution_y = 288
    scene.render.resolution_percentage = 100
    scene.render.use_border = False
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'

    def project(point):
        p = world_to_camera_view(scene, camera, point)
        return [round(p.x * 384, 3), round((1-p.y) * 288, 3)]

    # +Y is the side nearest this camera. These IDs remain fixed for the full cycle.
    feet = {'front_near': 'Bone.019', 'front_far': 'Bone.016',
            'hind_near': 'Bone.010', 'hind_far': 'Bone.013'}
    samples = []
    scene.frame_set(40)
    scene.frame_set(0)
    for i in range(100):
        time = 40 * i / 100
        scene.frame_set(math.floor(time), subframe=time % 1)
        bpy.context.view_layer.update()
        sample = {'frame': i, 'source_frame': time, 'feet': {}}
        for name, bone in feet.items():
            p = arm.matrix_world @ arm.pose.bones[bone].head
            sample['feet'][name] = {'world': list(p), 'pixel': project(p), 'support': p.z < .10}
        for name, bone in [('hip', 'Bone.001'), ('head', 'Bone.003')]:
            sample[name] = project(arm.matrix_world @ arm.pose.bones[bone].head)
        samples.append(sample)
        scene.render.filepath = str(output / f'{i:04d}.png')
        bpy.ops.render.render(write_still=True)
    metadata = {'author': 'Quaternius', 'license': 'CC0-1.0', 'action': 'Walking',
                'source': 'https://opengameart.org/content/animated-animales-low-poly',
                'source_cycle_frames': 40, 'source_fps': 24, 'fps': 60, 'frames': 100,
                'width': 384, 'height': 288, 'floor': project(Vector((0, 0, -.0563)))[1],
                'purpose': 'Motion reference only; this is not the approved Jellybean character.',
                'samples': samples}
    (output.parent / 'motion.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    return {'frames': 100, 'fps': 60, 'folder': str(output),
            'both_hind_airborne': sum(not s['feet']['hind_near']['support'] and
                                     not s['feet']['hind_far']['support'] for s in samples)}


if __name__ == '__main__':
    export(sys.argv[sys.argv.index('--') + 1])
