import os

def check_file(path):
    with open(path, 'r') as f:
        content = f.read()
        if content.lstrip().startswith("<#>"):
            return
        print(path)


def check_directory(dir):
    for f in os.listdir(dir):
        p = os.path.join(dir, f)
        if os.path.isdir(p):
            check_directory(p)
        elif p.endswith(".bdsl"):
            check_file(p)

check_directory("../../../../")