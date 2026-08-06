NOME: torch_wall_stratum1_01
CATEGORIA: prop (fonte de luz diegética)
ESTRATO: I — Fundação (Babilônia de Nabucodonosor, em declínio)
DIMENSÕES REAIS (m): 0.9 altura x 0.35 largura x 0.45 projeção a partir da face da parede
PALETA: ouro açafrão embaçado (#F4C430), ouro brilhante só no lambido da chama (#FFD700), madeira envelhecida (#3E2E22), sombra de barro (#7A5C3E), poeira (#C9A876), laranja de chama (#FF9A3C)
PASTA DE DESTINO FINAL (Unity): Assets/Art/Props/
ESTILO DE MODELAGEM SUGERIDO: hard-surface geométrico (Rota A — script procedural bpy)

## Descrição do objeto

Suporte de tocha fixado na parede — **a fonte de luz principal do Estrato I**.
Substitui/atualiza o antigo greybox do Godot `torch_metal_lit.tscn`.

- Placa de montagem: quadrada, ~0.25 × 0.25 m, metal dourado fosco, com uma roseta rasa.
- Braço: curto, inclinado para cima e para fora, projetando 0.45 m da parede.
- Bacia: rasa, no fim do braço.
- Cabo: toco de madeira envelhecida enrolado em pano velho.
- Chama: no topo, à frente e acima da parede.
- Sujeira: fuligem manchando a parede acima da chama (isso vai na textura da tocha, como
  um decalque na placa — não pintar na parede).

## ⚠ Nota técnica crítica — pivô é a FACE DE MONTAGEM

**Este é o único asset do lote que NÃO usa pivô na base.**

Pivô: **centro da face traseira da placa de montagem** — X centrado, Y no centro vertical
da placa, **Z = 0 no plano que encosta na parede**.

Motivo: a tocha é posicionada colando-a numa superfície vertical. Com pivô na base, toda
instância exigiria compensar altura *e* profundidade à mão; com pivô na face de montagem,
basta encostar na parede e escolher a altura. Isso vale principalmente porque a tocha vai
ser instanciada **muitas vezes por andar**, e no futuro possivelmente por script.

Toda a geometria fica em **Z positivo** (para fora da parede) e a chama em **Y positivo**.

## ⚠ Nota técnica crítica — a luz faz parte do prefab

O `.glb` é só a geometria. O Prefab `Assets/Prefabs/Props/torch_wall_stratum1_01.prefab` **precisa**
conter, além da malha:

1. Uma **`Light` (`Type = Point`)** posicionada no centro da chama (não na base do asset):
   - cor quente ~`#FF9A3C`, `light_energy` ~2.0, `omni_range` **8–12 m**
   - `Shadow Type = Soft Shadows` (é a sombra dela que faz a sala parecer um lugar)
2. Um **material emissivo** na malha da chama (shader Lit com **Emission** ligada, cor `#FFB347`) — sem
   isso a chama fica um triângulo cinza no meio da luz.
3. **Flicker sutil** — uma pequena oscilação de `light_energy`. Pode ficar para uma
   segunda passada; a luz estática já entrega o essencial.

> ⚠️ Ponto de coordenação: o item **"Sistema de áudio do zero"** do backlog prevê SFX de
> fogueira/tocha. Quando isso existir, o `AudioStreamPlayer3D` entra **neste mesmo prefab**.
> Vale avisar o Thiago que este prefab vai ser o ponto de encontro dos dois sistemas.

## Notas técnicas

- **Chanfro** 0.03–0.05 m (peça pequena — chanfro grande come a forma).
- **Orçamento de polígonos:** ~150–250 tris.
- **Colisão:** **nenhuma.** É decorativa e fica a 2+ m do chão; colisão aqui só cria
  chance de o jogador prender o dash numa parede. O GameObject raiz do Prefab não leva
  **nenhum componente de Collider** — nem no raiz, nem nos filhos.
- **Fora do Layer `Level` (coletada pelo `NavMeshSurface`)** (não deve influenciar o navmesh).
- **Material:** ~~M3 (ouro fosco)~~ → **metal escuro fosco (ferro)** + M5 (madeira, textura
  `Assets/Art/Textures/weathered_brown_planks_diff_4k.jpg`) + material emissivo da chama.

> ✅ **Decidido por Gustavo em 2026-08-04:** *"não tem por que uma tocha usar ouro"*.
> O suporte passa a ser **ferro fosco escuro**, não M3. Faz sentido de mundo: tocha é
> objeto utilitário, feito para queimar e ser trocado — não peça cerimonial. O ouro fica
> onde tem função de ornamento (friso, rosetas, bacia do braseiro).
>
> Isso também **elimina metade da pendência do M3**: a tocha deixa de precisar dele.

## Checklist de validação

- [ ] Pivô na **face traseira da placa** (não na base), geometria toda em Z+
- [ ] Chama acima e à frente da parede, não colada nela
- [ ] `Light` (`Type = Point`) no centro da chama, range 8–12 m, sombra ligada
- [ ] Material da chama é emissivo
- [ ] Sem colisão, fora da Layer `Level`
- [ ] Silhueta legível de longe numa sala escura
- [ ] Sem corrente / lanterna de vidro / vela
- [ ] ≤ 250 tris

## Medidas verificadas (Blender 5.1, 2026-07-30)

Rodando `04_gerar_modelo.py` headless:

> 170 tris | 0.300 (X) x 0.420 (projecao) x 0.905 (Z) m | pivo na face de montagem OK (geometria toda em Y <= 0). Levemente menor que os 0.35 x 0.45 x 0.9 da ficha — diferenca visualmente irrelevante, nao vale reajustar.
