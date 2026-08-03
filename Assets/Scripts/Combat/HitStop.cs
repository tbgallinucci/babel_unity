using UnityEngine;

namespace Babel.Combat
{
    // Congela (ou desacelera) o MUNDO por alguns milissegundos no instante do
    // acerto — o "hit-stop" clássico de action game. É o que dá peso ao golpe
    // sem precisar de nenhum VFX: o impacto lê como impacto porque o tempo
    // para, não porque apareceu uma partícula.
    //
    // Equivalente ao Engine.time_scale que o projeto Godot de referência já
    // usava (ver o glossário do guia de migração).
    //
    // Divisão de trabalho com o HitStopReceiver: aqui é o lado GLOBAL (uma
    // timeScale, uma janela, um dono), lá é o lado POR PERSONAGEM (quanto a
    // animação de cada envolvido ainda avança e como o corpo treme). Parar o
    // mundo pela timeScale é o que faz movimento, gravidade, NavMeshAgent,
    // knockback e os OUTROS inimigos pararem juntos de graça, sem nenhum
    // desses sistemas precisar saber que hit-stop existe.
    //
    // Continua sendo MonoBehaviour (e não um static puro) porque a tuning da
    // timeScale é pra ser mexida ao vivo no Inspector. O acesso virou estático
    // via Active: quem dispara hoje não é só o player (EnemyAttackHitbox
    // também trava a tela ao acertar), e obrigar cada hitbox a ter o próprio
    // HitStop daria duas timeScales brigando pela mesma variável global.
    // Basta UM na cena — segue morando no mesmo GameObject do
    // PlayerAttackHitbox.
    public class HitStop : MonoBehaviour
    {
        // Frames de 60fps são a unidade em que hit-stop é autorado na
        // indústria (e no vídeo do Sakurai), não segundos. Converter num lugar
        // só evita "0.083s" espalhado por Inspector nenhum sabendo que aquilo
        // eram 5 frames.
        public const float ReferenceFps = 60f;

        // 0 = congelamento total. Valores pequenos (0.05-0.15) dão um
        // "slow-mo" curtíssimo em vez de trava seca — questão de gosto,
        // testável ao vivo. As técnicas 6 e 7 funcionam nos dois casos: o
        // Animator dos dois envolvidos roda em UnscaledTime durante a janela,
        // então ignora este valor (ver HitStopReceiver).
        [SerializeField, Range(0f, 1f)] private float timeScaleDuringHit = 0f;

        private float remaining;
        private float scaleBeforeHit = 1f;
        private bool active;

        // Null se ninguém na cena tiver o componente — nesse caso o hit-stop
        // global simplesmente não acontece e o resto (tremida, animação em
        // câmera lenta) continua funcionando. Degradação silenciosa de
        // propósito: é feedback cosmético, não deve derrubar o combate.
        public static HitStop Active { get; private set; }

        // TÉCNICA 5 do vídeo do Sakurai: a duração da trava escala com a força
        // do golpe. Um tapinha e um golpe carregado com a MESMA trava é o erro
        // mais comum — some a hierarquia inteira dos ataques.
        //
        // Mesma forma da fórmula do Smash Ultimate (base + dano * fator, com
        // teto): lá é ~(0.65 * dano + 6) frames. Os coeficientes daqui são bem
        // mais tímidos porque a escala de dano deste projeto é outra — quem
        // manda são os campos dos hitboxes, isto aqui só faz a conta.
        public static float DurationFromDamage(float damage, float baseFrames,
            float framesPerDamage, float maxFrames, float multiplier)
        {
            float frames = (baseFrames + Mathf.Max(0f, damage) * framesPerDamage) * Mathf.Max(0f, multiplier);
            return Mathf.Clamp(frames, 0f, maxFrames) / ReferenceFps;
        }

        // Ponto de entrada único de um acerto: trava o mundo e avisa os dois
        // envolvidos. Cada parâmetro é opcional (um alvo sem HitStopReceiver
        // só não treme) — mesma tolerância que HealthComponent/
        // KnockbackReceiver já têm nos hitboxes.
        public static void Apply(HitStopReceiver attacker, HitStopReceiver victim, float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            if (Active != null)
            {
                Active.Trigger(duration);
            }

            if (attacker != null)
            {
                attacker.FreezeAsAttacker(duration);
            }

            if (victim != null)
            {
                victim.FreezeAsVictim(duration);
            }
        }

        private void OnEnable()
        {
            // Último a habilitar ganha, sem log de erro: dois HitStop na cena
            // é erro de montagem, mas derrubar o combate por causa disso seria
            // pior que o sintoma (a timeScale de um só ia sobrescrever a do
            // outro de qualquer jeito).
            Active = this;
        }

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

            if (Active == this)
            {
                Active = null;
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
