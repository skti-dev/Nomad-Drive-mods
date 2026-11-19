using BepInEx;

namespace SpongeWash
{
    // Entrada para quem usa o BepInExPack_NomadDrive (o padrão no Nexus).
    // Sem BepInEx instalado, o NomadModLoader ignora esta classe e chama ModEntry.Init() direto.
    [BepInPlugin("seabra.nomaddrive.spongewash", Plugin.Name, Plugin.Version)]
    public class BepInExEntry : BaseUnityPlugin
    {
        private void Awake()
        {
            ModEntry.Init();
        }
    }
}
