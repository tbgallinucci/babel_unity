// ============================================================================
//  CenterWallTileGenerator.cs  —  camada WFC.Editor
//
//  Gera as 5 peças de parede do tileset de GRID DUAL, onde a parede não mora
//  mais na borda da célula do esqueleto e sim CENTRADA nela — e o pivô da peça
//  fica em cima de um VÉRTICE do grid.
//
//  Menu:  WFC ▸ Center Wall Tile Generator
//
//  ------------------------------------------------------------------------
//  POR QUE ISTO EXISTE (leia antes de mexer)
//
//  No tileset greybox original a parede é um cubo DENTRO da célula, encostado
//  na borda. Dois problemas nascem disso, e são o mesmo problema:
//
//    • parede que termina sem continuação deixa a espessura exposta;
//    • dois cubos de parede perpendiculares vindos de CÉLULAS DIFERENTES se
//      encontram aresta com aresta, com zero sobreposição, e sobra um buraco
//      quadrado de T×T na quina convexa.
//
//  A raiz é que o socket (`open`/`wall`/`door`) descreve a borda, não o PERFIL
//  da face — ele não sabe dizer "tem um toco de parede no canto norte desta
//  face", então o bake libera vizinhos cujas superfícies não casam.
//
//  A solução estrutural é mudar de grid: as peças daqui são posicionadas nos
//  VÉRTICES do grid do esqueleto, não nas células. Um vértice tem 4 "braços"
//  (os quatro meio-segmentos de borda que saem dele), e a peça escolhida é
//  função de quais braços são parede. Consequência que resolve tudo de uma vez:
//  a curva de 90° passa a acontecer INTEIRA dentro de uma peça só, então quina
//  partida entre duas peças deixa de ser possível por construção.
//
//  Convenção das peças (canônica, antes de rotacionar):
//
//      Wall_End       1 braço          → N
//      Wall_Straight  2 opostos        → N + S
//      Wall_Corner    2 adjacentes     → N + E     ← a curva mora aqui
//      Wall_Tee       3 braços         → N + E + S
//      Wall_Cross     4 braços
//
//  Pivô no vértice, y=0 no nível do piso, cada braço com S/2 de comprimento
//  (duas peças vizinhas somam os S de um segmento de borda inteiro).
//  Espessura T CENTRADA na linha da borda (T/2 pra cada lado) — diferente do
//  greybox antigo, onde os T ficavam todos dentro de uma célula só.
// ============================================================================

using UnityEditor;
using UnityEngine;
using WFC.Runtime;

namespace WFC.EditorTools
{
    public sealed class CenterWallTileGenerator : EditorWindow
    {
        // ATENÇÃO: Cell Size tem que bater com o FloorSpec que vai consumir estas peças —
        // é ele que define o passo do grid, e o braço de S/2 só encosta no braço do vértice
        // vizinho se os dois usarem o mesmo S.
        private float cellSize = 6f;
        private float wallHeight = 10f;
        private float wallThickness = 0.6f;

        // Raio da curva do canto, medido na LINHA DE CENTRO da parede. 0 = miter reto
        // (os dois braços simplesmente se sobrepõem no quadrado central — já sem buraco,
        // só sem arredondamento). Quanto maior, mais a parede "corta" a quina.
        // Cantos vivos de propósito: referência de Babilônia usa muito canto reto. 0 = miter puro
        // (os 2 braços se sobrepõem no quadrado central, sem buraco, sem arredondamento nenhum) —
        // não esbarra no clamp de EffectiveRadius (que existiria a partir de 0 < r < T/2).
        private float cornerRadius = 0f;
        private int arcSegments = 10;

        // Piso: mesma convenção do kit antigo (topo da laje em y=0, footprint = Cell Size).
        private float floorThickness = 0.6f;

        // Porta: altura do vão em metros — igual ao kit antigo, independente de Wall Height.
        // Só usada pelo Arm_Door (a verga por cima do vão); os braços genéricos (Arm_Wall/
        // Arm_Door) e os hubs (Hub_End/Hub_Junction) são o que monta um vértice MISTO (com
        // pelo menos uma porta) em vez de uma peça dedicada por combinação — ver o cabeçalho
        // do DualGridWallBuilder pro raciocínio completo.
        private float doorHeight = 3.3f;

        // Largura LIVRE do vão (por onde o jogador passa), em metros, CENTRADA no meio do
        // segmento de borda inteiro (a junção entre os dois braços de vértices vizinhos que
        // formam a porta). Cada braço contribui com metade: um trecho de batente (parede normal,
        // altura inteira) perto do próprio vértice, e o vão só abre perto da ponta externa do
        // braço, que é o meio do segmento. >= Cell Size = vão consome o braço inteiro, sem
        // batente (era o único comportamento antes deste campo existir).
        private float doorGapWidth = 2.2f;

        // Âncora de luz — só a peça RETA leva. As de canto/T/cruz/ponta ficam sem, de
        // propósito: é a regra "luz nunca em quina" do BasicLightingPopulator satisfeita de
        // graça, do mesmo jeito que o Tile_Corner do kit antigo já fazia.
        private float wallLightOffset = 1.0f;
        private float wallLightHeightRatio = 0.5f;

        // Âncora de baú — só o Wall_Corner leva (mesma regra do kit antigo: baú só nasce
        // exatamente na quina de uma sala). PropRoomPopulator procura SpawnAnchorKind.Chest;
        // sem essa âncora em NENHUMA peça do kit novo, ChestRoom nunca sorteia baú — foi
        // esquecida na migração do Tile_Corner (kit antigo) pro Wall_Corner (grid dual).
        private float chestAnchorInset = 3.0f;

        // Deslocamento do anchor de pilar, só no Wall_Corner, na mesma diagonal do anchor de
        // baú (pra sala, não pro vazio). Sem isso o pilar nasce exatamente no vértice — que
        // fica pouco visível na sala quando o Wall_Cross (4 braços, mesmo raio de pilar) come
        // mais da circunferência dele do que o Corner (2 braços). O Wall_Cross fica centrado
        // (não tem UMA direção "pra fora" única — é simétrico nos 4 lados), de propósito.
        private float pillarAnchorOutset = 0.4f;

        // Cada degrau sobe H/stairSteps — com H=10 e o default de 4 (herdado do kit antigo,
        // onde a escada era só decoração de fundo) cada degrau tinha 2.5m, mais alto que o
        // personagem. 15 dá ~0.67m por degrau — ainda estilizado/greybox, mas na escala certa.
        private int stairSteps = 15;

        private string outputFolder = "Assets/WFC/CenterWallTiles";

        private Material matWall, matFloor, matAccent;

        [MenuItem("WFC/Center Wall Tile Generator")]
        private static void Open() => GetWindow<CenterWallTileGenerator>("Center Walls");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Paredes de grid dual", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gera as 5 peças de parede posicionadas nos VÉRTICES do grid (End, Straight, " +
                "Corner, Tee, Cross) — usadas quando o vértice é todo parede — mais 4 peças de " +
                "composição (Arm_Wall, Arm_Door, Hub_End, Hub_Junction) que montam qualquer vértice " +
                "MISTO, com porta em qualquer posição. A curva de 90° fica dentro da peça de canto " +
                "toda-parede, então quina partida entre duas peças não pode acontecer.\n\n" +
                "Idempotente: regerar sobrescreve.",
                MessageType.Info);

            EditorGUILayout.Space();
            cellSize = EditorGUILayout.FloatField(
                new GUIContent("Cell Size", "Passo do grid, em metros. Tem que ser o MESMO cellSize do " +
                                "FloorSpec que vai usar estas peças — cada braço mede S/2 e precisa " +
                                "encostar no braço do vértice vizinho."),
                cellSize);
            wallHeight = EditorGUILayout.FloatField("Wall Height", wallHeight);
            wallThickness = EditorGUILayout.FloatField(
                new GUIContent("Wall Thickness", "Espessura total, CENTRADA na linha da borda (T/2 pra " +
                                "cada lado). No greybox antigo os T ficavam todos dentro de uma célula."),
                wallThickness);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Curva do canto", EditorStyles.boldLabel);
            cornerRadius = EditorGUILayout.Slider(
                new GUIContent("Corner Radius", "Raio da curva medido na linha de centro da parede. " +
                                "0 = miter reto (sem buraco, mas sem arredondamento). O máximo é Cell Size / 2, " +
                                "onde a curva consome o braço inteiro."),
                cornerRadius, 0f, Mathf.Max(0.01f, cellSize * 0.5f));
            arcSegments = EditorGUILayout.IntSlider(
                new GUIContent("Arc Segments", "Subdivisões da curva. Mais = mais liso e mais triângulo."),
                arcSegments, 2, 32);

            if (cornerRadius > 0.001f && Mathf.Abs(EffectiveRadius - cornerRadius) > 0.001f)
                EditorGUILayout.HelpBox(
                    $"Corner Radius {cornerRadius:F2} é menor que Wall Thickness/2 ({T * 0.5f:F2}) — " +
                    $"abaixo disso o raio INTERNO da curva viraria negativo (geometria invertida). " +
                    $"Raio efetivo usado na geração: {EffectiveRadius:F2}. Pra um raio menor de " +
                    "verdade, baixe Wall Thickness junto, ou use 0 pra miter reto (sem curva nenhuma).",
                    MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Âncora de luz (só na peça reta)", EditorStyles.boldLabel);
            wallLightOffset = EditorGUILayout.FloatField(
                new GUIContent("Wall Light Offset", "Distância do anchor até a LINHA DE CENTRO da parede. " +
                                "A face da parede fica a T/2 daí, então a folga real é offset − T/2."),
                wallLightOffset);
            wallLightHeightRatio = EditorGUILayout.Slider(
                new GUIContent("Wall Light Height", "Altura do anchor como fração de Wall Height."),
                wallLightHeightRatio, 0f, 1f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Âncora de baú (só no Wall_Corner)", EditorStyles.boldLabel);
            chestAnchorInset = EditorGUILayout.FloatField(
                new GUIContent("Chest Anchor Inset", "Distância do anchor até a quina interna da sala, na " +
                                "diagonal (afastando dos dois braços de parede ao mesmo tempo), em metros."),
                chestAnchorInset);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Âncora de pilar (Wall_Corner e Wall_Cross)", EditorStyles.boldLabel);
            pillarAnchorOutset = EditorGUILayout.FloatField(
                new GUIContent("Pillar Anchor Outset", "Só afeta o Wall_Corner — desloca o anchor na mesma " +
                                "diagonal do baú, pra sala, em metros. 0 = exatamente no vértice. O Wall_Cross " +
                                "fica sempre centrado (não tem uma direção 'pra fora' única)."),
                pillarAnchorOutset);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Piso", EditorStyles.boldLabel);
            floorThickness = EditorGUILayout.FloatField("Floor Thickness", floorThickness);
            stairSteps = EditorGUILayout.IntSlider(
                new GUIContent("Stair Steps", "Quantos degraus dividem a subida de Wall Height inteiro. " +
                                "Mais degraus = cada um mais baixo (H / Stair Steps por degrau)."),
                stairSteps, 2, 30);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Porta (braços/hubs de composição)", EditorStyles.boldLabel);
            doorHeight = EditorGUILayout.FloatField(
                new GUIContent("Door Opening Height", "Altura do vão em metros — fixa, NÃO acompanha " +
                                "Wall Height. Precisa ser menor que Wall Height (o resto vira verga)."),
                doorHeight);
            doorGapWidth = EditorGUILayout.Slider(
                new GUIContent("Door Opening Width", "Largura LIVRE do vão, centrada no meio do " +
                                "segmento de borda (a junção entre os 2 braços de vértices vizinhos). " +
                                "Menor que Cell Size sobra batente de parede normal perto do vértice; " +
                                "= Cell Size é o vão consumindo o braço inteiro (comportamento antigo)."),
                doorGapWidth, 0.5f, cellSize);

            EditorGUILayout.Space();
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

            EditorGUILayout.Space();
            if (GUILayout.Button("Gerar kit (9 paredes + 4 pisos)", GUILayout.Height(32)))
                Generate();
            if (GUILayout.Button("Apagar peças geradas"))
                DeleteGenerated();
        }

        // Atalhos de dimensão (mesmo idioma do GreyboxTileGenerator)
        private float S => cellSize;
        private float H => wallHeight;
        private float T => wallThickness;

        /// <summary>Raio efetivo: 0 vira miter reto; acima disso fica preso entre T/2 e S/2.</summary>
        private float EffectiveRadius
        {
            get
            {
                if (cornerRadius <= 0.001f) return 0f;
                // Abaixo de T/2 o raio interno da curva ficaria negativo (geometria invertida);
                // acima de S/2 a curva comeria mais que o braço inteiro.
                return Mathf.Clamp(cornerRadius, T * 0.5f, S * 0.5f);
            }
        }

        // ------------------------------------------------------------------ Build
        private void Generate()
        {
            EnsureFolder(outputFolder);
            EnsureFolder(outputFolder + "/Materials");
            matWall   = MakeMat("M_CenterWall",   new Color(0.55f, 0.55f, 0.55f));
            matFloor  = MakeMat("M_CenterFloor",  new Color(0.80f, 0.80f, 0.78f));
            matAccent = MakeMat("M_CenterAccent", new Color(0.62f, 0.58f, 0.48f));

            BuildEnd();
            BuildStraight();
            BuildCorner();
            BuildTee();
            BuildCross();

            BuildArmWall();
            BuildArmDoor();
            BuildHubEnd();
            BuildHubJunction();

            BuildFloorFlat();
            BuildStairs();
            BuildSolidFill();
            BuildAir();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CenterWallTileGenerator] kit gerado em {outputFolder} " +
                      $"(cellSize {S}, thickness {T}, radius {EffectiveRadius:F2}).");
        }

        // Direções canônicas de braço. A rotação real é aplicada na instanciação
        // (DualGridWallBuilder), nunca aqui — cada peça é autorada UMA vez.
        private enum Arm { North, East, South, West }

        /// <summary>
        /// Braço reto saindo do vértice: vai de <paramref name="from"/> (distância com sinal
        /// a partir do centro, ao longo do eixo do braço) até S/2, com T de largura centrado
        /// no eixo perpendicular. `from` negativo faz o braço entrar no quadrado central —
        /// é isso que garante sobreposição entre braços perpendiculares.
        /// </summary>
        private GameObject ArmBox(Transform parent, string name, Arm arm, float from)
        {
            float far = S * 0.5f;
            float len = far - from;
            float mid = (from + far) * 0.5f;

            Vector3 pos, size;
            switch (arm)
            {
                case Arm.North: pos = new Vector3(0, H * 0.5f,  mid); size = new Vector3(T, H, len); break;
                case Arm.South: pos = new Vector3(0, H * 0.5f, -mid); size = new Vector3(T, H, len); break;
                case Arm.East:  pos = new Vector3( mid, H * 0.5f, 0); size = new Vector3(len, H, T); break;
                default:        pos = new Vector3(-mid, H * 0.5f, 0); size = new Vector3(len, H, T); break;
            }
            return Cube(name, pos, size, parent);
        }

        private void BuildStraight()
        {
            var root = Root("Wall_Straight");
            // Uma caixa só cobrindo os dois braços: o quadrado central não precisa de
            // tratamento porque não há mudança de direção aqui.
            Cube("Wall", new Vector3(0, H * 0.5f, 0), new Vector3(T, H, S), root.transform);

            // Um anchor de CADA lado. A peça não sabe (nem pode saber) qual lado é sala e qual
            // é vazio — quem decide é o DualGridWallBuilder, que resolve pela posição no mundo
            // e destrói o que cair fora de célula jogável. Parede entre duas salas fica com os
            // dois; fachada fica só com o de dentro.
            WallLight(root.transform, "Anchor_Light_E", Vector3.right);
            WallLight(root.transform, "Anchor_Light_W", Vector3.left);

            Save(root);
        }

        /// <summary>
        /// Anchor de luz deslocado da linha de centro da parede na direção
        /// <paramref name="inward"/> e virado pra ela — mesmo idioma do WallLight do
        /// GreyboxTileGenerator, mas medido a partir do CENTRO da parede (a face está a T/2).
        /// </summary>
        private void WallLight(Transform parent, string name, Vector3 inward)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = inward * wallLightOffset + Vector3.up * (H * wallLightHeightRatio);
            go.transform.localRotation = Quaternion.LookRotation(inward, Vector3.up);

            var anchor = go.AddComponent<SpawnAnchor>();
            anchor.kind = SpawnAnchorKind.Light;
            anchor.clearanceRadius = 0.3f;
        }

        // Marca "cabe baú aqui" — só nasce no Wall_Corner, de propósito: é a peça que só existe
        // exatamente na quina de uma sala (mesma regra do Tile_Corner do kit antigo). `inward` é
        // a diagonal que afasta o anchor dos DOIS braços de parede ao mesmo tempo, pra dentro da
        // sala — no Wall_Corner canônico (braços Norte+Leste: ArmN separa a célula NW da NE,
        // ArmE separa a SE da NE — as DUAS bordas dessa peça encostam na célula NE), a sala é a
        // célula NE, ou seja, o MESMO quadrante pra onde os braços se estendem, não o oposto.
        // inward = (1,0,1). O vértice é a própria quina interna da sala (não um ponto fora dela);
        // a quina INTERNA de verdade (onde as duas faces de dentro das paredes se encontrariam)
        // fica a T/2 do vértice nessa diagonal — é daí que o inset conta.
        private void CornerChestAnchor(Transform parent, Vector3 inward)
        {
            Vector3 dir = inward.normalized;
            Vector3 innerCorner = dir * (T * 0.5f);

            var go = new GameObject("Anchor_Chest");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = innerCorner + dir * chestAnchorInset;
            go.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);

            var anchor = go.AddComponent<SpawnAnchor>();
            anchor.kind = SpawnAnchorKind.Chest;
            anchor.clearanceRadius = 0.8f;
        }

        // ---------------------------------------------------------------- pisos
        // Neste kit a peça de célula é SÓ PISO: quem descreve formato de parede é o grid dual.
        // Por isso um prefab de laje serve pra todos os módulos de piso do tileset (aberto,
        // parede, canto, corredor, beco, porta...) — eles diferem só no socket, não na malha.
        private GameObject Floor(Transform parent)
            => Cube("Floor", new Vector3(0, -floorThickness * 0.5f, 0),
                    new Vector3(S, floorThickness, S), parent, matFloor);

        private void BuildFloorFlat()
        {
            var root = Root("Tile_Floor_Flat");
            Floor(root.transform);
            Save(root);
        }

        private void BuildStairs()
        {
            var root = Root("Tile_Stairs");
            Floor(root.transform);
            int n = stairSteps;
            float d = S / n;
            for (int s = 0; s < n; s++)
            {
                float h = (s + 1) * H / n;                  // sobe em direção a +Z
                float zc = -S * 0.5f + d * s + d * 0.5f;
                Cube($"Step_{s}", new Vector3(0, h * 0.5f, zc), new Vector3(S, h, d),
                     root.transform, matAccent);
            }
            Save(root);
        }

        private void BuildSolidFill()
        {
            var root = Root("Wildcard_SolidFill");
            Cube("Solid", new Vector3(0, H * 0.5f, 0), new Vector3(S, H, S), root.transform);
            Save(root);
        }

        private void BuildAir()
        {
            var root = Root("Wildcard_Air"); // vazio: sem malha, sem collider
            Save(root);
        }

        private void BuildEnd()
        {
            var root = Root("Wall_End");
            // Braço até o centro + calota cilíndrica no vértice: a parede termina arredondada
            // em vez de mostrar a face fina da espessura, que era a queixa original do kit antigo.
            ArmBox(root.transform, "Arm_N", Arm.North, 0f);
            Cylinder("Cap", new Vector3(0, H * 0.5f, 0), T * 0.5f, H, root.transform);
            Save(root);
        }

        private void BuildTee()
        {
            var root = Root("Wall_Tee");
            // -T/2: cada braço entra no quadrado central, então os três se sobrepõem lá.
            ArmBox(root.transform, "Arm_N", Arm.North, -T * 0.5f);
            ArmBox(root.transform, "Arm_E", Arm.East,  -T * 0.5f);
            ArmBox(root.transform, "Arm_S", Arm.South, -T * 0.5f);
            Save(root);
        }

        private void BuildCross()
        {
            var root = Root("Wall_Cross");
            ArmBox(root.transform, "Arm_N", Arm.North, -T * 0.5f);
            ArmBox(root.transform, "Arm_E", Arm.East,  -T * 0.5f);
            ArmBox(root.transform, "Arm_S", Arm.South, -T * 0.5f);
            ArmBox(root.transform, "Arm_W", Arm.West,  -T * 0.5f);
            PillarAnchor(root.transform);
            Save(root);
        }

        // Marca "cabe pilar decorativo aqui" — só em Wall_Corner e Wall_Cross, de propósito: são
        // os dois vértices onde um pilar reforçando a junção faz sentido arquitetônico (canto de
        // sala, cruzamento de corredores). No CENTRO do vértice (0,0,0 local), não deslocado como
        // WallLight/CornerChestAnchor — o pilar fica embaixo do próprio ponto onde os braços se
        // encontram, não do lado. Kind própria (SpawnAnchorKind.Pillar), não WallProp — ver
        // comentário dela em SpawnAnchor.cs.
        // `outward` = default(Vector3) (zero) → anchor fica exatamente no vértice (caso
        // Wall_Cross, sem direção única de "pra fora"). Qualquer vetor não-zero desloca o
        // anchor nessa direção por `pillarAnchorOutset` metros (caso Wall_Corner).
        private void PillarAnchor(Transform parent, Vector3 outward = default, string suffix = "")
        {
            var go = new GameObject("Anchor_Pillar" + suffix);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = outward.sqrMagnitude > 0.0001f
                ? outward.normalized * pillarAnchorOutset
                : Vector3.zero;

            var anchor = go.AddComponent<SpawnAnchor>();
            anchor.kind = SpawnAnchorKind.Pillar;
            anchor.clearanceRadius = 0.6f;
        }

        // ------------------------------------------------------ composição (vértices com porta)
        // Ver o cabeçalho de DualGridWallBuilder.PlaceComposed pro raciocínio de por que estas 4
        // peças bastam pra montar QUALQUER combinação de parede/porta num vértice, em vez de
        // precisar de uma peça dedicada por combinação.

        // Braço genérico sólido, como prefab AUTÔNOMO (não filho de uma peça maior) — em DUAS
        // variantes, porque o ponto de origem certo depende de quem mais está no vértice:
        //
        //   Arm_Wall       (from=0)    — par OPOSTO, sem Hub_Junction. Os 2 braços do vértice são
        //                                os ÚNICOS a cobrir o centro, então precisam se encontrar
        //                                exatamente no plano do vértice (zero de gap).
        //   Arm_Wall_AtHub (from=T/2)  — canto misto/T/cruz/ponta, COM Hub_Junction/Hub_End. O hub
        //                                sozinho já cobre o quadrado central — o braço não pode
        //                                invadir esse território, senão vira sobra tripla (2
        //                                braços perpendiculares + hub, todos com topo/base na
        //                                mesma altura) — z-fighting confirmado bem aqui.
        //
        // Por que não dava pra usar só "from=0" em todo lugar (o que a v1 fazia): dois braços
        // PERPENDICULARES (não só opostos) com from=0 ainda se sobrepõem numa faixa T/2×T/2 perto
        // do vértice — a espessura de um braço sempre invade o território do outro um pouco,
        // mesmo sem estender pra trás do zero. from=T/2 elimina isso: nesse ponto a faixa de
        // espessura de um braço já não alcança mais o território do outro.
        private void BuildArmWall()
        {
            BuildArmWallVariant("Arm_Wall", 0f);
            BuildArmWallVariant("Arm_Wall_AtHub", T * 0.5f);
        }

        private void BuildArmWallVariant(string prefabName, float from)
        {
            var root = Root(prefabName);
            ArmBox(root.transform, "Arm", Arm.North, from);
            Save(root);
        }

        // Braço em duas partes: um trecho de BATENTE (parede normal, altura inteira) perto do
        // próprio vértice, e o VÃO (aberto embaixo, só verga em cima) perto da ponta externa —
        // que é o meio do segmento de borda inteiro, onde este braço encontra o do vértice
        // vizinho. Doorgap maior = batente encolhe; doorGapWidth >= Cell Size = sem batente
        // nenhum, vão consome o braço inteiro (era o único comportamento antes deste campo).
        //
        // Duas variantes pelo mesmo motivo do Arm_Wall (ver comentário lá): Arm_Door (from=0,
        // par oposto sem hub) e Arm_Door_AtHub (from=T/2, com Hub_Junction/Hub_End por perto).
        private void BuildArmDoor()
        {
            BuildArmDoorVariant("Arm_Door", 0f);
            BuildArmDoorVariant("Arm_Door_AtHub", T * 0.5f);
        }

        private void BuildArmDoorVariant(string prefabName, float from)
        {
            var root = Root(prefabName);
            float doorH = Mathf.Min(doorHeight, H - 0.05f);
            float lintelH = H - doorH;
            float far = S * 0.5f;
            float gapHalf = Mathf.Clamp(doorGapWidth * 0.5f, 0f, far - from);
            float openFrom = far - gapHalf; // onde o vão começa, a partir do vértice

            float jambLen = openFrom - from;
            if (jambLen > 0.01f)
            {
                float jambMid = (from + openFrom) * 0.5f;
                Cube("Jamb", new Vector3(0, H * 0.5f, jambMid), new Vector3(T, H, jambLen), root.transform);
            }

            if (gapHalf > 0.01f)
            {
                float lintelMid = (openFrom + far) * 0.5f;
                // Cube() normal — voltou. A tentativa de malha própria (WallAlignedBox, pra
                // corrigir o desalinhamento vertical de UV) quebrou geometria em runtime (peça
                // achatada em 2D, texturas com glitch/flicker perto da parede vizinha) e eu não
                // tenho como depurar sem rodar a Unity. Revertido pro estado que funcionava —
                // o desalinhamento de textura volta a existir, mas é cosmético; o glitch não era.
                // Se for resolver a textura de novo, seguir pela rota do Shader Graph
                // (world-aligned) que já estava em andamento, não malha à mão.
                Cube("Lintel", new Vector3(0, doorH + lintelH * 0.5f, lintelMid), new Vector3(T, lintelH, gapHalf),
                     root.transform);
            }

            Save(root);
        }

        // Tampa arredondada pro vértice de 1 braço só, quando esse braço é PORTA (o caso
        // all-parede usa o Wall_End normal, com o mesmo formato).
        private void BuildHubEnd()
        {
            var root = Root("Hub_End");
            Cylinder("Cap", new Vector3(0, H * 0.5f, 0), T * 0.5f, H, root.transform);
            Save(root);
        }

        // Cubo T×T de reforço central — só entra em jogo no caso raro de canto/T/cruz onde
        // NENHUM braço presente é parede (ex.: T com porta nos 3 lados), pra garantir que o
        // vértice não fique sem geometria nenhuma no meio.
        private void BuildHubJunction()
        {
            var root = Root("Hub_Junction");
            Cube("Stub", new Vector3(0, H * 0.5f, 0), new Vector3(T, H, T), root.transform);
            Save(root);
        }

        // A peça que justifica o tileset inteiro: a curva de 90° acontece aqui dentro,
        // então nenhuma quina fica dividida entre duas peças.
        private void BuildCorner()
        {
            var root = Root("Wall_Corner");
            float r = EffectiveRadius;

            if (r <= 0f)
            {
                // Miter reto. Já é correto (os braços se sobrepõem no quadrado central,
                // sem buraco) — só não é arredondado.
                ArmBox(root.transform, "Arm_N", Arm.North, -T * 0.5f);
                ArmBox(root.transform, "Arm_E", Arm.East,  -T * 0.5f);
                CornerChestAnchor(root.transform, new Vector3(1f, 0f, 1f));
                // 2 anchors, direções opostas: um pra dentro desta sala (NE), outro pro lado de
                // fora da quina (SW — vazio a maior parte do tempo, mas às vezes é OUTRA sala/
                // corredor encostando na mesma quina). DualGridWallBuilder descarta sozinho
                // qualquer anchor que caia em célula não-jogável — não precisa checar aqui qual
                // lado é qual, o filtro de "célula vazia" já resolve.
                PillarAnchor(root.transform, new Vector3(1f, 0f, 1f), "_Inner");
                PillarAnchor(root.transform, new Vector3(-1f, 0f, -1f), "_Outer");
                Save(root);
                return;
            }

            // Com curva: os braços retos param nos pontos de tangência (z=r no norte,
            // x=r no leste) e a curva liga um ao outro. O overlap de 1cm evita fresta por
            // erro de float exatamente no plano onde caixa e malha se encontram.
            const float seam = 0.01f;
            ArmBox(root.transform, "Arm_N", Arm.North, r - seam);
            ArmBox(root.transform, "Arm_E", Arm.East,  r - seam);
            BuildCornerArc(root.transform, r);
            CornerChestAnchor(root.transform, new Vector3(1f, 0f, 1f));
            PillarAnchor(root.transform, new Vector3(1f, 0f, 1f), "_Inner");
            PillarAnchor(root.transform, new Vector3(-1f, 0f, -1f), "_Outer");

            Save(root);
        }

        /// <summary>
        /// Setor anular extrudado ligando o braço Norte ao Leste.
        ///
        /// O centro do arco fica em (r, r): é o único ponto equidistante `r` das duas linhas
        /// de centro dos braços (x=0 e z=0), o que faz a curva sair tangente aos dois — sem
        /// isso a parede teria uma quebra angular na emenda. O arco varre de 180° a 270°, o
        /// que coloca o ponto de tangência norte em (0, r) e o leste em (r, 0), casando com
        /// onde os braços retos param.
        ///
        /// Efeito colateral desejado: a curva se afasta da origem (o ponto mais próximo fica
        /// a 0.41·r do vértice, do lado do canto), então a quina CONVEXA — a que o jogador
        /// enxerga — é recuada e arredondada, em vez de bicuda.
        /// </summary>
        private void BuildCornerArc(Transform parent, float r)
        {
            int n = Mathf.Max(2, arcSegments);
            float ri = Mathf.Max(0f, r - T * 0.5f); // raio interno (lado côncavo)
            float ro = r + T * 0.5f;                // raio externo (lado que o jogador vê)

            var verts = new Vector3[(n + 1) * 4];
            var uvs = new Vector2[(n + 1) * 4];

            for (int k = 0; k <= n; k++)
            {
                float t = k / (float)n;
                float ang = Mathf.PI + t * Mathf.PI * 0.5f; // 180° → 270°
                float c = Mathf.Cos(ang), s = Mathf.Sin(ang);

                var inner = new Vector3(r + ri * c, 0f, r + ri * s);
                var outer = new Vector3(r + ro * c, 0f, r + ro * s);

                int b = k * 4;
                verts[b + 0] = inner;                            // Ib
                verts[b + 1] = inner + Vector3.up * H;           // It
                verts[b + 2] = outer;                            // Ob
                verts[b + 3] = outer + Vector3.up * H;           // Ot

                uvs[b + 0] = new Vector2(t, 0f);
                uvs[b + 1] = new Vector2(t, 1f);
                uvs[b + 2] = new Vector2(t, 0f);
                uvs[b + 3] = new Vector2(t, 1f);
            }

            // 3 superfícies × 2 triângulos × n segmentos. A face de baixo é omitida de
            // propósito: fica em y=0, coincidente com o topo do piso, e nunca é vista.
            var tris = new int[n * 3 * 2 * 3];
            int w = 0;
            for (int k = 0; k < n; k++)
            {
                int b = k * 4, x = (k + 1) * 4;

                // Externa (normal apontando pra FORA do centro do arco — o lado visível).
                tris[w++] = b + 2; tris[w++] = b + 3; tris[w++] = x + 2;
                tris[w++] = b + 3; tris[w++] = x + 3; tris[w++] = x + 2;

                // Interna (normal pro centro): winding invertido em relação à externa.
                tris[w++] = b + 0; tris[w++] = x + 0; tris[w++] = b + 1;
                tris[w++] = x + 0; tris[w++] = x + 1; tris[w++] = b + 1;

                // Topo.
                tris[w++] = b + 1; tris[w++] = x + 1; tris[w++] = b + 3;
                tris[w++] = x + 1; tris[w++] = x + 3; tris[w++] = b + 3;
            }

            var mesh = new Mesh { name = "Mesh_WallCornerArc" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // A malha precisa virar ASSET: malha criada em memória não sobrevive ao
            // SaveAsPrefabAsset — o prefab salvaria uma referência quebrada.
            string meshPath = $"{outputFolder}/Mesh_WallCornerArc.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject("Arc");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = matWall;
            // MeshCollider (não-convexo) porque a peça é estática: é ele que faz o
            // NavMeshSurface recortar a curva do caminho navegável, igual às paredes retas.
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // ------------------------------------------------------------ Utilitários
        private static GameObject Root(string name) => new GameObject(name);

        private GameObject Cube(string name, Vector3 pos, Vector3 size, Transform parent, Material mat = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // já vem com BoxCollider
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat != null ? mat : matWall;
            return go;
        }

        // Cylinder primitivo da Unity: diâmetro 1 e altura 2 (raio 0.5, meia-altura 1),
        // daí scale.x/z = raio*2 e scale.y = altura/2.
        private GameObject Cylinder(string name, Vector3 pos, float radius, float height, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = matWall;
            return go;
        }

        private void Save(GameObject root)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{outputFolder}/{root.name}.prefab");
            DestroyImmediate(root);
        }

        private Material MakeMat(string name, Color c)
        {
            string path = $"{outputFolder}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew)
            {
                // Funciona tanto em URP quanto em Built-in.
                //
                // Já existiu uma tentativa de shader custom aqui (World Aligned Lit, UV pela
                // posição no mundo em vez da malha, pra textura ficar contínua entre peças de
                // tamanho diferente sem tunar Tiling por peça) — revertida: sem erro nenhum no
                // Console, as paredes saíam transparentes e sem resposta a luz. Não valeu a pena
                // caçar às cegas sem conseguir compilar pra testar. Se for retomar essa ideia,
                // considere Shader Graph em vez de HLSL à mão — o Graph já gera o boilerplate de
                // luz/sombra certo pra versão de URP instalada, em vez de eu adivinhar API.
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            // Só aplica a cor-placeholder se NINGUÉM já botou textura nesse material — regerar
            // o kit não pode voltar a tingir de cinza por cima de uma textura de verdade que
            // você já arrastou pro Base Map na mão.
            bool hasTexture = mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null;
            if (!hasTexture)
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            }
            // Só no NASCIMENTO do material — igual _BaseColor acima, regerar o kit não pode
            // voltar a pisar num Smoothness que você já ajustou na mão. Fosco (0.05) é só o
            // chute inicial pro greybox: o Smoothness 0.5 padrão do URP reflete o skybox e faz
            // sala "escura" parecer ter luz vazando (ver GreyboxTileGenerator).
            if (isNew)
            {
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private void DeleteGenerated()
        {
            string[] names = { "Wall_End", "Wall_Straight", "Wall_Corner", "Wall_Tee", "Wall_Cross",
                               "Arm_Wall", "Arm_Wall_AtHub", "Arm_Door", "Arm_Door_AtHub", "Hub_End", "Hub_Junction",
                               "Tile_Floor_Flat", "Tile_Stairs", "Wildcard_SolidFill", "Wildcard_Air" };
            foreach (string n in names)
                AssetDatabase.DeleteAsset($"{outputFolder}/{n}.prefab");
            AssetDatabase.DeleteAsset($"{outputFolder}/Mesh_WallCornerArc.asset");
            AssetDatabase.DeleteAsset($"{outputFolder}/Mesh_Lintel.asset");
            AssetDatabase.Refresh();
            Debug.Log("[CenterWallTileGenerator] Peças removidas.");
        }
    }
}
