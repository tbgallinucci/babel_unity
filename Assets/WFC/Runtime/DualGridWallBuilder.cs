// ============================================================================
//  DualGridWallBuilder.cs  —  camada WFC.Runtime
//
//  Constrói as paredes de um andar num GRID DUAL: em vez de uma peça por
//  célula (com a parede encostada na borda), uma peça por VÉRTICE do grid,
//  com a parede centrada na linha da borda.
//
//  ------------------------------------------------------------------------
//  O MAPEAMENTO (é só isto que este arquivo faz)
//
//  A célula de render fica centrada no vértice (i,j) do grid do esqueleto e
//  cobre um quadrado de S×S ao redor dele — ou seja, um quadrante de cada uma
//  das 4 células que tocam aquele vértice. As bordas do esqueleto que chegam
//  no vértice viram quatro BRAÇOS saindo do centro da peça:
//
//                    N = borda entre as células (i-1, j)   e (i, j)
//                    │
//        W ──────────┼────────── E     W = borda entre (i-1,j-1) e (i-1,j)
//     (i-1,j-1)      │  (i,j-1)        E = borda entre (i,  j-1) e (i,  j)
//                    S                 S = borda entre (i-1,j-1) e (i,  j-1)
//
//  Quais braços são parede define a peça e a rotação. Cada braço tem S/2 de
//  comprimento, então dois vértices vizinhos somam os S de um segmento inteiro
//  de borda — as peças ladrilham a linha da parede sem sobrepor nem deixar vão.
//
//  POR QUE ISTO CONSERTA A QUINA PARTIDA: onde duas bordas perpendiculares se
//  encontram, o ponto de encontro é o CENTRO de uma peça, não a fronteira entre
//  duas. A curva de 90° passa a caber inteira dentro de uma peça só, e quina
//  dividida entre peças deixa de existir. Nenhuma heurística, nenhum caso
//  especial por tipo de peça — o defeito some por construção.
//
//  O que este builder NÃO decide: nada. Os braços saem direto dos rótulos de
//  borda do AnnotatedGrid, que o esqueleto já cravou. A colocação é
//  DETERMINÍSTICA — não há solver aqui, e por isso não há contradição possível.
//
//  PORTA: cada braço agora é um de TRÊS estados — nada, parede, ou porta — não
//  mais binário. Quando TODOS os braços presentes de um vértice são parede, a
//  peça é a monolítica de sempre (Wall_End/Straight/Corner/Tee/Cross, com a
//  curva autorada à mão). Quando PELO MENOS UM é porta, a peça é COMPOSTA em
//  vez de monolítica: um hub pequeno no centro (garante o vértice preenchido)
//  + um braço genérico por direção presente (Arm_Wall ou Arm_Door, girado pra
//  apontar pro lado certo). Ver PlaceComposed pro raciocínio de por que isso
//  não precisa de uma peça dedicada por combinação.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Runtime
{
    public static class DualGridWallBuilder
    {
        public const string GeneratedRootName = "__DualGridWalls";

        // Bits do braço. A ordem é a do ciclo de rotação em Y do projeto (ver Directions):
        // um quarto de volta positivo leva N→E→S→W→N, que é exatamente deslocar 1 bit à
        // esquerda nesta máscara. É isso que permite achar a rotação com um shift.
        private const int ArmN = 1 << 0;
        private const int ArmE = 1 << 1;
        private const int ArmS = 1 << 2;
        private const int ArmW = 1 << 3;

        private const int CanonEnd      = ArmN;
        private const int CanonStraight = ArmN | ArmS;
        private const int CanonCorner   = ArmN | ArmE;
        private const int CanonTee      = ArmN | ArmE | ArmS;

        /// <summary>
        /// Instancia as paredes do andar inteiro sob <paramref name="root"/>.
        /// Devolve quantas peças foram plantadas.
        /// </summary>
        /// <param name="root">Onde as peças nascem (normalmente a raiz do andar gerado).</param>
        /// <param name="originTransform">
        /// Transform cuja posição serve de referência pra converter mundo → local. Passe o
        /// mesmo transform que ancora o andar; as peças recebem localPosition relativa a ele.
        /// </param>
        /// <param name="layer">Layer aplicada em cascata. -1 = mantém a do prefab.</param>
        /// <param name="anchorsOut">
        /// Opcional. Recebe os SpawnAnchor das peças, já com <c>cellIndex</c> resolvido —
        /// ver <see cref="CollectAnchors"/> pro porquê disso não ser trivial aqui.
        /// </param>
        public static int Build(Transform root, Transform originTransform, AnnotatedGrid grid,
                                WallPieceSet pieces, int layer = -1,
                                List<SpawnAnchor> anchorsOut = null)
        {
            if (root == null || grid == null || !pieces.IsComplete) return 0;

            Grid3D g = grid.Grid;
            Vector3 originPos = originTransform != null ? originTransform.position : Vector3.zero;
            var buffer = anchorsOut != null ? new List<SpawnAnchor>() : null;
            int placed = 0;

            // <= porque são VÉRTICES, não células: um grid de N células tem N+1 vértices por eixo.
            for (int y = 0; y < g.SizeY; y++)
            for (int vz = 0; vz <= g.SizeZ; vz++)
            for (int vx = 0; vx <= g.SizeX; vx++)
            {
                ComputeArmMasks(grid, y, vx, vz, out int wallMask, out int doorMask);
                int present = wallMask | doorMask;
                if (present == 0) continue;

                Vector3 world = grid.Origin + new Vector3(vx * grid.CellSize,
                                                          y * grid.CellHeight,
                                                          vz * grid.CellSize);
                Vector3 localPos = world - originPos;

                placed += doorMask == 0
                    ? PlaceMonolithic(root, pieces, present, localPos, vx, y, vz, layer, grid, buffer, anchorsOut)
                    : PlaceComposed(root, pieces, wallMask, doorMask, localPos, vx, y, vz, layer, grid, buffer, anchorsOut);
            }

            return placed;
        }

        /// <summary>Instancia a peça monolítica de sempre — todos os braços presentes são parede.</summary>
        private static int PlaceMonolithic(Transform root, WallPieceSet pieces, int mask, Vector3 localPos,
                                           int vx, int y, int vz, int layer, AnnotatedGrid grid,
                                           List<SpawnAnchor> buffer, List<SpawnAnchor> anchorsOut)
        {
            if (!TryResolve(mask, pieces, out GameObject prefab, out int rot)) return 0;

            GameObject instance = Instantiate(prefab, root);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = Quaternion.Euler(0f, 90f * rot, 0f);
            instance.name = $"{prefab.name}@{rot * 90}° [{vx},{y},{vz}]";
            if (layer >= 0) SetLayerRecursively(instance, layer);
            if (buffer != null) CollectAnchors(instance, grid, y, buffer, anchorsOut);
            return 1;
        }

        /// <summary>
        /// Instancia a peça COMPOSTA — pelo menos um braço é porta. Em vez de uma peça dedicada
        /// por combinação (o catálogo explodiria: Straight vira 4 variantes, Tee 8, Cross 16), a
        /// peça é montada de um hub (às vezes) + um braço genérico por direção presente.
        ///
        /// Por que o hub é só ÀS VEZES necessário: no par OPOSTO (2 braços, mesmo eixo) os dois
        /// braços (variante normal, origem exatamente no vértice) se encontram sozinhos no plano
        /// do vértice — sem gap, sem hub. Em QUALQUER outro caso (1, 2-adjacente, 3 ou 4 braços)
        /// entra Hub_Junction/Hub_End, que cobre o centro sozinho — e os braços usam a variante
        /// "AtHub" (origem recuada T/2), que NÃO invade esse território.
        ///
        /// Por que duas variantes de braço em vez de uma só cobrindo tudo: braços PERPENDICULARES
        /// (não só opostos) com origem no vértice ainda se sobrepõem numa faixa T/2×T/2 perto dele
        /// — a espessura de um sempre invade um pouco o território do outro. Com hub por perto,
        /// isso vira sobreposição TRIPLA (2 braços + hub, todos com topo/base na mesma altura) —
        /// z-fighting confirmado. A variante "AtHub" existe só pra eliminar essa invasão.
        /// </summary>
        private static int PlaceComposed(Transform root, WallPieceSet pieces, int wallMask, int doorMask,
                                         Vector3 localPos, int vx, int y, int vz, int layer, AnnotatedGrid grid,
                                         List<SpawnAnchor> buffer, List<SpawnAnchor> anchorsOut)
        {
            int present = wallMask | doorMask;
            int count = CountArms(present);
            int placed = 0;

            bool needsHub = count == 1                                   // End: tampa o vértice
                         || (count == 2 && !IsOpposite(present))         // Corner misto
                         || count >= 3;                                  // Tee / Cross

            if (needsHub)
            {
                GameObject hub = count == 1 ? pieces.hubEnd : pieces.hubJunction;
                placed += SpawnComposedPiece(hub, root, localPos, 0, vx, y, vz, "Hub",
                                             layer, grid, buffer, anchorsOut);
            }

            for (int bit = 0; bit < 4; bit++)
            {
                int flag = 1 << bit;
                if ((present & flag) == 0) continue;

                // Com hub por perto, o braço usa a variante "AtHub" (origem recuada T/2, não
                // invade o centro que o hub já cobre sozinho). Sem hub (par oposto), a variante
                // normal (origem no vértice) — ver comentário no header de WallPieceSet.armWallAtHub.
                bool isDoor = (doorMask & flag) != 0;
                GameObject arm = needsHub
                    ? (isDoor ? pieces.armDoorAtHub : pieces.armWallAtHub)
                    : (isDoor ? pieces.armDoor : pieces.armWall);
                placed += SpawnComposedPiece(arm, root, localPos, bit, vx, y, vz, $"Arm{"NESW"[bit]}",
                                             layer, grid, buffer, anchorsOut);
            }

            return placed;
        }

        private static int SpawnComposedPiece(GameObject prefab, Transform root, Vector3 localPos, int rot,
                                              int vx, int y, int vz, string tag, int layer, AnnotatedGrid grid,
                                              List<SpawnAnchor> buffer, List<SpawnAnchor> anchorsOut)
        {
            if (prefab == null) return 0;

            GameObject instance = Instantiate(prefab, root);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = Quaternion.Euler(0f, 90f * rot, 0f);
            instance.name = $"{prefab.name}_{tag} [{vx},{y},{vz}]";
            if (layer >= 0) SetLayerRecursively(instance, layer);
            if (buffer != null) CollectAnchors(instance, grid, y, buffer, anchorsOut);
            return 1;
        }

        private static bool IsOpposite(int mask) => mask == (ArmN | ArmS) || mask == (ArmE | ArmW);

        /// <summary>
        /// Coleta os anchors de uma peça de parede, resolvendo o <c>cellIndex</c> de cada um
        /// pela POSIÇÃO no mundo, e descartando os que caem em célula não-jogável.
        ///
        /// Por que isto não é trivial (e é diferente do TileInstancer): lá cada peça pertence
        /// a UMA célula, então o cellIndex é o do laço. Aqui a peça fica num vértice, tocando
        /// até 4 células — não existe "a célula dela". Mas o consumidor precisa desse índice:
        /// o BasicLightingPopulator usa `GetRegion(anchor.cellIndex)` pra saber a que sala a
        /// luz pertence, e ordena por cellIndex pra ser determinístico.
        ///
        /// A saída é deixar a GEOMETRIA responder: o anchor de luz já nasce deslocado pro lado
        /// da parede que ele ilumina, então a célula em que ele cai no mundo É a sala que ele
        /// serve. Isso resolve de brinde o problema de a peça reta ter dois lados: autora-se um
        /// anchor de cada lado, e o que aponta pro vazio é DESTRUÍDO aqui — parede entre duas
        /// salas fica com os dois, parede de fachada fica só com o de dentro, sem a peça
        /// precisar saber nada sobre o andar.
        /// </summary>
        private static void CollectAnchors(GameObject instance, AnnotatedGrid grid, int y,
                                           List<SpawnAnchor> buffer, List<SpawnAnchor> anchorsOut)
        {
            buffer.Clear();
            instance.GetComponentsInChildren(true, buffer);

            foreach (SpawnAnchor anchor in buffer)
            {
                int cell = WorldToCell(grid, anchor.transform.position, y);

                if (cell < 0 || !grid.IsPlayable(cell))
                {
                    // Aponta pro vazio: nada pra iluminar. Desativa em vez de destruir porque
                    // fora do play mode as peças são instâncias de prefab, e a Unity recusa
                    // DestroyImmediate num filho de instância de prefab. Desativado basta: o
                    // populador só enxerga o que sai em anchorsOut.
                    anchor.gameObject.SetActive(false);
                    continue;
                }

                anchor.cellIndex = cell;
                anchorsOut.Add(anchor);
            }
        }

        /// <summary>Célula que contém este ponto do mundo, ou -1 se cair fora do grid.</summary>
        private static int WorldToCell(AnnotatedGrid grid, Vector3 world, int y)
        {
            Vector3 local = world - grid.Origin;
            int x = Mathf.FloorToInt(local.x / grid.CellSize);
            int z = Mathf.FloorToInt(local.z / grid.CellSize);
            return grid.Grid.InBounds(x, y, z) ? grid.Grid.Index(x, y, z) : -1;
        }

        /// <summary>Remove paredes de grid dual geradas antes sob este transform.</summary>
        public static void ClearGenerated(Transform parent)
        {
            if (parent == null) return;

            Transform existing = parent.Find(GeneratedRootName);
            while (existing != null)
            {
                // Desanexar ANTES de destruir: em Play Mode o Destroy só efetiva no fim do
                // frame, então sem isto o Find() seguinte acha o mesmo objeto e o laço nunca
                // sai (é o mesmo travamento que o TileInstancer.ClearGenerated documenta).
                existing.SetParent(null);

                if (Application.isPlaying) Object.Destroy(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);

                existing = parent.Find(GeneratedRootName);
            }
        }

        // ------------------------------------------------------------------ braços
        /// <summary>
        /// Classifica os 4 meio-segmentos de borda que saem do vértice (vx,vz): cada um vira um
        /// bit em <paramref name="wallMask"/> ou em <paramref name="doorMask"/> (nunca os dois —
        /// as máscaras são disjuntas), ou fica ausente de ambas se não houver nada ali.
        /// </summary>
        private static void ComputeArmMasks(AnnotatedGrid grid, int y, int vx, int vz,
                                            out int wallMask, out int doorMask)
        {
            wallMask = 0;
            doorMask = 0;
            Accumulate(grid, y, vx - 1, vz,     vx,     vz,     Direction.PosX, ArmN, ref wallMask, ref doorMask);
            Accumulate(grid, y, vx,     vz - 1, vx,     vz,     Direction.PosZ, ArmE, ref wallMask, ref doorMask);
            Accumulate(grid, y, vx - 1, vz - 1, vx,     vz - 1, Direction.PosX, ArmS, ref wallMask, ref doorMask);
            Accumulate(grid, y, vx - 1, vz - 1, vx - 1, vz,     Direction.PosZ, ArmW, ref wallMask, ref doorMask);
        }

        private static void Accumulate(AnnotatedGrid grid, int y, int ax, int az, int bx, int bz,
                                       Direction aToB, int armBit, ref int wallMask, ref int doorMask)
        {
            switch (EdgeLabel(grid, y, ax, az, bx, bz, aToB))
            {
                case BorderLabel.Wall: wallMask |= armBit; break;
                case BorderLabel.Door: doorMask |= armBit; break;
                    // Open (ou nem A nem B jogável): nada aqui, os dois bits ficam de fora.
            }
        }

        /// <summary>
        /// O rótulo da borda entre as células (ax,az) e (bx,bz) — adjacentes, com
        /// (ax,az)→(bx,bz) na direção <paramref name="aToB"/>.
        ///
        /// Pergunta pelo lado JOGÁVEL da borda, qualquer um dos dois. GetBorder já é simétrico,
        /// então basta achar uma célula válida e jogável no par. Os dois filtros importam:
        ///
        ///  • sem olhar os DOIS lados, uma sala cercada de vazio (o caso comum aqui) perde
        ///    metade das paredes reais, porque o lado "de fora" é justamente o não-jogável;
        ///  • sem o filtro de IsPlayable, uma borda com os dois lados vazios contaria como
        ///    parede à toa — GetBorder devolve Wall de graça pra célula não-jogável, que é o
        ///    "selo" automático dela.
        ///
        /// Nem A nem B jogável (ou nenhum dos dois existe) devolve Open — "nada aqui", não Wall
        /// nem Door.
        /// </summary>
        private static BorderLabel EdgeLabel(AnnotatedGrid grid, int y, int ax, int az, int bx, int bz,
                                             Direction aToB)
        {
            Grid3D g = grid.Grid;

            if (g.InBounds(ax, y, az))
            {
                int a = g.Index(ax, y, az);
                if (grid.IsPlayable(a)) return grid.GetBorder(a, aToB);
            }
            if (g.InBounds(bx, y, bz))
            {
                int b = g.Index(bx, y, bz);
                if (grid.IsPlayable(b)) return grid.GetBorder(b, Directions.Opposite(aToB));
            }
            return BorderLabel.Open;
        }

        // ------------------------------------------------------------- peça + rotação
        private static bool TryResolve(int mask, WallPieceSet pieces, out GameObject prefab, out int rot)
        {
            rot = 0;
            switch (CountArms(mask))
            {
                case 1:
                    prefab = pieces.end;
                    return TryFindRotation(CanonEnd, mask, out rot);

                case 2:
                    // Opostos = parede reta; adjacentes = canto (o caso que motivou tudo isto).
                    bool opposite = mask == (ArmN | ArmS) || mask == (ArmE | ArmW);
                    prefab = opposite ? pieces.straight : pieces.corner;
                    return TryFindRotation(opposite ? CanonStraight : CanonCorner, mask, out rot);

                case 3:
                    prefab = pieces.tee;
                    return TryFindRotation(CanonTee, mask, out rot);

                case 4:
                    prefab = pieces.cross; // simétrica: rotação é irrelevante
                    return true;

                default:
                    prefab = null;
                    return false;
            }
        }

        private static bool TryFindRotation(int canonical, int target, out int rot)
        {
            for (rot = 0; rot < 4; rot++)
                if (RotateMask(canonical, rot) == target) return true;
            rot = 0;
            return false;
        }

        /// <summary>
        /// Gira a máscara de braços em <paramref name="r"/> quartos de volta. Deslocar 1 bit
        /// à esquerda equivale a N→E→S→W→N, que é o mesmo sentido de
        /// Quaternion.Euler(0, +90, 0) usado na instanciação — se um dia essa convenção mudar
        /// em Directions.RotateY, tem que mudar aqui junto.
        /// </summary>
        private static int RotateMask(int mask, int r)
        {
            r &= 3;
            return ((mask << r) | (mask >> (4 - r))) & 0xF;
        }

        private static int CountArms(int mask)
        {
            int n = 0;
            while (mask != 0) { mask &= mask - 1; n++; }
            return n;
        }

        // ------------------------------------------------------------------ utilidades
        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent)
        {
#if UNITY_EDITOR
            // Fora do play mode, instanciar como prefab instance deixa o andar inspecionável
            // e ligado ao asset — ajuda a depurar o kit (mesma escolha do TileInstancer).
            if (!Application.isPlaying)
                return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
            return Object.Instantiate(prefab, parent);
        }
    }
}
