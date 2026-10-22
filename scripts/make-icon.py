"""Remove only edge-connected black background; retain the original circular art."""
from collections import deque
from pathlib import Path
import sys
from PIL import Image, PngImagePlugin

src = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(r'E:\Downloads\BetterMalodyV.jpg')
out = Path(__file__).resolve().parents[1] / 'src/BetterMalodyV.Windows/Assets'
im = Image.open(src).convert('RGBA')
im.info.pop('icc_profile', None)
w, h = im.size
pixels = im.load()
seen = bytearray(w * h)
queue = deque([(x, 0) for x in range(w)] + [(x, h-1) for x in range(w)]
              + [(0, y) for y in range(h)] + [(w-1, y) for y in range(h)])
while queue:
    x, y = queue.popleft()
    p = y * w + x
    if seen[p]:
        continue
    seen[p] = 1
    r, g, b, a = pixels[x, y]
    if max(r, g, b) > 32:
        continue
    pixels[x, y] = (r, g, b, 0)
    for nx, ny in ((x-1,y), (x+1,y), (x,y-1), (x,y+1)):
        if 0 <= nx < w and 0 <= ny < h and not seen[ny*w+nx]:
            queue.append((nx,ny))
meta = PngImagePlugin.PngInfo()
meta.add_text('Source', str(src) + '; edge-connected black background removed with make-icon.py')
im.save(out / 'mtp.png', pnginfo=meta)
im.save(out / 'mtp.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
print('Created transparent PNG and multi-size ICO.')
