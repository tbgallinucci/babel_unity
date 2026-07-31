using UnityEngine;

namespace Babel.Enemies
{
    // Disco visual do telegraph do jump attack — só executa o progresso que
    // EnemyBase manda, sem lógica de tempo própria (mesmo "burro por
    // design" que KnockbackReceiver/HitFlash já seguem: quem orquestra
    // timing é sempre o consumidor, o componente só reage).
    //
    // Assume que o mesh (Quad deitado — rotação -90 em X — ou Cylinder
    // achatado no Y) tem diâmetro 1 em escala 1, que é o padrão dos
    // primitivos do Unity — por isso escala localScale.x/z direto como
    // diâmetro em unidades de mundo, sem fator de conversão.
    public class JumpAttackTelegraph : MonoBehaviour
    {
        private float targetRadius;

        private void Awake()
        {
            // Some no boot — instanciado uma vez por EnemyBase e reusado a
            // cada jump attack (ver Show/Hide), não um objeto de vida curta.
            Hide();
        }

        public void Show(Vector3 center, float radius)
        {
            targetRadius = radius;
            transform.position = center;
            gameObject.SetActive(true);
            SetProgress01(0f);
        }

        // t em [0,1] — 0 = ponto no centro, 1 = diâmetro total (2x o raio).
        // EnemyBase chama isso todo frame durante o arco do pulo,
        // sincronizado com o mesmo normalizedTime que dirige a posição.
        public void SetProgress01(float t)
        {
            float diameter = Mathf.Clamp01(t) * targetRadius * 2f;
            transform.localScale = new Vector3(diameter, transform.localScale.y, diameter);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
