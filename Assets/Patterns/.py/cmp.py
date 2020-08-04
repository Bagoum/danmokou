
f1 = "../demo/mokou.txt"
f2 = "../demo/mokou-export.txt"
d1 = []
d2 = []
end_closers = {
    "phase", "declaration", "action"
}

def load(arr, fn):
    end_arr = [] # Relocate all end-phases to end of file :P
    end_open = False
    with open(fn, "r", encoding="utf-8") as f:
        for line in f:
            line = line.lower().split()
            if not line: continue
            if line[0].startswith("#"): continue
            if line[0].startswith("///"): break
            if line[0] == "end":
                end_open = True
            if end_open and line[0] in end_closers:
                end_open = False
            if end_open:
                end_arr.extend(line)
            else:
                arr.extend(line)
    arr.extend(end_arr)

load(d1, f1)
load(d2, f2)

def cmp(t1, t2):
    if t1 == "_" and t2 == "0" or t1 == "0" and t2 == "_":
        return True
    j1 = 0
    j2 = 0
    while j1 < len(t1) and j2 < len(t2):
        if j1 > 0 and t1[j1] == "-":
            j1 += 1
        elif j2 > 0 and t2[j2] == "-":
            j2 += 1
        else:
            if t1[j1] != t2[j2]:
                return False
            j1 += 1
            j2 += 1
    return j1 == len(t1) and j2 == len(t2)

i1, i2 = 0, 0
# Skip comments, so indices may be different
ld1, ld2 = len(d1), len(d2)
while i1 < ld1 and i2 < ld2:
    t1,t2 = d1[i1], d2[i2]
    if t1[0] == "#":
        i1 += 1
    elif t2[0] == "#":
        i2 += 1
    else:
        if not cmp(t1, t2):
            print("Diff at", i1, i2, ":", t1, t2)
            break
        i1 += 1
        i2 += 1
else:
    if i1 == ld1 and i2 == ld2:
        print("OK")
    else:
        print("Unfinished!")