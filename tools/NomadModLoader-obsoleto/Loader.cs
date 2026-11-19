using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;

// Carregador mínimo de mods para o Nomad Drive.
//
// O Doorstop (winhttp.dll) chama Doorstop.Entrypoint.Start() antes da Unity carregar o jogo.
// O BepInEx não serve aqui: o MonoMod dele depende de partes do System.dll que o jogo remove
// no build, e isso também quebra o console de desenvolvedor (Quantum Console).
// Este carregador não usa MonoMod nem Harmony. Ele espera a primeira cena carregar e chama
// ModEntry.Init() de cada DLL em Mods\plugins.
namespace Doorstop
{
    public static class Entrypoint
    {
        public static void Start()
        {
            NomadModLoader.Loader.Boot();
        }
    }
}

namespace NomadModLoader
{
    public static class Loader
    {
        public const string Version = "1.0.0";

        private static string _modsDir;
        private static bool _unityHooked;
        private static bool _pluginsLoaded;

        public static void Boot()
        {
            try
            {
                string self = Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH");
                if (string.IsNullOrEmpty(self))
                {
                    self = typeof(Loader).Assembly.Location;
                }
                _modsDir = Path.GetDirectoryName(self);
                File.WriteAllText(LogPath, "");
                Log($"NomadModLoader {Version} iniciado em {_modsDir}");

                // Ainda é cedo demais para usar a Unity; espera o CoreModule carregar para ligar o gancho de cena.
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "UnityEngine.CoreModule")
                    {
                        HookUnity();
                    }
                }
            }
            catch (Exception e)
            {
                Log("Falha ao iniciar: " + e);
            }
        }

        private static string LogPath => Path.Combine(_modsDir ?? ".", "ModLoader.log");

        internal static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Log nunca pode derrubar o jogo.
            }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (args.LoadedAssembly.GetName().Name == "UnityEngine.CoreModule")
            {
                HookUnity();
            }
        }

        // NoInlining: os tipos da Unity só podem ser tocados depois que o CoreModule existir.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HookUnity()
        {
            if (_unityHooked)
            {
                return;
            }
            _unityHooked = true;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Log("Aguardando a primeira cena");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_pluginsLoaded)
            {
                return;
            }
            _pluginsLoaded = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            LoadPlugins();
        }

        private static void LoadPlugins()
        {
            string pluginsDir = Path.Combine(_modsDir, "plugins");
            if (!Directory.Exists(pluginsDir))
            {
                Log("Pasta plugins não encontrada: " + pluginsDir);
                return;
            }
            foreach (string path in Directory.GetFiles(pluginsDir, "*.dll"))
            {
                try
                {
                    // Busca só <NomeDaDll>.ModEntry: listar todos os tipos quebraria em mods que também
                    // têm entrada para BepInEx (BaseUnityPlugin não existe sem ele).
                    Assembly assembly = Assembly.LoadFrom(path);
                    Type entry = assembly.GetType(assembly.GetName().Name + ".ModEntry");
                    MethodInfo init = entry?.GetMethod("Init", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    if (init == null)
                    {
                        Log($"Ignorado (sem {assembly.GetName().Name}.ModEntry.Init()): {Path.GetFileName(path)}");
                        continue;
                    }
                    init.Invoke(null, null);
                    Log($"Mod carregado: {Path.GetFileName(path)}");
                }
                catch (Exception e)
                {
                    Log($"Erro ao carregar {Path.GetFileName(path)}: {e}");
                }
            }
        }
    }
}
