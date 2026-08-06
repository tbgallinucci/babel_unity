// ============================================================================
//  SceneTeleportInteractable.cs  —  código do JOGO
//
//  Interagível genérico que troca de CENA (não de andar — isso é a Stairs).
//  Primeiro uso: um portal na Hub Scene que manda o jogador pra Loop Scene,
//  onde o FloorDirector.generateOnStart cuida de gerar o andar 1 sozinho.
//
//  Troca a cena inteira (SceneManager.LoadScene, modo Single): a Hub some da
//  memória e a Loop nasce do zero, com o Player e o FloorDirector que já
//  existem NELA — não é o mesmo Player da Hub sendo carregado junto. Se um dia
//  precisar levar estado (vida, equipamento) de uma cena pra outra, é aqui que
//  entra um singleton persistente; por ora não existe nenhum.
// ============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Scene Teleport Interactable")]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class SceneTeleportInteractable : MonoBehaviour, IInteractable
    {
        [Tooltip("Nome da cena de destino, exatamente como aparece em File > Build Settings " +
                 "(sem a extensão .unity). A cena PRECISA estar na lista de Build Settings — " +
                 "SceneManager.LoadScene por nome não acha uma cena que só existe em disco.")]
        [SerializeField] private string targetSceneName = "Loop_Scene";

        [SerializeField] private string prompt = "Entrar";

        // Trava na hora, mesmo padrão do StairsInteractable: segurar o botão perto do
        // portal não pode disparar duas cargas de cena sobrepostas.
        private bool used;

        public string Prompt => used ? "..." : prompt;

        public Transform Transform => transform;

        public bool CanInteract(GameObject interactor) => !used && !string.IsNullOrEmpty(targetSceneName);

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            used = true;
            SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 1.2f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        }
    }
}
