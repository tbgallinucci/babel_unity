// ============================================================================
//  DynamicLightBudget.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  Decide, por distância ao jogador, em que TIER cada tocha do andar está:
//
//      Key   — acesa E projetando sombra. Pouquíssimas (2 por padrão).
//      Fill  — acesa, sem sombra. A maioria.
//      Off   — apagada.
//
//  Por que "tier" e não "ligado/desligado": em Forward+ (o renderer deste
//  projeto) o teto de luzes adicionais visíveis é 256, e o limite de 4 luzes
//  POR OBJETO não se aplica — o URP faz early-out nesse caminho. Ou seja: luz
//  sem sombra é barata e cabe MUITA. O que custa caro é (a) sombra, que ocupa
//  slice de shadow atlas e um pass de render por luz, e (b) volume de luz, que
//  em clustering se paga por tile/z-bin tocado. Então o orçamento certo não é
//  "quantas luzes", é "quantas SOMBRAS" — daí Key ser um número pequeno e fixo
//  e Fill poder ser generoso.
//
//  Indexa TorchLight, não Light. A versão anterior coletava todo Light sob o
//  andar e ordenava por distância; como cada tocha TEM DUAS luzes (Spot key +
//  Point fill), o corte do orçamento caía no meio de uma tocha e acendia meia
//  tocha. Ver o cabeçalho de TorchLight.cs.
//
//  Nada disso depende de bake (o andar é gerado em runtime) — é tudo decidido
//  por distância, a cada updateInterval.
// ============================================================================

using System.Collections.Generic;
using Babel.VFX;
using UnityEngine;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Dynamic Light Budget")]
    public sealed class DynamicLightBudget : MonoBehaviour
    {
        [Tooltip("Quem serve de referência de distância. Normalmente o Player.")]
        [SerializeField] private Transform viewer;

        [Tooltip("Quantas tochas projetam sombra ao mesmo tempo. Este é o número que realmente " +
                 "custa caro — cada uma é um slice de shadow atlas e um pass de render. 2 é o " +
                 "ponto de partida; passar de 4 raramente compensa.")]
        [Min(0)] [SerializeField] private int keyLights = 2;

        [Tooltip("Quantas tochas ficam acesas SEM sombra, além das Key. Barato em Forward+ " +
                 "(o teto do URP é 256 luzes visíveis), então pode ser generoso — é o que " +
                 "fecha os buracos escuros entre as poças de luz.")]
        [Min(0)] [SerializeField] private int fillLights = 14;

        [Tooltip("Tocha além desta distância fica apagada mesmo que sobre orçamento. " +
                 "Mantenha próximo do Range do Spot da tocha: manter acesa uma luz cuja " +
                 "sombra já morreu no fade por distância é exatamente o que produz " +
                 "'sala acesa sem fonte visível'. 0 = sem limite de distância.")]
        [Min(0f)] [SerializeField] private float activeRadius = 28f;

        [Tooltip("Nos últimos metros antes do corte a tocha desvanece em vez de apagar de uma " +
                 "vez. Um sumiço instantâneo chama muito mais atenção que a ausência da luz.")]
        [Min(0f)] [SerializeField] private float fadeDistance = 4f;

        [Tooltip("Zona morta, em metros, na fronteira do raio ativo. Sem isto, ficar parado " +
                 "em cima do limiar faz a tocha piscar a cada reavaliação.")]
        [Min(0f)] [SerializeField] private float radiusHysteresis = 2f;

        [Tooltip("Folga de POSIÇÃO na fila antes de rebaixar de tier. Sem isto, duas tochas " +
                 "quase equidistantes trocam de lugar na ordenação a cada avaliação e ficam " +
                 "alternando quem projeta sombra.")]
        [Min(0)] [SerializeField] private int rankHysteresis = 1;

        [Tooltip("Segundos entre reavaliações. Não precisa rodar todo frame — a tocha não muda " +
                 "de lugar, só o jogador. 0.2s é imperceptível e custa quase nada.")]
        [Min(0.02f)] [SerializeField] private float updateInterval = 0.2f;

        [SerializeField] private bool logSummary = true;

        private readonly List<TorchLight> tracked = new List<TorchLight>();
        private readonly List<(float sqrDist, TorchLight torch)> scratch = new List<(float, TorchLight)>();
        private float nextUpdate;
        private int lastKeyCount = -1;

        /// <summary>Quantas tochas estão acesas agora (Key + Fill) — para diagnóstico/HUD.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>Quantas estão projetando sombra agora. É este o número que custa caro.</summary>
        public int ShadowCastingCount { get; private set; }

        /// <summary>
        /// Recadastra as tochas a partir da raiz do andar. Chame depois que TODOS os
        /// populadores rodarem — tocha plantada depois deste ponto não entra no orçamento.
        /// </summary>
        public void Rebuild(Transform floorRoot, Transform viewerOverride = null)
        {
            if (viewerOverride != null) viewer = viewerOverride;

            tracked.Clear();
            if (floorRoot == null) return;

            floorRoot.GetComponentsInChildren(true, tracked);
            tracked.RemoveAll(t => t == null);

            // Começa tudo apagado: sem isto, a primeira avaliação encontraria as tochas no
            // estado do prefab (todas acesas, todas com sombra) e o primeiro frame do andar
            // sairia com dezenas de shadow casters — justamente o pico que o orçamento existe
            // para evitar, bem no frame em que o jogador ainda está carregando o andar.
            foreach (TorchLight torch in tracked)
                torch.Apply(TorchTier.Off, 0f);

            nextUpdate = 0f; // força uma avaliação imediata, sem esperar o intervalo
            lastKeyCount = -1;
        }

        public void Clear()
        {
            tracked.Clear();
            ActiveCount = 0;
            ShadowCastingCount = 0;
            lastKeyCount = -1;
        }

        private void LateUpdate()
        {
            if (viewer == null || tracked.Count == 0) return;
            if (Time.time < nextUpdate) return;
            nextUpdate = Time.time + updateInterval;

            Apply();
        }

        private void Apply()
        {
            Vector3 origin = viewer.position;

            scratch.Clear();
            for (int i = 0; i < tracked.Count; i++)
            {
                TorchLight torch = tracked[i];
                if (torch == null) continue;

                scratch.Add(((torch.Anchor.position - origin).sqrMagnitude, torch));
            }

            // Sort completo é O(n log n) sobre algumas dezenas de tochas, a cada updateInterval —
            // irrelevante perto de manter uma fila de prioridade só pra isso.
            scratch.Sort((a, b) => a.sqrDist.CompareTo(b.sqrDist));

            int active = 0;
            int shadows = 0;

            for (int rank = 0; rank < scratch.Count; rank++)
            {
                (float sqrDist, TorchLight torch) = scratch[rank];
                float dist = Mathf.Sqrt(sqrDist);

                TorchTier tier = DecideTier(torch, rank, dist);
                float dim = tier == TorchTier.Off ? 0f : FadeFactor(dist);

                torch.Apply(tier, dim);

                if (tier == TorchTier.Off) continue;
                active++;
                if (tier == TorchTier.Key) shadows++;
            }

            ActiveCount = active;
            ShadowCastingCount = shadows;

            if (logSummary && shadows != lastKeyCount)
            {
                lastKeyCount = shadows;
                Debug.Log($"[DynamicLightBudget] {active} tocha(s) acesa(s) de {tracked.Count}, " +
                          $"{shadows} com sombra.", this);
            }
        }

        /// <summary>
        /// Tier por posição na fila, com histerese nas DUAS fronteiras: promover é imediato,
        /// rebaixar exige folga. Assim uma tocha que já está projetando sombra não a perde só
        /// porque outra encostou meio metro mais perto do jogador.
        /// </summary>
        private TorchTier DecideTier(TorchLight torch, int rank, float dist)
        {
            if (!WithinRadius(torch, dist)) return TorchTier.Off;

            TorchTier current = torch.Tier;

            int keyLimit = current == TorchTier.Key ? keyLights + rankHysteresis : keyLights;
            if (rank < keyLimit) return TorchTier.Key;

            int fillLimit = current != TorchTier.Off
                ? keyLights + fillLights + rankHysteresis
                : keyLights + fillLights;

            return rank < fillLimit ? TorchTier.Fill : TorchTier.Off;
        }

        /// <summary>
        /// Zona morta no raio: quem já está aceso só apaga passando de `activeRadius`; quem
        /// está apagado só acende abaixo de `activeRadius - radiusHysteresis`. Sem os dois
        /// limiares separados, parar em cima da fronteira faz a tocha piscar.
        /// </summary>
        private bool WithinRadius(TorchLight torch, float dist)
        {
            if (activeRadius <= 0f) return true;

            float threshold = torch.Tier != TorchTier.Off
                ? activeRadius
                : activeRadius - radiusHysteresis;

            return dist <= threshold;
        }

        private float FadeFactor(float dist)
        {
            if (activeRadius <= 0f || fadeDistance <= 0f) return 1f;

            float fadeStart = activeRadius - fadeDistance;
            if (dist <= fadeStart) return 1f;

            return Mathf.Clamp01(1f - (dist - fadeStart) / fadeDistance);
        }
    }
}
