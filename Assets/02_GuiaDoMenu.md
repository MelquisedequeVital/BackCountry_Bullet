# Guia do Menu e das Telas do Jogo

Este guia é para quem vai fazer o **menu principal** e as **telas do jogo** (pausa, morte e vitória).
A parte "de trás" já está pronta: o **FluxoDoJogo** pausa o tempo, solta o cursor, faz o jogador renascer,
detecta a vitória e troca de cena. Quem faz o menu só precisa **desenhar as telas** e **ligar os botões e eventos**
no Inspector — **sem programar**.

## Sumário

1. [O que já está pronto](#1-o-que-já-está-pronto)
2. [Telas que precisam ser feitas](#2-telas-que-precisam-ser-feitas)
3. [Antes de começar: EventSystem e ordem das telas](#3-antes-de-começar-eventsystem-e-ordem-das-telas)
4. [Telas do jogo (pausa, morte, vitória)](#4-telas-do-jogo-pausa-morte-vitória)
5. [Cena do menu principal](#5-cena-do-menu-principal)
6. [Build Profiles (lista de cenas)](#6-build-profiles-lista-de-cenas)
7. [Regras importantes](#7-regras-importantes)
8. [Como testar](#8-como-testar)
9. [Para quem quiser usar código (opcional)](#9-para-quem-quiser-usar-código-opcional)

---

## 1. O que já está pronto

### FluxoDoJogo (`Assets/Prefabs/Fluxo/FluxoDoJogo.prefab`)
Fica **um por cena de jogo** (quem monta a cena coloca). Se alguém esquecer, ele é criado sozinho no Play com um
aviso no Console, mas **sem as telas de vocês nem a vitória** — por isso as telas devem ficar no **prefab** (seção 4).
Ele controla 4 estados:

| Estado | Quando | O que ele já faz sozinho |
|---|---|---|
| **Jogando** | normal | cursor preso no centro, comandos do jogador ligados |
| **Pausado** | apertou **Esc** (ou chamou `Pausar()`) | para o tempo, pausa os sons, solta o cursor, trava os comandos do jogador |
| **Morto** | a vida do jogador zerou | trava os comandos e solta o cursor; **2,5 s depois** (tempo da animação de morte) avisa que é hora da tela de morte |
| **Vitória** | terminou a conversa final do Coronel (depois do boss) | trava os comandos, solta o cursor e avisa que é hora da tela de vitória |

**Botões — métodos que vocês ligam no `OnClick`:**
| Método | O que faz | Use em |
|---|---|---|
| `FluxoDoJogo.Continuar` | sai da pausa | tela de pausa |
| `FluxoDoJogo.TentarDeNovo` | o jogador **renasce no último ponto de retorno** (checkpoint), com a vida cheia; os inimigos vivos recuperam a vida; o que já foi feito continua feito | tela de morte |
| `FluxoDoJogo.ReiniciarTudo` | recomeça a cena do zero (tudo volta, inclusive as conversas) | pausa (opcional), vitória ("Jogar de novo") |
| `FluxoDoJogo.IrParaOMenuPrincipal` | vai para a cena do menu principal | pausa, morte, vitória |
| `FluxoDoJogo.Sair` | fecha o jogo (no Editor, só para o Play) | pausa (opcional) |
| `FluxoDoJogo.Pausar` | pausa (o Esc já faz isso) | um botão de pausa na tela, se quiserem |

**Eventos — onde vocês ligam as telas para aparecer e sumir:**
| Evento | Quando dispara | Ligue aqui |
|---|---|---|
| **Ao Pausar** | entrou na pausa | mostrar a tela de pausa |
| **Ao Continuar** | saiu da pausa | esconder a tela de pausa |
| **Ao Morrer** | 2,5 s depois de o jogador cair | mostrar a tela de morte |
| **Ao Tentar De Novo** | o jogador renasceu | esconder a tela de morte |
| **Ao Vencer** | 1 s depois da conversa final do Coronel | mostrar a tela de vitória |

### Telas provisórias
Enquanto um evento **não tiver nenhuma tela ligada**, o FluxoDoJogo mostra uma **tela provisória** de texto
(ex.: "Severino caiu... [R] Tentar de novo [M] Menu principal"). Assim o fluxo já pode ser testado.
**Assim que vocês ligarem uma tela num evento, a provisória daquele evento some sozinha.**
(Também dá para desligar todas no campo **Usar Telas Provisorias** do FluxoDoJogo.)

### MenuPrincipal (`Assets/Scripts/Fluxo/MenuPrincipal.cs`)
Componente para a cena do menu principal, com os métodos `Jogar` e `Sair` para os botões.

---

## 2. Telas que precisam ser feitas

Textos tirados do roteiro (`Assets/Roteiro/Tutorial/`). Podem mudar o visual à vontade.

| Tela | Onde | Texto | Botões → método |
|---|---|---|---|
| **Menu principal** | cena própria `MenuPrincipal` | título do jogo: **BackCountry Bullet** | **Jogar** → `MenuPrincipal.Jogar` · **Sair** → `MenuPrincipal.Sair` |
| **Pausa** | cena do jogo | "Pausado" | **Continuar** → `Continuar` · **Recomeçar** → `ReiniciarTudo` (opcional) · **Menu principal** → `IrParaOMenuPrincipal` · **Sair** → `Sair` (opcional) |
| **Morte** | cena do jogo | "Severino caiu... mas cangaceiro não desiste." | **Tentar de novo** → `TentarDeNovo` · **Menu principal** → `IrParaOMenuPrincipal` |
| **Vitória** | cena do jogo | "A água voltou pra vila." | **Jogar de novo** → `ReiniciarTudo` · **Menu principal** → `IrParaOMenuPrincipal` |
| **Controles** (opcional) | menu principal e/ou pausa | lista abaixo | **Voltar** (só esconde a tela) |

**Lista de controles** (para a tela de Controles, se fizerem):
| Tecla | Ação |
|---|---|
| W A S D | andar |
| Shift | correr (só desarmado) |
| Mouse | olhar em volta |
| Q | sacar / guardar a arma |
| Botão esquerdo | atirar (5 tiros, recarrega sozinha) |
| Botão direito | esquivar (armado: passo para o lado; desarmado: mergulho invencível) |
| E | falar com NPCs / passar as falas |
| Esc | pausar |

---

## 3. Antes de começar: EventSystem e ordem das telas

### EventSystem (obrigatório para clicar nos botões)
Em **cada cena** que tem botões (jogo e menu principal) precisa existir **um** EventSystem:
1. **GameObject → UI → Event System**.
2. Se aparecer no Inspector um aviso sobre o "Input System", clique em **Replace with InputSystemUIInputModule**
   (o projeto usa o Input System novo).

### Ordem de desenho (Sort Order)
O jogo já desenha estas coisas na tela, cada uma num Canvas:
| Sort Order | O quê |
|---|---|
| 5–10 | efeitos de dano, barras de vida, munição |
| 20 | caixa de diálogo, dicas amarelas |
| 50 | telas provisórias do FluxoDoJogo |

Use **Sort Order 30** (ou mais) no Canvas das telas de vocês, para ficarem por cima do HUD e do diálogo.

---

## 4. Telas do jogo (pausa, morte, vitória)

A forma mais simples: **as telas ficam dentro do próprio FluxoDoJogo**. Assim elas vão junto para qualquer cena
e os botões já ficam ligados.

### Passo a passo
1. Abra o prefab **`Assets/Prefabs/Fluxo/FluxoDoJogo`** (dois cliques).
2. Com o FluxoDoJogo selecionado: **botão direito → UI → Canvas**. Nomeie **TelasDoJogo**.
   - **Canvas → Render Mode:** Screen Space - Overlay · **Sort Order:** 30
   - **Canvas Scaler → UI Scale Mode:** Scale With Screen Size · **Reference Resolution:** 1920 × 1080 · **Match:** 0.5
3. Dentro do TelasDoJogo, crie **três painéis** (**botão direito → UI → Panel**): **TelaDePausa**, **TelaDeMorte**, **TelaDeVitoria**.
   Coloque em cada um o texto e os botões da tabela da seção 2.
4. **Deixe os três painéis desligados** (desmarque a caixinha ao lado do nome no Inspector). Eles só aparecem pelos eventos.
5. **Ligue os botões:** no botão → **On Click ()** → **+** → arraste o **FluxoDoJogo** para o campo de objeto →
   escolha **FluxoDoJogo → (método)**. Ex.: botão "Tentar de novo" → `FluxoDoJogo.TentarDeNovo`.
6. **Ligue os eventos:** no componente **Fluxo Do Jogo**, em cada evento clique em **+**, arraste o painel e escolha
   **GameObject → SetActive (bool)**:
   | Evento | Painel | Caixinha do SetActive |
   |---|---|---|
   | Ao Pausar | TelaDePausa | ✔ (ligar) |
   | Ao Continuar | TelaDePausa | ☐ (desligar) |
   | Ao Morrer | TelaDeMorte | ✔ |
   | Ao Tentar De Novo | TelaDeMorte | ☐ |
   | Ao Vencer | TelaDeVitoria | ✔ |
7. Saia do prefab (seta **<** no topo da Hierarchy). Pronto: toda cena com o FluxoDoJogo já tem as telas.

> Se preferirem montar as telas **fora** do prefab (direto na cena), funciona igual, mas os eventos e botões
> precisam ser ligados de novo em cada cena.

---

## 5. Cena do menu principal

1. **File → New Scene** (Basic) e salve como **`Assets/Scenes/MenuPrincipal.unity`**.
   O nome precisa ser exatamente **MenuPrincipal** (é o que o FluxoDoJogo procura; dá para mudar no campo
   **Cena Do Menu Principal** do FluxoDoJogo).
2. Crie o **EventSystem** (seção 3).
3. Crie um **Canvas** e monte o menu: título, botão **Jogar**, botão **Sair** (e **Controles**, se quiserem).
4. Crie um objeto vazio **Menu** → **Add Component → Menu Principal**.
   - **Cena Do Jogo:** o nome da cena do jogo (hoje `SampleScene`; troque se a cena do jogo tiver outro nome).
5. Botão **Jogar** → On Click → arraste o objeto **Menu** → `MenuPrincipal.Jogar`.
   Botão **Sair** → `MenuPrincipal.Sair`.

O MenuPrincipal já solta o cursor e garante que o jogo não fica pausado ao voltar do jogo.

---

## 6. Build Profiles (lista de cenas)

Para uma cena abrir a outra, as duas precisam estar na lista:
1. **File → Build Profiles** → **Scene List**.
2. Arraste **MenuPrincipal** e a **cena do jogo** para a lista.
3. Deixe o **MenuPrincipal em primeiro** (índice 0): é a cena que abre quando o jogo inicia.

Se faltar alguma, o Console avisa ("a cena '...' não existe ou não está no Build Profiles") e nada acontece.

---

## 7. Regras importantes

- **Não mexam em `Time.timeScale`, no cursor nem em `AudioListener.pause` pelos botões.** O FluxoDoJogo já cuida disso;
  se outra coisa mudar, o jogo pode ficar travado ou com o cursor solto.
- Para **mostrar/esconder telas**, usem os **eventos** do FluxoDoJogo; para **ações**, os **métodos** dele.
- **Animações nas telas** (ex.: menu de pausa deslizando): no **Animator** da tela, coloquem **Update Mode: Unscaled Time**,
  senão elas não tocam com o jogo pausado.
- **Som de clique com o jogo pausado:** o FluxoDoJogo pausa todos os sons. Para o clique tocar na pausa,
  no **AudioSource** do som marquem **Ignore Listener Pause** (só dá por código: `audioSource.ignoreListenerPause = true`)
  ou toquem o som só fora da pausa.
- **Esc** já pausa e despausa. Não criem outro atalho para Esc.
- **Uma** tela de cada vez: a morte e a vitória já impedem a pausa.

---

## 8. Como testar

Na cena do jogo, com o FluxoDoJogo:
| Teste | Esperado |
|---|---|
| Apertar **Esc** | tempo para, cursor aparece, tela de pausa (ou provisória) |
| **Continuar** / Esc de novo | volta ao jogo, cursor preso de novo |
| Morrer (dica: abaixe a **Vida Maxima** do Jogador para testar rápido) | o jogador cai; 2,5 s depois, tela de morte |
| **Tentar de novo** | o jogador renasce no último ponto de retorno, com a vida cheia, e consegue andar e atirar |
| **Menu principal** | abre a cena MenuPrincipal (precisa estar no Build Profiles) |
| Vencer o boss | depois da conversa final do Coronel, tela de vitória |
| No menu: **Jogar** | abre a cena do jogo |

**Teclas das telas provisórias** (enquanto não houver tela ligada): **R** = Tentar de novo / Jogar de novo,
**M** = Menu principal, **Esc** = Continuar (na pausa).

---

## 9. Para quem quiser usar código (opcional)

Tudo acima funciona pelo Inspector. Para quem preferir código:
```csharp
FluxoDoJogo.Instancia.Pausar();            // e os outros métodos: Continuar, TentarDeNovo, ReiniciarTudo...
FluxoDoJogo.Instancia.EstadoAtual;          // Jogando, Pausado, Morto ou Vitoria
FluxoDoJogo.JogoParado;                     // true quando pausado, morto ou na vitória
FluxoDoJogo.AoMudarEstado += estado => { /* mostrar/esconder telas */ };
```
