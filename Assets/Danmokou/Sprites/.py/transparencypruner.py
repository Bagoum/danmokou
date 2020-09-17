import numpy as np
from PIL import Image

def prune(a, b=None):
    rows = len(a)
    for i in range(rows// 2):
        if not empty(a[i]) or not empty(a[rows-1-i]):
            break
    print("Row", i)
    if i > 0:
        i -= 1 #Leave one pixel room for clamping next pixel detection
        a = a[i:-i]
        if b is not None: b = b[i:-i]
    cols = len(a[0])
    for i in range(cols// 2):
        if not empty(a[:,i]) or not empty(a[:,cols-1-i]):
            break
    if i > 0:
        i -= 1
        a = a[:,i:-i]
        if b is not None: b = b[:,i:-i]
    return a, b

def empty(row):
    return all(x[3] == 0 for x in row)