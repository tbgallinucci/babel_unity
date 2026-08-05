// ============================================================================
//  BorderLabel.cs  —  camada WFC.Runtime
//
//  O que existe na borda ENTRE duas células. É a moeda de troca entre o
//  esqueleto e o filler: o esqueleto decide os rótulos, o filler procura no
//  tileset uma peça cujas faces batam com eles.
//
//  Por que rótulo de borda e não tag de peça: uma célula com tag `Floor` pode
//  ser tanto piso aberto quanto parede — a tag não diz por onde se passa. Já o
//  rótulo de borda diz exatamente isso, e é o que garante jogabilidade por
//  construção (Decisão 1).
//
//  E é o que devolve liberdade ao WFC quando o tileset crescer: se um dia
//  existirem 5 variantes de piso aberto, todas continuam casando com quatro
//  bordas `Open` e o solver volta a ter o que escolher, sem mudar uma linha
//  do esqueleto.
// ============================================================================

namespace WFC.Runtime
{
    public enum BorderLabel
    {
        /// <summary>Fechado: parede, ou o lado de fora do andar.</summary>
        Wall = 0,

        /// <summary>Passagem livre — o piso continua.</summary>
        Open = 1,

        /// <summary>Vão de porta cravado pelo esqueleto (junção sala ↔ corredor).</summary>
        Door = 2,
    }

    /// <summary>Marca de passagem que o esqueleto crava numa borda entre regiões diferentes.</summary>
    public enum Junction
    {
        /// <summary>Nada cravado — a borda segue a regra normal (mesma região = aberto).</summary>
        None = 0,

        /// <summary>Passagem simples, sem batente. Serve em qualquer formato de célula.</summary>
        Opening = 1,

        /// <summary>Vão de porta. Exige que o kit tenha peça para aquele formato de célula.</summary>
        Door = 2,
    }

    public static class BorderLabels
    {
        /// <summary>Nome do socket correspondente na SocketLibrary do tileset greybox.</summary>
        public static string ToSocket(BorderLabel label)
        {
            switch (label)
            {
                case BorderLabel.Open: return "open";
                case BorderLabel.Door: return "door";
                default:               return "wall";
            }
        }
    }
}
