# Checklist — Pendências Gerais

Levantado em 2026-08-05. Marcar aqui conforme for fechando; o "como
fazer" de cada item está em
[Guide - Trabalho Pendente (Consolidado)](Guide%20-%20Trabalho%20Pendente%20%28Consolidado%29.md),
que por sua vez aponta pra seção exata do guia original em `Merged/`.

---

## 1. Fase 4 do Combate

- [x] Ataque Carregado (`Attack1Charged`)
- [ ] Run Start / Run End (import + parâmetros + estados + transições
      de entrada/saída natural)
- [ ] Total Input Cancel (9 transições saindo de `RunStart`/`RunEnd`/
      `SprintEnd` — ataque/pulo/dodge cancelam na hora)
- [ ] Sprint End (decidir se reaproveita `Run End Greatsword` ou espera
      clipe dedicado)
- [ ] Sprint/Run Heavy Attack 1 (escolher clipe entre os dois
      candidatos livres, curva `ForwardMomentum`, estado + transições)
- [ ] Plunge Attack — Inspector/layers (`plungeImpactMask`, 2×
      `CinemachineImpulseSource`, `CinemachineImpulseListener` na vcam)
- [ ] Plunge Attack — corte dos clipes (`AirHeavyAttack1`/`2`)
- [ ] Plunge Attack — Animation Event `OnPlungeImpact`
- [ ] Plunge Attack — estados + 7 transições
- [ ] Checklist de Play Mode da Fase 4 (seção 5 do guia original)

## 2. Combate Aéreo

- [ ] Limpeza dos parâmetros órfãos no Animator (jump attack antigo)
- [ ] Smash (Triângulo no ar) — **bloqueado**, falta animação (não é
      wiring — só desbloqueia quando o clipe chegar)

## 3. Vida, Dano e Inimigo de Teste

- [ ] Confirmar `HealthComponent` no Player persistido na cena (Ctrl+S
      depois de checar) — bloqueia dano ao Player, cura (§4) e i-frames
      do dodge

## 4. Habilidades (Heal e Magia de Ataque)

- [ ] Estados `Heal` e `AttackMagic` no Animator (tag `Ability`, layer
      base, entrada/saída por `IsWielded`)
- [ ] Animation Event `OnHealApplied` no clipe de Heal
- [ ] Animation Event `OnAttackHitRadial` no clipe de AttackMagic
- [ ] Checklist de Play Mode (cura sobe vida sem passar do máximo, dano
      radial funciona, `IsAbilityModifierHeld` não vaza pro combo normal)

## 5. Inimigo Warrok (Fase 3)

- [ ] Layer `Player` nova + atribuída no root do Player
- [ ] Reconstruir `PushTestDummy` → `Enemy_Warrok` (modelo, Animator,
      `EnemyAttackHitbox`, `NavMeshAgent`, `EnemyBase`)
- [ ] Bake do NavMesh (`NavMeshSurface` no chão)
- [ ] `EnemyTelegraphDisc.mat` + `JumpAttackTelegraph.prefab` (atenção:
      prefab precisa nascer ativo)
- [ ] `EnemyAnimatorController.controller` (Blend Tree 1D em `Speed`
      real, `Attack`/`JumpAttack`/`Hit`, Animation Events)
- [ ] Checklist de Play Mode completo (seção "Verificação" do guia
      original — roaming, aggro, leash, ataque, jump attack, hit
      reaction, morte, sem erros de NavMeshAgent)

## 6. Robustez e Data-Driven (baixa prioridade — sem prazo)

- [ ] Item 1 — fila universal (`PendingAction`), só se aparecer sintoma
- [ ] Item 4 — data-driven por arma (Animator Override Controller +
      `WeaponMoveset`), só quando a segunda arma chegar
- [ ] Item 5 — validação em Editor dos nomes de parâmetro contra o
      controller real
