using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Confere se um mod só usa tipos e métodos que existem nas DLLs do jogo.
// O Nomad Drive é distribuído com o mscorlib/System "enxugados" (managed stripping):
// um método que compila normalmente pode não existir no jogo e virar MissingMethodException.
//
// Uso: RefCheck <pasta Managed do jogo> <mod.dll> [mod2.dll ...]
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Uso: RefCheck <pasta Managed> <mod.dll> [mod2.dll ...]");
            return 2;
        }
        string managedDir = args[0];
        Dictionary<string, GameAssembly> cache = new Dictionary<string, GameAssembly>(StringComparer.OrdinalIgnoreCase);
        int missing = 0;

        foreach (string modPath in args.Skip(1))
        {
            Console.WriteLine($"== {Path.GetFileName(modPath)}");
            using FileStream stream = File.OpenRead(modPath);
            using PEReader pe = new PEReader(stream);
            MetadataReader md = pe.GetMetadataReader();

            HashSet<string> reported = new HashSet<string>();

            foreach (TypeReferenceHandle handle in md.TypeReferences)
            {
                if (!TryDescribeTypeRef(md, handle, out string scope, out string typeName))
                {
                    continue;
                }
                GameAssembly game = Load(managedDir, scope, cache);
                if (game != null && !game.Types.Contains(typeName) && reported.Add("T:" + typeName))
                {
                    Console.WriteLine($"  FALTA tipo   [{scope}] {typeName}");
                    missing++;
                }
            }

            foreach (MemberReferenceHandle handle in md.MemberReferences)
            {
                MemberReference member = md.GetMemberReference(handle);
                TypeReferenceHandle parentRef;
                if (member.Parent.Kind == HandleKind.TypeReference)
                {
                    parentRef = (TypeReferenceHandle)member.Parent;
                }
                else if (member.Parent.Kind == HandleKind.TypeSpecification && TryGetGenericBase(md, (TypeSpecificationHandle)member.Parent, out parentRef))
                {
                    // Ex.: HashSet<Bucket>.RemoveWhere -> confere em HashSet`1.
                }
                else
                {
                    continue;
                }
                if (!TryDescribeTypeRef(md, parentRef, out string scope, out string typeName))
                {
                    continue;
                }
                GameAssembly game = Load(managedDir, scope, cache);
                if (game == null || !game.Types.Contains(typeName))
                {
                    continue;
                }
                string name = md.GetString(member.Name);
                bool isMethod = member.GetKind() == MemberReferenceKind.Method;
                string key = isMethod ? $"{typeName}::{name}/{ParamCount(md, member.Signature)}" : $"{typeName}::{name}";
                bool exists = isMethod ? game.Methods.Contains(key) : game.Fields.Contains(key);
                if (!exists && reported.Add(key))
                {
                    Console.WriteLine($"  FALTA {(isMethod ? "método" : "campo ")} [{scope}] {key}");
                    missing++;
                }
            }
        }

        Console.WriteLine(missing == 0 ? "OK: tudo o que os mods usam existe no jogo." : $"{missing} referência(s) não existem no jogo.");
        return missing == 0 ? 0 : 1;
    }

    private static bool TryDescribeTypeRef(MetadataReader md, TypeReferenceHandle handle, out string scope, out string typeName)
    {
        TypeReference type = md.GetTypeReference(handle);
        string name = md.GetString(type.Name);
        string ns = md.GetString(type.Namespace);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            // Tipo aninhado: resolve o escopo pelo tipo que o contém.
            if (!TryDescribeTypeRef(md, (TypeReferenceHandle)type.ResolutionScope, out scope, out string outer))
            {
                typeName = null;
                return false;
            }
            typeName = outer + "/" + name;
            return true;
        }
        if (type.ResolutionScope.Kind != HandleKind.AssemblyReference)
        {
            scope = typeName = null;
            return false;
        }
        scope = md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name);
        typeName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
        return true;
    }

    // Blob de TypeSpec de um genérico instanciado: GENERICINST (0x15), CLASS/VALUETYPE, tipo base.
    private static bool TryGetGenericBase(MetadataReader md, TypeSpecificationHandle handle, out TypeReferenceHandle baseType)
    {
        baseType = default;
        BlobReader reader = md.GetBlobReader(md.GetTypeSpecification(handle).Signature);
        if (reader.ReadByte() != 0x15)
        {
            return false;
        }
        reader.ReadByte();
        EntityHandle type = reader.ReadTypeHandle();
        if (type.Kind != HandleKind.TypeReference)
        {
            return false;
        }
        baseType = (TypeReferenceHandle)type;
        return true;
    }

    private static int ParamCount(MetadataReader md, BlobHandle signature)
    {
        BlobReader reader = md.GetBlobReader(signature);
        SignatureHeader header = reader.ReadSignatureHeader();
        if (header.IsGeneric)
        {
            reader.ReadCompressedInteger();
        }
        return reader.ReadCompressedInteger();
    }

    private static GameAssembly Load(string managedDir, string name, Dictionary<string, GameAssembly> cache)
    {
        if (cache.TryGetValue(name, out GameAssembly cached))
        {
            return cached;
        }
        string path = Path.Combine(managedDir, name + ".dll");
        GameAssembly result = File.Exists(path) ? GameAssembly.Read(path, managedDir, cache) : null;
        cache[name] = result;
        return result;
    }

    private sealed class GameAssembly
    {
        public readonly HashSet<string> Types = new HashSet<string>();
        public readonly HashSet<string> Methods = new HashSet<string>();
        public readonly HashSet<string> Fields = new HashSet<string>();

        public static GameAssembly Read(string path, string managedDir, Dictionary<string, GameAssembly> cache)
        {
            GameAssembly result = new GameAssembly();
            using FileStream stream = File.OpenRead(path);
            using PEReader pe = new PEReader(stream);
            MetadataReader md = pe.GetMetadataReader();

            foreach (TypeDefinitionHandle typeHandle in md.TypeDefinitions)
            {
                string typeName = FullName(md, typeHandle);
                result.Types.Add(typeName);
                TypeDefinition type = md.GetTypeDefinition(typeHandle);
                foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
                {
                    MethodDefinition method = md.GetMethodDefinition(methodHandle);
                    result.Methods.Add($"{typeName}::{md.GetString(method.Name)}/{ParamCount(md, method.Signature)}");
                }
                foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
                {
                    result.Fields.Add($"{typeName}::{md.GetString(md.GetFieldDefinition(fieldHandle).Name)}");
                }
            }

            // Tipos encaminhados (type forwarders) para outra DLL: herda os membros dela.
            foreach (ExportedTypeHandle exportedHandle in md.ExportedTypes)
            {
                ExportedType exported = md.GetExportedType(exportedHandle);
                if (exported.Implementation.Kind != HandleKind.AssemblyReference)
                {
                    continue;
                }
                string ns = md.GetString(exported.Namespace);
                string name = md.GetString(exported.Name);
                string typeName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                string target = md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)exported.Implementation).Name);
                GameAssembly other = Load(managedDir, target, cache);
                if (other == null || !other.Types.Contains(typeName))
                {
                    continue;
                }
                result.Types.Add(typeName);
                result.Methods.UnionWith(other.Methods.Where(m => m.StartsWith(typeName + "::", StringComparison.Ordinal)));
                result.Fields.UnionWith(other.Fields.Where(f => f.StartsWith(typeName + "::", StringComparison.Ordinal)));
            }
            return result;
        }

        private static string FullName(MetadataReader md, TypeDefinitionHandle handle)
        {
            TypeDefinition type = md.GetTypeDefinition(handle);
            string name = md.GetString(type.Name);
            TypeDefinitionHandle declaring = type.GetDeclaringType();
            if (!declaring.IsNil)
            {
                return FullName(md, declaring) + "/" + name;
            }
            string ns = md.GetString(type.Namespace);
            return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
        }
    }
}
