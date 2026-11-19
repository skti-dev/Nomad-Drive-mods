import sys, os, UnityPy
root = sys.argv[1]
keys = [k.lower() for k in sys.argv[2:]]
files = sorted(os.listdir(root))
print(len(files), "bundles")
for f in files:
    p = os.path.join(root, f)
    try:
        env = UnityPy.load(p)
    except Exception as e:
        continue
    for path in env.container.keys():
        lp = path.lower()
        if any(k in lp for k in keys):
            print(f, "|", path)
