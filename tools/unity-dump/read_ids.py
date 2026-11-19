import sys, UnityPy, json
env = UnityPy.load(*sys.argv[1].split(";"))
ids = set(int(x) for x in sys.argv[2:] if x != "--")
for f in env.files.values():
    for sf in (f.files.values() if hasattr(f, "files") else [f]):
        objs = getattr(sf, "objects", None)
        if not objs: continue
        for pid, o in objs.items():
            if pid in ids:
                try:
                    tt = o.read_typetree()
                    tt.pop("m_GameObject", None); tt.pop("m_Script", None)
                    print(pid, o.type.name, json.dumps(tt, default=str)[:1500])
                except Exception as e:
                    print(pid, o.type.name, "ERR", e)
