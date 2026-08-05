# Plugin de Geração Procedural 3D (Wave Function Collapse) para Unity — Prompt de Projeto

> **Como usar:** cole este documento inteiro como sua primeira mensagem numa conversa nova com o Claude. Ele contém todo o contexto e as decisões de arquitetura já tomadas. No fim há a instrução do que começar a construir primeiro.

---

## Contexto

Estou desenvolvendo um **Action Roguelite 3D em Unity** e quero construir, do zero e com a sua ajuda, um **plugin próprio de Geração Procedural 3D** para os andares de uma torre, usando o algoritmo **Wave Function Collapse (WFC)**.

Requisitos do jogo:
- A geração roda em **runtime**, na transição entre andares (tenho uma tela de loading a favor).
- Cada andar é uma geração nova e isolada (a escada para o andar de cima é apenas um gatilho que dispara a próxima geração — não há NavMesh contínuo entre níveis).
- Precisa integrar com o **NavMesh** da Unity e instanciar prefabs 3D de forma performática.
- Precisa **garantir jogabilidade**: sempre existir caminho válido entre entrada, salas de combate e saída/escada. Andar sem saída é inaceitável.

**Ambiente:** Unity **6000.5.5f1** (Unity 6). Posso usar o pacote **AI Navigation** (`com.unity.ai.navigation`, `NavMeshSurface`), `InstantiateAsync`, `Awaitable`, e Jobs/Burst se necessário.

Quero código limpo, comentado **em português**, com separação clara de responsabilidades e testável.

---

## Princípio-mestre da arquitetura

**Separar o Core do WFC (C# puro, sem `UnityEngine`) da camada Unity** (MonoBehaviours, ScriptableObjects, instanciação). Isso garante testabilidade, permite rodar o solver de forma assíncrona e mantém o algoritmo limpo. Nunca acoplar o solver a GameObjects.

Estrutura em 4 camadas com `.asmdef` separados:
- `WFC.Core` — C# puro, zero `UnityEngine`.
- `WFC.Data` — ScriptableObjects.
- `WFC.Runtime` — MonoBehaviours, instanciação, NavMesh.
- `WFC.Editor` — tooling de inspector/gizmos.

---

## Decisões de arquitetura JÁ TRAVADAS (não reabrir sem eu pedir)

### DECISÃO 1 — Conectividade: abordagem HÍBRIDA (esqueleto conexo → WFC)
WFC puro garante apenas regras **locais** de adjacência; não garante caminho global. Retry cego em runtime pode causar freezes de vários segundos. Portanto:

1. Um **gerador de esqueleto determinístico (com seed)** cria primeiro um grafo/layout **garantidamente conexo** (ex.: Entrada → Corredor → Sala de Combate → Escada). Ele **não desenha paredes**; apenas marca cada célula do grid com um **papel** (`Entrada`, `Corredor`, `Combate`, `Escada`, `Vazio`) e crava **pré-constraints de porta** nas conexões entre salas.
2. O **WFC recebe o grid já anotado** e resolve apenas a estética/geometria 3D, **respeitando** essas âncoras. O WFC nunca pode cortar um caminho que o esqueleto prometeu.
3. Jogabilidade é garantida **por construção**, não por sorte. Em regiões puramente decorativas fora do caminho crítico, o WFC pode ficar mais solto.

**Contrato explícito** entre as camadas: esqueleto → grid anotado com papéis + portas obrigatórias → WFC preenche.

### DECISÃO 2 — NavMesh: `NavMeshSurface.BuildNavMesh()` assíncrono por andar
- Começar simples: instanciar os prefabs → rodar `NavMeshSurface` **assíncrono** durante a tela de loading. Uma `NavMeshSurface` por andar, bakeada do zero a cada transição (nada de incremental, nada de costura entre andares).
- **NÃO** começar com pré-bake de NavMesh por módulo + stitching + `NavMeshLinks` (pesadelo de alinhamento). Isso fica guardado como otimização **futura**, só se o profiler acusar hitch.

### DECISÃO 3 — Módulos coringa (válvula de escape contra contradições)
- Criar **dois** módulos coringa de **peso baixíssimo**: `SolidFill` (bloco maciço, conecta com quase tudo pelas laterais) e `Air/Empty` (espaço vazio). São válvula de escape, **não** a opção padrão — peso baixo para não deixar o andar "mush".
- Manter teto de tentativas + **fallback para layout garantido** como defesa em profundidade (seguro que quase nunca é acionado).

### DECISÃO 4 — Populador (inimigos, loot, interagíveis) é um SISTEMA SEPARADO, fora do plugin
O plugin de geração é **agnóstico ao jogo**: ele NÃO sabe o que é um inimigo, um baú ou o balanceamento do roguelite. Colocar inimigo/loot como módulo WFC é usar a ferramenta errada — isso é design de gameplay dirigido, não adjacência emergente.

- O plugin gera **apenas o casco geométrico + o mapa de papéis + o NavMesh**. Sua responsabilidade termina aí.
- O **populador** (encounter tables, spawn de inimigos, loot, interagíveis) é um sistema **separado, específico do seu jogo** — na prática um assembly próprio (`Game.Population.asmdef`) ou parte do código de gameplay, **não** um plugin distribuível.
- **Dependência de mão única, obrigatória:** o populador conhece e consome a saída do gerador; o gerador **nunca** referencia o populador.

**Contrato de saída que o plugin DEVE expor** (para o populador consumir):
1. **Mapa de papéis por célula** — o resultado do `SkeletonGenerator` (`Entrada`, `Corredor`, `Combate`, `Escada`, `Vazio`…), acessível após a geração.
2. **Spawn anchors** — os prefabs estruturais carregam marcadores (componente `SpawnAnchor` em transforms vazios: "cabe inimigo aqui", "cabe baú aqui", "prop de parede aqui"). O populador só posiciona coisas nesses anchors, respeitando o papel da célula.
3. Referência ao andar instanciado (raiz da hierarquia) e ao `NavMeshSurface` bakeado.

O passe de população em si fica **fora do escopo deste plugin** — é fase/sistema posterior que consome o contrato acima.

### DECISÃO 5 — Preenchimento atrás de uma interface `IFloorFiller` (WFC é o motor primário)
O passo que transforma o grid anotado (papéis + portas) em geometria 3D instanciada fica **atrás de uma interface trocável**, `IFloorFiller`. Assim o esqueleto, o contrato de saída, o pooling, o NavMesh e o populador **não sabem** qual estratégia de preenchimento está em uso, e trocar/adicionar estratégias não quebra nada.

```csharp
public interface IFloorFiller {
    // Recebe o grid já anotado (papéis + portas) e devolve as instâncias + anchors.
    FloorFillResult Fill(AnnotatedGrid grid, IRandom rng);
}
```

Implementações da interface:

- **Caminho A — `WFCFiller` (implementação PRIMÁRIA, é o objetivo do projeto).** É o WFC "puro" descrito nas seções 1 e 2: solver com bitsets, sockets, adjacência, rotações automáticas, `ModuleDefinition`/`TileSet`. Consome o grid anotado respeitando as âncoras do esqueleto (Decisão 1). Dá geometria orgânica e variedade máxima. Custo: autorar **prefabs compostos por célula** a partir do kit (piso + as paredes daquela célula), com sockets. **É o que construímos primeiro.**
- **Caminho B — `EdgeWallFiller` (alternativa OPCIONAL / fallback, futura).** Não usa WFC: instancia piso nas células de chão e coloca parede/porta nas bordas, usando o kit como vem (sem sockets, sem compostos). Mais simples e rápido, porém menos orgânico. Fica disponível como implementação alternativa da mesma interface, caso você queira um fallback leve ou um modo mais controlado — **não é o foco agora.**

**A interface protege os dois lados:** esqueleto, papéis/RoomRoles, contrato de saída, pooling, instanciação, NavMesh e populador são compartilhados e independem de qual filler está ativo. Trocar/adicionar um filler não toca no resto.

### DECISÃO 6 — Escala: célula = TILE pequeno, sala = aglomerado de células
A **célula do grid NÃO é uma sala** — é um **tile pequeno** do mundo 3D (uma parede, um piso, uma porta), do tamanho do módulo do kit de arte (tipicamente 2–4 m). Uma **sala é uma região de várias células** (ex.: um bloco de 5×5 células ≈ 20×20 m). O `SkeletonGenerator` trabalha na **escala grossa** (grafo de salas): decide onde ficam entrada/salas/escada como *blocos* de células, carimba o **mesmo papel em todas as células do bloco** e marca as células de borda que viram porta. O filler trabalha na **escala fina** (célula a célula).

- **Kit de arte pretendido:** *Lite Dungeon Pack (Low Poly) by Gridness* (Unity Asset Store) — peças modulares inteiras (parede, piso, porta, canto) em grid. Num kit assim a **parede fica na borda entre células** e o **piso ocupa o interior**. Para o WFC (Caminho A), isso significa **montar prefabs compostos por célula** (piso + as paredes/portas daquela célula) e definir os sockets neles — é o passo de autoria do tileset. (O Caminho B usaria as peças soltas direto nas bordas, mas não é o foco.)
- **`Cell Size` = tamanho do módulo do kit.** Descobrir medindo os *bounds* de uma peça de piso no Inspector do Unity (provavelmente 2 ou 4 m) e usar esse valor. Todas as peças devem ter o mesmo footprint e o mesmo pivô.

---

## 1. Arquitetura de classes

### Camada Core (`WFC.Core`, C# puro)
- `WFCSolver` — loop observe (menor entropia) → collapse → propagate (fila de propagação). RNG **seedável** injetado (`IRandom`). Recebe pré-constraints.
- `Cell` — domínio como **bitset (`ulong[]`)**: 1 bit por módulo (propagação = `AND` de bitsets, ordens de magnitude mais rápido que listas).
- `Grid3D` — dimensões, indexação linear, vizinhança de 6 direções.
- `AdjacencyTable` — para cada `(módulo, direção)`, o bitset dos vizinhos permitidos.
- `enum Direction { PosX, NegX, PosY, NegY, PosZ, NegZ }`
- `Constraint`, `SolveResult`.
- **Testes unitários** cobrindo observe/collapse/propagate e reprodutibilidade por seed.

### Camada Data (`WFC.Data`, ScriptableObjects)

**Apparatus do WFC (Caminho A — modelo primário):**
- `ModuleDefinition`: `prefab` (um composto por célula), `SocketId[6]` (uma por face), `weight`, `bool[4] allowedYRotations` (0/90/180/270), `ModuleTag tags`.
- `SocketLibrary`: sockets horizontais (flag de simetria + pares flipped) e verticais (índice de rotação).
- `TileSet`: lista de módulos + **bake em edit-time** (expande variantes rotacionadas + gera `AdjacencyTable`, botão "Rebuild adjacency"). Nunca bakear em runtime.
- `FloorSpec` + `RoomRole`: papéis autorados por você + regras de composição do andar que o `SkeletonGenerator` usa (Decisão 1/6).

**Opcional — só se você adicionar o Caminho B (`EdgeWallFiller`) depois:**
- `FloorTheme` (ou `PieceSet`): por "slot" estrutural (piso, parede, porta, canto…), uma lista de variantes de prefab + pesos, **sem sockets**. Não é necessário para o WFC.

**`ModuleTag` (v1) — `[Flags] enum`, uma peça carrega várias:**
```csharp
[System.Flags]
public enum ModuleTag {
    None     = 0,
    Blocking = 1 << 0,   // bloqueia nav/conectividade (ausência = passável)
    Floor    = 1 << 1,
    Wall     = 1 << 2,
    Doorway  = 1 << 3,
    Corner   = 1 << 4,
    Entrance = 1 << 5,
    Stairs   = 1 << 6,
    Empty    = 1 << 7,
    Wildcard = 1 << 8,   // marca peças válvula-de-escape (SolidFill/Air), p/ debug
}
```
Tags classificam a **peça** (a célula é classificada pelo `RoomRole`). O FloorSpec liga os dois: "célula com `Role_Stairs` só aceita peças com tag `Stairs`". Fora da v1: tags de gameplay (`Combat`, `Treasure`) — isso é `RoomRole`; e tags decorativas finas — isso é população.

### Camada Runtime (`WFC.Runtime`, MonoBehaviours)
- `WFCFloorGenerator`: orquestra `GenerateAsync(FloorSpec)` → `SkeletonGenerator` → `IFloorFiller` (injetado; B ou A) → NavMesh. Ao final, expõe o **contrato de saída** (mapa de papéis + spawn anchors + raiz do andar + `NavMeshSurface`) para consumo do populador externo (ver Decisão 4).
- `SpawnAnchor` (componente marcador): fica nos prefabs estruturais indicando pontos de posicionamento válidos (`Enemy`, `Chest`, `Prop`…). O plugin apenas os coleta e expõe; **não** os usa.
- `PrefabInstancer`: **object pooling** (reaproveita GameObjects entre andares); instanciação distribuída por frames / `InstantiateAsync`.
- `NavMeshBuilderService`: `NavMeshSurface` async por andar.
- `ConnectivityValidator`: BFS/flood-fill no grafo de portas (defesa em profundidade / validação).
- `SkeletonGenerator`: gera o grid anotado conexo (Decisão 1).
- `IFloorFiller` (interface, Decisão 5) + implementação primária `WFCFiller` (Caminho A) e a alternativa opcional `EdgeWallFiller` (Caminho B, futura). O `WFCFloorGenerator` recebe um `IFloorFiller` injetado e **não sabe** qual é.

### Camada Editor (`WFC.Editor`)
- `CustomEditor` de `ModuleDefinition` com **preview de sockets como Gizmos coloridos** nas 6 faces.
- Botão "Auto-detect sockets" (por bounding-box/geometria de contato).
- Botão "Rebuild adjacency" no `TileSet`.

---

## 2. Sockets 3D e rotações automáticas — *(Caminho A / WFC; não usado no Caminho B)*

- Cada módulo tem **6 faces**, cada face carrega um **socket ID**. Duas células adjacentes só coexistem se os sockets das faces que se tocam forem compatíveis.
- **Convenção (padrão Oskar Stålberg / DeBroglie):**
  - **Horizontais (X/Z):** socket assimétrico `"3"` conecta com sua versão espelhada `"3F"` (flipped), não consigo mesmo. Simétricos (sufixo `s`, ex. `"2s"`) conectam consigo mesmos.
  - **Verticais (Y):** precisam de **índice de rotação** (`v0..v3`) ou flag "invariante" (`i`) para simetria rotacional.
- **Rotações automáticas em Y** (evita modelar 4 prefabs por peça): no bake do `TileSet`, para cada módulo com `allowedYRotations`, gerar variantes girando 90°. Girar em Y **permuta ciclicamente as faces horizontais** (`PosX→PosZ→NegX→NegZ→PosX`) e mantém os sockets remapeados; faces PosY/NegY incrementam o índice de rotação vertical. Cada variante vira um "módulo" próprio no solver com `rotationY` guardado; na instanciação aplica-se `Quaternion.Euler(0, 90*rot, 0)`.
- Definir `ModuleTag` distinguindo peças com passagem (`Door`, `Corridor`) de sólidas (`Wall`, `SolidFill`) — validador e NavMesh dependem disso.

---

## 3. Performance (metas e táticas)

- **Solve em background** via `Awaitable`/`Task` (Core é C# puro). ⚠️ Instanciação de GameObjects e bake de NavMesh **só no main thread** — padrão: solve em background → voltar ao main thread → instanciar.
- **Bitsets** na propagação; pré-alocar arrays; evitar LINQ/GC no hot path.
- **Object pooling** de prefabs; espalhar instanciação por frames; considerar `InstantiateAsync`.
- **Chunking** do andar se o grid crescer demais.
- RNG seedável para reprodutibilidade e possível pré-geração.
- Teto de tentativas + fallback para layout garantido se houver contradição repetida (raro com os coringas).
- Esperado: a parte de *algoritmo* some no orçamento de uma tela de loading; o que se cronometra de fato é **instanciação + NavMesh**.

---

## 4. Tileset mínimo, greybox e assets (autoria manual, em paralelo ao código)

> Comece em **greybox** (cubos primitivos / ProBuilder), sem asset de arte. Valida o algoritmo inteiro sem gastar tempo com modelagem. Depois é só trocar o `prefab` de cada `ModuleDefinition` pela peça bonita — o plugin não percebe a diferença.

### As 9 peças mínimas (conjunto completo)
Pensar cada célula por "quais das 4 bordas têm parede". Cada peça é auto-rotacionada pelo plugin (modela-se **uma** orientação, ganham-se as demais):

1. `Tile_Floor_Open` — 0 paredes (interior de sala).
2. `Tile_Wall` — 1 parede (borda). ×4 rot
3. `Tile_Corner` — 2 paredes adjacentes, em L (quina). ×4 rot
4. `Tile_Corridor` — 2 paredes opostas (corredor). ×2 rot
5. `Tile_DeadEnd` — 3 paredes (beco/nicho). ×4 rot
6. `Tile_Door` — variante da parede reta com abertura (passagens marcadas pelo esqueleto). ×4 rot
7. `Tile_Stairs` — peça de saída (tag `Stairs`). ×4 rot
8. `Wildcard_SolidFill` — cubo maciço (coringa, peso 0.1).
9. `Wildcard_Air` — vazio, sem malha (coringa, peso 0.1).

Junções em T e cruzamentos de 4 vias **surgem sozinhos** da adjacência — não precisa modelá-los.

### Convenção obrigatória (vale para TODAS as peças)
- **Footprint = `Cell Size`** (ex.: 4×4 no plano). **Altura de parede** `H` à sua escolha (ex.: 3–4 m).
- **Pivô no CENTRO da célula, no nível do piso (y=0).** Assim a rotação de 90° em Y mantém a peça alinhada.
- **Mesmo footprint e mesmo pivô em todas** — senão o grid não alinha.

### Receita de greybox por peça (Unity)
Com `S` = Cell Size, `H` = altura, `t` = espessura de parede (~0.2). A célula vai de `-S/2` a `+S/2` em X e Z.

1. GameObject vazio, nome da peça, transform zerado `(0,0,0)` → é a raiz/pivô.
2. **Piso:** Cube filho, escala `(S, 0.1, S)`, posição `(0, -0.05, 0)` (topo em y=0).
3. **Parede numa borda** (adicionar conforme a peça pedir):
   - +Z: pos `(0, H/2, S/2 - t/2)`, escala `(S, H, t)`
   - −Z: pos `(0, H/2, -S/2 + t/2)`, escala `(S, H, t)`
   - +X: pos `(S/2 - t/2, H/2, 0)`, escala `(t, H, S)`
   - −X: pos `(-S/2 + t/2, H/2, 0)`, escala `(t, H, S)`
4. Arraste a raiz para a pasta do projeto → vira prefab. Repita para as 9 peças.

Arranjo de parede por peça (modele só UMA orientação; o resto é rotação automática):
- **Floor_Open:** só piso.
- **Wall:** piso + parede +Z.
- **Corner:** piso + paredes +Z e +X.
- **Corridor:** piso + paredes +Z e −Z.
- **DeadEnd:** piso + paredes +Z, +X e −X.
- **Door:** piso + parede +Z **dividida em duas metades com um vão no meio** (dois cubes de largura ~`S/3` deixando o centro aberto).
- **Stairs:** piso + uma rampa (Cube esticado e inclinado) ou degraus; para greybox, uma rampa basta. Tag `Stairs`.
- **SolidFill:** um Cube maciço `(S, H, S)` em `(0, H/2, 0)`.
- **Air:** GameObject vazio, sem malha e sem collider.

Dica: dê **materiais de cor distinta** por tipo (piso cinza, parede escura, porta destacada, escada azul) só para enxergar o greybox.

### Collider
- **Sim — o collider vai direto no prefab do tile.** Cubos primitivos do Unity já vêm com **BoxCollider**, então no greybox você ganha de graça. Mantenha `BoxCollider` (barato); evite `MeshCollider`.
- Paredes e `SolidFill` **precisam** de collider (bloqueiam o player). O piso precisa de collider se o player anda por física/CharacterController.
- `Air` **não** leva collider nem malha.
- NavMesh: o `NavMeshSurface` pode bakear a partir de *Render Meshes* ou *Physics Colliders* (campo "Use Geometry"). Com colliders limpos nos prefabs, qualquer um dos dois funciona.

### Ferramenta de bootstrap: `GreyboxTileGenerator.cs` (já existe)
Um script de Editor gera os 9 prefabs greybox **automaticamente**, para não montá-los à mão. Vai em `Assets/WFC/Editor/`; no menu **WFC ▸ Greybox Tile Generator** abre uma janela com `Cell Size` / `Wall Height` / `Wall Thickness` / `Door ratio` ajustáveis e um botão que cria os prefabs (cubos + BoxCollider + 3 materiais greybox) em `Assets/WFC/GreyboxTiles/`. Idempotente (regerar sobrescreve) e compatível com URP e Built-in.

- Gera **só a geometria** (a casca visual + colliders). **Não** cria sockets, `ModuleDefinition` nem `TileSet` — isso é a fase de código.
- Fluxo: rodar o gerador → ter os 9 prefabs → na fase de código, os `ModuleDefinition` apontam para esses prefabs.
- Se algum tile sair com proporção estranha, ajustar os números na janela (ou no script) e regenerar.

### Assets gratuitos (para quando trocar o greybox por arte)
- **KayKit — Dungeon Pack Remastered** (itch.io): versão free, licença **CC0** (uso comercial, sem atribuição), com paredes/pisos/portas/escadas + props. Melhor alinhado ao caso.
- **Kenney — Modular Dungeon Kit / Mini Dungeon** (kenney.nl): **CC0**, grátis, modular em grid.
- **Quaternius** (quaternius.com): kits modulares **CC0** de dungeon/ruínas.
- O pack pago *Gridness Lite Dungeon* também serve; a escolha é de estilo/orçamento, não técnica.

---

## Plano de ação em fases (construir incrementalmente, cada fase testável)

> O **WFC é o motor primário** (Decisão 5). A ordem prova o algoritmo primeiro (2D → 3D), depois o costura ao esqueleto híbrido e ao mundo Unity.

- **Fase 0 — Core do WFC provado em 2D:** `WFCSolver` (observe menor-entropia → collapse → propagate), `Cell` com bitset (`ulong[]`), `Grid`, `AdjacencyTable`, `IRandom` seedável, `Constraint`, `SolveResult`. Renderizar num grid 2D com tiles de teste (gizmos). **Testes unitários** de observe/collapse/propagate + reprodutibilidade por seed. Blinda o algoritmo antes do 3D.
- **Fase 1 — Core 3D + sockets + rotações + dados:** generalizar para 6 direções; `ModuleDefinition`/`SocketLibrary`/`TileSet`; bake de adjacência + rotações automáticas em Y (seção 2); os dois coringas (Decisão 3). Tileset mínimo em **greybox** (as 9 peças da seção 4).
- **Fase 2 — Híbrido: esqueleto + `WFCFiller`:** `Grid3D`/`AnnotatedGrid` (células com `RoomRole` + portas), `SkeletonGenerator` determinístico (grafo de salas conexo → papéis nos *blocos* → portas, Decisão 1/6), interface `IFloorFiller` e sua primeira implementação `WFCFiller` (solver respeitando as pré-constraints do esqueleto). `FloorSpec`/`RoomRole`. Teste de conectividade (BFS entrada→escada).
- **Fase 3 — Spawner + instanciação + tooling:** `PrefabInstancer` com pooling + `InstantiateAsync`/spread por frames; custom editor com **gizmos de socket** + "Rebuild adjacency"; gizmos de debug do grid (papéis, portas). Autorar os primeiros **prefabs compostos por célula** a partir do kit Gridness. Primeiro andar 3D real na tela.
- **Fase 4 — NavMesh + jogabilidade garantida:** `NavMeshSurface` async por andar (Decisão 2); `ConnectivityValidator` (BFS) + retry/fallback (Decisão 3). Validar que o agente vai da entrada à escada.
- **Fase 5 — Contrato de saída + populador (stub externo):** expor mapa de papéis + spawn anchors + raiz + `NavMeshSurface`; populador de teste simples **no código do jogo** (fora do plugin, Decisão 4) só para validar o contrato.
- **Fase 6 — Performance + polish + empacotamento:** solve em background (Decisão 2); chunking se o grid crescer; profiling; pesos/tuning; temas por andar (troca de `TileSet`); `.asmdef` separando Core/Data/Runtime/Editor; samples e docs.

**Opcional/futuro — Caminho B (`EdgeWallFiller`):** implementação alternativa da mesma `IFloorFiller`, sem WFC (planta + paredes nas bordas, kit como vem). Útil como fallback leve. Reaproveita toda a fundação; não é o foco.

Referência de convenções (não como dependência, só para "roubar" boas ideias de sockets/rotação): biblioteca **DeBroglie** (open-source, C#).

---

## O que quero que você faça AGORA

Vamos começar pelo **Core do WFC (Fases 0 e 1)** — é o coração do plugin e o objetivo do projeto. Escreva o scaffold real, já com as decisões acima embutidas:
- `WFCSolver` (observe/collapse/propagate), `Cell` com **bitset (`ulong[]`)**, `Grid`/`Grid3D`, `AdjacencyTable`, `IRandom` seedável, `Constraint`, `SolveResult`;
- provar primeiro em **2D** (grid de teste + gizmos) com **testes unitários** (observe/collapse/propagate + reprodutibilidade por seed);
- os ScriptableObjects `ModuleDefinition`, `SocketLibrary`, `TileSet` com bake de adjacência + rotações automáticas em Y;
- deixar a interface `IFloorFiller` definida (o `WFCFiller` será a implementação que pluga o solver nas fases seguintes);
- os `.asmdef` separando Core/Data/Runtime/Editor.

Tudo comentado em português. A costura com o esqueleto híbrido (`SkeletonGenerator`), o Spawner, o NavMesh e o contrato de saída vêm nas fases seguintes — mas já deixe a arquitetura preparada para eles.

Obs.: os 9 prefabs greybox já são gerados por um script de bootstrap (`GreyboxTileGenerator.cs`, ver seção 4) — os `ModuleDefinition` do tileset inicial devem apontar para eles, não é preciso modelar nada à mão nesta etapa.

Antes de escrever, me faça as perguntas de setup que faltarem (ex.: `Cell Size` = tamanho do módulo do kit Gridness — dá para medir nos bounds da peça; tamanho do grid alvo por andar; número aproximado de módulos/compostos no tileset inicial). Depois comece a produzir os arquivos.
