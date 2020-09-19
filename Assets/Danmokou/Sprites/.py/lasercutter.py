from PIL import Image
import os
import numpy as np
from transparencypruner import prune

out = "../bullets/export-png/laser/"
suff = ".png"
dirs = [
    ("../bullets/.base-png/laser/", 160),
    ("../bullets/.base-png/laserUncut/", 0)
]

def convert(dir, rem, f):
    print("converting ", f)
    a = np.asarray((Image.open(dir + f)))
    if rem > 0:
        a = a[:, rem: -rem]
    a, b = prune(a, None)
    img = Image.fromarray(a)
    img.save(out + f)

def convertdir(dir, rem):
    for f in os.listdir(dir):
        if f.endswith(suff):
            convert(dir, rem, f)

def main():
    for dir, rem in dirs:
        convertdir(dir, rem)

main()