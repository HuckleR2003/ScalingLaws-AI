"""Turns the two frame folders into mp4s, and cuts them together.

    python Tools/make_videos.py

Reads `CityFlight~/frame_*.png` and `UiFlight~/ui_*.png`, both written by the recorders in the
project, and writes three files into `Videos~/`:

    bayview.mp4      the map flight
    interface.mp4    the walk through the interface
    scaling_laws.mp4 the two cut together, map first

**ffmpeg comes from imageio-ffmpeg rather than the PATH.** There is no system ffmpeg on the build
machine, and a video tool that only works if somebody installed something first is a tool that will
not be there the day the footage is needed. `pip install imageio-ffmpeg` ships the binary.

The two sources are recorded at the same size on purpose. Concatenating clips of different
dimensions needs a filter graph that re-encodes both, and every re-encode of an already-encoded clip
is another generation of loss for no reason.
"""
import glob
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Videos~')

# Both recorders write at this size. Kept here as one number so a change to either is caught by the
# check below rather than by a video with a black border down one side.
WIDTH, HEIGHT = 1600, 900
FPS = 30

CLIPS = [
    ('bayview.mp4', 'CityFlight~', 'frame_%04d.png', 'frame_*.png'),
    ('interface.mp4', 'UiFlight~', 'ui_%04d.png', 'ui_*.png'),
]


def ffmpeg():
    try:
        import imageio_ffmpeg
    except ImportError:
        sys.exit('No ffmpeg. Run:  python -m pip install imageio-ffmpeg')

    return imageio_ffmpeg.get_ffmpeg_exe()


def run(exe, args, what):
    result = subprocess.run([exe, '-y', '-hide_banner', '-loglevel', 'error'] + args,
                            capture_output=True, text=True)

    if result.returncode != 0:
        sys.exit(f'{what} failed:\n{result.stderr.strip()}')


def encode(exe, name, folder, pattern, glob_pattern):
    source = os.path.join(ROOT, folder)
    frames = sorted(glob.glob(os.path.join(source, glob_pattern)))

    if not frames:
        print(f'  {name}: no frames in {folder}/, skipped')
        return None

    # The recorders number from zero and ffmpeg's image sequence reader starts at one unless it is
    # told otherwise, which silently drops the first frame.
    args = [
        '-framerate', str(FPS),
        '-start_number', '0',
        '-i', os.path.join(source, pattern),
        '-c:v', 'libx264',
        '-preset', 'slow',
        '-crf', '18',
        '-pix_fmt', 'yuv420p',
        # yuv420p halves the chroma resolution, so an odd dimension is not representable and ffmpeg
        # refuses the whole encode rather than rounding.
        '-vf', f'scale={WIDTH}:{HEIGHT}:flags=lanczos',
        '-movflags', '+faststart',
        os.path.join(OUT, name),
    ]

    run(exe, args, name)

    size = os.path.getsize(os.path.join(OUT, name)) / 1e6
    print(f'  {name}: {len(frames)} frames, {len(frames) / FPS:.1f}s, {size:.1f} MB')

    return name


def join(exe, parts):
    """Cuts the clips together without re-encoding them."""
    if len(parts) < 2:
        print('  scaling_laws.mp4: needs both clips, skipped')
        return

    listing = os.path.join(OUT, 'parts.txt')

    with open(listing, 'w', encoding='utf-8') as handle:
        for part in parts:
            # The concat demuxer takes a path per line and reads them in order. Forward slashes,
            # because it parses the file itself and treats a backslash as an escape.
            handle.write("file '" + os.path.join(OUT, part).replace('\\', '/') + "'\n")

    run(exe, ['-f', 'concat', '-safe', '0', '-i', listing,
              '-c', 'copy', os.path.join(OUT, 'scaling_laws.mp4')],
        'scaling_laws.mp4')

    os.remove(listing)

    size = os.path.getsize(os.path.join(OUT, 'scaling_laws.mp4')) / 1e6
    print(f'  scaling_laws.mp4: {" + ".join(parts)}, {size:.1f} MB')


def main():
    exe = ffmpeg()
    os.makedirs(OUT, exist_ok=True)

    print(f'ffmpeg: {os.path.basename(exe)}')

    made = [name for name, folder, pattern, globbed in CLIPS
            if encode(exe, name, folder, pattern, globbed)]

    join(exe, made)
    print(f'\nWritten to {OUT}')


if __name__ == '__main__':
    main()
