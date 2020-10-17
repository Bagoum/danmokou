import os
import numpy as np
from PIL import Image

src = "img/dot-source/"
out = "img/dot-output/"
bs = 128
bo = 72 ## 88
bd1 = (bs - bo) // 2
bd2 = (bs + bo) // 2


def convert(fromfile, tofile):
    print("converting ", fromfile)
    img = Image.open(fromfile)
    data = np.asarray(img)
    hy, hx, c = data.shape
    maxux = 0
    maxuy = 0
    for uy in range(hy // bs):
        for ux in range(hx // bs):
            ## Check if nonempty
            empty = True
            for iy in range(bs):
                for ix in range(bs):
                    if data[uy*bs+iy,ux*bs+ix][3] > 0:
                        empty = False
                        break
                if not empty: break
            if not empty:
                maxux = max(maxux, ux)
                maxuy = max(maxuy, uy)

    print(maxux, maxuy)
    maxux += 1
    maxuy += 1
    new_data = np.zeros((maxuy * bo, maxux * bo, c), data.dtype)

    for uy in range(maxuy):
        for ux in range(maxux):
            sx, sy = ux * bs, uy * bs
            ox, oy = ux * bo, uy * bo
            new_data[oy:oy+bo,ox:ox+bo] = data[sy+bd1:sy+bd2,sx+bd1:sx+bd2]

    if "gamma" in img.info:
        new_data = (255.0 * (new_data / 255.0)**(10*img.info["gamma"])).astype(data.dtype)


    outimg = Image.fromarray(new_data)
    outimg.save(tofile)


def main():
    for f in os.listdir(src):
        convert(src + f, out + f)


main()