import os
import numpy as np
from PIL import Image
from transparencypruner import prune

bdir = "../bullets/.base-png/"
bout = "../bullets/export-png/"
sdirs = ["small/", "large/", "special/"]
suff = ".png"
slen = len(suff)
mask_suff = "_mask.png"

def asmask(file):
    return file[:-slen] + mask_suff

def convert(fromfile, tofile, maskfile=None, masktofile=None):
    print("converting ", fromfile)
    b = maskfile and np.asarray((Image.open(maskfile)))
    a = np.asarray((Image.open(fromfile)))
    a, b = prune(a, b)
    img = Image.fromarray(a)
    img.save(tofile)
    if maskfile is not None:
        img = Image.fromarray(b)
        img.save(masktofile)


def main():
    for sdir in sdirs:
        for f in os.listdir(bdir + sdir):
            if f.endswith(suff) and not f.endswith(mask_suff):
                fullf = bdir + sdir + f
                fulloutf = bout + sdir + f
                maskf = asmask(fullf) if os.path.exists(asmask(fullf)) else None
                convert(fullf, fulloutf, maskf, asmask(fulloutf))

main()