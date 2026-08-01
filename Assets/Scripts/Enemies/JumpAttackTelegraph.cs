using UnityEngine;

namespace Babel.Enemies
{
    // Disco visual do telegraph do jump attack — só executa o progresso que
    // EnemyBase manda, sem lógica de tempo própria (mesmo "burro por
    // design" que KnockbackReceiver/HitFlash já seguem: quem orquestra
    // timing é sempre o consumidor, o componente só reage).
    //
    // Assume que o mesh ocupa 1 unidade no plano do chão em escala 1 (é o
    // caso do Quad e do Cylinder do Unity), então o progresso vira
    // diâmetro em unidades de mundo direto, sem fator de conversão.
    public class JumpAttackTelegraph : MonoBehaviour
    {
        // O ponto de pouso vem do NavMesh, ou seja, exatamente no nível do
        // piso — e um disco coplanar com o chão z-fighta e simplesmente
        // some. Levanta alguns centímetros pra ficar por cima.
        [SerializeField] private float groundOffset = 0.05f;

        private float targetRadius;
        // Escala autorada no prefab, capturada uma vez. O progresso
        // multiplica ELA em vez de montar um Vector3 na mão eixo a eixo —
        // assim funciona igual pra Quad (deitado por rotação, o plano é
        // X/Y local) e pra Cylinder (o plano é X/Z local), sem o código
        // precisar saber qual primitivo está lá. A versão anterior
        // assumia semântica de Cylinder e travava a profundidade em 1 num
        // Quad, o que virava uma tira em vez de um disco crescendo.
        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;

            // Some no boot — instanciado uma vez por EnemyBase e reusado a
            // cada jump attack (ver Show/Hide), não um objeto de vida curta.
            Hide();
        }

        public void Show(Vector3 center, float radius)
        {
            targetRadius = radius;
            transform.position = center + Vector3.up * groundOffset;
            gameObject.SetActive(true);
            SetProgress01(0f);
        }

        // t em [0,1] — 0 = ponto no centro, 1 = diâmetro total (2x o raio).
        // EnemyBase chama isso todo frame durante o arco do pulo,
        // sincronizado com o mesmo normalizedTime que dirige a posição.
        public void SetProgress01(float t)
        {
            float diameter = Mathf.Clamp01(t) * targetRadius * 2f;
            transform.localScale = baseScale * diameter;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
