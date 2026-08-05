// ============================================================================
//  NavMeshBuilderService.cs  —  camada WFC.Runtime  (Decisão 2)
//
//  Uma NavMeshSurface por andar, bakeada do zero a cada transição. Nada de
//  incremental, nada de costura entre andares: cada andar é uma geração isolada
//  e a escada é só um gatilho que dispara a próxima.
//
//  Pré-bake por módulo + NavMeshLinks continua guardado como otimização FUTURA,
//  e só se o profiler acusar hitch — é um pesadelo de alinhamento.
//
//  A superfície é criada na RAIZ do andar gerado, com collectObjects = Children:
//  assim ela enxerga exatamente os tiles daquele andar e nada mais da cena.
// ============================================================================

using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace WFC.Runtime
{
    public static class NavMeshBuilderService
    {
        /// <summary>
        /// Garante uma NavMeshSurface na raiz do andar, configurada para enxergar só
        /// os filhos dela.
        /// </summary>
        public static NavMeshSurface EnsureSurface(Transform floorRoot, int agentTypeId = 0)
        {
            if (floorRoot == null) return null;

            NavMeshSurface surface = floorRoot.GetComponent<NavMeshSurface>();
            if (surface == null) surface = floorRoot.gameObject.AddComponent<NavMeshSurface>();

            surface.agentTypeID = agentTypeId;
            surface.collectObjects = CollectObjects.Children;

            // Os prefabs greybox vêm com BoxCollider limpo; bakear por colisor evita
            // depender de malha de render (que a arte final pode ter em excesso).
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            return surface;
        }

        /// <summary>Bake síncrono. Simples, mas segura a thread principal — use na edição/teste.</summary>
        public static void Bake(NavMeshSurface surface)
        {
            if (surface == null) return;
            surface.BuildNavMesh();
        }

        /// <summary>
        /// Bake assíncrono, para rodar durante a tela de loading da transição de andar.
        /// A coleta da geometria ainda acontece na main thread; o que sai dela é a
        /// construção em si.
        /// </summary>
        public static IEnumerator BakeAsync(NavMeshSurface surface)
        {
            if (surface == null) yield break;

            // Primeiro bake: não há NavMeshData para atualizar, então UpdateNavMesh
            // precisa de uma instância. BuildNavMesh cria e registra tudo de uma vez.
            if (surface.navMeshData == null)
            {
                surface.BuildNavMesh();
                yield break;
            }

            AsyncOperation op = surface.UpdateNavMesh(surface.navMeshData);
            while (op != null && !op.isDone) yield return null;
        }

        /// <summary>Descarrega a NavMesh do andar. Chamar antes de destruir a raiz.</summary>
        public static void Release(NavMeshSurface surface)
        {
            if (surface == null) return;
            surface.RemoveData();
            surface.navMeshData = null;
        }

        /// <summary>
        /// Ajusta um ponto para cima da NavMesh. Devolve <c>false</c> se não houver
        /// NavMesh perto — é assim que se evita spawnar inimigo dentro da parede.
        /// </summary>
        public static bool TrySnapToNavMesh(Vector3 position, float maxDistance, out Vector3 result, int areaMask = NavMesh.AllAreas)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, areaMask))
            {
                result = hit.position;
                return true;
            }

            result = position;
            return false;
        }
    }
}
