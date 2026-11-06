"""Assemble an offline reference viewer from the author's complete animation frames.

Requires FFmpeg on PATH. Render the source with export_quaternius.py first.
"""
import base64
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'design/motion-reference'
FRAMES = ROOT / 'artifacts/motion-library/quaternius/frames'
PIXELS = ROOT / 'assets/motion-reference/shepardskin'


def ffmpeg(*args):
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', *map(str, args)], check=True)


def encoded(path):
    return base64.b64encode(path.read_bytes()).decode('ascii')


def build():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(FRAMES.parent / 'motion.json', OUTPUT / 'motion.json')
    ffmpeg('-framerate', 60, '-i', FRAMES / '%04d.png', '-vf', 'tile=10x10',
           '-frames:v', 1, OUTPUT / 'atlas.png')
    for action in ['walk', 'run']:
        ffmpeg('-i', PIXELS / f'{action}.gif', '-vf', 'tile=6x1',
               '-frames:v', 1, OUTPUT / f'shepardskin-{action}.png')
    background = 'color=c=0xf2eadc:s=384x288:r=60'
    ffmpeg('-f', 'lavfi', '-i', background, '-framerate', 60, '-i', FRAMES / '%04d.png',
           '-filter_complex', '[0:v][1:v]overlay=shortest=1,format=yuv420p',
           '-c:v', 'libx264', '-crf', 18, '-movflags', '+faststart', OUTPUT / 'walk.mp4')
    ffmpeg('-i', OUTPUT / 'walk.mp4', '-vf',
           'fps=30,split[a][b];[a]palettegen[p];[b][p]paletteuse', OUTPUT / 'walk.gif')
    template = (Path(__file__).parent / 'preview.html').read_text(encoding='utf-8')
    replacements = {
        '__MOTION_DATA__': json.dumps(json.loads((OUTPUT / 'motion.json').read_text(encoding='utf-8')),
                                    separators=(',', ':')),
        '__MOTION_ATLAS__': encoded(OUTPUT / 'atlas.png'),
        '__PIXEL_WALK__': encoded(OUTPUT / 'shepardskin-walk.png'),
        '__PIXEL_RUN__': encoded(OUTPUT / 'shepardskin-run.png'),
    }
    for name, value in replacements.items():
        template = template.replace(name, value)
    (OUTPUT / 'index.html').write_text(template, encoding='utf-8')
    print(f'Built {OUTPUT / "index.html"}')


if __name__ == '__main__':
    build()
