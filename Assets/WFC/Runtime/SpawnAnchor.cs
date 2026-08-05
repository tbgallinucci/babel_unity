// ============================================================================
//  SpawnAnchor.cs  —  camada WFC.Runtime  (Decisão 4)
//
//  Marcador que vive nos PREFABS estruturais: "cabe inimigo aqui", "cabe baú
//  aqui", "prop de parede aqui". Um transform vazio com este componente.
//
//  O plugin de geração é agnóstico ao jogo: ele só COLETA os anchors do andar
//  instanciado e os expõe no FloorFillResult. Quem decide o que nasce em cada
//  anchor — encounter tables, balanceamento, loot — é o populador, que é um
//  sistema separado, específico do jogo, e que NUNCA é referenciado daqui.
// ============================================================================

using UnityEngine;

namespace WFC.Runtime
{
    public enum SpawnAnchorKind
    {
        Enemy = 0,
        Chest = 1,
        Prop = 2,
        WallProp = 3,
        Light = 4,
        PlayerStart = 5,
    }

    [DisallowMultipleComponent]
    public sealed class SpawnAnchor : MonoBehaviour
    {
        [Tooltip("Que tipo de coisa cabe aqui. O plugin não interpreta isto — só repassa.")]
        public SpawnAnchorKind kind = SpawnAnchorKind.Prop;

        [Tooltip("Peso/prioridade opcional, para o populador desempatar entre anchors.")]
        public float weight = 1f;

        [Tooltip("Raio livre exigido em volta, em metros. Serve para o populador evitar sobreposição.")]
        public float clearanceRadius = 0.5f;

        /// <summary>Preenchido pelo filler na hora de coletar: índice linear da célula que gerou este anchor.</summary>
        [HideInInspector] public int cellIndex = -1;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = kind switch
            {
                SpawnAnchorKind.Enemy       => new Color(1f, 0.3f, 0.3f, 0.8f),
                SpawnAnchorKind.Chest       => new Color(1f, 0.85f, 0.2f, 0.8f),
                SpawnAnchorKind.PlayerStart => new Color(0.3f, 1f, 0.4f, 0.8f),
                SpawnAnchorKind.Light       => new Color(1f, 1f, 0.6f, 0.8f),
                _                           => new Color(0.5f, 0.7f, 1f, 0.8f),
            };
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, clearanceRadius));
            Gizmos.DrawRay(transform.position, transform.up * 0.6f);
        }
    }
}
