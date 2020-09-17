import os
import numpy as np
from PIL import Image

dir = "../../Kenney/Particle samples/Sprites/"
suffix = ".png"
out = "../bullets/.base-usable/fx/"

def convert(dir, f):
    a = np.asarray((Image.open(dir + f)))
    newa = []
    for row in a:
        newa.append([])
        for color in row:
            newa[-1].append([color[0],color[1],color[2],color[1]])
    img = Image.fromarray(np.array(newa, dtype=np.uint8))
    img.save(out + f)

def go(dir):
    for f in os.listdir(dir):
        if f.endswith(suffix):
            convert(dir,f)


go(dir)