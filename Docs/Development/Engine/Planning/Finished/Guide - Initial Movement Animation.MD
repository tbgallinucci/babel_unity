# Guia Clique a Clique: Teste de Root Motion na Unity (1 modelo + Idle/Run/Jump)

Este guia assume que você não tem nada aberto ainda. Siga na ordem, sem pular nenhum clique. Onde eu escrever um caminho de menu (ex.: `Assets > Import New Asset...`), é sempre: clique no primeiro nome na barra de menu do topo, depois no submenu que aparece, e assim por diante.

Antes de começar, uma correção que fiz em relação a uma versão anterior deste guia que você pode ter visto: na configuração original, o clipe de **Jump** foi marcado para extrair o `Root Transform Position (Y)` como root motion real, mas o script em C# **zera** essa componente Y do root motion (`rootMotionPosition.y = 0f`) e usa um impulso de física (`jumpForce`) para a subida. Isso é uma contradição: se o script já ignora o Y do root motion, não faz sentido configurar o import para extraí-lo — a configuração certa é manter esse Y **preso na malha** (igual ao Idle/Run), e deixar 100% do pulo por conta da física do script. Corrigi isso abaixo, na Parte 2.

Segunda correção: uma versão anterior deste guia mandava criar o `Player` como uma `Capsule` separada, com o `Character Controller` e o script nela, e colocar o modelo (com o `Animator`) como um filho chamado `MeshVisual`. Isso está **errado** e não funciona: a Unity só chama o callback `OnAnimatorMove()` (usado no script da Parte 5) no GameObject que tem o componente `Animator`. Com o Animator num filho separado, a Unity aplica o root motion automaticamente nesse filho, ignorando por completo o `Character Controller` do pai — o personagem parece andar, mas na verdade a malha desliza sozinha (inclusive para os lados, sem relação com a rotação/câmera), a câmera (que segue o pai, quase parado) fica com um comportamento estranho, e o pulo nunca funciona porque `controller.isGrounded` nunca é recalculado. Corrigi isso abaixo, na Parte 3: agora o `Animator`, o `Character Controller` e o script ficam todos no mesmo GameObject.

---

## Parte 0 — Criando o projeto na Unity

1. Abra o **Unity Hub** (ícone azul na área de trabalho ou barra de tarefas).
2. No Unity Hub, na aba **Projects** (barra lateral esquerda), clique no botão azul **New project** no canto superior direito.
3. Na tela que abre, na lista de templates à esquerda/centro, clique no template **3D** (ou **3D (URP)** — qualquer um dos dois serve para este teste; se aparecer as duas opções e você não tiver preferência, escolha **3D (URP)**).
4. No campo **Project name**, apague o texto padrão e digite, por exemplo, `TesteRootMotion`.
5. No campo **Location**, clique no ícone de pasta e escolha onde salvar o projeto no seu computador (ou deixe o caminho padrão).
6. Clique no botão azul **Create project**, no canto inferior direito da tela. Aguarde a barra de progresso — o Unity Editor vai abrir sozinho quando terminar.

### 0.1 Verificando o sistema de Input (checkpoint importante)

O script deste guia usa o sistema antigo de Input (`Input.GetAxis`, `Input.GetButtonDown`). Precisamos garantir que ele está ativo:

1. Na barra de menu do topo, clique em `Edit > Project Settings...`.
2. Na janela **Project Settings** que abre, na lista à esquerda, clique em **Player**.
3. No painel à direita, role para baixo até achar a seção **Other Settings**. Dentro dela, procure o campo **Active Input Handling**.
4. Se estiver escrito `Input System Package (New)`, clique no menu suspenso e mude para **`Both`** (ou `Input Manager (Old)`). Se o Unity pedir para reiniciar o Editor, clique em **Apply** / **Yes** e espere reabrir.
5. Ainda em Project Settings, na lista à esquerda, clique em **Input Manager**, depois em **Axes** para expandir. Confirme que existem as entradas `Horizontal`, `Vertical` e `Jump` (vêm por padrão em todo projeto novo — só estamos conferindo, não precisa criar nada).
6. Feche a janela **Project Settings**.

---

## Parte 1 — Mixamo: baixando o modelo e as 3 animações

### 1.1 Modelo base em T-Pose (com malha, sem animação de jogo)

1. No navegador, acesse `mixamo.com` e faça login.
2. Na aba **Characters** (topo da página), escolha um personagem (ex.: `Y Bot` ou `X Bot`) clicando na miniatura dele.
3. Depois de carregar o personagem no visualizador 3D, clique na aba **Animations** (topo da página).
4. Na caixa de busca à esquerda, digite `T-Pose` e clique no resultado correspondente (o personagem deve ficar parado, de braços abertos, sem nenhum movimento no preview).
5. No painel direito, clique no botão laranja **DOWNLOAD**.
6. Na janela de download que abre:
   - **Format**: clique no menu suspenso e escolha `FBX Binary (.fbx)`.
   - **Skin**: clique na opção `With Skin`.
   - Não existe mais nada a mexer nessa janela para esse download específico.
7. Clique no botão **DOWNLOAD** dentro dessa janela (o botão final de confirmação). Renomeie o arquivo para algo como `Personagem_TPose_WithSkin.fbx`.

Este arquivo serve só para trazer a malha (mesh) e criar o Avatar Humanoid na Unity — a pose estática dele nunca será usada como animação jogável, só as três animações abaixo.

### 1.2 Animação de Idle (sem malha)

1. Ainda na aba **Animations**, busque `Idle` na caixa de busca e clique na animação desejada.
2. O toggle **"In Place"** pode ficar marcado ou desmarcado aqui — como o personagem fica parado no lugar, não faz diferença prática. Deixe **marcado (ON)**.
3. Clique em **DOWNLOAD**. Configure:
   - **Format**: `FBX Binary (.fbx)`.
   - **Skin**: clique em `Without Skin`.
   - **Frame Rate (FPS)**: `30`.
   - **Keyframe Reduction**: `none`.
4. Clique em **DOWNLOAD** para confirmar. Renomeie para `Personagem_Idle.fbx`.

### 1.3 Animação de Run

1. Ainda na aba **Animations** do Mixamo, busque `Run` na caixa de busca e clique na animação desejada.
2. No preview central, olhe para o painel direito e localize o toggle **"In Place"** (geralmente logo abaixo do nome da animação, com um ícone de pessoa/setas). **Clique nele para deixá-lo desmarcado/cinza (OFF)**. Isso é essencial: se ficar marcado, o Mixamo zera o deslocamento do quadril e não sobra nada para extrairmos depois na Unity.
3. Clique em **DOWNLOAD**.
4. Na janela de download:
   - **Format**: `FBX Binary (.fbx)`.
   - **Skin**: clique em `Without Skin`.
   - **Frame Rate (FPS)**: deixe em `30`.
   - **Keyframe Reduction**: clique na opção `none`.
5. Clique em **DOWNLOAD** para confirmar. Renomeie para `Personagem_Run.fbx`.

### 1.4 Animação de Jump

1. Busque `Jump` na caixa de busca e clique na animação desejada.
2. Confirme que o toggle **"In Place" está desmarcado (OFF)**, do mesmo jeito que no Run.
3. Clique em **DOWNLOAD**, configure igual ao Run (`FBX Binary`, `Without Skin`, `30 FPS`, `Keyframe Reduction: none`), e clique em **DOWNLOAD** de novo para confirmar. Renomeie para `Personagem_Jump.fbx`.

Ao final desta parte você deve ter 4 arquivos `.fbx` na sua pasta de Downloads: `Personagem_TPose_WithSkin.fbx` (malha + esqueleto, sem animação de jogo), `Personagem_Idle.fbx`, `Personagem_Run.fbx` e `Personagem_Jump.fbx` (esses três só com esqueleto animado, sem malha).

---

## Parte 2 — Importando na Unity e configurando o Root Motion

### 2.1 Trazendo os arquivos para dentro do projeto

1. Na Unity, olhe para o painel **Project** (geralmente na parte inferior da tela; se não estiver visível, vá em `Window > General > Project`).
2. Dentro do painel Project, clique com o botão direito na pasta `Assets` (painel à esquerda dentro do Project) e escolha `Create > Folder`. Nomeie a pasta como `Art`.
3. Clique com o botão direito na pasta `Art` recém-criada, `Create > Folder`, nomeie como `Animations`.
4. Abra o explorador de arquivos do Windows (fora da Unity), localize os 4 arquivos `.fbx` baixados do Mixamo, selecione os quatro e arraste-os diretamente para dentro da pasta `Assets/Art/Animations` no painel Project da Unity. Solte o clique quando o cursor estiver sobre a pasta.
5. Aguarde a barra de progresso azul no canto inferior direito da Unity terminar de importar.

### 2.2 Configurando o Avatar Humanoid do modelo base

1. No painel Project, clique uma vez no arquivo `Personagem_TPose_WithSkin.fbx` para selecioná-lo.
2. No painel **Inspector** (geralmente à direita da tela), você verá abas horizontais: `Model`, `Rig`, `Animation`, `Materials`. Clique na aba **Rig**.
3. No campo **Animation Type**, clique no menu suspenso e escolha **Humanoid**.
4. No campo **Avatar Definition**, confirme que está em **Create From This Model**.
5. Clique no botão **Apply**, no canto inferior direito do Inspector.
6. Aguarde processar. Se aparecer um ícone de aviso amarelo ao lado do campo Avatar Definition, clique no botão **Configure...** que aparece logo abaixo:
   - Isso abre uma cena temporária de configuração do Avatar. No painel Inspector dela, clique na aba **Mapping** (barra de ícones no topo do painel do Avatar) e depois no menu **Mapping > Automap** (no topo da janela do Avatar).
   - Confira se os ossos ficaram todos verdes no boneco na tela. Se sim, clique no botão **Done**, no canto inferior direito.
   - Se algum osso ficar vermelho/cinza, clique nele na lista de ossos à direita e arraste manualmente o osso correspondente do modelo na cena para o slot vazio — mas isso é raro em modelos padrão do Mixamo.

### 2.3 Configurando o Avatar dos clipes de animação (Idle, Run e Jump)

1. No painel Project, clique no arquivo `Personagem_Idle.fbx`.
2. No Inspector, clique na aba **Rig**.
3. **Animation Type**: `Humanoid`.
4. **Avatar Definition**: clique no menu suspenso e escolha **Copy From Other Avatar**.
5. Um novo campo chamado **Source** aparece logo abaixo. Clique no pequeno círculo/alvo à direita dele (o seletor de asset) e escolha `Personagem_TPose_WithSkin` na lista (é o avatar que criamos no passo 2.2, a partir do arquivo em T-Pose).
6. Clique em **Apply**.
7. Repita exatamente os passos 1 a 6 para os arquivos `Personagem_Run.fbx` e `Personagem_Jump.fbx` (um de cada vez, sempre apontando **Source** para `Personagem_TPose_WithSkin`).

### 2.4 Extraindo o Root Motion — clipe por clipe

Agora vamos configurar cada clipe individualmente. Repare que dentro de cada arquivo `.fbx` existe uma sub-lista de clipes (visível se você clicar na setinha ao lado do ícone do arquivo no painel Project); vamos editar o clipe através da aba **Animation** do arquivo pai mesmo. O arquivo `Personagem_TPose_WithSkin.fbx` **não precisa** ser configurado nesta parte — a pose estática dele não vai para o Animator Controller, ele já cumpriu seu papel (malha + Avatar) nas Partes 2.2 e 2.3.

**Clipe de Idle** (dentro de `Personagem_Idle.fbx`):

1. Selecione `Personagem_Idle.fbx` no Project.
2. No Inspector, clique na aba **Animation**.
3. Na lista **Clips** (parte superior do painel), clique no clipe de Idle (deve ser o único listado, ou o que tiver o nome da animação de Idle).
4. Role para baixo até a seção **Root Transform Rotation**. Clique na caixinha **Bake Into Pose** para deixá-la **marcada (✓)**.
5. Logo abaixo, seção **Root Transform Position (Y)**. Marque **Bake Into Pose** (✓).
6. Logo abaixo, seção **Root Transform Position (XZ)**. Marque **Bake Into Pose** (✓).
7. Mais acima na mesma aba, marque a caixinha **Loop Time**.
8. Clique em **Apply**, no canto inferior direito do Inspector.

**Clipe de Run** (dentro de `Personagem_Run.fbx`):

1. Selecione `Personagem_Run.fbx`, aba **Animation**, clique no clipe de Run na lista **Clips**.
2. **Root Transform Rotation > Bake Into Pose**: **desmarque** (☐).
3. **Root Transform Position (Y) > Bake Into Pose**: **marque** (✓).
4. **Root Transform Position (XZ) > Bake Into Pose**: **desmarque** (☐). É este desmarcado que faz a Unity extrair o deslocamento para frente como root motion de verdade.
5. Marque **Loop Time**.
6. Clique em **Apply**.

**Clipe de Jump** (dentro de `Personagem_Jump.fbx`) — atenção, aqui está a correção mencionada no início do guia:

1. Selecione `Personagem_Jump.fbx`, aba **Animation**, clique no clipe de Jump na lista **Clips**.
2. **Root Transform Rotation > Bake Into Pose**: **marque** (✓) — pulos normalmente não giram o corpo, então deixamos preso na malha por segurança.
3. **Root Transform Position (Y) > Bake Into Pose**: **marque** (✓). Isto é diferente do que uma versão anterior deste guia dizia — aqui deixamos marcado de propósito, porque a subida do pulo vai ser 100% controlada pelo código (`jumpForce` no script), e não pela animação. Se deixássemos desmarcado, a Unity tentaria aplicar a subida da animação **e** o código somaria a força de pulo por cima, fazendo o personagem pular alto demais ou de forma inconsistente.
4. **Root Transform Position (XZ) > Bake Into Pose**: **desmarque** (☐), para o caso de essa animação de pulo específica ter algum avanço/impulso horizontal para frente — assim ele é extraído normalmente, igual ao Run.
5. Deixe **Loop Time desmarcado** (o pulo deve tocar uma vez até o fim, não repetir em loop).
6. Antes de clicar em Apply, você pode conferir visualmente: logo abaixo da lista de clipes há uma janelinha de preview com um play; arraste a barrinha de tempo e observe se o quadril se desloca para frente ou só para cima — isso só confirma visualmente, não muda a configuração que já fizemos.
7. Clique em **Apply**.

---

## Parte 3 — Montando a cena

### 3.1 Criando o Player a partir do modelo

Aqui usamos o próprio modelo importado como o GameObject `Player`, colocando o `Character Controller` diretamente nele — em vez de criar uma cápsula separada e colocar o modelo como filho. Isso é necessário porque a Unity só chama o callback `OnAnimatorMove()` (usado no script da Parte 5) no GameObject que tem o componente `Animator`; se o Animator ficasse num filho separado do `Character Controller`, o root motion seria aplicado errado (veja a nota no topo do guia).

1. No painel Project, localize o arquivo `Personagem_TPose_WithSkin.fbx` e clique na setinha ao lado do ícone para expandir, revelando o modelo 3D dentro (um ícone azul de "prefab"/modelo com o mesmo nome ou nome genérico do personagem).
2. Arraste esse ícone do modelo diretamente do painel Project para dentro do painel **Hierarchy** (geralmente à esquerda da tela; se não aparecer, `Window > General > Hierarchy`), soltando numa área vazia — **não** solte em cima de nenhum outro objeto, ele deve virar um objeto de nível raiz na cena, não filho de nada.
3. O objeto aparece na Hierarchy com o nome do modelo. Clique nele uma vez para selecionar, depois clique duas vezes lentamente sobre o nome (ou aperte `F2`) e renomeie para `Player`.
4. Com `Player` selecionado, no Inspector confira o **Transform > Position**: deixe em `X: 0, Y: 0, Z: 0`.
5. Ainda no Inspector, clique no botão **Add Component** (parte inferior do Inspector), digite `Character Controller` na caixa de busca e clique no resultado para adicioná-lo.
6. Localize o componente **Animator** (já vem automaticamente, pois todo modelo Humanoid importado tem um). Localize o campo **Apply Root Motion** dentro desse componente e clique na caixinha para **marcá-la (✓)**.
7. Confira que o `Player` agora tem, entre outros: `Transform`, `Animator`, `Character Controller`.

### 3.2 Criando um chão para teste

1. Na Hierarchy, clique com o botão direito em área vazia, vá em `3D Object > Plane`.
2. Um objeto `Plane` aparece na posição `0, 0, 0` — ele já vem com um **Mesh Collider** por padrão, então o `Character Controller` do `Player` vai colidir com ele normalmente, sem precisar adicionar nada.
3. Se quiser um chão maior, ajuste **Transform > Scale** do `Plane` (ex.: `5, 1, 5`; o Plane padrão tem 10x10 unidades com scale 1).
4. Selecione `Player` e confira que ele está posicionado **acima** do plano no eixo Y (ex.: `Y: 1`), para não nascer atravessando o chão.

### 3.3 Câmera livre com Cinemachine

1. Na barra de menu do topo, clique em `Window > Package Manager`.
2. Na janela Package Manager, no menu suspenso superior esquerdo (geralmente escrito "In Project" ou "Packages: In Project"), clique e mude para **Unity Registry**.
3. Na caixa de busca da janela, digite `Cinemachine`.
4. Clique no resultado `Cinemachine` na lista à esquerda, depois clique no botão **Install** no canto inferior direito do painel de detalhes. Aguarde instalar e feche a janela Package Manager.
5. Na Hierarchy, clique com o botão direito em área vazia, vá em `Cinemachine > FreeLook Camera` (esse é o item de menu no Cinemachine 3, que vem por padrão nas versões atuais do Unity 6; se no seu menu aparecer só `Cinemachine Camera` sem a opção `FreeLook Camera`, use `Cinemachine Camera` mesmo e ajuste o campo Orbital Follow manualmente depois, conforme o passo 7).
6. Um objeto (algo como `CM FreeLook Camera` ou `CinemachineCamera`) aparece na Hierarchy, já com os componentes **CinemachineCamera**, **Orbital Follow** e **Rotation Composer** no Inspector.
7. Com esse objeto selecionado, no Inspector, localize no componente **Cinemachine Camera** os campos **Tracking Target** e **LookAt Target** (ficam próximos ao topo do componente). Arraste o objeto `Player` da Hierarchy para dentro de cada um desses dois campos.
8. Ajuste, se quiser, os raios de órbita dentro do componente **Orbital Follow** (campos `Top`, `Center`, `Bottom` dentro de `Orbit Style`), mas isso não é obrigatório para o teste funcionar.

---

## Parte 4 — Animator Controller

### 4.1 Criando o Controller

1. No painel Project, clique com o botão direito dentro da pasta `Assets` (ou crie uma pasta `Assets/Animation` primeiro, mesmo processo do passo 2.1.2), escolha `Create > Animator Controller`.
2. Nomeie o arquivo recém-criado como `PlayerAnimatorController`.
3. Selecione o objeto `Player` na Hierarchy. No Inspector, localize o componente **Animator** e arraste o asset `PlayerAnimatorController` para o campo **Controller** desse componente.
4. Dê dois cliques no asset `PlayerAnimatorController` (dentro do painel Project) para abrir a janela **Animator** (ou vá em `Window > Animation > Animator` se ela não abrir sozinha).

### 4.2 Parâmetros

1. Na janela Animator, clique na aba **Parameters** (canto superior esquerdo da janela, ao lado de "Layers").
2. Clique no botão **+** e escolha **Float**. Renomeie o parâmetro criado para `Speed` (clique duas vezes no nome para editar).
3. Clique no **+** de novo, escolha **Trigger**. Renomeie para `Jump`.

### 4.3 Estado de Locomotion (Blend Tree 1D)

1. Ainda na janela Animator, clique com o botão direito em uma área vazia do grafo (painel maior à direita) e escolha `Create State > Empty`.
2. Um bloco cinza aparece. Clique nele, e no Inspector (à direita da tela do Unity, fora da janela Animator) renomeie o campo **Motion**... na verdade, primeiro renomeie o próprio estado: com o bloco selecionado, olhe o Inspector e mude o campo de nome no topo para `Locomotion`.
3. Clique com o botão direito nesse bloco `Locomotion` dentro do grafo e escolha `Automatic Life Cycle`... (ignore isso se aparecer, não é o que precisamos). Em vez disso: clique com o **botão direito** no bloco `Locomotion` e escolha **`Add Motion Field`**... 

   Como a forma mais confiável é recriar o estado já como Blend Tree, faça assim: delete o bloco cinza que você criou (clique nele, aperte `Delete`). Clique com o botão direito na área vazia do grafo novamente e escolha `Create State > From New Blend Tree`. Renomeie esse novo bloco (que já vem com um ícone de árvore) para `Locomotion`, usando o campo de nome no Inspector com o bloco selecionado.
4. Clique duas vezes sobre o bloco `Locomotion` para entrar dentro do Blend Tree (a janela Animator muda de contexto e mostra um breadcrumb no topo, tipo `Base Layer > Locomotion`).
5. Clique uma vez no nó **Blend Tree** que aparece dentro (bloco central). No Inspector à direita, confirme que **Blend Type** está em **1D**.
6. No campo **Parameter** (dentro do Inspector do Blend Tree), clique no menu suspenso e escolha `Speed`.
7. Na tabela **Motion** logo abaixo (dentro do Inspector), clique no botão **+** no canto inferior direito da tabela e escolha **Add Motion Field**. Uma linha vazia aparece.
8. Clique no pequeno círculo seletor na coluna `Motion` dessa linha e escolha o clipe de **Idle** na janela de seleção de asset que abre.
9. Repare na coluna `Threshold` dessa linha: deixe (ou edite para) `0`.
10. Clique em **+** de novo para adicionar outra linha, selecione o clipe de **Run** na coluna Motion, e edite o `Threshold` dessa linha para `1`.
11. No breadcrumb no topo da janela Animator, clique em `Base Layer` para voltar ao grafo principal.

### 4.4 Estado de Jump

1. No grafo principal (fora do Blend Tree), clique com o botão direito em área vazia e escolha `Create State > Empty`.
2. Selecione o bloco criado, renomeie no Inspector para `Jump`.
3. No campo **Motion** do Inspector desse estado, clique no seletor e escolha o clipe de animação de **Jump**.
4. Clique com o botão direito no bloco `Locomotion` (no grafo principal) e escolha **Make Transition**. Uma seta aparece grudada no cursor; clique no bloco `Jump` para completar a transição (`Locomotion → Jump`).
5. Clique na seta da transição recém-criada (fica fina, entre os dois blocos) para selecioná-la. No Inspector:
   - Desmarque a caixinha **Has Exit Time**.
   - Na seção **Conditions**, na parte de baixo do Inspector, clique no **+**, escolha o parâmetro `Jump` no menu suspenso (já deve aparecer sozinho, já que é do tipo Trigger).
6. Repita o processo de transição no sentido inverso: botão direito em `Jump` > **Make Transition** > clique em `Locomotion`.
7. Selecione essa nova seta (`Jump → Locomotion`) e no Inspector:
   - Marque **Has Exit Time**, e confirme que o valor **Exit Time** está em `1` (ou próximo de `0.9`–`1`) na lista de condições/tempo logo abaixo.
   - Não adicione nenhuma condição na seção Conditions (deixe vazia) — a transição deve acontecer sozinha quando a animação de pulo terminar.

---

## Parte 5 — Script C#

1. No painel Project, clique com o botão direito dentro de `Assets` (ou crie uma pasta `Assets/Scripts`), escolha `Create > C# Script`.
2. Nomeie o arquivo exatamente `SimpleNierController` (o nome do arquivo precisa bater com o nome da classe dentro dele).
3. Clique duas vezes no arquivo para abrir no seu editor de código (Visual Studio / VS Code / Rider, o que estiver associado).
4. Apague todo o conteúdo padrão e cole o código abaixo:

```csharp
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleNierController : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float jumpForce = 5f;

    private CharacterController controller;
    private Animator animator;
    private Transform mainCameraTransform;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        HandleMovement();
        HandleJump();
    }

    private void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 inputDir = new Vector3(moveX, 0f, moveZ);
        float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);

        animator.SetFloat("Speed", inputMagnitude, 0.05f, Time.deltaTime);

        if (inputMagnitude > 0.05f && mainCameraTransform != null)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + mainCameraTransform.eulerAngles.y;
            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleJump()
    {
        if (controller.isGrounded && Input.GetButtonDown("Jump"))
        {
            animator.SetTrigger("Jump");
            verticalVelocity = jumpForce;
        }
    }

    private void OnAnimatorMove()
    {
        if (animator == null) return;

        Vector3 rootMotionPosition = animator.deltaPosition;
        rootMotionPosition.y = 0f;

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        Vector3 finalMovement = rootMotionPosition + Vector3.up * verticalVelocity * Time.deltaTime;
        controller.Move(finalMovement);

        transform.rotation *= animator.deltaRotation;
    }
}
```

5. Salve o arquivo (`Ctrl+S`) e volte para a janela da Unity.
6. Na Hierarchy, selecione o objeto `Player`.
7. Arraste o script `SimpleNierController` (de dentro do painel Project) para dentro do Inspector do `Player`, em qualquer área vazia abaixo dos componentes existentes — ele se anexa como um novo componente.

---

## Parte 6 — Testando

1. No topo da tela da Unity, clique no botão de **Play** (▶, ícone triangular, centro superior).
2. Use as teclas `W`, `A`, `S`, `D` (ou setas do teclado) para andar — o personagem deve fazer a transição de Idle para Run suavemente e realmente se deslocar no chão (não só animar no lugar).
3. Aperte `Espaço` para pular — o personagem deve subir de fato (pela física do script) enquanto a animação de pulo toca.
4. Clique de novo no botão **Play** para parar o teste.

### Checklist se algo não funcionar

- Personagem anima mas não sai do lugar ao correr → confira se `Root Transform Position (XZ) > Bake Into Pose` do clipe **Run** está mesmo **desmarcado** (Parte 2.4).
- Personagem não pula (fica parado no ar ou não sobe) → confira se `Apply Root Motion` está marcado no Animator do `Player` (Parte 3.1, passo 6) e se o script está de fato anexado ao `Player`.
- Personagem pula alto/estranho demais → confira se `Root Transform Position (Y) > Bake Into Pose` do clipe **Jump** está **marcado** (Parte 2.4) — se estiver desmarcado, a animação soma sua própria subida por cima do `jumpForce` do código.
- Tecla `W`/`Espaço` não faz nada → volte na Parte 0.1 e confirme o `Active Input Handling`.
- Câmera não segue o personagem → confira se `Player` foi arrastado tanto em `Tracking Target` quanto em `LookAt Target` do Cinemachine (Parte 3.3, passo 7).
- Personagem cai para sempre / atravessa o chão → confira se você criou o `Plane` da Parte 3.2 e se o `Player` nasce acima dele no eixo Y.
- Personagem desliza para os lados ao andar, ou anda mas a câmera não acompanha direito → confira se o `Animator`, o `Character Controller` e o script `SimpleNierController` estão **todos no mesmo objeto** `Player` (Parte 3.1) — se o Animator estiver num filho separado, o `OnAnimatorMove()` do script nunca é chamado e a Unity aplica o root motion sozinha, no objeto errado.
