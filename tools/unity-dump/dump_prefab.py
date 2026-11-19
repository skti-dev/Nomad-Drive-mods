import sys, UnityPy, json
bundle = sys.argv[1].split(";"); targets = sys.argv[2:]
env = UnityPy.load(*bundle)
def short(v, depth=0):
    if isinstance(v, dict):
        if set(v.keys()) == {"m_FileID","m_PathID"}: return f"ref({v['m_PathID']})" if v['m_PathID'] else "null"
        return {k: short(x, depth+1) for k, x in v.items() if not k.startswith("m_ObjectHideFlags") and k not in ("m_CorrespondingSourceObject","m_PrefabInstance","m_PrefabAsset","m_EditorHideFlags","m_EditorClassIdentifier")}
    if isinstance(v, list):
        if len(v) > 12: return [short(x, depth+1) for x in v[:12]] + [f"...{len(v)} items"]
        return [short(x, depth+1) for x in v]
    return v
def walk(go, ind=0):
    print("  "*ind + f"[GO] {go.m_Name}  (pathid {go.object_reader.path_id})")
    tr = None
    for c in go.m_Component:
        comp = c.component if hasattr(c, "component") else c[1]
        r = comp.deref() if hasattr(comp, "deref") else comp
        t = r.type.name
        if t in ("Transform","RectTransform"):
            tr = r.read(); continue
        if t == "MonoBehaviour":
            try:
                tt = r.read_typetree()
                script = r.read().m_Script.deref().read()
                name = script.m_ClassName
                tt = short(tt)
                for k in ("m_GameObject","m_Enabled","m_Script","m_Name"): tt.pop(k, None)
                print("  "*ind + f"   <{name}> " + json.dumps(tt, default=str)[:1800])
            except Exception as e:
                print("  "*ind + f"   <MonoBehaviour ?> {e}")
        else:
            print("  "*ind + f"   <{t}>")
    if tr is not None:
        for ch in tr.m_Children:
            walk(ch.deref().read().m_GameObject.deref().read(), ind+1)
for path, obj in env.container.items():
    if path.split("/")[-1] in targets:
        o = obj.deref() if hasattr(obj,"deref") else obj
        if o.type.name != "GameObject": print("=====", path, o.type.name); continue
        print("=====", path)
        walk(o.read())
