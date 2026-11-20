# Tutorial — Visão Geral

Personagem jogável: **Severino** (ex-cangaceiro, braço mecânico).

## Controles
- **W A S D** — andar | **mouse** — câmera
- **E** — falar com NPCs
- **Q** — sacar / guardar a arma (armado: anda mais devagar e não corre)
- **Botão esquerdo** — atirar (5 tiros, recarga automática)
- **Botão direito** — esquivar (armado: esquiva direcional sem largar a arma | desarmado: mergulho com invencibilidade)

> Para montar as cenas no Unity, siga o **`Assets/01_GuiaDeMontagemDasCenas.md`**.

## Cenas
1. `Cena1_Bar` — **NPCTutorial1** (Seu Zé): andar, câmera, sacar arma e atirar → **InimigoTutorial1** (Robô Azul)
2. `Cena2_Praca` — **NPCTutorial2** (Véio Bento): esquiva armada → **InimigoTutorial2** (Robô Roxo)
3. `Cena3_Portao` — **NPCTutorial3** (Dona Socorro): esquiva desarmada → **InimigoTutorial3** (Robô Laranja)
4. `Cena4_Torre` — **NPCBoss** (Coronel Silício) → **Boss** (Super Mecha)

## Elenco
Os arquivos usam o papel do personagem (NPCTutorial1, NPCTutorial2...), não o nome dele.
O **nome** fica só no campo **Nome** do componente NPC, no prefab: é ele que aparece nas falas e no "[E] Falar com ...".
O nome do jogador fica no campo **Nome Do Jogador** da CaixaDeDialogo (padrão: Severino).

| Papel | Nome atual | Modelo (`Characters/NPCs/`) | Prefab (`Prefabs/NPCs/`) | Conversas (`Roteiro/Tutorial/Dialogos/`) |
|---|---|---|---|---|
| NPCTutorial1 | Seu Zé (dono do bar) | `NPCTutorial1` (Beard_man) | `NPCTutorial1` | `NPCTutorial1/` |
| NPCTutorial2 | Véio Bento (ex-vigia do datacenter) | `NPCTutorial2` (village-head, sem esqueleto) | `NPCTutorial2` | `NPCTutorial2/` |
| NPCTutorial3 | Dona Socorro (ex-cangaceira) | `NPCTutorial3` (Bandit) | `NPCTutorial3` | `NPCTutorial3/` |
| NPCBoss | Coronel Silício | `NPCBoss` (man-in-suit) | `NPCBoss` | `NPCBoss/` |
| NPCMorador | Morador | `NPCMorador` (Farmer) | `NPCMorador` | `NPCMorador/` |

Falas dos robôs: `Roteiro/Tutorial/Dialogos/Inimigos/`.

### Nomes escritos dentro do texto (mudar à mão se renomear alguém)
- `NPCTutorial1_DepoisDaLuta`: "fala com o **Véio Bento**, na praça"
- `NPCTutorial2_Conversa`: "**Coronel Silício**. Gerente da NimbusCorp..." e várias falas com "Coronel"
- `NPCBoss_Antes`: "Devolve a água da vila, **Coronel**."

## Regras do roteiro
- NPCs falam **antes** e **depois** das lutas, nunca durante.
- O inimigo só aparece quando o jogador chega até ele (nada de ele chegar no meio da conversa). Exceção: na cena 1 o Robô Azul é visto pela janela.
- NPCs com pouca ou nenhuma animação: evitar gestos no roteiro.
- Manter as gírias e o jeito sertanejo das falas; Severino fala pouco e seco.

## Telas
- **Morte (qualquer luta, inclusive o boss):** "Severino caiu... mas cangaceiro não desiste." **[Tentar de novo]**
- **Vitória (depois do boss):** "A água voltou pra vila." **[Menu principal]**
