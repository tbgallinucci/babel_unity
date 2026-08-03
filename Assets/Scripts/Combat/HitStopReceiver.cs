using UnityEngine;

namespace Babel.Combat
{
    // O lado POR PERSONAGEM do hit-stop. Enquanto o HitStop global congela o
    // mundo inteiro (timeScale), este componente cuida do que acontece com
    // este ator específico durante a trava: quanto a animação dele ainda
    // avança e como o corpo treme.
    //
    // Mora na RAIZ do personagem (junto do HealthComponent), não no filho do
    // Animator — é a raiz que carrega CharacterController/Rigidbody/collider,
    // e é justamente ela que NÃO pode se mexer (ver "técnica 2" abaixo).
    // Player e inimigo têm o Animator num filho (o modelo importado), então o
    // alvo da tremida é resolvido sozinho a partir dele.
    //
    // As oito técnicas do vídeo "8つのヒットストップ仕様" do Sakurai estão
    // marcadas nos campos/métodos que as implementam. As que moram aqui:
    //   1 - quem apanha treme mais que quem bate
    //   2 - a tremida não move o hitbox
    //   3 - horizontal no chão, vertical no ar
    //   4 - a tremida vai diminuindo
    //   6 - interpolar os frames até a pose de dano (não dar corte seco)
    //   7 - quem bateu continua andando só um pouquinho
    //   8 - amplitude muda conforme a distância da câmera
    // As técnicas 5 (duração escala com a força do golpe) moram no HitStop /
    // nos hitboxes, que é quem conhece o dano do golpe.
    public class HitStopReceiver : MonoBehaviour
    {
        [Header("Alvo visual")]
        // TÉCNICA 2: o que treme é o pivô VISUAL (o filho do modelo), nunca a
        // raiz. Colliders, CharacterController, Rigidbody e a origem dos
        // OverlapSphere de dano ficam exatamente onde estavam — a tremida é
        // 100% cosmética e não consegue influenciar nada de gameplay.
        //
        // Se algum dia o hitbox de ataque passar a ser um collider filho do
        // modelo, aponte isto pra um pivô dedicado só com a malha embaixo.
        [SerializeField] private Transform shakePivot;
        [SerializeField] private Animator targetAnimator;

        [Header("Velocidade da animação durante a trava")]
        // Estes dois são o coração das técnicas 6 e 7, e o motivo de NÃO
        // serem zero. Com o mundo em timeScale 0 e o Animator deste ator em
        // UnscaledTime, um valor pequeno faz a animação continuar avançando
        // em câmera lentíssima em vez de virar um freeze-frame seco.
        //
        // TÉCNICA 7: quem bateu continua se movendo só um pouquinho — a trava
        // lê como peso, não como o jogo ter engasgado.
        [SerializeField, Range(0f, 0.5f)] private float attackerAnimatorSpeed = 0.04f;
        // TÉCNICA 6: interpolar os frames até a pose de dano. Quem apanha não
        // salta pro frame 1 do flinch: a transição pro estado Hit continua
        // rodando devagar durante a trava, então a pose ENTRA suavemente.
        // Vale só pra quem tem estado de dano no Animator (o inimigo tem —
        // trigger "Hit"/State.Hit em EnemyBase; o player ainda não tem
        // reação de dano nenhuma, então nele isto não muda nada por ora).
        [SerializeField, Range(0f, 0.5f)] private float victimAnimatorSpeed = 0.09f;

        [Header("Tremida")]
        // TÉCNICA 1: quem apanha treme MUITO mais que quem bate. É a
        // assimetria que comunica quem levou o dano — os dois tremendo igual
        // vira ruído e some a informação.
        [SerializeField] private float victimShakeAmplitude = 0.05f;
        [SerializeField] private float attackerShakeAmplitude = 0.012f;
        // Em Hz. Alto de propósito: a tremida tem que ler como vibração, não
        // como o personagem deslizando pro lado e voltando.
        [SerializeField] private float shakeFrequency = 34f;
        // TÉCNICA 4: a amplitude vai diminuindo ao longo da trava em vez de
        // vibrar em força total e cortar. Curva (e não só um lerp linear) pra
        // dar pra segurar forte no começo e soltar rápido no fim sem tocar em
        // código.
        [SerializeField]
        private AnimationCurve shakeFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Eixo da tremida")]
        // TÉCNICA 3: horizontal no chão, vertical no ar. "Horizontal" aqui é
        // o right da CÂMERA, não o do personagem: num jogo 3D com câmera
        // orbital, tremer no eixo local do ator daria uma tremida que
        // desaparece quando você olha ele de perfil. Pelo right da câmera a
        // vibração lê sempre igual na tela — que é o que o Smash tem de graça
        // por ser 2D.
        //
        // Chão vs ar é resolvido por um raycast curto pra baixo em vez de
        // perguntar pro PlayerController/EnemyBase: mantém o componente
        // plugável em qualquer coisa com vida (mesma ideia do HitFlash e do
        // KnockbackReceiver), sem acoplar em CharacterController nem
        // NavMeshAgent.
        [SerializeField] private float groundCheckDistance = 0.4f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Distância da câmera")]
        // TÉCNICA 8: a amplitude escala com a distância da câmera. Uma tremida
        // de 5cm que lê perfeita em close vira invisível com a câmera puxada
        // pra trás — escalando em espaço de mundo, o tamanho APARENTE na tela
        // fica constante. Distância de referência = a distância "normal" de
        // combate, onde a amplitude autorada vale exatamente o que está no
        // campo acima.
        [SerializeField] private float referenceCameraDistance = 8f;
        [SerializeField, Range(0f, 1f)] private float cameraDistanceInfluence = 1f;
        [SerializeField] private float maxCameraDistanceScale = 3f;

        private Transform cameraTransform;
        private Vector3 restLocalPosition;
        private Vector3 shakeAxisLocal;
        private float remaining;
        private float totalDuration;
        private float elapsed;
        private float amplitude;
        private float speedBeforeHit = 1f;
        private AnimatorUpdateMode updateModeBeforeHit;
        private bool active;

        // Pra quem precisar ceder o controle enquanto a trava roda (mesma
        // ideia do KnockbackReceiver.IsActive). Ninguém consome ainda — o
        // candidato natural é o EnemyBase, se um dia a IA precisar não tomar
        // decisão no meio de um hit-stop.
        public bool IsFrozen => active;

        private void Awake()
        {
            if (targetAnimator == null)
            {
                targetAnimator = GetComponentInChildren<Animator>();
            }

            if (shakePivot == null && targetAnimator != null)
            {
                shakePivot = targetAnimator.transform;
            }

            if (shakePivot != null)
            {
                restLocalPosition = shakePivot.localPosition;
            }
        }

        public void FreezeAsAttacker(float duration)
        {
            Freeze(duration, attackerAnimatorSpeed, attackerShakeAmplitude);
        }

        public void FreezeAsVictim(float duration)
        {
            Freeze(duration, victimAnimatorSpeed, victimShakeAmplitude);
        }

        private void Freeze(float duration, float animatorSpeed, float shakeAmplitude)
        {
            if (duration <= 0f)
            {
                return;
            }

            if (!active)
            {
                if (targetAnimator != null)
                {
                    // Captura o estado original só na PRIMEIRA trava — mesma
                    // armadilha que o HitStop global já documenta: um segundo
                    // hit durante o congelamento guardaria a velocidade
                    // reduzida como "normal" e o ator nunca voltaria ao ritmo.
                    speedBeforeHit = targetAnimator.speed;
                    updateModeBeforeHit = targetAnimator.updateMode;

                    // É ISTO que viabiliza as técnicas 6 e 7. Com o mundo em
                    // timeScale 0, um Animator em Normal simplesmente para —
                    // não existe "avançar devagarinho". Em UnscaledTime ele
                    // ignora o timeScale e passa a obedecer só o .speed
                    // abaixo, então dá pra congelar o MUNDO sem congelar por
                    // completo os dois personagens envolvidos.
                    targetAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                }

                elapsed = 0f;
                // Eixo e escala de câmera são resolvidos uma vez, na entrada:
                // durante a trava o mundo (e a câmera junto) está parado, e
                // recalcular por frame só daria chance de a tremida trocar de
                // eixo no meio dela mesma.
                shakeAxisLocal = ResolveShakeAxis();
                amplitude = 0f;
                active = true;
            }

            // Hits em sequência renovam a janela em vez de somar (mesma regra
            // do HitStop global) e ficam com a versão mais forte de cada
            // parâmetro — dois inimigos atingidos no mesmo swing não devem
            // dobrar nada.
            remaining = Mathf.Max(remaining, duration);
            totalDuration = Mathf.Max(totalDuration, duration);
            amplitude = Mathf.Max(amplitude, shakeAmplitude * CameraDistanceScale());

            if (targetAnimator != null)
            {
                targetAnimator.speed = speedBeforeHit * animatorSpeed;
            }
        }

        private void Update()
        {
            if (!active)
            {
                return;
            }

            // unscaledDeltaTime é obrigatório: com timeScale em 0 o
            // Time.deltaTime também é 0 e nem o contador nem a tremida
            // andariam.
            float dt = Time.unscaledDeltaTime;
            remaining -= dt;
            elapsed += dt;

            if (remaining <= 0f)
            {
                Restore();
                return;
            }

            if (shakePivot == null)
            {
                return;
            }

            float progress = totalDuration > 0f ? 1f - (remaining / totalDuration) : 1f;
            float falloff = shakeFalloff.Evaluate(progress); // técnica 4
            float offset = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f) * amplitude * falloff;

            shakePivot.localPosition = restLocalPosition + shakeAxisLocal * offset;
        }

        // TÉCNICA 3.
        private Vector3 ResolveShakeAxis()
        {
            // Sobe um pouco a origem pra sair de dentro do próprio collider —
            // o Raycast do Unity não reporta collider em que o raio NASCE
            // dentro, mas começar rente ao chão é o que dá falso negativo em
            // rampa.
            Vector3 origin = transform.position + Vector3.up * 0.15f;
            bool grounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance + 0.15f,
                groundLayers, QueryTriggerInteraction.Ignore);

            Vector3 worldAxis = grounded ? HorizontalAxis() : Vector3.up;
            return ToPivotLocal(worldAxis);
        }

        private Vector3 HorizontalAxis()
        {
            Transform cam = ResolveCamera();
            if (cam == null)
            {
                return transform.right;
            }

            Vector3 right = cam.right;
            right.y = 0f;
            return right.sqrMagnitude > 0.0001f ? right.normalized : transform.right;
        }

        // A tremida é escrita em localPosition (pra não brigar com o pai nem
        // depender da orientação do modelo), então o eixo de mundo precisa ser
        // convertido pro espaço do pai do pivô.
        private Vector3 ToPivotLocal(Vector3 worldAxis)
        {
            if (shakePivot == null || shakePivot.parent == null)
            {
                return worldAxis;
            }

            return shakePivot.parent.InverseTransformDirection(worldAxis);
        }

        // TÉCNICA 8.
        private float CameraDistanceScale()
        {
            Transform cam = ResolveCamera();
            if (cam == null || referenceCameraDistance <= 0f)
            {
                return 1f;
            }

            float distance = Vector3.Distance(cam.position, transform.position);
            float raw = Mathf.Clamp(distance / referenceCameraDistance, 0.25f, maxCameraDistanceScale);
            return Mathf.Lerp(1f, raw, cameraDistanceInfluence);
        }

        private Transform ResolveCamera()
        {
            // Camera.main é uma busca por tag; cacheia, mas revalida se a
            // câmera tiver sido destruída/trocada (o rig de Cinemachine troca
            // de câmera virtual, não da Camera em si, mas trocar de cena
            // invalida a referência).
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            return cameraTransform;
        }

        // Rede de segurança igual à do HitStop global: desativar o objeto (ou
        // sair do Play Mode) no meio de uma trava deixaria o Animator preso em
        // câmera lenta e o modelo deslocado do lugar de repouso.
        private void OnDisable()
        {
            if (active)
            {
                Restore();
            }
        }

        private void Restore()
        {
            if (targetAnimator != null)
            {
                targetAnimator.speed = speedBeforeHit;
                targetAnimator.updateMode = updateModeBeforeHit;
            }

            if (shakePivot != null)
            {
                shakePivot.localPosition = restLocalPosition;
            }

            remaining = 0f;
            totalDuration = 0f;
            elapsed = 0f;
            amplitude = 0f;
            active = false;
        }
    }
}
