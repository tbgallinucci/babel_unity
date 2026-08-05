// ============================================================================
//  RoomArchetype.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  O "RoomRole configurável pelo Inspector" que faltava. RoomRole (WFC.Data) é
//  ESTRUTURAL — Vazio/Entrada/Corredor/Combate/Escada mexem com geometria e
//  conectividade, e o SkeletonGenerator depende deles por IDENTIDADE (sabe
//  procurar "a sala de Entrada", "a mais distante" etc.). Não dá pra tornar
//  isso 100% dado sem reabrir o esqueleto inteiro.
//
//  O que ESTE asset resolve é o outro pedaço: dentro de "sala de Combate"
//  (a única categoria "normal" hoje), qual SABOR de sala é essa — o elenco de
//  props, sem tocar em C#/recompilar. Criar um arquétipo novo é
//  Assets ▸ Create ▸ Babel ▸ Room Archetype, preencher a lista de props, e
//  arrastar pro PropRoomPopulator. Zero código.
//
//  Cada sala de Combate sorteia UM arquétipo (por peso, igual RoomRoleWeight)
//  quando o PropRoomPopulator roda.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using WFC.Runtime;

namespace Babel.Floor
{
    [CreateAssetMenu(fileName = "RoomArchetype", menuName = "Babel/Room Archetype", order = 10)]
    public sealed class RoomArchetype : ScriptableObject
    {
        [Serializable]
        public sealed class PropEntry
        {
            public GameObject prefab;

            [Tooltip("Peso relativo no sorteio, entre os props deste arquétipo.")]
            [Min(0.01f)] public float weight = 1f;

            [Tooltip("Desmarcado (padrão): nasce numa célula de piso aberto qualquer da sala, igual o " +
                     "EnemyPopulator já faz — bom para prop solto (baú, estátua no meio da sala).\n" +
                     "Marcado: só nasce em cima de um SpawnAnchor do 'Anchor Kind' escolhido, plantado " +
                     "na PEÇA de parede/piso pelo autor do tileset — bom para prop que precisa de um " +
                     "encosto específico (banner na parede, prateleira no nicho). Se a sala não tiver " +
                     "nenhum anchor desse tipo, esta entrada simplesmente não nasce ali.")]
            public bool useAnchor = false;

            public SpawnAnchorKind anchorKind = SpawnAnchorKind.Prop;
        }

        [Tooltip("Só pra você reconhecer isto no Inspector e nos logs — não afeta o sorteio.")]
        public string displayName = "Novo Arquétipo";

        [Tooltip("Peso relativo no sorteio ENTRE arquétipos, quando uma sala de Combate é populada.")]
        [Min(0.01f)] public float weight = 1f;

        [Tooltip("Quantos props (somando todas as entradas abaixo) essa sala recebe.")]
        public Vector2Int propCount = new Vector2Int(1, 3);

        [Tooltip("Os props deste arquétipo e como cada um nasce.")]
        public List<PropEntry> props = new List<PropEntry>();

        [Header("Acoplamento com inimigos")]
        [Tooltip("Desligado: o EnemyPopulator pula a sala inteira — nenhum inimigo nasce nela, mesmo " +
                 "sendo RoomRole.Combate. Pensado pra arquétipo tipo loja/santuário, onde monstro não " +
                 "faz sentido. O acoplamento só funciona se o FloorDirector chamar RollArchetypes ANTES " +
                 "do EnemyPopulator.Populate — ver comentário de cabeçalho do PropRoomPopulator.")]
        public bool allowEnemies = true;

        [Tooltip("Multiplica a quantidade de inimigos que a sala receberia normalmente. 1 = sem mudança, " +
                 "0.5 = metade, 2 = dobro. Ignorado se 'Allow Enemies' estiver desligado (nesse caso já é " +
                 "zero de qualquer jeito). Prefira o toggle acima quando a intenção for 'nenhum inimigo' — " +
                 "fica explícito nos logs; use o multiplicador só pra afinar densidade.")]
        [Min(0f)] public float enemyDensityMultiplier = 1f;
    }
}
