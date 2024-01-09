import os
import numpy as np
import shutil
from PIL import Image

#Usage note: now that ADV sprites are also emote-distinguished,
# instead of using adv_icon_cutter separately, just make one set of icons
# to be used for ADV and piecewise-switching.

src = "img/stand-source/"
out = "img/stand-output/"
root = "normal.png"

root_left = 52
root_left2 = 744
root_top = 40
root_top2 = 1160

left = 320
left2 = 600
top = 40
top2 = 324

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
    print("Left: [%d, %d] Top: [%d, %d]" % (left, left2, top, top2))
    print("RootLeft: [%d, %d] RootTop: [%d, %d]" % (root_left, root_left2, root_top, root_top2))
    print("Root center is at %s. Face center is at %s." % (rc, sc))
    print("The face offset is %s." % (delta,))


main()