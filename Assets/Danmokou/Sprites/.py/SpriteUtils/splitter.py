from PIL import Image
import os
import numpy as np

src = "../../DD/arrowpather"
suffix = ".png"
sy = 4
sx = 1

def main():
    a = np.asarray((Image.open(src + suffix)))
    dx = a.shape[1] // sx
    dy = a.shape[0] // sy
    i = 0
    for ix in range(sx):
        for iy in range(sy):
            data = a[dy*iy:dy*(iy+1), dx*ix:dx*(ix+1)]
            img = Image.fromarray(data)
            img.save(src + str(i) + suffix)
            i += 1



main()