// ============================================================================
//  ModuleSocketGizmo.cs  —  camada WFC.Runtime
//
//  Solta este componente num GameObject vazio na cena, aponta um
//  ModuleDefinition e ele desenha as 6 faces do módulo com a cor do socket de
//  cada uma. É o jeito mais rápido de conferir "esta parede está com o socket
//  errado" sem abrir a tabela de adjacência.
//
//  Opcionalmente instancia o prefab do módulo junto, já rotacionado, para você
//  ver geometria e socket ao mesmo tempo.
// ============================================================================

using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Runtime
{
    [ExecuteAlways]
    [AddComponentMenu("WFC/Module Socket Gizmo (debug)")]
    public sealed class ModuleSocketGizmo : MonoBehaviour
    {
        public ModuleDefinition module;
        public SocketLibrary socketLibrary;

        [Tooltip("Qual rotação em Y visualizar (0..3 quartos de volta). Os sockets são permutados igual ao bake.")]
        [Range(0, 3)]
        public int rotationY;

        [Min(0.01f)] public float cellSize = 6f;
        [Min(0.01f)] public float cellHeight = 5f;

        [Tooltip("Instancia o prefab do módulo (já rotacionado) como filho, para ver a geometria.")]
        public bool showPrefab;

        private const string PreviewName = "__ModulePreview";

        [ContextMenu("Atualizar preview")]
        public void RefreshPreview()
        {
            Transform old = transform.Find(PreviewName);
            while (old != null)
            {
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
                old = transform.Find(PreviewName);
            }

            if (!showPrefab || module == null || module.prefab == null) return;

            var holder = new GameObject(PreviewName);
            holder.transform.SetParent(transform, false);
            holder.transform.localRotation = Quaternion.Euler(0f, 90f * rotationY, 0f);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.InstantiatePrefab(module.prefab, holder.transform);
                return;
            }
#endif
            Instantiate(module.prefab, holder.transform);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) RefreshPreview(); };
#endif
        }

        private void OnDrawGizmos()
        {
            if (module == null) return;

            Vector3 center = transform.position + Vector3.up * (cellHeight * 0.5f);
            float hx = cellSize * 0.5f;
            float hy = cellHeight * 0.5f;
            float hz = cellSize * 0.5f;

            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawWireCube(center, new Vector3(cellSize, cellHeight, cellSize));

            foreach (Direction dir in Directions.All)
            {
                FaceSocket face = TileSet.RotatedFace(module, rotationY, dir);
                Color color = socketLibrary != null
                    ? socketLibrary.GetDebugColor(dir, face.socket)
                    : Color.magenta;
                color.a = face.IsDefined ? 0.55f : 0.15f;

                Directions.Offset(dir, out int dx, out int dy, out int dz);
                Vector3 offset = new Vector3(dx * hx, dy * hy, dz * hz);

                // Placa fina colada na face correspondente.
                Vector3 plateSize = Directions.IsVertical(dir)
                    ? new Vector3(cellSize * 0.75f, 0.08f, cellSize * 0.75f)
                    : (dx != 0
                        ? new Vector3(0.08f, cellHeight * 0.6f, cellSize * 0.75f)
                        : new Vector3(cellSize * 0.75f, cellHeight * 0.6f, 0.08f));

                Gizmos.color = color;
                Gizmos.DrawCube(center + offset, plateSize);
            }
        }
    }
}
