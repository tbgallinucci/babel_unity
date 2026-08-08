# Sessão de refino da iluminação

Protocolo para uma sessão local de ajuste, não uma referência de arquitetura —
essa já existe em
[Como Funciona a Iluminacao do Loop.md](Como%20Funciona%20a%20Iluminacao%20do%20Loop.md).
Aqui é "em que ordem mexer, em quê, e como saber se melhorou".

**Alvo:** sombrio, mas legível. Fora do alcance das tochas continua escuro, mas
nunca preto absoluto — dá pra ler silhueta de parede, porta e inimigo. A tocha
continua sendo o evento, não a base.

**Escopo desta sessão:** só a escuridão. Orçamento de luz, shadow atlas e sombra
de Fill ficam para outra sessão (ver Apêndice B).

---

## Contexto

A cena de jogo lê como escura demais. O pedido original era "implementar SSAO e
iluminação ambiente", mas o levantamento mostrou outra coisa:

- **SSAO já está implementado e ativo** — `Assets/Settings/PC_Renderer.asset:69`,
  renderer feature `ScreenSpaceAmbientOcclusion` (Intensity 0.7, Radius 0.6,
  Downsample on, Source DepthNormals, DirectLightingStrength 0.25). Não há o que
  implementar.
- **SSAO escurece por definição** — ele não pode ser a solução para escuridão.
- **E hoje ele está quase invisível**, pelo mesmo motivo da escuridão: o SSAO da
  URP multiplica principalmente o termo de ambiente/GI. Com ambiente perto de
  zero não há o que ocluir. Subir o ambiente é o que faz o SSAO aparecer — por
  isso ele é o **último** passo do plano, não o primeiro.

As causas reais da escuridão, em ordem de impacto:

| # | Causa | Onde | Estado atual |
|---|---|---|---|
| 1 | **`FloorLightField` praticamente desligado** | `Loop_Scene.unity`, objeto `Runtime_Gen` | `intensityMultiplier: 0.001` — **50× abaixo do default do próprio script (0.05)** e 20–80× abaixo da faixa que o tooltip recomenda (0.02–0.08). A Etapa 3 (o "ambiente quente entre as tochas") está efetivamente desligada. Ainda: `rangeMultiplier: 0.5`, metade do alcance. |
| 2 | **Sem tonemapping** | `Assets/Settings/DefaultVolumeProfile.asset` | `Tonemapping mode: 0` (None) — HDR clipa direto: o miolo da tocha estoura pra branco e todo o resto lê como preto. O `Global Volume` da cena, que teria Neutral, está **desativado** (`m_IsActive: 0`). |
| 3 | **Ambient Flat quase preto** | `Loop_Scene`, Lighting → Environment | `m_AmbientMode: 3` (Flat), cor linear `0.018 / 0.020 / 0.026` |
| 4 | **Fog engolindo o fundo** | mesma cena | ExpSquared, densidade `0.012`, cor **mais escura que o ambiente** — é isso que cria parede preta a ~40 m |

> **Onde a cena de jogo mora:** `Assets/Scenes/Loop Scene/Loop_Scene.unity`,
> GameObject **`Runtime_Gen`** (tem `FloorDirector`, `FloorLightField`,
> `DynamicLightBudget`, `BasicLightingPopulator`, `RegionLightMask`).
> A `Generated_Level_Scene` é cena de teste — afinar nela não muda o jogo.

---

## Antes de começar (5 min)

- [ ] **Confirmar o quality level.** `Project Settings → Quality`. São dois
      renderers e só um tem SSAO:
      - nível `0 = Mobile` → `Mobile_RPAsset` → `Mobile_Renderer` →
        `m_RendererFeatures: []`, **sem SSAO nenhum**
      - nível `1 = PC` → `PC_RPAsset` → `PC_Renderer` → com SSAO

      `Standalone: 1`, então build de desktop pega o PC. Mas se o build target
      do Editor estiver em Android ou WebGL (ambos mapeiam pro `0`), você está
      no `Mobile_Renderer` e **nada que você editar no `PC_Renderer.asset` tem
      efeito**.

- [ ] **Abrir a `Loop_Scene`** e entrar em Play Mode até um andar gerar.

- [ ] **Tirar duas capturas de referência**, sempre dos mesmos dois pontos:
      1. uma sala **sem tocha acesa por perto** (o pior caso de legibilidade)
      2. um corredor com uma tocha no fim (o caso de contraste)

      Sem esse par, cada passo vai parecer "melhorou um pouco" e você não vai
      conseguir julgar o acumulado.

- [ ] **Não use o bloco "Prefab da tocha"** da janela Etapa 1 nesta sessão —
      está quebrado (ver Apêndice A).

### Regra da sessão

**Uma variável por vez, comparando com a captura de referência.** É o que o
comentário de preset do próprio `LightingSetupWindow.cs:122` já diz: o erro da
sessão anterior foi mudar oito coisas juntas. Os passos abaixo estão em ordem de
dependência — cada um muda como o seguinte é percebido.

---

## Passo 1 — Tonemapping Neutral

**Por que primeiro:** sem isso você calibra tudo o resto para compensar um
clipping que ia sumir. É um campo só.

**Onde:** `Project Settings → Graphics` → seção Volume → **Default Volume
Profile**, ou selecionar `Assets/Settings/DefaultVolumeProfile.asset` no Project
e editar no Inspector.

| Campo | De | Para |
|---|---|---|
| Tonemapping → Mode | `None` | **`Neutral`** |

**O que esperar:** a chama e o miolo da tocha param de virar um borrão branco
chapado e voltam a ter gradiente. A cena não fica mais clara — fica com mais
faixa útil. Se parecer que "ficou mais escuro", é o clipping sumindo; siga.

**Alternativa que não recomendo:** reativar o `Global Volume` na cena
(`m_IsActive: 0`) também traria Neutral, porque o `SampleSceneProfile` já tem
`mode: 1`. Mas esse profile carrega junto **Bloom, Vignette e Motion Blur** — e
a Vignette escurece justamente as bordas da tela, o oposto do objetivo desta
sessão. Editar o `DefaultVolumeProfile` é cirúrgico.

**Rollback:** voltar Mode para `None`.

---

## Passo 2 — `FloorLightField` (o maior ganho isolado)

**Onde:** Hierarchy → **`Runtime_Gen`** → componente *Floor Light Field*.
Tem `OnValidate` (`FloorLightField.cs:147`) — **recalcula ao vivo, inclusive em
Play Mode**, sem regenerar o andar. Afine com o jogo rodando.

| Campo | Atual | Primeiro alvo | Faixa para explorar |
|---|---|---|---|
| `intensityMultiplier` | `0.001` | **`0.03`** | 0.02 – 0.08 |
| `rangeMultiplier` | `0.5` | **`1.0`** | 0.75 – 1.25 |
| `colorSaturation` | `0.5` | manter | 0.4 – 0.6 |

**Ordem dentro do passo:** suba `intensityMultiplier` sozinho até a sala sem
tocha ficar legível. **Só depois** mexa em `rangeMultiplier` — ele muda até onde
o bounce chega, não o quanto ele vale, e mexer nos dois juntos torna impossível
saber qual causou o quê.

**O que vigiar:** se a cena inteira começar a tender pro laranja monocromático,
o problema é `colorSaturation`, não intensidade — baixe para 0.4 antes de
desistir da intensidade. Se aparecer luz vazando através de parede, troque
`textureFilterMode` para `Point` para diagnosticar (o tooltip em
`FloorLightField.cs:55` explica o teste).

**Rollback:** `intensityMultiplier` de volta para `0.001`.

---

## Passo 3 — Ambient: Flat → Gradient

**Por que Gradient e não só clarear o Flat:** Flat aplica a mesma cor por todos
os ângulos de normal e **chapa a cena** — é parte do motivo de tudo parecer sem
volume. Gradient dá céu/horizonte/chão separados: teto mais claro, chão mais
escuro e quente, e a verticalidade volta de graça.

**Onde:** `Window → Rendering → Lighting` → aba **Environment** →
*Environment Lighting* → Source: **`Gradient`**.

> **Atenção:** o projeto é Linear (`m_ActiveColorSpace: 1`). O número que você vê
> no `.unity` **não** é o que você digita no picker. Use os hexes:

| | hex (é isso que você digita) | linear (é isso que vai pro arquivo) |
|---|---|---|
| *(ambient atual, Flat)* | `#24272D` | 0.018 / 0.020 / 0.026 |
| **Sky** | **`#383C46`** | 0.040 / 0.045 / 0.062 |
| **Equator** | **`#2F3035`** | 0.028 / 0.030 / 0.036 |
| **Ground** | **`#24221F`** | 0.018 / 0.016 / 0.014 |

Isso é ~1,5× o ambiente atual no horizonte e ~2,3× no teto — deliberadamente
contido, porque o alvo é *sombrio mas legível*. O chão fica no mesmo nível de
hoje, só levemente quente (pedra devolvendo a luz da tocha).

**Se ainda estiver escuro demais:** suba os três juntos em passos de ~15%,
mantendo a proporção entre eles. Se subir só o Equator, você perde a
verticalidade que o Gradient acabou de comprar.

**Nota:** o campo *Intensity Multiplier* da janela de Lighting **só funciona no
modo Skybox** — no Gradient/Flat ele é ignorado. A intensidade está nas cores.

**Rollback:** Source de volta para `Color` com `#24272D`.

---

## Passo 4 — Fog

**Onde:** mesma janela → **Environment** → *Other Settings* → Fog.

| Campo | Atual | Alvo |
|---|---|---|
| Density | `0.012` | **`0.007`** |
| Color | `#1D1E24` (linear 0.012/0.013/0.018) | **`#2A2C33`** |

**A regra que está sendo violada hoje:** a cor do fog está **mais escura que o
ambiente**. Fog mais escuro que o ambiente é uma parede preta se fechando com a
distância. Mantenha a cor do fog **igual ou um pouco acima** do Equator do
Passo 3 — aí a distância vira névoa, não vazio.

**Rollback:** Density `0.012`, Color `#1D1E24`.

---

## Passo 5 — SSAO (agora, não antes)

Só agora existe ambiente para o SSAO ocluir. Antes deste ponto qualquer
julgamento sobre ele é inválido.

**Onde:** Project → `Assets/Settings/PC_Renderer.asset` → Inspector →
*Renderer Features* → **Screen Space Ambient Occlusion**. (A janela
`Babel → Iluminação → Configurar iluminação (Etapa 1)` também expõe Radius,
Intensity e Downsample — o bloco de SSAO dela funciona; só o de tocha não.)

**Ordem:** olhe primeiro **sem mexer em nada**. Com o ambiente dos passos 2–4,
o AO que já está lá (Intensity 0.7 / Radius 0.6) provavelmente aparece pela
primeira vez. Ajuste só o que incomodar:

| Sintoma | Campo | Direção |
|---|---|---|
| Cantos não assentam, geometria "flutuando" | `Intensity` | 0.7 → 0.9 |
| AO some em parede grande (célula de 6 m) | `Radius` | 0.6 → 0.8 |
| Halo/borda suja em volta de objeto | `Downsample` | ligado → desligado (custa mais) |

**Cuidado:** `DirectLightingStrength` (0.25) tira AO da **luz direta**, não do
ambiente. Subir isso escurece justamente as áreas iluminadas pela tocha — é o
contrário do objetivo da sessão. Deixe onde está.

---

## Verificação

Rode com o jogo em Play Mode na `Loop_Scene`, e compare com o par de capturas do
início.

- [ ] **Sala sem tocha por perto** — dá pra ler onde é parede, onde é porta e
      onde tem inimigo? É esse o critério de "legível". Não precisa estar
      confortável, precisa estar decifrável.
- [ ] **Corredor com tocha no fim** — a tocha ainda é claramente o ponto mais
      brilhante do quadro? Se a base subiu tanto que a tocha não se destaca
      mais, você passou do alvo: volte o Passo 3 em ~15%.
- [ ] **Distância** — o fundo do corredor longo vira névoa, não vira parede
      preta?
- [ ] **Atravessar 3–4 andares** pela escada, verificando que nada regrediu na
      transição (o `FloorLightField.Rebuild` roda por andar,
      `FloorDirector.cs:210`).
- [ ] **Console limpo** — em especial, se aparecer
      `Reduced additional punctual light shadows resolution...`, isso é o shadow
      atlas estourando. **Não é desta sessão** (Apêndice B), mas anote se
      apareceu, porque muda o julgamento de qualidade de sombra.
- [ ] **Salvar:** `File → Save` (cena) **e** `File → Save Project` (os `.asset`
      do renderer e do volume profile só vão pro disco no Save Project).

---

## Apêndice A — Armadilha conhecida: janela Etapa 1

`Assets/Scripts/Editor/LightingSetupWindow.cs:31` aponta para
`Assets/Prefabs/Props/TestTorchLight.prefab`, que **não existe**. O prefab real
é `Assets/Prefabs/Props/TorchPrefab.prefab`.

Consequência: se você clicar **Aplicar** com o bloco "Prefab da tocha" marcado,
ele loga `[LightingSetup] Prefab não encontrado:` e **não aplica nada da tocha**
— mas os outros três blocos aplicam normalmente, então é fácil concluir que a
tocha foi ajustada quando não foi.

**Nesta sessão:** desmarque o bloco "Prefab da tocha". A correção da constante é
de uma linha, mas é mudança de código — fica para quando formos mexer em tocha.

## Apêndice B — Fora do escopo (próxima sessão)

Levantado durante o diagnóstico, não tocar agora:

- **`DynamicLightBudget` em `keyLights: 10` / `fillLights: 50`** contra os 2/14
  que a documentação descreve como padrão. 10 tochas com sombra simultâneas num
  atlas 2048 provavelmente força a URP a reduzir resolução por luz — é o aviso
  do Console citado na verificação.
- **Sombra do Fill no prefab.** `TorchPrefab.prefab` tem o Point (`Point Light`)
  com `m_Shadows.m_Type: 2` (Soft). Em Play Mode isso é anulado
  (`shadowCasterMode: 0` = KeyOnly força `LightShadows.None` em
  `TorchLight.cs:231`), então **não afeta o jogo** — mas afeta o que você vê
  fora do Play Mode e no `ArtPreviewPopulator`, que de propósito não roda o
  orçamento. Pode confundir julgamento em Scene View.
- **`AmbientLighting.cs`** — componente `[ExecuteAlways]` para virar fonte única
  da verdade de ambiente + fog, com tuning ao vivo e variação por profundidade
  de andar. Só vale escrever **depois** que esta sessão decidir os valores bons;
  senão é chute com mais passos.
