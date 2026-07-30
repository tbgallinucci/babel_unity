using UnityEngine;

namespace Babel.Combat
{
    // Congela (ou desacelera) o jogo por alguns milissegundos no instante do
    // acerto — o "hit-stop" clássico de action game. É o que dá peso ao golpe
    // sem precisar de nenhum VFX: o impacto lê como impacto porque o tempo
    // para, não porque apareceu uma partícula.
    //
    // Equivalente ao Engine.time_scale que o projeto Godot de referência já
    // usava (ver o glossário do guia de migração).
    //
    // Mora no mesmo GameObject do PlayerAttackHitbox por enquanto — só o
    // player causa hit hoje. Quando inimigos também precisarem disparar
    // hit-stop, isso vira um singleton/serviço; a API pública (Trigger) não
    // muda com isso.
    public class HitStop : MonoBehaviour
    {
        // 0 = congelamento total. Valores pequenos (0.05-0.15) dão um
        // "slow-mo" curtíssimo em vez de trava seca — questão de gosto,
        // testável ao vivo.
        [SerializeField, Range(0f, 1f)] private float timeScaleDuringHit = 0f;

        private float remaining;
        private float scaleBeforeHit = 1f;
        private bool active;

        public void Trigger(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            if (!active)
            {
                // Só captura a escala original na PRIMEIRA trava — senão um
                // segundo hit durante o congelamento guardaria 0 como
                // "normal" e o jogo nunca voltaria à velocidade.
                scaleBeforeHit = Time.timeScale;
                active = true;
            }

            // Hits em sequência renovam a janela em vez de somar — sem isso
            // um combo de 3 acertos rápidos empilharia num congelamento
            // longo e o controle ficaria pastoso.
            remaining = Mathf.Max(remaining, duration);
            Time.timeScale = timeScaleDuringHit;
        }

        private void Update()
        {
            if (!active)
            {
                return;
            }

            // unscaledDeltaTime é obrigatório: com timeScale em 0 o
            // Time.deltaTime também é 0, e o contador nunca andaria — o jogo
            // congelaria pra sempre.
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                Restore();
            }
        }

        // Rede de segurança: se este componente/GameObject for desativado (ou
        // o Play Mode parar) no meio de uma trava, a timeScale global ficaria
        // presa em 0 — inclusive vazando pro Editor.
        private void OnDisable()
        {
            if (active)
            {
                Restore();
            }
        }

        private void Restore()
        {
            Time.timeScale = scaleBeforeHit;
            remaining = 0f;
            active = false;
        }
    }
}
