// ============================================================================
//  TileInstancer.cs  —  camada WFC.Runtime
//
//  Instanciação dos prefabs de tile. Versão simples e síncrona: um Instantiate
//  por célula, tudo num frame.
//
//  A Fase 3 substitui isto por PrefabInstancer com object pooling e
//  InstantiateAsync/spread por frames. A separação já está aqui para que essa
//  troca não encoste no WFCFiller.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Runtime
{
    public static class TileInstancer
    {
        public const string GeneratedRootName = "__GeneratedFloor";

        // Nome do filho de piso dentro de cada prefab de peça — mesma convenção que
        // GreyboxTileGenerator.Floor() usa ("Floor" sem sufixo de célula; o sufixo
        // "[cell]" só entra no nome da RAIZ instanciada, não no filho). Arte de produção
        // que substituir o greybox precisa manter esse nome pro CounterRotateFloor
        // continuar achando o piso — é o mesmo tipo de convenção de pivô/nome que o
        // prefab de tocha já respeita (ver Incluindo e Editando Prefabs de Luz de Parede.md).
        private const string FloorChildName = "Floor";

        /// <summary>Remove qualquer andar gerado anteriormente sob este transform.</summary>
        public static void ClearGenerated(Transform parent)
        {
            if (parent == null) return;

            Transform existing = parent.Find(GeneratedRootName);
            while (existing != null)
            {
                // Object.Destroy() em Play Mode só efetiva no FIM do frame — o objeto
                // continua no lugar até lá. Sem desanexar antes, parent.Find() acha o
                // MESMO objeto de novo na próxima iteração e o laço nunca sai: é
                // exatamente o travamento ao trocar de andar (a 2ª geração encontra o
                // andar da 1ª ainda por destruir e trava aqui para sempre).
                // Desanexar tira o objeto de baixo de `parent` na hora, então o Find()
                // seguinte não o encontra mais — Destroy() cuida da limpeza depois.
                existing.SetParent(null);

                if (Application.isPlaying) Object.Destroy(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);

                existing = parent.Find(GeneratedRootName);
            }
        }

        /// <summary>
        /// Instancia uma peça por célula e coleta os SpawnAnchors encontrados nos prefabs.
        /// Devolve a raiz criada.
        /// </summary>
        /// <param name="layer">
        /// Layer aplicada em cascata na raiz e em cada peça instanciada (inclusive filhos).
        /// -1 = não mexe, mantém a layer que vier do prefab (comportamento antigo).
        /// </param>
        /// <param name="piecesOut">
        /// Opcional. Se vier um array de tamanho <c>variants.Length</c>, cada posição recebe
        /// a peça instanciada naquela célula (null onde não houve geometria). É o que permite
        /// ao jogo agrupar/ligar/desligar geometria por sala depois — sem isso o consumidor
        /// teria que redescobrir a célula de cada peça pelo nome ou pela posição.
        /// </param>
        /// <param name="cornerFillerPrefab">
        /// Opcional (normalmente <c>FloorSpec.cornerFillerPrefab</c>). Se vier preenchido,
        /// depois de instanciar todas as células roda <see cref="PlaceCornerFillers"/> — tampa
        /// com este prefab os vértices do grid onde uma parede horizontal e uma vertical se
        /// cruzam (ver comentário do método). Null = pula esse passo, comportamento antigo.
        /// </param>
        /// <param name="cornerFillerEverywhere">
        /// false (padrão) = pula cruzamentos que uma única peça já resolve sozinha (ver
        /// <see cref="OwnsCorner"/>) — evita excesso nas quinas externas/convexas de sala sem
        /// perder cobertura dos cruzamentos internos entre células diferentes. true = ignora
        /// esse filtro, planta em todo cruzamento perpendicular sem exceção (mais colunas).
        /// </param>
        public static Transform Build(Transform parent, TileSet tileSet, AnnotatedGrid grid,
                                      int[] variants, List<SpawnAnchor> anchorsOut, int layer = -1,
                                      Transform[] piecesOut = null, GameObject cornerFillerPrefab = null,
                                      bool cornerFillerEverywhere = false)
        {
            ClearGenerated(parent);

            var root = new GameObject(GeneratedRootName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            if (layer >= 0) root.layer = layer;

            var buffer = new List<SpawnAnchor>();

            for (int cell = 0; cell < variants.Length; cell++)
            {
                int variant = variants[cell];
                if (variant < 0) continue;

                GameObject prefab = tileSet.GetPrefab(variant);
                if (prefab == null) continue; // Wildcard_Air não tem geometria — nada a fazer

                GameObject instance = Instantiate(prefab, root.transform);
                instance.transform.localPosition = grid.CellToWorld(cell) - parent.position;
                Quaternion rotation = tileSet.GetRotationQuaternion(variant);
                instance.transform.localRotation = rotation;
                instance.name = $"{tileSet.GetVariantName(variant)} [{cell}]";

                CounterRotateFloor(instance.transform, rotation);

                // Todo o casco gerado precisa sair na MESMA layer (física/renderização
                // consistentes andar afora), independente da layer que o prefab de
                // origem tiver no asset — daí forçar em cascata em vez de confiar
                // no valor herdado.
                if (layer >= 0) SetLayerRecursively(instance, layer);

                if (piecesOut != null && cell < piecesOut.Length) piecesOut[cell] = instance.transform;

                if (anchorsOut == null) continue;

                // O plugin apenas COLETA os anchors; quem decide o que nasce neles é o
                // populador, que é externo (Decisão 4).
                buffer.Clear();
                instance.GetComponentsInChildren(true, buffer);
                foreach (SpawnAnchor anchor in buffer)
                {
                    anchor.cellIndex = cell;
                    anchorsOut.Add(anchor);
                }
            }

            if (cornerFillerPrefab != null)
                PlaceCornerFillers(root.transform, parent, grid, cornerFillerPrefab, layer, cornerFillerEverywhere);

            return root.transform;
        }

        // ------------------------------------------------------- corner fillers
        /// <summary>
        /// Tampa a costura de espessura de parede exposta nos vértices do grid onde 2 paredes
        /// PERPENDICULARES DE CÉLULAS DIFERENTES se cruzam — ex.: o fim de um Tile_Wall
        /// encontrando outro Tile_Wall girado 90°, ou parede contra corredor. Cada cubo de
        /// parede cobre a largura/profundidade inteira da própria célula, então dentro de UM
        /// prefab (Tile_Corner, Tile_DeadEnd) as duas paredes sempre se sobrepõem sozinhas —
        /// só a costura ENTRE células fica com a face fina (espessura) exposta, sem nada do
        /// lado perpendicular pra cobrir. É só isso que este passo resolve; por isso ele pula
        /// de propósito qualquer vértice onde uma única célula já "é dona" das duas paredes
        /// que se encontram ali (<see cref="OwnsCorner"/>) — plantar de novo ali seria
        /// redundante.
        ///
        /// Roda depois do laço principal porque precisa do <see cref="AnnotatedGrid"/> só —
        /// os rótulos de borda (<see cref="AnnotatedGrid.GetBorder"/>) já valem pro andar
        /// inteiro antes de qualquer peça ser instanciada, então isto nem depende de
        /// `variants`/rotação, só da grade anotada.
        /// </summary>
        /// <param name="everywhere">
        /// false (padrão) = usa <see cref="OwnsCorner"/> pra pular quina auto-suficiente.
        /// true = ignora o filtro, planta em todo cruzamento perpendicular sem exceção.
        /// </param>
        private static void PlaceCornerFillers(Transform root, Transform parent, AnnotatedGrid grid,
                                               GameObject fillerPrefab, int layer, bool everywhere)
        {
            Grid3D g = grid.Grid;
            int placed = 0;

            for (int y = 0; y < g.SizeY; y++)
            for (int vz = 0; vz <= g.SizeZ; vz++)
            for (int vx = 0; vx <= g.SizeX; vx++)
            {
                // Vão de porta: o jogador nunca encara esse "ângulo" de frente (a peça de porta
                // já resolve visualmente o vão sozinha), então nenhuma quina tocando um vértice
                // com borda Door ganha coluna — senão sobra viga do lado do batente sem sentido.
                if (NearDoor(grid, y, vx, vz)) continue;

                // As até 4 células que tocam este vértice (SW/SE/NW/NE em relação a ele).
                // Se alguma já tem, sozinha, as duas paredes que se cruzam bem na própria
                // quina dela, a geometria dela já cobre o buraco — nada a fazer aqui. Isso é
                // o que reduz o excesso nas quinas EXTERNAS/convexas de sala (Tile_Corner
                // sempre "é dono" das duas paredes que tem), sem tocar nos cruzamentos
                // INTERNOS de célula diferente (que continuam sem dono, então continuam
                // ganhando coluna). SÓ pula se `everywhere` for false.
                if (!everywhere)
                {
                    if (OwnsCorner(grid, vx - 1, y, vz - 1, Direction.PosX, Direction.PosZ)) continue; // SW → quina NE
                    if (OwnsCorner(grid, vx,     y, vz - 1, Direction.NegX, Direction.PosZ)) continue; // SE → quina NW
                    if (OwnsCorner(grid, vx - 1, y, vz,     Direction.PosX, Direction.NegZ)) continue; // NW → quina SE
                    if (OwnsCorner(grid, vx,     y, vz,     Direction.NegX, Direction.NegZ)) continue; // NE → quina SW
                }

                // Parede "horizontal" (corre Leste-Oeste) tocando o vértice: o segmento a
                // Oeste dele (borda entre a célula SW e a NW) OU o segmento a Leste (borda
                // entre SE e NE). Parede "vertical" (corre Norte-Sul): segmento ao Sul (SW-SE)
                // OU ao Norte (NW-NE). Duas paredes perpendiculares aqui = costura.
                bool horizontal =
                    EdgeIsWall(grid, y, vx - 1, vz - 1, vx - 1, vz,     Direction.PosZ) || // segmento Oeste
                    EdgeIsWall(grid, y, vx,     vz - 1, vx,     vz,     Direction.PosZ);   // segmento Leste

                bool vertical =
                    EdgeIsWall(grid, y, vx - 1, vz - 1, vx,     vz - 1, Direction.PosX) || // segmento Sul
                    EdgeIsWall(grid, y, vx - 1, vz,     vx,     vz,     Direction.PosX);   // segmento Norte

                if (!horizontal || !vertical) continue;

                Vector3 worldPos = grid.Origin +
                    new Vector3(vx * grid.CellSize, y * grid.CellHeight, vz * grid.CellSize);

                GameObject instance = Instantiate(fillerPrefab, root);
                instance.transform.localPosition = worldPos - parent.position;
                instance.transform.localRotation = Quaternion.identity; // coluna é simétrica em Y — não importa girar
                instance.name = $"{fillerPrefab.name}_CornerFiller [{vx},{y},{vz}]";
                if (layer >= 0) SetLayerRecursively(instance, layer);
                placed++;
            }

            Debug.Log($"[TileInstancer] PlaceCornerFillers: {placed} coluna(s) plantada(s) " +
                     $"(everywhere={everywhere}).");
        }

        /// <summary>
        /// Alguma das até 4 células jogáveis que tocam este vértice tem um vão de porta
        /// (<see cref="BorderLabel.Door"/>) numa das 4 bordas horizontais? Basta uma pra
        /// suprimir a coluna neste vértice inteiro — é uma margem generosa de propósito
        /// (1 célula de raio ao redor de toda porta), porque o objetivo é nunca ter viga
        /// decorativa disputando atenção com o batente, não maximizar cobertura de quina.
        /// </summary>
        private static bool NearDoor(AnnotatedGrid grid, int y, int vx, int vz)
        {
            Grid3D g = grid.Grid;
            int[] xs = { vx - 1, vx, vx - 1, vx };
            int[] zs = { vz - 1, vz - 1, vz, vz };

            for (int i = 0; i < 4; i++)
            {
                if (!g.InBounds(xs[i], y, zs[i])) continue;
                int cell = g.Index(xs[i], y, zs[i]);
                if (!grid.IsPlayable(cell)) continue;

                foreach (Direction dir in Directions.Horizontal)
                    if (grid.GetBorder(cell, dir) == BorderLabel.Door)
                        return true;
            }
            return false;
        }

        /// <summary>
        /// A borda entre a célula (ax,az) e (bx,bz) [adjacentes, (ax,az)→(bx,bz) na direção
        /// `aToB`] é parede? Consulta pelo lado JOGÁVEL da borda, qualquer um dos dois —
        /// GetBorder já é simétrico (checa os dois lados por dentro), então só precisa achar
        /// UMA célula válida+jogável no par pra ter a resposta certa.
        ///
        /// Por que não fixar sempre o mesmo lado (ex.: só A): a maioria das salas deste projeto
        /// fica cercada de vazio, então metade das bordas reais de parede tem exatamente o lado
        /// "de fora" não-jogável — perguntar só por esse lado perde a parede real que a sala
        /// (do outro lado, jogável) sabe que existe. E não dá pra simplesmente tirar o filtro de
        /// IsPlayable: sem ele, uma borda com OS DOIS lados vazios (interior do vazio, longe de
        /// qualquer sala) volta a contar como "parede" à toa — é o bug anterior.
        /// Nem A nem B jogável (ou nenhum dos dois existe) = não há parede real, devolve false.
        /// </summary>
        private static bool EdgeIsWall(AnnotatedGrid grid, int y, int ax, int az, int bx, int bz, Direction aToB)
        {
            Grid3D g = grid.Grid;

            if (g.InBounds(ax, y, az))
            {
                int a = g.Index(ax, y, az);
                if (grid.IsPlayable(a)) return grid.GetBorder(a, aToB) == BorderLabel.Wall;
            }
            if (g.InBounds(bx, y, bz))
            {
                int b = g.Index(bx, y, bz);
                if (grid.IsPlayable(b)) return grid.GetBorder(b, Directions.Opposite(aToB)) == BorderLabel.Wall;
            }
            return false;
        }

        /// <summary>
        /// Célula (x,y,z) existe, é JOGÁVEL, e tem parede nas DUAS bordas `d1`/`d2` (sua
        /// própria quina)? Só faz sentido perguntar pra própria célula (diferente de
        /// <see cref="EdgeIsWall"/>): "dona da quina" é sempre uma célula específica, uma
        /// vizinha vazia nunca pode "possuir" a quina de outra.
        /// </summary>
        private static bool OwnsCorner(AnnotatedGrid grid, int x, int y, int z, Direction d1, Direction d2)
        {
            Grid3D g = grid.Grid;
            if (!g.InBounds(x, y, z)) return false;
            int cell = g.Index(x, y, z);
            if (!grid.IsPlayable(cell)) return false;
            return grid.GetBorder(cell, d1) == BorderLabel.Wall && grid.GetBorder(cell, d2) == BorderLabel.Wall;
        }

        /// <summary>
        /// Cancela a rotação da PEÇA só no filho de piso, mantendo a orientação dele fixa no
        /// mundo (identidade) não importa como o WFC girou a peça pra encaixar as paredes.
        ///
        /// Por que isto é seguro: o contorno do piso é um quadrado centrado na célula — girar
        /// 90/180/270 não muda footprint nem cria buraco/sobreposição com a peça vizinha,
        /// então contra-rotacionar só ele não quebra encaixe nenhum.
        ///
        /// Por que isto existe: sem isto, a MESMA textura de piso aparece virada de um jeito
        /// diferente em cada rotação de peça — e se a textura tiver qualquer sombra/luz assada
        /// nela (comum em material extraído de .blend/FBX), cada orientação lê como um bloco
        /// de brilho diferente do vizinho, um mosaico visível que não tem nada a ver com luz
        /// em tempo real (foi confirmado: zerar o FloorLightField não muda nada nele).
        /// </summary>
        private static void CounterRotateFloor(Transform piece, Quaternion pieceRotation)
        {
            Transform floor = piece.Find(FloorChildName);
            if (floor == null) return; // peça sem filho "Floor" (ex.: Wildcard_Air) — nada a fazer

            floor.localRotation = Quaternion.Inverse(pieceRotation);
        }

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
            // Fora do play mode, instanciar como prefab instance deixa o andar
            // inspecionável e ligado ao asset original — ajuda a depurar o kit.
            if (!Application.isPlaying)
                return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
            return Object.Instantiate(prefab, parent);
        }
    }
}
