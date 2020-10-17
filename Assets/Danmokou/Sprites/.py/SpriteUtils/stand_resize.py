import os
import numpy as np
import shutil
from PIL import Image

src = "img/stand-source/"
out = "img/stand-output/"
root = "normal.png"

root_left = 0
root_left2 = 1128
root_top = 80
root_top2 = 1180

left = 480
left2 = 620
top = 200
top2 = 360

def root_convert(fromfile, tofile):
    img = Image.open(fromfile)
    data = np.asarray(img)
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