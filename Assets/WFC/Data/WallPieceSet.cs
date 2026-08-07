// ============================================================================
//  WallPieceSet.cs  —  camada WFC.Data
//
//  As 5 peças de parede do tileset de GRID DUAL, cada uma autorada UMA vez na
//  orientação canônica. Quem calcula a rotação e planta é o
//  DualGridWallBuilder (WFC.Runtime).
//
//  Mora em WFC.Data — e não junto do builder — porque o FloorSpec precisa
//  carregar este campo, e WFC.Data não referencia WFC.Runtime (a dependência é
//  de mão única: Runtime conhece Data, nunca o contrário).
//
//  A ORIENTAÇÃO CANÔNICA importa: o builder acha a rotação comparando a máscara
//  de braços da peça com a do vértice. Se você reautorar uma peça virada, ela
//  nasce torta no andar inteiro e o sintoma não aponta pra cá.
// ============================================================================

using System;
using UnityEngine;

namespace WFC.Data
{
    [Serializable]
    public struct WallPieceSet
    {
        [Header("Só parede — usadas quando NENHUM braço do vértice é porta")]

        [Tooltip("1 braço. Canônico: Norte. É a ponta de parede (extremidade de um vão, por ex.).")]
        public GameObject end;

        [Tooltip("2 braços opostos. Canônico: Norte + Sul. É a peça que leva os anchors de luz.")]
        public GameObject straight;

        [Tooltip("2 braços adjacentes. Canônico: Norte + Leste. É a peça que contém a curva de 90° " +
                 "inteira — a razão de existir do grid dual.")]
        public GameObject corner;

        [Tooltip("3 braços. Canônico: Norte + Leste + Sul.")]
        public GameObject tee;

        [Tooltip("4 braços. Simétrica: a rotação é irrelevante.")]
        public GameObject cross;

        [Header("Composição — usadas quando PELO MENOS UM braço é porta")]

        [Tooltip("Braço genérico sólido, autorado apontando pro Norte, origem no PRÓPRIO vértice " +
                 "(sem invadir o centro). Usado no par OPOSTO (reto, sem hub) — os 2 braços do " +
                 "vértice se encontram exatamente no plano dele.")]
        public GameObject armWall;

        [Tooltip("Mesmo braço, mas com origem recuada T/2 do vértice — usado em TODO vértice " +
                 "misto que também tem hub (canto/T/cruz/ponta), pra não invadir o território que " +
                 "o Hub_Junction/Hub_End já cobre sozinho (invadir causa topo/base coincidentes " +
                 "com o hub e com o braço perpendicular vizinho — z-fighting confirmado).")]
        public GameObject armWallAtHub;

        [Tooltip("Braço genérico de porta (batente perto do vértice + vão com verga na ponta " +
                 "externa — largura do vão ajustável no Center Wall Tile Generator), autorado " +
                 "apontando pro Norte, origem no vértice. Mesma lógica do armWall — usado no par " +
                 "oposto sem hub.")]
        public GameObject armDoor;

        [Tooltip("Mesmo braço de porta, origem recuada T/2 — usado em vértice misto COM hub. " +
                 "Mesma lógica do armWallAtHub.")]
        public GameObject armDoorAtHub;

        [Tooltip("Tampa de vértice pra quando o ÚNICO braço presente é porta (o caso all-parede já " +
                 "usa o `end` normal).")]
        public GameObject hubEnd;

        [Tooltip("Cubo T×T de reforço no centro do vértice, usado em TODO vértice misto (com pelo " +
                 "menos uma porta) de 1, 2-adjacente, 3 ou 4 braços — garante o centro preenchido " +
                 "sem depender de nenhum braço invadir aquele território.")]
        public GameObject hubJunction;

        /// <summary>Todas as 11 preenchidas? O builder não roda pela metade — faltando uma, o
        /// andar sairia com buraco justamente na configuração que ela cobre.</summary>
        public bool IsComplete => end != null && straight != null && corner != null
                               && tee != null && cross != null
                               && armWall != null && armWallAtHub != null
                               && armDoor != null && armDoorAtHub != null
                               && hubEnd != null && hubJunction != null;
    }
}
