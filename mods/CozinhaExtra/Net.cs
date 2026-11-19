using Mirror;

namespace CozinhaExtra
{
    // Tipos de sopa. O número viaja na rede e vai para o arquivo de panelas, então não reordene.
    public enum SoupType : byte
    {
        None = 0,
        Tomato = 1,
        Potato = 2
    }

    // O que o jogador pediu ao apertar a tecla.
    public enum CeAction : byte
    {
        Hello = 0,
        AddWater = 1,
        AddIngredient = 2,
        EatFromPot = 3,
        ServeToPlate = 4,
        TakePlate = 5,
        ServeFromPot = 6     // segurando a panela, mirando um prato/caneca vazio (Target = panela, Held = recipiente)
    }

    // Respostas do host para quem pediu (mostradas como aviso na tela).
    public enum CeFeedback : byte
    {
        Ate = 0,
        WaterAdded = 1,
        IngredientAdded = 2,
        NoWater = 3,
        PotFull = 4,
        OtherSoup = 5,
        Served = 6,
        PlateTaken = 7,
        PotEmpty = 8,
        Rotten = 9,
        HandBusy = 10,
        TooFar = 11,
        MugNotEmpty = 12,
        Evaporated = 13
    }

    // Cliente -> host.
    public struct CeRequest : NetworkMessage
    {
        public byte Action;
        public uint Target;
        public uint Held;   // item na mão de quem pediu (o host confere se é mesmo)
    }

    // Host -> clientes com o mod: estado de uma panela.
    public struct CePotState : NetworkMessage
    {
        public uint Pot;
        public float Water;
        public byte Soup;
        public byte Raw;
        public byte Portions;
        public float Progress;
        public float Evap;      // segundos acumulados no fogo desde a última porção evaporada
    }

    // Host -> quem pediu: "pegue este item" (prato recém-criado).
    public struct CeEquip : NetworkMessage
    {
        public uint Item;
    }

    // Host -> clientes com protocolo >= 2: esta caneca de sopa foi feita na caneca <Source> (assetId).
    public struct CeMugSkin : NetworkMessage
    {
        public uint Item;
        public uint Source;
    }

    // Host -> quem pediu: resultado da ação.
    public struct CeResult : NetworkMessage
    {
        public byte Code;
        public byte Soup;
    }

    internal static class Net
    {
        // "Esconder objeto" com um netId que não existe: cliente sem o mod ignora (é uma mensagem
        // padrão do Mirror); cliente com o mod reconhece e sabe que o host tem o mod.
        public const uint BeaconNetId = 0xCE5AFEED;

        // Versão das mensagens do mod, enviada no Hello (CeRequest.Target). 1.4.2 manda 0.
        // 2 = entende CeMugSkin (1.4.3). O host só manda mensagem nova a quem disse que entende,
        // porque o Mirror desconecta quem recebe mensagem desconhecida.
        public const uint Protocol = 2;
        public const uint ProtocolMugSkin = 2;

        // O weaver do Mirror não roda neste DLL, então os serializadores são registrados à mão.
        public static void RegisterSerializers()
        {
            Writer<CeRequest>.write = (w, m) => { w.WriteByte(m.Action); w.WriteUInt(m.Target); w.WriteUInt(m.Held); };
            Reader<CeRequest>.read = r => new CeRequest { Action = r.ReadByte(), Target = r.ReadUInt(), Held = r.ReadUInt() };

            Writer<CePotState>.write = (w, m) =>
            {
                w.WriteUInt(m.Pot);
                w.WriteFloat(m.Water);
                w.WriteByte(m.Soup);
                w.WriteByte(m.Raw);
                w.WriteByte(m.Portions);
                w.WriteFloat(m.Progress);
                w.WriteFloat(m.Evap);
            };
            Reader<CePotState>.read = r => new CePotState
            {
                Pot = r.ReadUInt(),
                Water = r.ReadFloat(),
                Soup = r.ReadByte(),
                Raw = r.ReadByte(),
                Portions = r.ReadByte(),
                Progress = r.ReadFloat(),
                Evap = r.ReadFloat()
            };

            Writer<CeEquip>.write = (w, m) => w.WriteUInt(m.Item);
            Reader<CeEquip>.read = r => new CeEquip { Item = r.ReadUInt() };

            Writer<CeMugSkin>.write = (w, m) => { w.WriteUInt(m.Item); w.WriteUInt(m.Source); };
            Reader<CeMugSkin>.read = r => new CeMugSkin { Item = r.ReadUInt(), Source = r.ReadUInt() };

            Writer<CeResult>.write =(w, m) => { w.WriteByte(m.Code); w.WriteByte(m.Soup); };
            Reader<CeResult>.read = r => new CeResult { Code = r.ReadByte(), Soup = r.ReadByte() };
        }
    }
}
