# Guia de Montagem das Cenas — Tutorial

Este guia é para quem vai **montar as cenas no Unity** usando os prefabs que já estão prontos.
Ele segue o que **existe no jogo hoje** (prefabs e scripts), e não o roteiro completo: algumas coisas do roteiro
ainda não existem e estão listadas na seção 9, para ninguém perder tempo tentando montá-las.

> Leia as seções 1 a 6 uma vez antes de começar. Depois, monte cena por cena (seção 7).

## Sumário

1. [Visão geral](#1-visão-geral)
   - [Uma cena do Unity só, com 4 áreas](#uma-cena-do-unity-só-com-4-áreas)
2. [Onde está cada prefab](#2-onde-está-cada-prefab)
   - [Jogador](#jogador) · [Inimigos](#inimigos-assetsprefabsinimigos) · [NPCs](#npcs-assetsprefabsnpcs) · [Fluxo do jogo](#fluxo-do-jogo-assetsprefabsfluxo) · [Conversas](#conversas-não-precisa-mexer) · [O que aparece sozinho](#o-que-aparece-sozinho-não-coloque-na-cena)
3. [Os dois componentes que vocês vão preencher na cena](#3-os-dois-componentes-que-vocês-vão-preencher-na-cena)
   - [Componente NPC](#componente-npc-em-cada-npc) · [Componente Zona De Dialogo](#componente-zona-de-dialogo-nos-prefabs-de-zona)
4. [Regras de distância](#4-regras-de-distância-importante)
5. [NavMesh](#5-navmesh-o-chão-por-onde-os-inimigos-andam)
   - [O que é](#o-que-é) · [Como montar](#como-montar-uma-vez-por-cena) · [Quando refazer o Bake](#quando-refazer-o-bake) · [Cuidados](#cuidados)
6. [Fluxo do jogo: morte, checkpoints, pausa e vitória](#6-fluxo-do-jogo-morte-checkpoints-pausa-e-vitória)
   - [Como funciona](#como-funciona) · [O que colocar na cena](#o-que-colocar-na-cena) · [Onde colocar os pontos de retorno](#onde-colocar-os-pontos-de-retorno) · [Como testar](#como-testar)
7. [Montagem, cena por cena](#7-montagem-cena-por-cena)
   - [Cena 1 — Bar](#cena-1--bar-andar-câmera-sacar-e-atirar)
   - [Cena 2 — Praça da vila](#cena-2--praça-da-vila-esquiva-armado)
   - [Cena 3 — Portão da NimbusCorp](#cena-3--portão-da-nimbuscorp-esquiva-desarmado)
   - [Cena 4 — Torre de resfriamento](#cena-4--torre-de-resfriamento-boss)
8. [Checklist final](#8-checklist-final)
   - [Problemas comuns](#problemas-comuns)
9. [O que o roteiro cita, mas ainda NÃO existe](#9-o-que-o-roteiro-cita-mas-ainda-não-existe-não-montar)

---

## 1. Visão geral

O tutorial tem 4 partes, uma depois da outra. Cada parte ensina uma mecânica com um NPC e termina com uma luta:

| Parte | Lugar | NPC (quem ensina) | Mecânica | Inimigos |
|---|---|---|---|---|
| Cena 1 | Bar | **NPCTutorial1** (Seu Zé) | andar, câmera, sacar a arma (Q), atirar | **2× InimigoTutorial1** (robô azul) |
| Cena 2 | Praça da vila | **NPCTutorial2** (Véio Bento) | esquiva **armado** (botão direito) | **3× InimigoTutorial2** (robô roxo) |
| Cena 3 | Portão da NimbusCorp | **NPCTutorial3** (Dona Socorro) | esquiva **desarmado** (mergulho) | **1× InimigoTutorial3** (robô laranja) |
| Cena 4 | Torre de resfriamento | **NPCBoss** (Coronel Silício) | usar tudo | **1× Boss** (Super Mecha) |

### Uma cena do Unity só, com 4 áreas
**Ainda não existe troca de cena** (carregar a próxima cena do Unity quando uma parte termina).
Por isso, **monte as 4 partes na mesma cena do Unity** (hoje é `Assets/_Project/Scenes/Caatinga.unity`),
como 4 áreas lado a lado, ligadas por caminhos. O jogador sai do bar e anda até a praça, o portão e a torre.

Sugestão de organização na **Hierarchy** (objetos vazios só para agrupar, com **Create Empty**):
```
Cena1_Bar
Cena2_Praca
Cena3_Portao
Cena4_Torre
```
Coloque cada NPC, inimigo e zona dentro do grupo da sua parte.

---

## 2. Onde está cada prefab

### Jogador
| Prefab | O que é |
|---|---|
| `Assets/Prefabs/Jogador/Jogador.prefab` | O Severino. **Já vem com tudo:** câmeras (Cinemachine), mira, barra de vida, munição, efeitos de dano, morte e a tag `Player`. Coloque **um só** na cena. |

> Não coloque outra câmera na cena: a do Jogador já é a câmera principal.

### Inimigos (`Assets/Prefabs/Inimigos/`)
| Prefab | O que é | Vida |
|---|---|---|
| `InimigoTutorial1.prefab` | **Robô azul.** Anda até o jogador e dá soco de perto. | 150 |
| `InimigoTutorial2.prefab` | **Robô roxo.** Fica longe e arremessa bolas de energia. | 150 |
| `InimigoTutorial3.prefab` | **Robô laranja.** Grande; soca e faz uma **investida** que só se desvia mergulhando desarmado. Depois da investida fica cansado e leva mais dano. | 250 |
| `Boss/Boss.prefab` | **Super Mecha** (o robozão do Coronel). A barra de vida dele aparece embaixo da tela. | 800 |
| `InimigoTutorial2_ZonaAlerta.prefab` | Zona do aviso do robô roxo ("Visitante não autorizado detectado..."). | — |
| `InimigoTutorial3_ZonaAlerta.prefab` | Zona do aviso do robô laranja ("Senhor, esta é uma área restrita!..."). | — |

**Todos os inimigos percebem o jogador a 15 m** e atacam a partir daí, **mesmo através de paredes** (eles medem só a distância). Eles **já começam ativos**: não esperam conversa nenhuma.
Por isso as distâncias da seção 4 são importantes.

### NPCs (`Assets/Prefabs/NPCs/`)
| Prefab | Personagem | Já vem configurado com |
|---|---|---|
| `NPCTutorial1.prefab` | **Seu Zé**, dono do bar | chamado de longe + conversa do balcão + agradecimento depois da luta |
| `NPCTutorial1_ZonaJanela.prefab` | Zona da janela do bar | a conversa em que o Seu Zé pede para derrotar o robô azul |
| `NPCTutorial2.prefab` | **Véio Bento**, ex-vigia do datacenter | chamado de longe + conversa da praça + agradecimento |
| `NPCTutorial3.prefab` | **Dona Socorro**, ex-cangaceira | chamado de longe + conversa do portão + aviso sobre o robozão do Coronel |
| `NPCBoss.prefab` | **Coronel Silício** | conversa de abertura (começa sozinha) + conversa final (começa sozinha quando o boss morre) |
| `NPCMorador.prefab` | Morador da vila | uma fala sorteada cada vez que o jogador aperta E |

Os NPCs têm animação parada (respirando), menos o **NPCBoss**, que fica parado.

### Fluxo do jogo (`Assets/Prefabs/Fluxo/`)
| Prefab | O que é |
|---|---|
| `FluxoDoJogo.prefab` | Controla pausa, morte ("Tentar de novo"), vitória e a ida para o menu. Coloque **um** na cena. Já vem ligado à conversa final do Coronel para saber quando o jogador venceu. |
| `PontoDeRetorno.prefab` | Checkpoint: quando o jogador passa por ele, é ali que renasce se morrer. Coloque **um no começo de cada parte**. Ele renasce olhando para a **seta azul** do ponto (gire o objeto para escolher). |

As telas (pausa, morte, vitória, menu principal) são feitas por outra pessoa, seguindo `Assets/02_GuiaDoMenu.md`.
Enquanto não estiverem prontas, o FluxoDoJogo mostra telas provisórias de texto para testar.

### Conversas (não precisa mexer)
Ficam em `Assets/Roteiro/Tutorial/Dialogos/`, uma pasta por NPC. Já estão ligadas nos prefabs.
O texto completo de cada cena está em `Assets/Roteiro/Tutorial/Cena1_Bar.md` … `Cena4_Torre.md`.

### O que aparece sozinho (não coloque na cena)
- **Caixa de diálogo, dicas amarelas e "[E] Falar com ..."**: criadas na primeira vez que um NPC precisa.
- **Câmera da conversa** (a diagonal mostrando jogador e NPC): criada sozinha.
- **Barras de vida dos inimigos e do boss**: criadas pelos próprios prefabs.

---

## 3. Os dois componentes que vocês vão preencher na cena

Quase tudo vem pronto. Na cena só falta **ligar cada NPC aos inimigos dele**, porque um prefab não consegue
guardar referência a objetos da cena.

### Componente **NPC** (em cada NPC)
Clique no NPC na Hierarchy → componente **NPC** → **Conversas**. Cada item da lista é uma conversa, em ordem:

| Campo | Para que serve | O que fazer na cena |
|---|---|---|
| **Dialogo** | a conversa | já vem preenchido |
| **Exigir Conversa** | só libera depois de outra conversa | já vem preenchido onde precisa |
| **Liberar Quando Morrerem** | só libera depois que **todos** estes inimigos morrerem | **arrastar os inimigos da parte dele** (ver cada cena) |
| **Ir Para O Ponto** | quando a conversa é liberada, o NPC vai para este ponto | opcional (ex.: Seu Zé aparecer na porta do bar) |
| **Comecar Sozinha** / **Distancia Para Comecar** | começa sem apertar E / a que distância | já vem preenchido no NPCBoss |

Como arrastar para **Liberar Quando Morrerem**: abra a lista, clique no **+** uma vez para cada inimigo e
arraste o inimigo **da Hierarchy** (não do Project) para cada campo.

> **Se esquecer de preencher**, o agradecimento fica disponível logo depois da primeira conversa,
> antes da luta. Esse é o erro mais comum.

### Componente **Zona De Dialogo** (nos prefabs de zona)
É uma caixa invisível (aparece **amarela transparente** na aba Scene) que começa uma conversa quando o jogador entra.
Os prefabs de zona já vêm com a conversa ligada. Só posicione e, se quiser, mude o tamanho no **Box Collider → Size**.
A zona funciona uma vez só.

---

## 4. Regras de distância (importante)

Use a régua da Scene (ou a posição no Inspector) para respeitar estas medidas:

| Regra | Por quê |
|---|---|
| Inimigos a **pelo menos 25 m** do NPC da mesma parte e **20 m** de qualquer zona de conversa | eles percebem o jogador a 15 m (até através de paredes); se estiverem perto, atacam durante a conversa |
| Zona de alerta **entre o NPC e os inimigos**, a **17–20 m** dos inimigos | o aviso do robô toca antes de ele perceber o jogador |
| Partes diferentes a **pelo menos 35 m** uma da outra | para os inimigos de uma parte não puxarem briga na outra |
| O jogador fala com o NPC a **2,5 m** dele | é quando aparece "[E] Falar com ..." |
| O NPC chama o jogador de longe a **10 m** | o "Ei, tu! Chega aqui!" aparece como legenda |
| O caminho do jogador até os inimigos **passa pela zona de alerta** | se ele contornar a zona, o aviso não toca (deixe a zona larga, de parede a parede do caminho) |

---

## 5. NavMesh (o chão por onde os inimigos andam)

Os inimigos usam o **NavMesh** para andar até o jogador. Sem ele, **ficam parados** (e o Console avisa).

### O que é
É um "mapa" azul gerado a partir do chão e dos obstáculos da cena, mostrando onde os inimigos podem andar.
Ele é **gerado para cada cena** e precisa ser **gerado de novo** sempre que o chão ou os obstáculos mudarem.

### Como montar (uma vez por cena)
1. Na Hierarchy: **botão direito → Create Empty**, nomeie **NavMesh**.
2. **Add Component → NavMesh Surface**.
3. Deixe:
   - **Agent Type:** Humanoid (todos os inimigos usam este)
   - **Collect Objects:** All Game Objects
   - **Use Geometry:** Physics Colliders (assim os personagens não abrem buracos no chão azul)
4. Clique em **Bake**. Deve aparecer uma camada **azul** sobre o chão.

### Quando refazer o Bake
- Mudou o chão, as paredes, o balcão, caixas ou qualquer obstáculo → **Bake de novo**.
- Inimigo parado sem se mexer → confira se ele está **em cima da parte azul**. Se não estiver, mova-o ou refaça o Bake.

### Cuidados
- O **Boss é grande** (raio de 1 m). Deixe a arena da Cena 4 **aberta**, sem corredores estreitos, senão ele trava.
- Escadas e rampas precisam ser suaves; degraus altos cortam o caminho azul.
- O NavMesh **não vira prefab**: cada cena tem o seu e o seu Bake.

---

## 6. Fluxo do jogo: morte, checkpoints, pausa e vitória

### Como funciona
O **FluxoDoJogo** é o objeto que controla o "estado" do jogo:

| Estado | Quando | O que acontece |
|---|---|---|
| **Jogando** | normal | tudo funciona |
| **Pausado** | o jogador apertou **Esc** | o tempo para, o cursor aparece, a tela de pausa abre; **Esc** de novo volta |
| **Morto** | a vida do jogador zerou | o jogador cai (animação de morte); 2,5 s depois abre a tela de morte |
| **Vitória** | terminou a conversa final do Coronel, depois do boss | abre a tela de vitória |

**Tentar de novo** (na tela de morte): o jogador **renasce no último ponto de retorno** por onde passou, com a vida cheia.
- Inimigos que ainda estão **vivos** recuperam a vida toda; os **mortos continuam mortos**.
- Conversas **já feitas não se repetem**.
- Se o jogador ainda não passou por nenhum ponto de retorno, ele renasce **onde o jogo começou**.

**As telas** (pausa, morte, vitória e menu principal) são feitas por outra pessoa, seguindo `Assets/02_GuiaDoMenu.md`.
Enquanto não estiverem prontas, aparecem **telas provisórias** de texto:
| Tela provisória | Teclas |
|---|---|
| Pausa | **Esc** = continuar · **M** = menu principal |
| Morte ("Severino caiu... mas cangaceiro não desiste.") | **R** = tentar de novo · **M** = menu principal |
| Vitória ("A água voltou pra vila.") | **R** = jogar de novo (recomeça a cena) · **M** = menu principal |

> **M** só funciona depois que a cena `MenuPrincipal` existir e estiver no Build Profiles (ver o guia do menu).

### O que colocar na cena
| Objeto | Prefab | Quantos |
|---|---|---|
| Fluxo do jogo | `Prefabs/Fluxo/FluxoDoJogo` | **um** para a cena toda (em qualquer lugar; é invisível) |
| Ponto de retorno | `Prefabs/Fluxo/PontoDeRetorno` | **um no começo de cada parte**, menos no bar (o começo do jogo já é o primeiro) |

- **Não precisa ligar nada** no FluxoDoJogo: ele acha o jogador sozinho e já vem ligado à conversa final do Coronel (campo **Conversa Da Vitoria** = `NPCBoss_Depois`).
- **Se esquecerem o FluxoDoJogo**, ele é criado sozinho no Play e o Console avisa
  ("não há um FluxoDoJogo nesta cena; criei um provisório"). Pausa e morte funcionam, mas **a tela de vitória não aparece**: coloque o prefab.

### Onde colocar os pontos de retorno
O PontoDeRetorno é uma caixa invisível (**verde transparente** na aba Scene) com uma **seta azul**.
Quando o jogador atravessa a caixa, aquele vira o lugar de renascer, **olhando para onde a seta aponta**.

Regras:
- Coloque **no caminho por onde o jogador passa** ao chegar numa parte nova, ocupando a largura do caminho
  (ajuste em **Box Collider → Size**).
- **Longe dos inimigos**: a mais de 20 m de qualquer inimigo, senão o jogador renasce já levando dano.
- **Gire o ponto** (ferramenta **E**) para a seta azul apontar para onde o jogador deve seguir.
- Na **torre**: a mais de 30 m do Coronel, senão o jogador renasce dentro do alcance do boss.

### Como testar
1. Para morrer rápido: no **Jogador** da cena, abaixe a **Vida Maxima** (componente **Vida**) para 10 — **lembre de voltar para 100**.
2. **Play**, passe por um ponto de retorno e deixe um inimigo te derrubar.
3. Esperado: o Severino cai → 2,5 s depois, tela de morte → **R** → ele aparece no ponto de retorno, com a vida cheia, andando e atirando normalmente.
4. **Esc** a qualquer momento: o jogo pausa; **Esc** de novo volta.

## 7. Montagem, cena por cena

> Em todas as cenas: arraste os prefabs do **Project** para a **Hierarchy** (ou direto na Scene), posicione com a
> ferramenta de mover (**W**) e gire com a de girar (**E**). Os personagens precisam ficar **com os pés no chão**.

### Cena 1 — Bar (andar, câmera, sacar e atirar)

**O que colocar:**
| Objeto | Prefab | Onde |
|---|---|---|
| Jogador | `Prefabs/Jogador/Jogador` | **dentro do bar**, perto da entrada (o jogo começa aqui) |
| Fluxo do jogo | `Prefabs/Fluxo/FluxoDoJogo` | em qualquer lugar (é invisível); **um só para a cena toda** |
| Seu Zé | `Prefabs/NPCs/NPCTutorial1` | **atrás do balcão**, virado para o salão, a uns 6–10 m do jogador (ele chama de longe) |
| Zona da janela | `Prefabs/NPCs/NPCTutorial1_ZonaJanela` | **na frente da janela**, dentro do bar, longe do balcão (não pode cobrir o lugar onde se fala com o Seu Zé) |
| Robô azul 1 | `Prefabs/Inimigos/InimigoTutorial1` | **fora do bar**, na rua, visível pela janela, a **20 m ou mais da zona da janela** (e 25 m ou mais do balcão) |
| Robô azul 2 | `Prefabs/Inimigos/InimigoTutorial1` | também na rua, a uns 5–8 m do primeiro |

> O começo do bar já é o primeiro ponto de retorno (o lugar onde o Jogador começa); não precisa de PontoDeRetorno aqui.

**Ligações na cena:**
1. **Seu Zé → Conversas → Element 1** (`NPCTutorial1_DepoisDaLuta`) → **Liberar Quando Morrerem**: arraste os **2 robôs azuis**.
2. (Opcional) Seu Zé aparecer na porta depois da luta: crie um objeto vazio `Ponto_PortaDoBar` na porta,
   virado para a rua, e arraste para **Element 1 → Ir Para O Ponto**.
3. A **zona da janela** acha o Seu Zé sozinha (não precisa preencher **Quem Fala**).

**Como deve funcionar (teste):**
1. O jogo começa no bar; o Seu Zé chama de longe e aparece a dica de **WASD / Shift / mouse**.
2. No balcão, **E** → conversa (termina com "Chega ali na janela").
3. Na janela, a conversa começa sozinha → no fim, dicas de **Q** e **atirar**.
4. O jogador sai e luta com os 2 robôs azuis.
5. Com os dois mortos, volta ao Seu Zé (ou à porta) → **E** → agradecimento.

---

### Cena 2 — Praça da vila (esquiva armado)

**O que colocar:**
| Objeto | Prefab | Onde |
|---|---|---|
| Ponto de retorno | `Prefabs/Fluxo/PontoDeRetorno` | **na entrada da praça**, no caminho que vem do bar, seta azul apontando para a praça |
| Véio Bento | `Prefabs/NPCs/NPCTutorial2` | **na praça**, perto do poço, virado para o caminho que vem do bar |
| Moradores (2 a 4, opcional) | `Prefabs/NPCs/NPCMorador` | espalhados pela praça, perto das casas |
| Zona do alerta | `Prefabs/Inimigos/InimigoTutorial2_ZonaAlerta` | **na saída da praça**, no caminho para o portão, entre o Bento e os robôs |
| Robô roxo 1, 2 e 3 | `Prefabs/Inimigos/InimigoTutorial2` | **depois da zona**, a 17–20 m dela, separados uns 6–10 m entre si (eles atiram de longe; espalhe para o jogador ter que esquivar de vários lados) |

**Ligações na cena:**
1. **Véio Bento → Conversas → Element 1** (`NPCTutorial2_DepoisDaLuta`) → **Liberar Quando Morrerem**: arraste os **3 robôs roxos**.
2. A zona do alerta não precisa de ligação. Ajuste o **Box Collider → Size X** para cobrir o caminho de lado a lado.

**Como deve funcionar (teste):**
1. Chegando perto, o Bento chama de longe.
2. **E** → conversa → no fim, dica da **esquiva armada**.
3. Moradores: **E** em qualquer um → uma fala sorteada.
4. Ao atravessar a zona, o robô roxo dá o aviso e a luta começa.
5. Com os 3 mortos, **E** no Bento → agradecimento.

---

### Cena 3 — Portão da NimbusCorp (esquiva desarmado)

**O que colocar:**
| Objeto | Prefab | Onde |
|---|---|---|
| Ponto de retorno | `Prefabs/Fluxo/PontoDeRetorno` | **no caminho entre a praça e o portão**, antes da Dona Socorro, seta azul apontando para o portão |
| Dona Socorro | `Prefabs/NPCs/NPCTutorial3` | **antes do portão**, encostada num muro, virada para quem chega da praça |
| Zona do alerta | `Prefabs/Inimigos/InimigoTutorial3_ZonaAlerta` | **logo depois do portão**, ocupando toda a largura da passagem |
| Robô laranja | `Prefabs/Inimigos/InimigoTutorial3` | **depois da zona**, a 17–20 m dela, num **espaço aberto e comprido** (a investida dele corre em linha reta e precisa de espaço) |

**Ligações na cena:**
1. **Dona Socorro → Conversas → Element 1** (`NPCTutorial3_DepoisDaLuta`) → **Liberar Quando Morrerem**: arraste o **robô laranja**.

**Como deve funcionar (teste):**
1. A Dona Socorro chama de longe → **E** → conversa → no fim, as dicas **"Q para desarmar e botão direito para desviar"** e **"robô cansado: saque e atire"**.
2. Depois do portão, o robô laranja dá o aviso e a luta começa.
3. Investida: desarmado + botão direito = mergulho invencível. Se acertar, o jogador é arremessado e cai.
4. Com o robô morto, **E** na Dona Socorro → ela avisa sobre o robozão do Coronel.

---

### Cena 4 — Torre de resfriamento (boss)

**O que colocar:**
| Objeto | Prefab | Onde |
|---|---|---|
| Ponto de retorno | `Prefabs/Fluxo/PontoDeRetorno` | **na entrada da torre**, a **mais de 30 m do Coronel** (senão o jogador renasce já dentro do alcance do boss), seta azul apontando para a arena |
| Boss (Super Mecha) | `Prefabs/Inimigos/Boss/Boss` | **no centro da arena**, virado para a entrada |
| Coronel | `Prefabs/NPCs/NPCBoss` | **no alto, atrás do boss**: de 5 a 10 m atrás dele e uns 5 m acima do chão (num camarote, sacada ou plataforma), virado para a arena |

**Ligações na cena:**
1. **Coronel → Conversas → Element 1** (`NPCBoss_Depois`) → **Liberar Quando Morrerem**: arraste o **Boss**.

**Regras desta cena:**
- A conversa de abertura do Coronel começa **sozinha** quando o jogador chega a **28 m dele** (campo **Distancia Para Comecar** do Element 0).
  Com o Coronel até 10 m atrás do boss, isso acontece antes de o boss perceber o jogador (15 m). Se mudar a posição do Coronel, ajuste esse valor.
- A conversa final começa **sozinha** quando o boss morre.
- A barra de vida do boss aparece **embaixo da tela** e some durante as conversas.
- Arena **aberta**, sem obstáculos no meio (o boss é grande e corre em investida).

**Como deve funcionar (teste):**
1. Entrando na torre, a câmera mostra o jogador e o Coronel lá em cima, e a conversa começa sozinha → dica "Use tudo o que aprendeu e derrote o Mecha".
2. Luta. Fora do atordoamento o boss quase não leva dano; depois de cada investida ele fica atordoado e leva muito dano.
3. Boss derrotado → conversa final do Coronel começa sozinha → ao terminar, aparece a tela de vitória.
4. Teste também morrer: aparece a tela de morte; **Tentar de novo** faz o jogador renascer na entrada da torre com a vida cheia, e o boss recupera a vida.

---

## 8. Checklist final

- [ ] **Um** Jogador na cena, no começo do bar.
- [ ] **Um** FluxoDoJogo na cena.
- [ ] Um PontoDeRetorno no começo da praça, do portão e da torre, longe dos inimigos, com a seta azul para a frente.
- [ ] Nenhuma outra câmera além da que vem no Jogador.
- [ ] NavMesh com **Bake** feito depois de o cenário ficar pronto; todos os inimigos em cima da área azul.
- [ ] Cada NPC com **Liberar Quando Morrerem** preenchido (Seu Zé: 2 azuis; Bento: 3 roxos; Socorro: 1 laranja; Coronel: Boss).
- [ ] Zonas no caminho do jogador, cobrindo a passagem inteira.
- [ ] Distâncias da seção 4 respeitadas.
- [ ] Cena salva (**Ctrl+S**).

### Problemas comuns
| Problema | Causa provável |
|---|---|
| Agradecimento aparece antes da luta | **Liberar Quando Morrerem** vazio |
| Inimigo não anda | fora do NavMesh ou faltou **Bake** |
| Inimigo ataca durante a conversa | inimigo perto demais do NPC (menos de 25 m) |
| Aviso do robô não aparece | o jogador passou por fora da zona; aumente o **Size** |
| Conversa da janela não começa | o jogador ainda não falou com o Seu Zé no balcão (ela exige essa conversa antes) |
| "[E] Falar" não aparece | o NPC não tem conversa liberada agora (é esperado entre a conversa e o fim da luta) |
| Morreu e não apareceu tela nenhuma | falta o FluxoDoJogo na cena (o Console avisa) |
| "Tentar de novo" leva para o começo do jogo | o jogador não passou por nenhum PontoDeRetorno, ou o ponto está fora do caminho |
| Renasce levando dano | o PontoDeRetorno está perto demais de inimigos |
| Venceu o boss e a tela de vitória não apareceu | o FluxoDoJogo foi criado sozinho (sem o prefab); coloque o prefab na cena |
| NPC rosa | material sem converter para URP: selecione o material → **Edit → Rendering → Materials → Convert Selected Built-in Materials to URP** |

---

## 9. O que o roteiro cita, mas ainda NÃO existe (não montar)

| No roteiro | Situação |
|---|---|
| Troca de cena entre as partes | não existe: por isso tudo fica numa cena só (os PontoDeRetorno fazem o papel de "começo de fase") |
| Tela de morte, tela de vitória, pausa e menu principal | o **fluxo** já existe (FluxoDoJogo) e mostra telas provisórias de texto; as telas de verdade são feitas seguindo `Assets/02_GuiaDoMenu.md` |
| Cerveja do Seu Zé como cura | só na fala; não existe item nem cura |
| Atirar nos canos e a água voltando para a vila | não existe; a conversa final do Coronel é o fim |
| Coronel numa cabine/camarote de vidro | não há modelo de camarote; posicione o NPCBoss num lugar alto qualquer |
| Portão explodindo / abrindo | não existe; deixe a passagem aberta |
| Olho mecânico da Dona Socorro, braço e perna mecânicos | ainda não colocados nos modelos |
| Inimigos esperando o fim da conversa para atacar | não existe; os inimigos ficam ativos desde o início (use as distâncias da seção 4) |
