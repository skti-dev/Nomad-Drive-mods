# Lista os prefabs de um bundle (filtra por trecho do caminho).
# Uso: python -I list_prefabs.py <bundle> [filtro ...]
import sys, UnityPy
env = UnityPy.load(sys.argv[1])
keys = [k.lower() for k in sys.argv[2:]]
for path in sorted(env.container.keys()):
    if not keys or any(k in path.lower() for k in keys):
        print(path)
