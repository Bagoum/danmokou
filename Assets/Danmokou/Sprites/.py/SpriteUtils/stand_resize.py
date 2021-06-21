import os
import numpy as np
import shutil
from PIL import Image

src = "img/stand-source/"
out = "img/stand-output/"
root = "normal.png"

root_left = 0
root_left2 = 600
root_top = 2
root_top2 = 1050

left = 216
left2 = 348
top = 104
top2 = 220

def root_convert(fromfile, tofile):
    img = Image.open(fromfile)
    data = np.array(img)
    for y in range(top+1, top2-1):
        for x in range(left+1, left2-1):
            data[y][x] = np.zeros(data.shape[2], dtype=data.dtype)
    new_data = data[root_top:root_top2, root_left:root_left2]
    outimg = Image.fromarray(new_data)
    outimg.save(tofile)

def convert(fromfile, tofile):
    print("converting ", fromfile)
    img = Image.open(fromfile)
    data = np.asarray(img)
    new_data = data[top:top2, left:left2]
    outimg = Image.fromarray(new_data)
    outimg.save(tofile)


def main():
    root_convert(src + root, out + "root.png")
    for f in os.listdir(src):
        convert(src + f, out + f)
    rc = (root_left + root_left2) / 2, (root_top + root_top2) / 2
    sc = (left + left2) / 2, (top + top2) / 2
    delta = (sc[0] - rc[0], rc[1] - sc[1])
    print("Root center is at %s. Face center is at %s." % (rc, sc))
    print("The face offset is %s." % (delta,))


main()