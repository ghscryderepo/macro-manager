"""Build a Windows ICO from the 32x32 Lineage II item icon.

Requires ffmpeg only for PNG resizing; ICO packaging uses the standard library.
"""

from pathlib import Path
import struct
import subprocess

root = Path(__file__).resolve().parents[1]
source = root / "assets" / "hero-tiara-source.jpg"
output = root / "assets" / "hero-tiara.ico"

sizes = (16, 32, 48, 256)
images = []
for size in sizes:
    image = subprocess.check_output(
        [
            "ffmpeg", "-v", "error", "-i", str(source), "-vf",
            f"scale={size}:{size}:flags=neighbor", "-frames:v", "1",
            "-f", "image2pipe", "-vcodec", "png", "-",
        ]
    )
    images.append(image)

header = struct.pack("<HHH", 0, 1, len(sizes))
offset = len(header) + 16 * len(sizes)
entries = []
for size, image in zip(sizes, images):
    entries.append(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(image), offset))
    offset += len(image)

output.write_bytes(header + b"".join(entries) + b"".join(images))
print(output)
