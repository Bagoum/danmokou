import os
import numpy as np
import shutil
from PIL import Image

src = "img/stand-source/"
out = "img/stand-output/"

left = 124
left2 = 248
top = 76
top2 = 180


def convert(fromfile, tofile):
    print("converting ", fromfile)
    img = Image.open(fromfile)
    data = np.asarray(img)
    new_data = data[top:top2, left:left2]
    outimg = Image.fromarray(new_data)
    outimg.save(tofile)

def main():
    for f in os.listdir(src):
        convert(src + f, out + f)
    print("ADV sprites bounds: %d %d %d %d" % (left, left2, top, top2))

main()