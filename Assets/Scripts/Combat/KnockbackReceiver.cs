using UnityEngine;

namespace Babel.Combat
{
    // Empurrão físico genérico, desacoplado de HealthComponent de propósito
    // (dano e push não dependem um do outro — algo pode empurrar sem
    // ferir, ou ter vida sem ser empurrável). Pensado pro Rigidbody alvo
    // ficar Is Kinematic (é a convenção que EnemyDummy também vai usar,
    // "só trigger, sem física de verdade nele"), então o deslocamento é
    // inteiramente por código via MovePosition, não Rigidbody.AddForce —
    // mesmo princípio do movimento forçado por código usado em
    // PlayerController.OnAnimatorMove(), só que aqui é
    // Rigidbody+FixedUpdate em vez de CharacterController+Update, já que
    // não existe CharacterController nesse alvo.
    [RequireComponent(typeof(Rigidbody))]
    public class KnockbackReceiver : MonoBehaviour
    {
        [SerializeField] private float knockbackDuration = 0.2f;
        // Gravidade do arco de LAUNCH (ver ApplyLaunch). Própria, e não a
        // Physics.gravity, porque o alvo é kinematic — física nenhuma age nele,
        // o arco é integrado à mão aqui. Ter o número separado também deixa
        // afinar "peso" de juggle sem mexer na gravidade do mundo, que é o que
        // se quer na prática: launcher bom costuma usar gravidade MAIOR que a
        // real, pra subir rápido e cair rápido sem ficar flutuando.
        //
        // 18 era baixo demais e o sintoma foi relatado em teste: "o monstro
        // sobe em velocidade constante, perdeu a sensação de launch". A
        // desaceleração É o launch — é ela que comunica que algo jogou o
        // inimigo pra cima em vez dele estar sendo carregado. Com gravidade
        // baixa a velocidade cai devagar demais pra leitura acontecer dentro
        // do tempo do arco.
        //
        // Mesma lição que a gravity do PlayerController (9.81 -> 30): num arco
        // balístico, quem dá PESO é a gravidade, não a velocidade inicial.
        // Subir a força sem subir a gravidade só deixa tudo mais flutuante e
        // mais alto.
        [SerializeField] private float launchGravity = 40f;
        // Quanto tempo o alvo fica PARADO no ápice do arco de launch antes de
        // começar a cair. É o "hangtime" clássico de juggle: sem ele o ápice é
        // um instante matemático (a velocidade cruza zero e já inverte), e o
        // alvo nunca parece flutuar — só sobe e desce.
        //
        // Vale só pro ápice e só uma vez por launch: relançar reinicia a
        // contagem, mas um mesmo arco não pendura duas vezes. Diferente do
        // ApplyAirHold, que é disparado por golpe (cada acerto aéreo segura o
        // alvo onde ele estiver) — este é automático e faz parte do arco.
        //
        // Aumenta o tempo total de voo em exatamente este valor, então mexer
        // aqui afrouxa a janela do juggle sem mudar a ALTURA (ao contrário de
        // subir launchUpwardForce, que muda as duas coisas de uma vez).
        [SerializeField] private float apexHangTime = 0.5f;

        private Rigidbody rb;
        private Vector3 knockbackVelocity;
        private float remaining;
        // Duração do push ATUAL, não o campo serializado: um launch dura o
        // tempo de voo do arco (calculado a partir da força), não os 0.2s fixos
        // do empurrão normal.
        private float currentDuration;
        private float verticalVelocity;
        private float groundY;
        private bool launching;
        // Tempo restante de "pendurado no ar" (ver ApplyAirHold). Enquanto
        // maior que zero, o arco congela na altura atual em vez de integrar
        // gravidade.
        private float hangRemaining;
        // Trava do hang de ápice (ver apexHangTime): garante uma pendurada só
        // por arco. Sem isso, qualquer flutuação numérica em torno de
        // velocidade zero durante o próprio hang re-disparia o efeito e o alvo
        // ficaria preso no ar pra sempre.
        private bool apexHangUsed;

        // Pra quem mais tenta escrever transform.position no mesmo objeto
        // (ex.: EnemyBase dirigindo um NavMeshAgent) saber quando ceder o
        // controle em vez de brigar por ele. Fica genérico de propósito —
        // não sabe nada de NavMeshAgent, só expõe "ainda tenho um push
        // rolando"; a coordenação é responsabilidade de quem consome isso.
        public bool IsActive => remaining > 0f;

        // Mais específico que o IsActive: "está VOANDO", não só "tem um push
        // rolando". Um empurrão normal no chão também deixa IsActive true, e
        // quem precisa saber disso (a IA, pra se desligar) só se importa com o
        // caso aéreo — durante um push de chão o inimigo pode continuar
        // pensando normalmente.
        public bool IsAirborne => launching;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void ApplyKnockback(Vector3 direction, float force)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || force <= 0f)
            {
                return;
            }

            // Sobrescreve em vez de somar — um push no meio de outro em
            // andamento simplesmente reinicia com a nova direção/força, sem
            // stacking. Suficiente pra essa primeira passada.
            knockbackVelocity = direction.normalized * force;

            // Alvo ainda NO AR (launcher em andamento): o push só redireciona o
            // horizontal e o arco continua. Cair no reset abaixo zeraria a
            // velocidade vertical e travaria ele congelado na altura em que
            // estava, sem nunca voltar pro chão — e esse é justamente o caso
            // mais comum depois de um launcher: emendar o resto do combo.
            if (launching)
            {
                return;
            }

            currentDuration = knockbackDuration;
            remaining = currentDuration;
            verticalVelocity = 0f;
        }

        // Empurrão que também JOGA PRA CIMA — o launcher de juggle. Separado do
        // ApplyKnockback e não um parâmetro opcional dele porque o
        // comportamento é outro, não só um vetor a mais:
        //
        // - a duração deixa de ser knockbackDuration e passa a ser o tempo de
        //   voo do arco, derivado da própria força (senão o alvo "pousaria" no
        //   meio do ar quando o timer acabasse, ou ficaria parado no chão
        //   esperando o timer);
        // - o horizontal NÃO decai. No push normal o decaimento linear é o que
        //   faz o alvo escorregar e parar; no ar isso lê como freada mágica no
        //   meio do voo. Sem arrasto, o arco fica parabólico de verdade.
        //
        // upwardForce <= 0 cai de volta no empurrão normal, então quem chama
        // não precisa decidir qual dos dois usar.
        public void ApplyLaunch(Vector3 direction, float force, float upwardForce)
        {
            if (upwardForce <= 0f)
            {
                ApplyKnockback(direction, force);
                return;
            }

            direction.y = 0f;
            // Ao contrário do ApplyKnockback, direção/força horizontal zeradas
            // NÃO cancelam nada: um launcher puramente vertical (sobe reto, sem
            // afastar) é um golpe legítimo.
            knockbackVelocity = direction.sqrMagnitude < 0.0001f || force <= 0f
                ? Vector3.zero
                : direction.normalized * force;

            verticalVelocity = upwardForce;
            // Um launch novo manda mais que um hang em andamento. Sem zerar,
            // relançar alguém pendurado seria engolido: o FixedUpdate veria
            // hangRemaining > 0 e zeraria a velocidade que acabou de ser dada.
            hangRemaining = 0f;
            // Arco novo, ápice novo — o relance ganha a própria pendurada.
            apexHangUsed = false;

            // Altura de origem, pra cravar o pouso exatamente onde decolou. O
            // alvo anda em NavMesh plana, então "o chão" é o Y de onde saiu —
            // bem mais barato e estável que raycast por frame, e evita o alvo
            // terminar meio enterrado por erro de integração.
            //
            // Só é capturada se ele NÃO estiver já voando: relançar no meio de
            // um juggle (o ponto do juggle) gravaria a altura do ar como "o
            // chão" e o alvo pousaria flutuando, cada relance subindo o piso
            // dele um pouco mais.
            if (!launching)
            {
                groundY = rb.position.y;
            }
            // Tempo de voo de um arco que sobe e volta: 2v/g.
            currentDuration = 2f * upwardForce / launchGravity;
            remaining = currentDuration;
            launching = true;
        }

        // Segura o alvo NA ALTURA EM QUE ELE ESTÁ por um instante — o hangtime
        // do combo aéreo, o espelho exato do que
        // PlayerController.airAttackGravityScale faz no player.
        //
        // Não é um relaunch de propósito. Somar velocidade pra cima a cada
        // acerto (chamar ApplyLaunch de novo no meio do voo, que o código acima
        // até suporta) faria o inimigo ESCALAR ao longo do combo, cada hit
        // jogando ele mais alto que o anterior, até sair do alcance por cima —
        // o oposto do problema que isto resolve. Congelar o arco mantém o alvo
        // alcançável sem mexer no teto que o launcher original definiu.
        //
        // Só age em quem JÁ está voando: um golpe aéreo que acerta alguém em
        // pé no chão não deve pendurar ninguém (não houve launcher), e sem essa
        // guarda o alvo ficaria suspenso com groundY nunca capturado.
        public void ApplyAirHold(float hangTime)
        {
            if (!launching || hangTime <= 0f)
            {
                return;
            }

            verticalVelocity = 0f;
            hangRemaining = Mathf.Max(hangRemaining, hangTime);
            // O `remaining` é o orçamento de tempo do arco, calculado como 2v/g
            // lá no ApplyLaunch. Sem estender junto, o voo expiraria durante o
            // hang e o alvo seria cravado no groundY — teleportado pro chão no
            // meio do combo, que é exatamente o que se quer evitar.
            remaining += hangTime;
        }

        private void FixedUpdate()
        {
            if (remaining <= 0f)
            {
                return;
            }

            Vector3 delta;

            if (launching && hangRemaining > 0f)
            {
                // Pendurado: o horizontal continua (o alvo não trava no ar como
                // uma estátua, ele desliza), só o vertical é que fica parado.
                hangRemaining -= Time.fixedDeltaTime;
                verticalVelocity = 0f;
                delta = knockbackVelocity * Time.fixedDeltaTime;
            }
            else if (launching)
            {
                verticalVelocity -= launchGravity * Time.fixedDeltaTime;

                // Ápice: a velocidade acabou de cruzar de subindo pra caindo.
                // Segura aqui em vez de deixar inverter na hora — é o que faz
                // o alvo FLUTUAR no topo em vez de só trocar de direção.
                //
                // Cravar verticalVelocity em 0 junto é o que garante que a
                // queda comece do repouso quando o hang terminar; sem isso o
                // resto do frame (que já ficou negativo) seria devolvido
                // depois, e o alvo sairia da pendurada com um tranco.
                if (!apexHangUsed && apexHangTime > 0f && verticalVelocity <= 0f)
                {
                    apexHangUsed = true;
                    verticalVelocity = 0f;
                    hangRemaining = apexHangTime;
                    // O orçamento de tempo do arco (2v/g, calculado no
                    // ApplyLaunch) não previa a pausa — sem estender, o voo
                    // expiraria durante ela e o alvo seria cravado no chão do
                    // ápice, teleportado.
                    remaining += apexHangTime;
                }

                delta = (knockbackVelocity + Vector3.up * verticalVelocity) * Time.fixedDeltaTime;
            }
            else
            {
                float t = currentDuration > 0f ? remaining / currentDuration : 0f;
                delta = knockbackVelocity * t * Time.fixedDeltaTime;
            }

            Vector3 target = rb.position + delta;
            remaining -= Time.fixedDeltaTime;

            // Pouso: o que encerra o voo é cruzar a altura de origem, não o
            // timer. O 2v/g acima é exato na matemática e aproximado na
            // integração por passos — deixar o timer mandar sozinho pousaria o
            // alvo um pouco acima ou abaixo do chão dependendo do fixedDeltaTime.
            if (launching && (remaining <= 0f || target.y <= groundY))
            {
                target.y = groundY;
                remaining = 0f;
                launching = false;
                verticalVelocity = 0f;
                hangRemaining = 0f;
                apexHangUsed = false;
            }

            // Kinematic não sofre resposta a colisão — MovePosition atravessa
            // geometria sem ser barrado. Inofensivo pro dummy de teste
            // isolado, mas não é "física real" contra paredes/cenário.
            rb.MovePosition(target);
        }
    }
}
