using System.Collections.Generic;
using NomadDrive.Features.Cooking;
using NomadDrive.Features.ObjectPlacement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CozinhaExtra
{
    // Em todos os PCs: a superfície de encaixe da panela passa a aceitar tomate e batata
    // (o jogo só aceita maçã, banana, coxa e bife). Assim dá para usar o modo de colocação.
    // Também guarda a lista de panelas na cena para o host.
    internal static class PotPatcher
    {
        private static readonly string[] ExtraItems = { "Tomato", "Potato" };
        private static float _nextScan;

        public static readonly List<Pan> Pots = new List<Pan>();

        public static void Tick()
        {
            float now = Time.unscaledTime;
            if (now < _nextScan)
            {
                return;
            }
            _nextScan = now + 2f;
            Pots.Clear();
            foreach (Pan pan in Object.FindObjectsByType<Pan>(FindObjectsSortMode.None))
            {
                if (Game.AsCookingPot(pan) == null)
                {
                    continue;
                }
                Pots.Add(pan);
                foreach (SnappingPlane plane in pan.GetComponentsInChildren<SnappingPlane>(true))
                {
                    List<string> allowed = plane.AllowedItemIDs;
                    // Lista vazia = aceita tudo; não mexe.
                    if (allowed == null || allowed.Count == 0)
                    {
                        continue;
                    }
                    foreach (string id in ExtraItems)
                    {
                        if (!allowed.Contains(id))
                        {
                            allowed.Add(id);
                        }
                    }
                }
            }
        }
    }
}
