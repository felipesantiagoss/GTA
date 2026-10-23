# A CALOTEIRA DO OESTE
## Temporada 1 — 15 episódios · Roteiro de produção para o Diretor de Cenas

---

# PARTE 0 — COMO LER ESTE ARQUIVO

> **Esta seção é para o Codex. Leia inteira antes de gerar qualquer arquivo de cena.**

Este documento é o roteiro completo da novela, escrito para ser transformado em arquivos de cena do Diretor de Cenas. Cada cena já vem com local, hora, clima, elenco, câmera, duração e falas.

## 0.1 A estrutura de cada cena

```
### EPnn_Cnn · NOME DA CENA
`local:` CODIGO_DO_LOCAL · `hora:` 17:00 · `clima:` limpo
`elenco:` PERSONAGEM_1, PERSONAGEM_2 · `cam:` auto · `dur:` 24s · `gravar:` SIM

**Ação:** descrição do que acontece fisicamente na cena.

**Diálogo:**
| # | Quem | Texto | Dur | Gesto |
```

> ⚠️ O bloco acima é **só o modelo do formato**, não é uma cena real. As cenas de verdade começam na Parte 5.

Significado dos campos:

| Campo | O que é |
|---|---|
| `EPnn_Cnn` | ID da cena, por exemplo `EP01_C04`. Vira o nome do arquivo: `EP01_C04.json` e `EP01_C04.mp4` |
| `local` | Código do local no catálogo (Parte 2). Eu marco no jogo, você usa o código |
| `hora` | Hora do jogo, congelada |
| `clima` | Clima travado |
| `elenco` | Códigos dos personagens no catálogo (Parte 1) |
| `cam` | `auto` = corte automático seguindo quem fala. Ou lista de planos |
| `dur` | Duração estimada, para eu saber quanto gravar |
| `gravar` | `SIM` = gera arquivo de vídeo. `NÃO` = cena de apoio, reaproveitada de outra |

## 0.2 🔴 A regra mais importante: NARRAÇÃO não é FALA

O roteiro tem dois tipos de texto, e eles se comportam de forma **completamente diferente**:

| Tipo | O que é | Boca mexe? | O personagem... |
|---|---|---|---|
| **NARRAÇÃO** | Voz da Djanira contando a história por cima da imagem | **NÃO** | só age: anda, olha, trabalha |
| **DIÁLOGO** | Personagens conversando dentro da cena | **SIM** | fala com a boca mexendo e gesto |

**Cerca de 60% do texto desta novela é NARRAÇÃO.** Nessas cenas você não precisa de animação facial nenhuma — só da ação e da câmera. Isso simplifica muito o seu trabalho e evita o erro de deixar a personagem mexendo a boca enquanto a voz dela narra em off.

Nos blocos abaixo, o tipo está sempre declarado. Nunca presuma.

## 0.3 O que você faz com este arquivo

1. **Leia tudo.** Monte os catálogos da Parte 1 (personagens) e da Parte 2 (locais).
2. Me diga **quantos locais eu preciso marcar no jogo** e em que ordem convém marcar.
3. Me diga **quais figurinos você monta sozinho** e quais preciso montar no Menyoo.
4. Comece gerando **uma cena só** — a `EP01_C04` — para eu testar o ciclo inteiro.
5. Depois do meu "ok", gere o resto do Episódio 1.
6. Só então siga para os episódios 2 em diante.

**Não gere os 15 episódios de uma vez.** Um episódio por vez, com teste meu no meio.

## 0.4 Avisos técnicos — leia antes de planejar

Três coisas neste roteiro são difíceis no GTA V. Trate cada uma como está escrito:

**1. Interiores de boate e cassino.** No modo história, os interiores da Bahama Mamas e do Diamond Casino **não abrem** por padrão. Existe um mod conhecido chamado **Open All Interiors** (gta5-mods.com) que destrava interiores no modo história. Verifique, me proponha a instalação se for necessário, e **tenha sempre um plano B gravando só a fachada**. Marquei no roteiro quais cenas dependem disso.

**2. Briga de verdade.** Combate com a inteligência artificial do jogo é imprevisível e não repete igual duas vezes. **Não use combate real.** Use animações de luta encadeadas, que rodam sempre igual.

**3. A queda do avião.** É a cena de abertura e a mais importante da série. Precisa de invencibilidade ligada e de teste cuidadoso. Trate como uma etapa própria, não como mais uma cena.

---

# PARTE 1 — O ELENCO

> Monte estes catálogos primeiro. Todos os códigos em MAIÚSCULA são os nomes que uso nas cenas.

## DJANIRA_POBRE
**Mulher branca, cabelo ruivo preso em rabo de cavalo, olhos azuis.**
Modelo: `mp_f_freemode_01`

| Item | Valor |
|---|---|
| Roupa | Short curto, camisa simples, chinelo. Cabelo preso, sem maquiagem |
| Expressão padrão | `normal` |
| Andar | `normal` |
| Episódios | 1 a 10 |

## DJANIRA_RICA
**Mesma mulher, mesmo rosto.** Só muda roupa, cabelo e postura.
Modelo: `mp_f_freemode_01`

| Item | Valor |
|---|---|
| Roupa | Terninho ou vestido caro, salto, joias, cabelo solto |
| Expressão padrão | `convencido` |
| Andar | `na passarela` |
| Episódios | 11 a 15 |

> ⚠️ **O rosto precisa ser idêntico ao da DJANIRA_POBRE.** É isso que faz a Dinjer não reconhecê-la no Episódio 12. Salve os dois figurinos no mesmo personagem.

## DINJER
**Mulher negra, cabelo afro volumoso, batom forte.**
Modelo: `mp_f_freemode_01`

| Item | Valor |
|---|---|
| Roupa | **Uniforme de aeromoça** — blazer, saia lápis, lencinho no pescoço, salto. **Nunca muda, a série inteira** |
| Expressão padrão | `convencido` |
| Andar | `na passarela` |
| Episódios | 1 a 15 |

> 💡 O uniforme é a piada e a tragédia ao mesmo tempo. Ela não é rica — ela só trabalha perto de gente rica. E termina a temporada exatamente como começou: de uniforme, servindo.

## DONA_IRACI
**Senhora idosa.** Modelo de pedestre do jogo, mulher idosa.

| Item | Valor |
|---|---|
| Roupa | Simples, de dona de casa. Sacola na mão |
| Expressão padrão | `machucado` (ela está doente) |
| Andar | `normal`, devagar |
| Episódios | 1 a 8 |

## O_CHEFE
**Homem branco, meia-idade, terno caro, cabelo grisalho.**
Modelo de pedestre em terno, ou um modelo de ator do jogo.

| Item | Valor |
|---|---|
| Roupa | Terno escuro caro |
| Expressão padrão | `convencido` |
| Andar | `playboy` |
| Episódios | 6, 7, 13, 15 |

## CAPANGA_1 a CAPANGA_4
NPCs de terno. Andar `durão`. Expressão `com raiva`. Figurantes, sem fala.

## COLEGA_1 a COLEGA_3
Colegas de trabalho da Dinjer no aeroporto. Homens, roupa casual. Episódio 1 apenas.

## ATENDENTE
NPC genérico. Aparece no banco (Eps 9 e 11). Sem rosto definido, sempre de costas ou em plano aberto.

---

# PARTE 2 — OS LOCAIS

> Eu marco cada um no jogo com a tecla de marcar local. Você usa o código.
> **Me diga a ordem em que convém eu marcar**, agrupando por região para eu não ficar atravessando o mapa.

| Código | Lugar no GTA | Onde fica | Usado em |
|---|---|---|---|
| `ROCA_GRAPESEED` | Grapeseed, no meio da plantação | Canto de cima à direita do mapa, acima do Alamo Sea | Eps 1,2,3,5,8,9 |
| `CASA_VO` | Grapeseed, frente de uma casa de fazenda | Mesma região | Eps 1,2,3,4,5,8,9 |
| `HOSPITAL_SANDY` | Clínica de Sandy Shores, entrada | À direita do Alamo Sea | Eps 1,4 |
| `CASSINO_FORA` | Diamond Casino, fachada | East Vinewood | Ep 1 |
| `AEROPORTO_HANGAR` | LS Intl. Airport, área dos hangares | Canto de baixo à esquerda | Eps 1,7 |
| `AEROPORTO_TERMINAL` | LS Intl. Airport, frente do terminal | Mesma região | Eps 1,4 |
| `AEROPORTO_AR` | Ponto no ar, saindo do aeroporto | — | Ep 7 |
| `CANAL_LS` | Rio Los Santos, canal de concreto | Faixa cinza no lado direito da cidade, entre La Mesa e Murrieta Heights | Eps 1,7,8 |
| `BOATE_FORA` | Bahama Mamas, calçada da entrada | Hawick, parte de cima da cidade | Eps 2,6,12 |
| `BOATE_DENTRO` | Interior da boate ⚠️ | Ver aviso 0.4 | Eps 2,6,12,13 |
| `BANCO_MAZE` | Maze Bank Tower, calçada da frente | Pillbox Hill, centro | Eps 9,11 |
| `IGREJA_CHAPARRAL` | Igrejinha de Great Chaparral, cemitério | Região verde no meio do mapa | Ep 9 |
| `GALPAO_CYPRESS` | Cypress Flats, entre galpões | Lado direito da cidade | Ep 7 |
| `MANSAO_CHEFE` | Vinewood Hills, mansão com vista | Morro acima da cidade | Ep 15 |
| `MANSAO_DJANIRA` | Rockford Hills, portão de mansão | Bairro rico, lado esquerdo | Eps 11,14 |
| `CENTRO_LEGION` | Legion Square, entre os arranha-céus | Centro da cidade | Eps 6,10 |
| `PRAIA_DELPERRO` | Del Perro Beach, banco de praça | Costa esquerda | Ep 10 |
| `RUA_STRAWBERRY` | Strawberry, ponto de ônibus | Abaixo do centro | Ep 10 |
| `VIADUTO_LAPUERTA` | La Puerta, embaixo do viaduto | Abaixo de Vespucci | Ep 10 |
| `ESTRADA_SENORA` | Grand Senora Desert, estrada vazia | Faixa amarelada no centro do mapa | Eps 3,4,8,9 |
| `CASA_DINJER` | Casa simples em Sandy Shores | À direita do Alamo Sea | Ep 3 |

**Total: 21 marcas.** Agrupadas por região:
- **Grapeseed / Sandy Shores / deserto:** 6 marcas
- **Aeroporto e canal:** 4 marcas
- **Centro e Vinewood:** 6 marcas
- **Bairros ricos e praia:** 5 marcas

---

# PARTE 3 — VOCABULÁRIO

## Câmeras
`LONGE` · `DOIS` · `MEDIA` · `PERTO` · `BAIXA` · `ALTA` · `ATRAS` · `OLHOS` · `auto`

## Gestos de fala (só em DIÁLOGO)
`neutro` · `explicando` · `negando` · `implorando` · `irritado`

## Expressões
`normal` · `chateado` · `feliz` · `machucado` · `com raiva` · `estressado` · `convencido`

## Andar
`normal` · `na passarela` · `playboy` · `durão` · `criminoso`

---

# PARTE 4 — A HISTÓRIA EM CINCO LINHAS

Djanira tem 18 anos, mora com a avó numa casa de favor em Vila Oeste e trabalha na roça. Empresta os mil reais do aluguel para a amiga de infância Dinjer, que voltou dizendo que virou aeromoça de jatinho particular. Leva calote. Persegue a dívida até ser convidada para um voo, onde é jogada do avião por ter visto o que não devia. Sobrevive, volta para casa e descobre pela avó que é herdeira de uma fortuna que só pode tocar aos 21 anos. Passa três anos na miséria sabendo que é bilionária. Aos 21 pega o dinheiro e volta — para descobrir que o homem que mandou matá-la é o próprio pai, e que a amiga a entregou por exatamente mil reais.

**Regra fixa: a dívida é R$ 1.000.** Não altere valores, falas, nomes nem a ordem dos episódios.

---

# 🎬 EPISÓDIO 1 — "O EMPRÉSTIMO"

### EP01_C01 · A QUEDA
`local:` AEROPORTO_AR → CANAL_LS · `hora:` 18:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE → OLHOS · `dur:` 14s · `gravar:` SIM

⚠️ **Cena técnica.** Invencibilidade LIGADA. Trate como etapa separada.

**Ação:** Avião voando. A Djanira cai do avião, despenca e bate na água do canal. Emerge encharcada, sobe pela rampa de concreto e corre.

**NARRAÇÃO** — DJANIRA — 14s *(sem boca)*:
> "Meu nome é Djanira. O que fizeram comigo foi tão deplorável que, se eu tô contando essa história, é porque eu sobrevivi. E sobreviver não foi sorte, foi teimosia."

---

### EP01_C02 · A ROÇA
`local:` ROCA_GRAPESEED · `hora:` 07:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 10s · `gravar:` SIM

**Ação:** Djanira andando devagar no meio da plantação, trabalhando. Câmera bem afastada, ela pequena no quadro.

**CARTELA NA TELA:** `QUATRO SEMANAS ANTES`

**NARRAÇÃO** — DJANIRA — 10s *(sem boca)*:
> "A minha vida era bem diferente. Eu acordava antes do sol, trabalhava na roça e voltava pra casa com o corpo pedindo descanso."

---

### EP01_C03 · A AVÓ
`local:` CASA_VO · `hora:` 18:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DONA_IRACI · `cam:` DOIS → MEDIA · `dur:` 9s · `gravar:` SIM

**Ação:** As duas sentadas ou em pé na frente da casa. Djanira ajeita algo para a avó. Sem diálogo — só convivência.

**NARRAÇÃO** — DJANIRA — 9s *(sem boca)*:
> "Eu morava com a minha avó, dona Iraci. Foi ela que me criou. Agora que ela tava muito doente, era a minha vez de cuidar dela."

---

### EP01_C04 · O HOSPITAL
`local:` HOSPITAL_SANDY · `hora:` 08:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DONA_IRACI · `cam:` MEDIA · `dur:` 7s · `gravar:` SIM

**Ação:** Djanira deixa a avó na entrada da clínica, se despede e vai embora. A avó entra.

**NARRAÇÃO** — DJANIRA — 7s *(sem boca)*:
> "Eu levava minha avó pro hospital, deixava ela na entrada e voltava pra trabalhar."

---

### EP01_C05 · O SEGREDO DA AVÓ
`local:` HOSPITAL_SANDY → CASSINO_FORA · `hora:` 10:00 · `clima:` limpo
`elenco:` DONA_IRACI · `cam:` ATRAS → LONGE · `dur:` 8s · `gravar:` SIM

⚠️ Se o interior do cassino não abrir, grave só a **fachada** com ela entrando. Funciona igual.

**Ação:** A avó sai da clínica olhando para os lados, pega um caminho diferente e entra no cassino. Expressão muda de `machucado` para `feliz`.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Tinha dia que ela passava horas fora. Mas isso é bem normal nos hospitais do SUS."

> 💡 **O público vê o que a Djanira não vê.** A narração continua inocente de propósito. Não mude.

---

### EP01_C06 · A DINJER NO TRABALHO
`local:` AEROPORTO_TERMINAL · `hora:` 09:00 · `clima:` limpo
`elenco:` DINJER · `cam:` LONGE → MEDIA · `dur:` 9s · `gravar:` SIM

**Ação:** Dinjer descendo de um ônibus e andando até o terminal, com jeito de passarela, confiante.

**NARRAÇÃO** — DJANIRA — 9s *(sem boca)*:
> "E tinha a Dinjer. Amiga de infância que virou aeromoça e vivia pra cima e pra baixo conhecendo o mundo."

---

### EP01_C07 · O BARRACO NO HANGAR
`local:` AEROPORTO_HANGAR · `hora:` 11:00 · `clima:` limpo
`elenco:` DINJER, COLEGA_1, COLEGA_2, COLEGA_3 · `cam:` auto · `dur:` 20s · `gravar:` SIM

⚠️ **Não use combate real.** Use animações de luta encadeadas, que repetem igual todas as vezes.

**Ação:** Dinjer passa por três colegas. Eles falam dela. Ela para, vira e responde. Termina em empurrão e confusão coreografada.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | COLEGA_1 | "Eu pegaria ela só no off mesmo." | 3s | explicando |
| 2 | DINJER | "Eu acho que vocês deveriam me respeitar aqui, pois sou mulher, mãe, defensora do aborto, a aeromoça feminista assumidamente não monogâmica." | 9s | irritado |
| 3 | COLEGA_2 | "Só faltou falar comunista." | 3s | explicando |
| 4 | DINJER | "Me respeita, cara. Tá tirando onda?" | 3s | irritado |

**Ação final:** 2 segundos de empurra-empurra. Corte seco.

---

### EP01_C08 · O PEDIDO
`local:` ROCA_GRAPESEED · `hora:` 17:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DINJER · `cam:` auto · `dur:` 28s · `gravar:` SIM

> 🔴 **Esta é a cena de teste do sistema.** Gere esta primeiro.

**Ação:** Dinjer chega de moto, desce, anda com jeito de passarela até a Djanira e para na frente dela. As duas conversam. No fim, Djanira mexe no celular fazendo o Pix.

**NARRAÇÃO** — DJANIRA — 6s *(sem boca, antes do diálogo começar)*:
> "Ela não devia nem lembrar mais de mim, porque ela sumiu. Mas naquele dia ela apareceu com um problema."

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DINJER | "Amiga, eu tô desesperada. Minha mãe tá doente, eu preciso pagar uns exames, comprar remédio, e o meu dinheiro não caiu. Pode me emprestar mil reais?" | 9s | implorando |
| 2 | DJANIRA | "Amiga, eu tenho só o dinheiro do aluguel. Me promete que devolve em duas semanas?" | 6s | neutro |
| 3 | DINJER | "Prometo pela vida da minha mãe." | 3s | implorando |
| 4 | DJANIRA | "Então tá bom. Vou fazer o Pix." | 3s | neutro |
| 5 | DINJER | "Amiga, muito obrigada. Te amo!" | 3s | explicando |

---

### EP01_C09 · FECHAMENTO
`local:` ROCA_GRAPESEED · `hora:` 18:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` PERTO · `dur:` 7s · `gravar:` SIM

**Ação:** Djanira sozinha, olhando a moto da Dinjer ir embora.

**NARRAÇÃO** — NARRADOR (voz masculina) — 7s *(sem personagem na fala)*:
> "E aí, será que a Dinjer vai pagar? Não perca o próximo episódio de A Caloteira do Oeste."

---

**NO PRÓXIMO EPISÓDIO** *(montagem de 5s, cortes do Ep 2)*:
Dinjer na boate bebendo. Dinjer sentada com quatro homens de terno.

---

# 🎬 EPISÓDIO 2 — "O DINHEIRO SUMIU"

**ANTERIORMENTE** *(reaproveite, não grave)*: EP01_C01 (a queda) · EP01_C08 (o pedido)

---

### EP02_C01 · A BALADA
`local:` BOATE_DENTRO · `hora:` 23:00 · `clima:` limpo
`elenco:` DINJER · `cam:` MEDIA → PERTO · `dur:` 8s · `gravar:` SIM

⚠️ Se o interior não abrir, use `BOATE_FORA` com ela entrando pela porta, à noite, com neon.

**Ação:** Dinjer bebendo, dançando, tirando foto. Expressão `convencido`.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Enquanto eu contava moeda pra comprar pão, o meu dinheiro tava fazendo outra coisa."

---

### EP02_C02 · A MESA DOS HOMENS
`local:` BOATE_DENTRO · `hora:` 23:30 · `clima:` limpo
`elenco:` DINJER, CAPANGA_1, CAPANGA_2, CAPANGA_3, CAPANGA_4 · `cam:` LONGE → MEDIA · `dur:` 9s · `gravar:` SIM

**Ação:** Dinjer sentada numa mesa reservada com quatro homens de terno. Um deles entrega um envelope para ela. Ela guarda rápido.

**NARRAÇÃO** — DJANIRA — 9s *(sem boca)*:
> "E ela não tava só gastando. Ela tava comprando um lugar. Um lugar numa mesa onde gente como a gente não senta."

---

### EP02_C03 · O SILÊNCIO
`local:` CASA_VO · `hora:` 21:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` MEDIA · `dur:` 8s · `gravar:` SIM

**Ação:** Djanira no escuro, mexendo no celular, esperando resposta. Expressão `estressado`.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Passou uma semana. Passou a segunda. Eu mandei mensagem e não teve resposta. Liguei e caiu direto."

---

### EP02_C04 · O AVISO DA AVÓ
`local:` CASA_VO · `hora:` 19:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DONA_IRACI · `cam:` auto · `dur:` 18s · `gravar:` SIM

**Ação:** A avó chama a Djanira para perto e avisa. Djanira ouve de pé.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DONA_IRACI | "Minha filha, chega aqui. Tu deu dinheiro pra aquela menina, né?" | 5s | neutro |
| 2 | DJANIRA | "Dei, vó. É a Dinjer. A gente cresceu junto." | 4s | neutro |
| 3 | DONA_IRACI | "Essa menina não é flor que se cheira. Vai lá na internet e procura o nome dela. Procura, minha filha. Tu vai ver o tanto de gente que ela já passou pra trás." | 9s | explicando |

---

### EP02_C05 · O BLOQUEIO
`local:` CASA_VO · `hora:` 19:30 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` PERTO · `dur:` 12s · `gravar:` SIM

**Ação:** Djanira com o celular na mão. Rosto fecha. Expressão muda para `com raiva`.

**NARRAÇÃO** — DJANIRA — 12s *(sem boca)*:
> "Eu peguei o celular pra provar pra minha avó que ela tava errada. E eu tava bloqueada. Em tudo. Não tinha feito nem uma semana que ela saiu da minha casa com o meu dinheiro na mão, e eu já não existia mais pra ela."

---

### EP02_C06 · O STORY
`local:` CASA_VO · `hora:` 19:45 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DONA_IRACI · `cam:` DOIS → PERTO · `dur:` 16s · `gravar:` SIM

**Ação:** A avó mostra o celular dela para a Djanira. As duas olham a tela.

**DIÁLOGO** *(com boca)* — 1 fala só:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DONA_IRACI | "Olha aqui no meu, que eu não sou bloqueada de ninguém." | 4s | explicando |

**NARRAÇÃO** — DJANIRA — 12s *(sem boca, logo em seguida)*:
> "Ela tava numa balada. Naquela mesma noite. Na legenda dizia que ela tava passando por um momento difícil, que a mãe dela tava internada, e que Deus ia honrar quem ajudasse. A mãe da Dinjer tá viva. Nunca botou o pé num hospital."

---

### EP02_C07 · A DECISÃO
`local:` ROCA_GRAPESEED · `hora:` 18:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` BAIXA · `dur:` 6s · `gravar:` SIM

**Ação:** Djanira parada olhando o horizonte. Câmera baixa, olhando para cima — ela parece maior.

**NARRAÇÃO** — DJANIRA — 6s *(sem boca)*:
> "Foi ali que eu parei de pedir. E comecei a cobrar."

**NARRADOR** — 7s:
> "Ela emprestou o que não tinha. Agora só quer de volta o que é dela. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 3 — "A COBRANÇA"

**ANTERIORMENTE**: EP02_C04 (o aviso) · EP02_C05 (o bloqueio) · EP02_C06 (o story)

---

### EP03_C01 · A CARONA
`local:` ESTRADA_SENORA · `hora:` 07:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 7s · `gravar:` SIM

**Ação:** Djanira andando na beira da estrada, acenando para carros que passam.

**NARRAÇÃO** — DJANIRA — 7s *(sem boca)*:
> "Eu não tinha carro. Eu não tinha dinheiro pra passagem. Mas eu tinha o endereço dela."

---

### EP03_C02 · DE MANHÃ
`local:` CASA_DINJER · `hora:` 08:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` MEDIA · `dur:` 6s · `gravar:` SIM

**Ação:** Djanira bate na porta três vezes. Espera. Ninguém atende.

**NARRAÇÃO** — DJANIRA — 6s *(sem boca)*:
> "Eu fui de manhã. Bati três vezes. Ninguém atendeu."

---

### EP03_C03 · DE TARDE
`local:` CASA_DINJER · `hora:` 14:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 8s · `gravar:` SIM

**Ação:** Djanira sentada no meio-fio em frente à casa, esperando. Sol forte.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Fui de tarde. Esperei duas horas sentada no meio-fio. A televisão tava ligada lá dentro. Eu escutava daqui de fora."

---

### EP03_C04 · DE NOITE
`local:` CASA_DINJER · `hora:` 21:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` MEDIA · `dur:` 9s · `gravar:` SIM

**Ação:** Djanira bate na porta de novo. Luz acesa dentro. Expressão `estressado`.

**NARRAÇÃO** — DJANIRA — 9s *(sem boca)*:
> "Fui de noite. A luz acesa, a música tocando, e ninguém atendendo uma mulher que tava ali só pra pegar o que era dela."

---

### EP03_C05 · A FUGA
`local:` CASA_DINJER, fundos · `hora:` 21:30 · `clima:` limpo
`elenco:` DINJER, DJANIRA_POBRE · `cam:` LONGE · `dur:` 12s · `gravar:` SIM

**Ação:** A porta dos fundos abre. Dinjer sai correndo, descalça, com os sapatos na mão. Djanira parada no meio da rua, vendo ela sumir.

**NARRAÇÃO** — DJANIRA — 12s *(sem boca)*:
> "E foi aí que eu vi. Ela saiu correndo pelos fundos. Descalça. De uma casa que ela pagava com o meu dinheiro. Correndo de mim, como se eu fosse o problema dela."

---

### EP03_C06 · O QUE ELA ENTENDEU
`local:` CASA_DINJER, rua · `hora:` 21:40 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` PERTO · `dur:` 11s · `gravar:` SIM

**Ação:** Close na Djanira, parada, sem reação.

**NARRAÇÃO** — DJANIRA — 11s *(sem boca)*:
> "Eu não gritei. Eu não corri atrás. Pra ela, eu nunca fui amiga. Eu era reserva. Eu era o dinheiro que dava pra pegar sem precisar devolver."

---

### EP03_C07 · OS TRINTA DIAS
`local:` CASA_VO · `hora:` 10:00 · `clima:` limpo
`elenco:` DONA_IRACI, DJANIRA_POBRE, CAPANGA_1 *(como dono da fazenda, sem terno)* · `cam:` auto · `dur:` 15s · `gravar:` SIM

**Ação:** Um homem bate na porta. A avó atende. Djanira ouve de dentro.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | CAPANGA_1 | "Dona Iraci, eu gosto muito da senhora. Mas já são três meses de aluguel atrasado. Eu vou ter que pedir a casa. A senhora tem trinta dias." | 10s | explicando |

**NARRAÇÃO** — DJANIRA — 5s *(sem boca, depois)*:
> "Mil reais. Foi só isso. Mil reais foi o que separou a minha avó de morrer na casa dela."

**NARRADOR** — 6s:
> "Trinta dias pra achar o que ela nunca devia ter perdido. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 4 — "NO TRABALHO DELA"

**ANTERIORMENTE**: EP03_C05 (a fuga) · EP03_C07 (os trinta dias)

---

### EP04_C01 · O AEROPORTO
`local:` AEROPORTO_TERMINAL · `hora:` 10:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 12s · `gravar:` SIM

**Ação:** Djanira desce de um ônibus e anda pelo terminal, deslocada no meio de gente bem vestida.

**NARRAÇÃO** — DJANIRA — 12s *(sem boca)*:
> "Se ela não aparecia em casa, eu ia onde ela não podia fugir. No trabalho. Eu nunca tinha entrado num aeroporto na vida. Todo mundo ali andava rápido, de mala, de terno. E eu de chinelo, com um papel na mão."

---

### EP04_C02 · A COBRANÇA PÚBLICA
`local:` AEROPORTO_TERMINAL · `hora:` 10:30 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DINJER, COLEGA_1, COLEGA_2 · `cam:` auto · `dur:` 26s · `gravar:` SIM

**Ação:** Dinjer aparece com colegas. Vê a Djanira e muda de expressão. Os colegas param para olhar. Dinjer percebe a plateia e vira o jogo.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DJANIRA | "Dinjer. Mil reais. Eu só quero os meus mil reais." | 5s | irritado |
| 2 | DINJER | "Amiga, eu já mandei o comprovante. Deve ser o banco, tá fora do ar." | 5s | negando |
| 3 | DJANIRA | "Tu me mandou o print de uma tela preta." | 4s | irritado |
| 4 | DINJER | "Você veio me cobrar no meu trabalho? Na frente dos meus colegas? Por mil reais? Djanira, mil reais aqui é dinheiro de gorjeta." | 9s | explicando |
| 5 | DJANIRA | "Pra você. Pra mim é a casa da minha avó." | 4s | neutro |

---

### EP04_C03 · A SAÍDA
`local:` AEROPORTO_TERMINAL · `hora:` 10:40 · `clima:` limpo
`elenco:` DINJER, DJANIRA_POBRE · `cam:` LONGE · `dur:` 9s · `gravar:` SIM

**Ação:** Dinjer vai embora andando na passarela com os colegas, rindo. Djanira fica parada no meio do saguão.

**NARRAÇÃO** — DJANIRA — 9s *(sem boca)*:
> "Ela foi embora rindo. E os colegas dela riram junto. Eu fiquei ali, sendo olhada como quem tinha ido pedir esmola."

---

### EP04_C04 · A VOLTA A PÉ
`local:` ESTRADA_SENORA · `hora:` 18:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 6s · `gravar:` SIM

**Ação:** Djanira andando sozinha na estrada, câmera o mais longe possível.

**NARRAÇÃO** — DJANIRA — 6s *(sem boca)*:
> "Eu voltei a pé metade do caminho porque não tinha dinheiro da volta."

---

### EP04_C05 · A AVÓ PIORA
`local:` CASA_VO · `hora:` 20:00 · `clima:` limpo
`elenco:` DONA_IRACI, DJANIRA_POBRE · `cam:` auto · `dur:` 14s · `gravar:` SIM

**Ação:** A avó deitada, muito mal. Djanira sentada do lado.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DONA_IRACI | "Minha filha, deixa esse dinheiro pra lá. Tem coisa que a gente perde só pra aprender." | 7s | neutro |
| 2 | DJANIRA | "A senhora tá na cama porque eu não tenho com que comprar o seu remédio, vó. Eu não vou deixar pra lá." | 7s | irritado |

---

### EP04_C06 · A CONTA
`local:` CASA_VO · `hora:` 23:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` PERTO · `dur:` 8s · `gravar:` SIM

**Ação:** Close na Djanira segurando uma caixa de remédio vazia. Expressão `machucado`.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Naquela noite eu não dormi. Não era raiva. Era conta. Eu fiquei a noite inteira fazendo conta de como salvar a minha avó com zero reais."

**NARRADOR** — 8s:
> "Ela perdeu o dinheiro, o respeito e está prestes a perder a casa. Mas o pior ainda não aconteceu. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 5 — "O CONVITE"

**ANTERIORMENTE**: EP04_C02 (a humilhação) · EP04_C05 (a avó doente) · EP04_C06 (a caixa vazia)

---

### EP05_C01 · OS TRÊS SERVIÇOS
`local:` ROCA_GRAPESEED · `hora:` 12:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 12s · `gravar:` SIM

**Ação:** Djanira capinando no sol forte, exausta. Expressão `chateado`.

**NARRAÇÃO** — DJANIRA — 12s *(sem boca)*:
> "Passaram mais duas semanas. Eu peguei três serviços. Capinei terreno, lavei roupa dos outros, colhi de sol a sol. Juntei duzentos reais. Faltavam oitocentos e a minha avó tinha vinte e dois dias."

---

### EP05_C02 · A LIGAÇÃO
`local:` ROCA_GRAPESEED · `hora:` 15:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` MEDIA · `dur:` 5s · `gravar:` SIM

**Ação:** O celular toca. Djanira olha a tela e atende.

**NARRAÇÃO** — DJANIRA — 5s *(sem boca)*:
> "E aí, depois de um mês e meio sumida, ela ligou."

---

### EP05_C03 · A PROPOSTA
`local:` ROCA_GRAPESEED *(Djanira)* · `hora:` 15:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DINJER · `cam:` auto · `dur:` 30s · `gravar:` SIM

> 💡 **Cena de telefone.** As duas estão em lugares diferentes. Grave cada uma separada, na sua locação, e alterne na edição. A Dinjer fica em `AEROPORTO_TERMINAL`.

**Ação:** As duas ao telefone, cada uma no seu lugar.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DINJER | "Amiga! Desculpa o sumiço, eu tava fora do país. Escuta, eu tenho uma notícia boa. Muito boa." | 7s | explicando |
| 2 | DJANIRA | "Eu não quero notícia. Eu quero o meu dinheiro." | 4s | irritado |
| 3 | DINJER | "É exatamente disso que eu tô falando. Eu falei de você pro meu chefe. Falei que você é a pessoa mais honesta que eu conheço na vida. E ele quer te conhecer." | 10s | explicando |
| 4 | DJANIRA | "E por que é que um chefe seu ia querer me conhecer?" | 5s | neutro |
| 5 | DINJER | "Vai ter uma festa sábado. Vem. Ele te paga tudo que eu te devo, na mão, com juros. Ele faz questão." | 8s | implorando |

---

### EP05_C04 · O ARMÁRIO
`local:` CASA_VO · `hora:` 19:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` MEDIA · `dur:` 8s · `gravar:` SIM

**Ação:** Djanira parada olhando o guarda-roupa, quase vazio.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Minha melhor roupa. Eu abri o armário e fiquei olhando pra ele um tempo. Eu tinha duas roupas. Uma de trabalhar e uma de ir na igreja."

---

### EP05_C05 · O AVISO QUE ELA NÃO OUVIU
`local:` CASA_VO · `hora:` 19:30 · `clima:` limpo
`elenco:` DONA_IRACI, DJANIRA_POBRE · `cam:` auto · `dur:` 18s · `gravar:` SIM

**Ação:** A avó chama a Djanira. Djanira ouve de pé, já decidida.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DONA_IRACI | "Minha filha, não vai." | 3s | implorando |
| 2 | DJANIRA | "Vó, é o dinheiro do aluguel. É o seu remédio." | 4s | neutro |
| 3 | DONA_IRACI | "Eu sei o que é. Mas escuta. Gente rica não convida pobre pra festa pra pagar dívida. Gente rica convida pobre pra festa quando quer alguma coisa de pobre." | 11s | explicando |

**NARRAÇÃO** — DJANIRA — 6s *(sem boca)*:
> "Minha avó nunca errou uma na vida. E foi justo essa que eu não escutei."

**NARRADOR** — 7s:
> "Um convite bom demais pra ser verdade. E o que é bom demais sempre cobra o preço. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 6 — "A BOATE"

**ANTERIORMENTE**: EP05_C03 (a proposta) · EP05_C05 (o aviso da avó)

---

### EP06_C01 · A CIDADE GRANDE
`local:` CENTRO_LEGION · `hora:` 22:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` BAIXA · `dur:` 11s · `gravar:` SIM

**Ação:** Djanira desce do ônibus e olha os prédios. Câmera baixa, olhando para cima, os arranha-céus enormes em volta dela.

**NARRAÇÃO** — DJANIRA — 11s *(sem boca)*:
> "Eu nunca tinha entrado na cidade grande de noite. Tudo era alto demais, claro demais, caro demais. E eu com a única roupa boa que eu tinha, que já era velha quando eu ganhei."

---

### EP06_C02 · A FILA
`local:` BOATE_FORA · `hora:` 23:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` MEDIA · `dur:` 7s · `gravar:` SIM

> 🔴 **Guarde o enquadramento exato desta tomada.** No Episódio 12 você vai repetir a mesma câmera com a Djanira rica. O corte entre as duas é o momento mais forte da temporada.

**Ação:** Djanira parada na fila, sendo olhada de cima a baixo pelo segurança. Expressão `chateado`.

**NARRAÇÃO** — DJANIRA — 7s *(sem boca)*:
> "Eu fiquei quarenta minutos naquela fila. Todo mundo que chegou depois de mim entrou antes."

---

### EP06_C03 · O QUE ELA VIU
`local:` BOATE_DENTRO · `hora:` 23:30 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DINJER, O_CHEFE, CAPANGA_1, CAPANGA_2 · `cam:` LONGE → PERTO · `dur:` 13s · `gravar:` SIM

⚠️ Se o interior não abrir, grave na fachada com as portas abertas ao fundo.

**Ação:** Djanira vê, de longe, a Dinjer sentada com o Chefe e os capangas. Não é festa — é negócio.

**NARRAÇÃO** — DJANIRA — 13s *(sem boca)*:
> "E aí eu vi ela. Mas não do jeito que eu esperava. A Dinjer não tava se divertindo naquela boate. Ela tava trabalhando. E o trabalho dela não era servir champanhe."

---

### EP06_C04 · A APRESENTAÇÃO
`local:` BOATE_DENTRO · `hora:` 23:40 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DINJER, O_CHEFE · `cam:` auto · `dur:` 40s · `gravar:` SIM

**Ação:** Dinjer vê a Djanira, levanta rápido e vai abraçar. Leva até a mesa do Chefe. Ele se levanta devagar.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DINJER | "Amiga! Que bom que você veio! Gente, olha! Essa aqui é a Djanira. A mulher mais honesta que eu conheço na vida inteira." | 9s | explicando |
| 2 | O_CHEFE | "Honestidade é raro hoje em dia, minha filha. Vale muito mais do que mil reais." | 6s | neutro |
| 3 | DJANIRA | "Como é que o senhor sabe dos mil reais?" | 4s | neutro |
| 4 | O_CHEFE | "Eu sei de tudo. É por isso que eu tô sentado nessa mesa e não naquela ali." | 6s | explicando |
| 5 | O_CHEFE | "Vou te fazer uma proposta. Amanhã eu tenho um voo. Coisa rápida, um jantar lá em cima. Vem comigo. E lá eu te pago tudo que a Dinjer te deve. Em dinheiro vivo. Com juros." | 12s | explicando |
| 6 | DJANIRA | "E por que é que o senhor faria isso por mim?" | 4s | neutro |
| 7 | O_CHEFE | "Porque eu gosto de gente que trabalha. E porque eu quero te conhecer melhor." | 5s | neutro |

---

### EP06_C05 · A SAÍDA FELIZ
`local:` BOATE_FORA · `hora:` 00:30 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` MEDIA · `dur:` 8s · `gravar:` SIM

**Ação:** Djanira saindo da boate, sorrindo. Expressão `feliz` — a primeira vez na série.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Eu saí daquela boate feliz. Eu liguei pra minha avó de um orelhão e falei que ia dar tudo certo. Que no domingo a gente pagava o aluguel."

---

### EP06_C06 · O GESTO
`local:` BOATE_DENTRO · `hora:` 00:31 · `clima:` limpo
`elenco:` O_CHEFE, DINJER · `cam:` PERTO · `dur:` 6s · `gravar:` SIM

> 🔴 **A tomada mais importante da primeira metade da temporada. Sem fala nenhuma.**

**Ação:** Close no Chefe. Ele olha para a Dinjer e faz o gesto de **passar a mão no pescoço** — ordem de matar. Corta para a Dinjer: ela abaixa a cabeça e não diz nada. **Corte seco.**

**NARRADOR** — 7s:
> "Um jantar no céu. Uma promessa de pagamento. E um gesto que ninguém deveria ter visto. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 7 — "O VOO"

**ANTERIORMENTE**: EP06_C04 (o convite) · EP06_C06 (o gesto)

---

### EP07_C01 · O HANGAR
`local:` AEROPORTO_HANGAR · `hora:` 19:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 9s · `gravar:` SIM

**Ação:** Djanira chega no hangar arrumada com a roupa de igreja. Expressão `feliz`.

**NARRAÇÃO** — DJANIRA — 9s *(sem boca)*:
> "Eu cheguei naquele hangar com o coração batendo de alegria. Eu ia receber o meu dinheiro. Eu ia salvar a casa da minha avó."

---

### EP07_C02 · O QUE ELA NÃO DEVIA VER
`local:` GALPAO_CYPRESS · `hora:` 19:30 · `clima:` limpo
`elenco:` O_CHEFE, CAPANGA_1, CAPANGA_2, DJANIRA_POBRE · `cam:` LONGE → PERTO · `dur:` 16s · `gravar:` SIM

**Ação:** Djanira escondida atrás de um contêiner, escutando. O Chefe conversa com dois homens perto de umas caixas. Filme de longe, pouca luz, nada claramente visível.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | O_CHEFE | "Isso aqui não pode passar pelo porto. Vai tudo no meu voo. E ninguém, ninguém mesmo, fica sabendo." | 8s | irritado |

**NARRAÇÃO** — DJANIRA — 8s *(sem boca, depois)*:
> "Eu não entendi metade do que eles falaram. Eu só entendi que eu não podia estar ali."

---

### EP07_C03 · A DECOLAGEM
`local:` AEROPORTO_HANGAR · `hora:` 20:30 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DINJER, O_CHEFE · `cam:` LONGE · `dur:` 10s · `gravar:` SIM

**Ação:** Os três embarcam. Djanira `feliz`, Dinjer `chateado` — primeira vez que ela não está convencida. O avião decola.

**NARRAÇÃO** — DJANIRA — 10s *(sem boca)*:
> "Eu subi naquele avião mesmo assim. Porque quando você é pobre, você aprende a fingir que não viu. É assim que a gente sobrevive."

---

### EP07_C04 · O JANTAR NO CÉU
`local:` AEROPORTO_AR · `hora:` 21:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE, DINJER, O_CHEFE · `cam:` auto · `dur:` 34s · `gravar:` SIM

**Ação:** Dentro do avião. O Chefe servindo, simpático. Dinjer em pé no fundo, calada. No meio da conversa o Chefe para de sorrir.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | O_CHEFE | "Bebe, minha filha. Olha essa cidade daqui de cima. Bonita, né? Daqui de cima ninguém vê a sujeira." | 9s | explicando |
| 2 | DJANIRA | "É bonita, sim senhor." | 3s | neutro |
| 3 | O_CHEFE | "Você sabe qual é o problema de ser honesta? É que gente honesta olha pras coisas. Gente honesta vê. E depois lembra." | 10s | neutro |
| 4 | DJANIRA | "Eu não vi nada." | 2s | negando |
| 5 | O_CHEFE | "Eu sei que você não viu. Mas eu não posso confiar na sua memória." | 6s | neutro |
| 6 | DINJER | "Desculpa, amiga." | 2s | implorando |

> 💡 Na fala 6 a Dinjer **não olha para a Djanira**. Olha para o chão. É o detalhe que o público vai lembrar no Episódio 15.

---

### EP07_C05 · A QUEDA
`local:` AEROPORTO_AR → CANAL_LS · `hora:` 21:10 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` OLHOS · `dur:` 10s · `gravar:` SIM

> 💡 **Reaproveite a EP01_C01.** É a mesma queda, agora explicada. Se quiser regravar com outro ângulo, melhor ainda.

**NARRAÇÃO** — DJANIRA — 6s *(sem boca)*:
> "Meu nome é Djanira. E eles me jogaram de um avião achando que eu ia morrer calada."

**NARRADOR** — 7s:
> "Ela caiu do céu por um crime que não cometeu. Mas quem cai em água suja nem sempre morre. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 8 — "O QUE A VÓ GUARDOU"

**ANTERIORMENTE**: EP07_C04 (o aviso do Chefe) · EP07_C04 fala 6 ("desculpa, amiga") · EP07_C05 (a queda)

---

### EP08_C01 · SAINDO DA ÁGUA
`local:` CANAL_LS · `hora:` 06:00 · `clima:` nublado
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 6s · `gravar:` SIM

**Ação:** Djanira sai da água do canal, encharcada, subindo a rampa de concreto com dificuldade. Expressão `machucado`.

**NARRAÇÃO** — DJANIRA — 6s *(sem boca)*:
> "A água me quebrou três costelas. Mas quebrar não é morrer."

---

### EP08_C02 · DOIS DIAS DE VOLTA
`local:` ESTRADA_SENORA · `hora:` 06:30 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 11s · `gravar:` SIM

**Ação:** Djanira andando descalça na beira da estrada, ao nascer do sol. Câmera o mais longe possível.

**NARRAÇÃO** — DJANIRA — 11s *(sem boca)*:
> "Eu levei dois dias pra voltar pra casa. A pé e de carona, com um sapato só e a roupa de igreja rasgada. Eu não chorei nenhuma vez. Eu só pensava na minha avó esperando na porta."

---

### EP08_C03 · A REVELAÇÃO
`local:` CASA_VO · `hora:` 20:00 · `clima:` limpo
`elenco:` DONA_IRACI, DJANIRA_POBRE · `cam:` auto · `dur:` 42s · `gravar:` SIM

> 🔴 **A cena mais importante do primeiro bloco.** Cortes lentos, planos fechados, deixe respirar.

**Ação:** A avó deitada, muito pior. Chama a Djanira para perto e conta tudo.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DONA_IRACI | "Chega aqui, minha filha. Senta. Eu preciso te contar uma coisa antes que eu não consiga mais falar." | 8s | implorando |
| 2 | DONA_IRACI | "Teu pai não morreu pobre. Teu pai morreu muito, muito rico." | 6s | neutro |
| 3 | DJANIRA | "A senhora tá delirando, vó. É o remédio." | 4s | negando |
| 4 | DONA_IRACI | "Tem uma conta num banco americano. Tá no teu nome desde o dia que tu nasceu. Eu te trouxe pra roça pra ninguém te achar. Enquanto tu fosse pobre, tu tava viva." | 12s | explicando |
| 5 | DJANIRA | "Por que é que a senhora nunca me contou isso?" | 4s | neutro |
| 6 | DONA_IRACI | "Porque dinheiro sem idade pra segurar só serve pra te matar mais cedo, minha filha." | 8s | neutro |

---

### EP08_C04 · A CONFISSÃO DO CASSINO
`local:` CASA_VO · `hora:` 20:30 · `clima:` limpo
`elenco:` DONA_IRACI, DJANIRA_POBRE · `cam:` auto · `dur:` 30s · `gravar:` SIM

**Ação:** A avó desvia o olhar, envergonhada.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DONA_IRACI | "E tem mais uma coisa. Aquelas terça, quinta e sábado... eu não passava o dia todo no hospital não." | 8s | neutro |
| 2 | DJANIRA | "A senhora ia aonde?" | 3s | neutro |
| 3 | DONA_IRACI | "Eu ia jogar. No cassino." | 4s | neutro |
| 4 | DONA_IRACI | "Eu tava tentando juntar dinheiro pra tirar nós duas daqui antes de tu fazer vinte e um ano. Porque quando tu fizesse vinte e um, ele ia te achar. E eu perdi tudo. Me perdoa, minha filha." | 15s | implorando |

> 💡 **Insira aqui o flashback da EP01_C05** — a avó saindo do hospital e entrando no cassino. Agora o público entende.

---

### EP08_C05 · O PAPEL
`local:` CASA_VO · `hora:` 21:00 · `clima:` limpo
`elenco:` DONA_IRACI, DJANIRA_POBRE · `cam:` PERTO · `dur:` 12s · `gravar:` SIM

**Ação:** A avó entrega um papel dobrado na mão da Djanira. Close nas mãos.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DONA_IRACI | "Guarda isso. Tá escrito o nome do banco e o nome dele. E não abre até tu precisar de verdade." | 9s | implorando |

---

### EP08_C06 · A MORTE
`local:` CASA_VO · `hora:` 23:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 7s · `gravar:` SIM

**Ação:** Djanira parada na porta do quarto, de costas para a câmera. **Não mostre a avó.** Corte para o preto.

**NARRAÇÃO** — DJANIRA — 7s *(sem boca)*:
> "Minha avó morreu naquela noite. E eu fiquei com um papel na mão e ninguém no mundo."

**NARRADOR** — 7s:
> "Um nome escrito num papel. Uma fortuna que ela não pode tocar. E uma menina sozinha no mundo. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 9 — "VINTE E UM"

**ANTERIORMENTE**: EP08_C03 (a herança) · EP08_C04 (o cassino) · EP08_C05 (o papel)

---

### EP09_C01 · O ENTERRO
`local:` IGREJA_CHAPARRAL · `hora:` 10:00 · `clima:` nublado
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 13s · `gravar:` SIM

**Ação:** Djanira sozinha na frente de uma cova. Ninguém mais em cena. É a solidão que conta a história.

**NARRAÇÃO** — DJANIRA — 13s *(sem boca)*:
> "Foram quatro pessoas no enterro da minha avó. Eu, o coveiro e dois vizinhos que foram por educação. A mulher que me criou foi enterrada num caixão que a prefeitura pagou. Eu não tinha dinheiro nem pra flor."

---

### EP09_C02 · O PAPEL ABERTO
`local:` CASA_VO · `hora:` 11:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` PERTO · `dur:` 9s · `gravar:` SIM

**Ação:** Djanira abre o papel dobrado. Close no papel e no rosto dela.

**NARRAÇÃO** — DJANIRA — 9s *(sem boca)*:
> "Uma semana depois, eu abri o papel. Eu li só a primeira linha, que era o nome do banco. A segunda linha eu nem olhei, porque naquele momento eu só queria o dinheiro."

> 🔴 **Plantado aqui, colhido no Episódio 11.** Ela não lê a segunda linha. O público também não vê.

---

### EP09_C03 · O BANCO
`local:` BANCO_MAZE · `hora:` 14:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` BAIXA · `dur:` 6s · `gravar:` SIM

**Ação:** Djanira parada na calçada, olhando o prédio enorme. Câmera baixa — o prédio gigante, ela minúscula.

**NARRAÇÃO** — DJANIRA — 6s *(sem boca)*:
> "E eu fui atrás do dinheiro."

---

### EP09_C04 · TRÊS ANOS
`local:` BANCO_MAZE · `hora:` 14:30 · `clima:` limpo
`elenco:` DJANIRA_POBRE, ATENDENTE · `cam:` auto · `dur:` 24s · `gravar:` SIM

**Ação:** Djanira sentada na frente de um atendente.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | ATENDENTE | "A conta existe, senhorita. O valor existe. A senhorita é que ainda não existe legalmente." | 8s | explicando |
| 2 | DJANIRA | "Como assim?" | 2s | neutro |
| 3 | ATENDENTE | "É um banco americano. Lá a idade pra movimentar é vinte e um anos. A senhorita tem dezoito." | 8s | explicando |
| 4 | DJANIRA | "Faltam quantos anos?" | 3s | neutro |
| 5 | ATENDENTE | "Três." | 2s | neutro |

---

### EP09_C05 · A SAÍDA DE CASA
`local:` CASA_VO · `hora:` 08:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 10s · `gravar:` SIM

**Ação:** Djanira sai de casa com uma sacola, de costas. Câmera parada. Ela some do quadro sozinha.

**NARRAÇÃO** — DJANIRA — 10s *(sem boca)*:
> "No mesmo mês eu perdi a casa. Juntei o que cabia numa sacola, deixei a chave em cima da mesa e nem tranquei a porta, porque não era minha pra trancar."

---

### EP09_C06 · O ÔNIBUS
`local:` ESTRADA_SENORA · `hora:` 09:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` MEDIA · `dur:` 12s · `gravar:` SIM

**Ação:** Djanira esperando na beira da estrada com a sacola. Expressão `chateado`.

**NARRAÇÃO** — DJANIRA — 12s *(sem boca)*:
> "Três anos. Eu ia passar três anos sendo a mulher mais pobre daquela cidade, sabendo que eu era a mais rica. E foi isso, exatamente isso, que me manteve viva."

**NARRADOR** — 7s:
> "Três anos separam ela de tudo que é dela. E em três anos, muita coisa morre por dentro. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 10 — "TRÊS ANOS"

**ANTERIORMENTE**: EP09_C04 ("três anos") · EP09_C05 (a saída de casa)

> 💡 **O episódio mais fácil de gravar da temporada.** Um personagem só, zero diálogo, tudo narração. Oito tomadas curtas em lugares diferentes, montadas com música. **Varie muito a hora do dia** — é a mudança de luz que vende os três anos passando.

---

### EP10_C01 · O BANCO DE PRAÇA
`local:` PRAIA_DELPERRO · `hora:` 03:00 · `clima:` nublado
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 10s · `gravar:` SIM

**Ação:** Djanira dormindo num banco, abraçada na sacola.

**NARRAÇÃO** — DJANIRA — 10s *(sem boca)*:
> "No primeiro mês eu dormi na rodoviária. No segundo, numa pensão que cobrava por noite. No terceiro eu já sabia quais bancos de praça a polícia não passava de madrugada."

---

### EP10_C02 · OS SERVIÇOS
`local:` RUA_STRAWBERRY · `hora:` 05:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` MEDIA · `dur:` 9s · `gravar:` SIM

**Ação:** Djanira num ponto de ônibus, esperando, de uniforme de faxina.

**NARRAÇÃO** — DJANIRA — 9s *(sem boca)*:
> "Eu lavei prato, limpei chão, carreguei caixa, entreguei panfleto. Eu fiz tudo que apareceu e nunca perguntei quanto pagava antes de aceitar."

---

### EP10_C03 · AS QUATRO DA MANHÃ
`local:` RUA_STRAWBERRY · `hora:` 04:00 · `clima:` nublado
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 8s · `gravar:` SIM

**Ação:** Djanira andando sozinha na rua vazia, ainda escuro.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Eu acordava às quatro da manhã pra pegar dois ônibus e limpar a casa de gente que tinha menos dinheiro do que eu."

---

### EP10_C04 · O VIADUTO
`local:` VIADUTO_LAPUERTA · `hora:` 23:00 · `clima:` nublado
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 7s · `gravar:` SIM

**Ação:** Djanira andando embaixo do viaduto à noite.

**NARRAÇÃO** — DJANIRA — 7s *(sem boca)*:
> "Eu dormia embaixo de um viaduto sendo dona de um prédio que eu nunca vi."

---

### EP10_C05 · A INVISÍVEL
`local:` CENTRO_LEGION · `hora:` 08:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` LONGE · `dur:` 9s · `gravar:` SIM

**Ação:** Djanira andando rápido na calçada, no meio de outras pessoas, sem ninguém olhar para ela.

**NARRAÇÃO** — DJANIRA — 9s *(sem boca)*:
> "Ninguém me dava bom dia. Ninguém sabia o meu nome. Eu era só mais uma mulher andando rápido na calçada de manhã cedo."

---

### EP10_C06 · O CALENDÁRIO
`local:` VIADUTO_LAPUERTA · `hora:` 22:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` PERTO · `dur:` 14s · `gravar:` SIM

**Ação:** Close na Djanira riscando algo com a mão. O calendário em si você monta no CapCut.

**NARRAÇÃO** — DJANIRA — 14s *(sem boca)*:
> "E todo dia, antes de dormir, eu riscava um dia. Mil e cem. Setecentos. Quatrocentos e doze. Trezentos e dois. Eu não queria o dinheiro pra ser rica. Eu queria o dinheiro pra poder olhar na cara dela."

---

### EP10_C07 · O ÚLTIMO DIA
`local:` VIADUTO_LAPUERTA · `hora:` 22:30 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` PERTO · `dur:` 4s · `gravar:` SIM

**Ação:** Close no rosto dela. Um sorriso mínimo, o primeiro em três anos.

**NARRAÇÃO** — DJANIRA — 4s *(sem boca)*:
> "E aí faltou um."

**NARRADOR** — 6s:
> "Mil e noventa e cinco dias. Amanhã, ela faz vinte e um anos. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 11 — "A HERDEIRA"

**ANTERIORMENTE**: EP10_C01 (dormindo na rua) · EP10_C06 (o calendário) · EP10_C07 ("faltou um")

---

### EP11_C01 · A MESMA ROUPA
`local:` BANCO_MAZE · `hora:` 09:00 · `clima:` limpo
`elenco:` DJANIRA_POBRE · `cam:` MEDIA · `dur:` 8s · `gravar:` SIM

> ⚠️ Ainda é a **DJANIRA_POBRE**. A troca acontece só na próxima cena.

**Ação:** Djanira entrando no banco com a roupa velha e o chinelo. O segurança a segue com o olhar.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Eu entrei naquele banco com a mesma roupa de três anos antes. O segurança me seguiu com o olho da porta até o balcão."

---

### EP11_C02 · QUANTO CUSTOU
`local:` BANCO_MAZE · `hora:` 09:30 · `clima:` limpo
`elenco:` DJANIRA_POBRE, ATENDENTE · `cam:` auto · `dur:` 12s · `gravar:` SIM

**Ação:** O atendente olha a tela e muda de expressão.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | ATENDENTE | "A senhora... tem noção de quanto tem nessa conta?" | 5s | explicando |
| 2 | DJANIRA | "Não. Mas eu tenho noção de quanto me custou." | 5s | neutro |

---

### EP11_C03 · A TRANSFORMAÇÃO
`local:` MANSAO_DJANIRA · `hora:` 13:00 · `clima:` limpo
`elenco:` DJANIRA_RICA · `cam:` LONGE → MEDIA → PERTO · `dur:` 14s · `gravar:` SIM

> 🔴 **Aqui troca o figurino.** A partir desta cena é sempre `DJANIRA_RICA`, andar `na passarela`, expressão `convencido`.

**Ação:** Montagem de 5 ou 6 tomadas curtas: saindo do carro, na frente da mansão, no portão, andando de salto.

**NARRAÇÃO** — DJANIRA — 14s *(sem boca)*:
> "Todo mundo acha que dinheiro muda as pessoas. Não muda. Dinheiro só tira a educação que a pobreza obrigava a gente a ter. Eu comprei uma casa, comprei um carro, comprei roupa que não coçava. E eu não senti absolutamente nada."

---

### EP11_C04 · A CAMA MACIA
`local:` MANSAO_DJANIRA · `hora:` 23:00 · `clima:` limpo
`elenco:` DJANIRA_RICA · `cam:` LONGE · `dur:` 8s · `gravar:` SIM

**Ação:** Djanira sentada sozinha, no chão, numa sala enorme e vazia.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Na primeira noite eu dormi no chão. A cama era macia demais. Meu corpo não sabia mais dormir em cama."

---

### EP11_C05 · A SEGUNDA LINHA
`local:` MANSAO_DJANIRA · `hora:` 20:00 · `clima:` limpo
`elenco:` DJANIRA_RICA · `cam:` PERTO · `dur:` 20s · `gravar:` SIM

> 🔴 **O gancho de toda a segunda metade da temporada.**

**Ação:** Djanira recebe um envelope pardo. Lê. Pega o papel da avó e lê a segunda linha, que ela nunca tinha lido. Para de ler no meio. Começa a rir — um riso errado.

**NARRAÇÃO** — DJANIRA — 20s *(sem boca)*:
> "A primeira coisa que eu comprei de verdade não foi carro nem casa. Foi informação. Eu queria saber tudo sobre o homem que me jogou daquele avião. E aí eu peguei o papel da minha avó e li a segunda linha, que eu nunca tinha lido. Era o mesmo nome do relatório."

**NARRADOR** — 7s:
> "O homem que tentou matá-la. E o nome que a avó guardou por dezoito anos. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 12 — "A CLIENTE VIP"

**ANTERIORMENTE**: EP11_C03 (a transformação) · EP11_C05 (a segunda linha)

---

### EP12_C01 · A MESMA PORTA
`local:` BOATE_FORA · `hora:` 23:00 · `clima:` limpo
`elenco:` DJANIRA_RICA · `cam:` MEDIA · `dur:` 8s · `gravar:` SIM

> 🔴 **Use exatamente o mesmo enquadramento da EP06_C02.** Mesma posição de câmera, mesma distância, mesmo horário. Na edição eu coloco as duas lado a lado. **Este corte é o episódio inteiro.**

**Ação:** Um carro caro para na frente da boate. Djanira desce. O segurança abre a porta antes dela chegar perto.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "Três anos antes eu fiquei quarenta minutos naquela fila. Naquela noite, o segurança abriu a porta antes de eu chegar perto."

---

### EP12_C02 · A MESA DO CHEFE
`local:` BOATE_DENTRO · `hora:` 23:15 · `clima:` limpo
`elenco:` DJANIRA_RICA · `cam:` LONGE · `dur:` 6s · `gravar:` SIM

**Ação:** Djanira sentada sozinha na área VIP — na mesma mesa onde o Chefe sentava no Episódio 6.

**NARRAÇÃO** — DJANIRA — 6s *(sem boca)*:
> "Eu sentei na mesma mesa. Na mesa que três anos antes eu só tinha olhado de longe."

---

### EP12_C03 · ELA NÃO RECONHECE
`local:` BOATE_DENTRO · `hora:` 23:20 · `clima:` limpo
`elenco:` DJANIRA_RICA, DINJER · `cam:` auto · `dur:` 36s · `gravar:` SIM

> 💡 A Dinjer está com o mesmo uniforme de sempre. A Djanira mudou tudo menos o rosto. **A Dinjer olha e não reconhece.**

**Ação:** Dinjer chega para servir, com uma bandeja. Conversa de cliente e garçonete.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DINJER | "Boa noite, senhora. Champanhe por conta da casa?" | 4s | neutro |
| 2 | DJANIRA | "Você trabalha aqui há muito tempo?" | 3s | neutro |
| 3 | DINJER | "Cinco anos. Mas é temporário, viu? Eu tô subindo na vida. Eu já fui aeromoça, de avião particular." | 9s | explicando |
| 4 | DJANIRA | "E por que parou?" | 2s | neutro |
| 5 | DINJER | "Ah... é complicado. Coisa de patrão." | 4s | negando |
| 6 | DJANIRA | "Me diz uma coisa. Você já fez alguma coisa de que se arrepende?" | 5s | neutro |
| 7 | DINJER | "Ai, senhora, eu faço isso todo dia." | 3s | explicando |
| 8 | DJANIRA | "E teve alguma que doeu?" | 3s | neutro |
| 9 | DINJER | "...Uma." | 2s | neutro |
| 10 | DJANIRA | "Conta." | 1s | neutro |
| 11 | DINJER | "Eu não posso, senhora. Se eu contar, eu morro." | 5s | implorando |

---

### EP12_C04 · A GORJETA
`local:` BOATE_DENTRO · `hora:` 00:00 · `clima:` limpo
`elenco:` DJANIRA_RICA, DINJER · `cam:` PERTO · `dur:` 14s · `gravar:` SIM

**Ação:** Djanira deixa um envelope grosso na mesa e vai embora sem olhar para trás. Dinjer abre, conta, e começa a chorar.

**NARRAÇÃO** — DJANIRA — 14s *(sem boca)*:
> "Eu deixei de gorjeta o dobro do que ela ganhava no ano inteiro. E fui embora sem dizer quem eu era. Pelo retrovisor eu vi ela abrir o envelope, contar o dinheiro e começar a chorar. Ela achou que era sorte."

---

### EP12_C05 · O BILHETE
`local:` BOATE_DENTRO · `hora:` 00:05 · `clima:` limpo
`elenco:` DINJER · `cam:` PERTO · `dur:` 5s · `gravar:` SIM

**Ação:** Close no envelope. Dentro, junto com o dinheiro, um papel escrito à mão. O texto você monta no CapCut.

**TEXTO NA TELA:**
> "Duas semanas. Juro."

**NARRADOR** — 7s:
> "Ela recebeu de volta, com juros, tudo que emprestou. Sem saber de quem. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 13 — "A DONA DA CASA"

**ANTERIORMENTE**: EP12_C03 ("se eu contar, eu morro") · EP12_C05 (o bilhete)

---

### EP13_C01 · TUDO DIFERENTE
`local:` BOATE_DENTRO · `hora:` 21:00 · `clima:` limpo
`elenco:` DINJER · `cam:` MEDIA · `dur:` 8s · `gravar:` SIM

**Ação:** Dinjer chega para trabalhar e vê tudo mudado. Expressão `estressado`.

**NARRAÇÃO** — DJANIRA — 8s *(sem boca)*:
> "A primeira coisa que eu comprei depois da casa foi a boate onde ela trabalhava."

---

### EP13_C02 · A DEMISSÃO
`local:` BOATE_DENTRO · `hora:` 21:30 · `clima:` limpo
`elenco:` DINJER, CAPANGA_1, CAPANGA_2, CAPANGA_3 · `cam:` LONGE · `dur:` 10s · `gravar:` SIM

**Ação:** Um gerente entrega envelopes de demissão. Dinjer fica sem.

**NARRAÇÃO** — DJANIRA — 10s *(sem boca)*:
> "Eu demiti todo mundo no primeiro mês. Todo mundo menos ela. Não por bondade. Eu precisava dela ali, onde eu pudesse ver."

---

### EP13_C03 · COMPRANDO TUDO
`local:` GALPAO_CYPRESS · `hora:` 15:00 · `clima:` limpo
`elenco:` *(nenhum)* · `cam:` LONGE · `dur:` 13s · `gravar:` SIM

**Ação:** Montagem de 3 tomadas: galpões vazios, caminhões saindo, portões fechando. Sem personagem.

**NARRAÇÃO** — DJANIRA — 13s *(sem boca)*:
> "Depois eu comprei a transportadora dele. Depois o hangar. Depois a empresa de segurança que fazia a escolta dele. Eu não precisei de arma nenhuma. Eu comprei tudo que aquele homem tinha, com o dinheiro que era dele."

---

### EP13_C04 · O CHEFE DESCOBRE
`local:` MANSAO_CHEFE · `hora:` 16:00 · `clima:` limpo
`elenco:` O_CHEFE · `cam:` MEDIA · `dur:` 8s · `gravar:` SIM

**Ação:** O Chefe andando de um lado para o outro, nervoso, ao telefone. Expressão `com raiva`.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | O_CHEFE | "Como assim vendeu? Eu não autorizei venda nenhuma! Quem comprou? Me dá um nome!" | 8s | irritado |

---

### EP13_C05 · A FOTO NA PAREDE
`local:` BOATE_DENTRO · `hora:` 14:00 · `clima:` limpo
`elenco:` DINJER · `cam:` MEDIA → PERTO · `dur:` 12s · `gravar:` SIM

**Ação:** Dinjer limpando uma mesa na boate vazia. Olha para a parede. Aproxima o rosto. Entende. A foto emoldurada você monta no CapCut, usando um print da `DJANIRA_RICA`.

**NARRAÇÃO** — DJANIRA — 12s *(sem boca)*:
> "Penduraram a minha foto na parede da boate, porque é isso que se faz quando a casa troca de dono. Ela ficou uns dois minutos parada na frente daquela foto. E aí ela sentou no chão."

---

### EP13_C06 · NO CHÃO
`local:` BOATE_DENTRO · `hora:` 14:10 · `clima:` limpo
`elenco:` DINJER · `cam:` ALTA · `dur:` 8s · `gravar:` SIM

**Ação:** Dinjer sentada no chão da boate vazia. Câmera alta, olhando para baixo — ela pequena.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DINJER | "Não. Não, não, não. Ela morreu. Ela morreu." | 6s | negando |

**NARRADOR** — 6s:
> "A morta voltou. E agora ela é dona de tudo. Não perca o próximo episódio de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 14 — "VOCÊ SABIA"

**ANTERIORMENTE**: EP13_C05 (a foto) · EP13_C06 (no chão)

---

### EP14_C01 · O PORTÃO
`local:` MANSAO_DJANIRA · `hora:` 23:00 · `clima:` limpo
`elenco:` DINJER · `cam:` MEDIA · `dur:` 7s · `gravar:` SIM

**Ação:** Dinjer batendo no portão, desesperada, ainda de uniforme. Expressão `estressado`.

**NARRAÇÃO** — DJANIRA — 7s *(sem boca)*:
> "Ela apareceu no meu portão às onze da noite. Sem maquiagem, com o uniforme ainda."

---

### EP14_C02 · O REENCONTRO
`local:` MANSAO_DJANIRA · `hora:` 23:10 · `clima:` limpo
`elenco:` DJANIRA_RICA, DINJER · `cam:` auto · `dur:` 20s · `gravar:` SIM

> 💡 Posicione a Djanira **mais alta** que a Dinjer — em cima de um degrau, por exemplo. A câmera deve mostrar isso.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DINJER | "Djanira. Djanira, é você mesmo?" | 4s | implorando |
| 2 | DJANIRA | "Você levou três anos pra olhar na minha cara direito." | 5s | neutro |
| 3 | DINJER | "Amiga, eu procurei você. Eu juro que eu procurei." | 5s | implorando |
| 4 | DJANIRA | "Não me chama de amiga." | 3s | irritado |

---

### EP14_C03 · A PERGUNTA
`local:` MANSAO_DJANIRA · `hora:` 23:20 · `clima:` limpo
`elenco:` DJANIRA_RICA, DINJER · `cam:` PERTO · `dur:` 24s · `gravar:` SIM

> 🔴 **Depois da fala 2, deixe 4 segundos de silêncio absoluto antes da fala 3.** O silêncio é a cena.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DJANIRA | "Eu vou te fazer uma pergunta só. E se você mentir, eu descubro hoje ainda, porque agora eu tenho dinheiro pra descobrir qualquer coisa." | 10s | neutro |
| 2 | DJANIRA | "Você sabia?" | 2s | neutro |
| — | *(silêncio)* | *(Dinjer não responde. Câmera fica nela.)* | 4s | — |
| 3 | DJANIRA | "Você sabia." | 2s | neutro |

---

### EP14_C04 · DE JOELHOS
`local:` MANSAO_DJANIRA · `hora:` 23:25 · `clima:` limpo
`elenco:` DJANIRA_RICA, DINJER · `cam:` auto · `dur:` 28s · `gravar:` SIM

**Ação:** Dinjer cai de joelhos.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DINJER | "Eu não sabia que eles iam te matar. Eu juro pela minha mãe que eu não sabia." | 7s | implorando |
| 2 | DJANIRA | "Você já jurou pela sua mãe uma vez. Foi quando você pegou os meus mil reais." | 7s | irritado |
| 3 | DINJER | "Ele me mandou te levar pro avião. Ele falou que era só pra conversar. Ele falou que ia te dar um emprego." | 9s | implorando |
| 4 | DJANIRA | "E você acreditou." | 2s | neutro |
| 5 | DINJER | "Eu precisava acreditar, Djanira! Eu precisava!" | 4s | implorando |

---

### EP14_C05 · ELE SABE
`local:` MANSAO_DJANIRA · `hora:` 23:40 · `clima:` limpo
`elenco:` DJANIRA_RICA, DINJER · `cam:` auto · `dur:` 22s · `gravar:` SIM

**Ação:** Dinjer já na porta, para e fala sem se virar.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DINJER | "Djanira. Ele sabe que você tá viva. Ele descobriu na semana passada. E ele mandou te chamar pra jantar." | 10s | neutro |
| 2 | DJANIRA | "Então diz pra ele que eu aceito." | 3s | neutro |
| 3 | DINJER | "Você não pode ir. Você não sabe quem ele é." | 5s | implorando |
| 4 | DJANIRA | "Eu sei exatamente quem ele é. Eu descobri antes de você." | 5s | neutro |

**NARRADOR** — 7s:
> "Um jantar. Um pai. E uma verdade que vai virar tudo do avesso. Não perca o grande final de A Caloteira do Oeste."

---

# 🎬 EPISÓDIO 15 — GRANDE FINAL
## "O PAI"

**ANTERIORMENTE**: EP14_C03 ("você sabia") · EP14_C04 (de joelhos) · EP14_C05 ("ele mandou te chamar")

---

### EP15_C01 · A SUBIDA
`local:` MANSAO_CHEFE · `hora:` 20:00 · `clima:` limpo
`elenco:` DJANIRA_RICA · `cam:` LONGE · `dur:` 7s · `gravar:` SIM

**Ação:** Um carro subindo a estrada do morro até a mansão, à noite, com a cidade acesa lá embaixo.

**NARRAÇÃO** — DJANIRA — 7s *(sem boca)*:
> "Eu levei três anos pra subir aquela estrada. E eu subi de carro melhor que o dele."

---

### EP15_C02 · A MESA
`local:` MANSAO_CHEFE · `hora:` 20:15 · `clima:` limpo
`elenco:` DJANIRA_RICA, O_CHEFE, DINJER · `cam:` auto · `dur:` 22s · `gravar:` SIM

> 🔴 **Deixe a DINJER em quadro, ao fundo, parada, de uniforme, durante a cena inteira.** O público esquece que ela está ali. Quando a Djanira se virar para ela na cena C05, o susto dobra.

**Ação:** Jantar. O Chefe de um lado, a Djanira do outro. Dinjer em pé ao fundo com uma bandeja.

**NARRAÇÃO** — DJANIRA — 6s *(sem boca)*:
> "Ele não me reconheceu. Pra ele, aquela menina do interior morreu numa vala há três anos."

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | O_CHEFE | "Sabe o que eu mais admiro numa mulher de negócios? Discrição. A senhora comprou metade do que era meu e eu não fazia ideia de quem era a senhora." | 11s | explicando |
| 2 | DJANIRA | "Eu sou muito boa em ser invisível. Eu tive três anos de treino." | 5s | neutro |

---

### EP15_C03 · O PAPEL NA MESA
`local:` MANSAO_CHEFE · `hora:` 20:30 · `clima:` limpo
`elenco:` DJANIRA_RICA, O_CHEFE, DINJER · `cam:` auto · `dur:` 26s · `gravar:` SIM

> 🔴 **Depois da fala 1, deixe 3 segundos de silêncio.** O silêncio é a cena.

**Ação:** Djanira tira um papel dobrado do bolso e coloca na mesa.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DJANIRA | "O senhor conhece esse nome?" | 3s | neutro |
| — | *(silêncio)* | *(close no Chefe, expressão muda para `estressado`)* | 3s | — |
| 2 | O_CHEFE | "Onde é que a senhora conseguiu isso?" | 4s | neutro |
| 3 | DJANIRA | "Com a mulher que me criou. Ela me escondeu num roçado a vida inteira pro senhor não me achar." | 8s | neutro |
| 4 | O_CHEFE | "...Djanira." | 2s | machucado |
| 5 | DJANIRA | "O senhor sabe o meu nome." | 3s | neutro |
| 6 | O_CHEFE | "Eu escolhi o teu nome." | 3s | neutro |

---

### EP15_C04 · O MOTIVO
`local:` MANSAO_CHEFE · `hora:` 20:40 · `clima:` limpo
`elenco:` DJANIRA_RICA, O_CHEFE · `cam:` auto · `dur:` 34s · `gravar:` SIM

> 🔴 **O primeiro grande twist da temporada.**

**Ação:** O Chefe se levanta e anda até a janela, de costas.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DJANIRA | "Então o senhor sabia quem eu era naquele avião." | 5s | neutro |
| 2 | O_CHEFE | "Eu sabia." | 2s | neutro |
| 3 | O_CHEFE | "Eu não mandei te matar por causa das armas, minha filha. Arma eu resolvo com advogado. Eu mandei te matar porque tu ia fazer aniversário." | 12s | neutro |
| 4 | O_CHEFE | "Aquele dinheiro é meu. Eu botei no teu nome quando tu nasceu porque bebê não vai preso. Quando tu fizesse vinte e um, ele saía do meu controle pra sempre. Tu nunca foi minha filha. Tu era um prazo vencendo." | 15s | explicando |

---

### EP15_C05 · E VOCÊ?
`local:` MANSAO_CHEFE · `hora:` 20:50 · `clima:` limpo
`elenco:` DJANIRA_RICA, DINJER · `cam:` auto · `dur:` 30s · `gravar:` SIM

> 🔴 **O segundo twist, e o mais cruel.** A tomada da fala 4 é a mais longa da temporada: 12 segundos no rosto da Dinjer, sem cortar.

**Ação:** Djanira se levanta devagar e vira para trás, onde a Dinjer está.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | DJANIRA | "E você? Você sabia por quê?" | 4s | neutro |
| 2 | DINJER | "Djanira, não..." | 2s | implorando |
| 3 | DJANIRA | "Fala. Você me deve uma resposta há três anos." | 5s | irritado |
| 4 | DINJER | "Ele me disse que se eu te levasse pro avião, ele quitava a minha dívida." | 8s | implorando |
| 5 | DJANIRA | "Que dívida?" | 2s | neutro |
| 6 | DINJER | "Mil reais, Djanira. Eu devia mil reais pra ele." | 6s | machucado |
| 7 | DINJER | "Os teus mil reais." | 3s | machucado |

---

### EP15_C06 · A VOLTA COMPLETA
`local:` MANSAO_CHEFE · `hora:` 21:00 · `clima:` limpo
`elenco:` DJANIRA_RICA · `cam:` MEDIA · `dur:` 14s · `gravar:` SIM

**Ação:** Djanira parada no meio da sala, entre o pai e a amiga. Sem reação.

**NARRAÇÃO** — DJANIRA — 14s *(sem boca)*:
> "O meu dinheiro saiu da minha mão. Passou pela mão dela. Foi parar na mão do meu pai. E voltou pra mim como herança. Ninguém ganhou nada. Todo mundo só perdeu três anos."

---

### EP15_C07 · A SAÍDA
`local:` MANSAO_CHEFE · `hora:` 21:05 · `clima:` limpo
`elenco:` DJANIRA_RICA, O_CHEFE · `cam:` auto · `dur:` 12s · `gravar:` SIM

**Ação:** Djanira anda até a porta e sai, sem olhar para trás.

**DIÁLOGO** *(com boca)*:

| # | Quem | Texto | Dur | Gesto |
|---|---|---|---|---|
| 1 | O_CHEFE | "Djanira. Djanira, espera. A gente pode conversar." | 5s | implorando |
| 2 | DJANIRA | "O senhor teve dezoito anos pra conversar." | 5s | neutro |

---

### EP15_C08 · OS QUE FICARAM
`local:` MANSAO_CHEFE · `hora:` 21:10 · `clima:` limpo
`elenco:` O_CHEFE, DINJER · `cam:` LONGE · `dur:` 12s · `gravar:` SIM

**Ação:** Duas tomadas de 4 segundos, sem música, só som ambiente. Primeiro o Chefe sentado sozinho na mesa enorme. Depois a Dinjer ainda em pé, no mesmo lugar, de uniforme, com a bandeja na mão.

**NARRAÇÃO** — DJANIRA — 10s *(sem boca)*:
> "E a Dinjer continuou ali. De pé, de uniforme, com a bandeja na mão. Não mudou nada pra ela. Ela começou servindo e terminou servindo."

---

### EP15_C09 · A ÚLTIMA IMAGEM
`local:` MANSAO_CHEFE · `hora:` 21:15 · `clima:` limpo
`elenco:` DJANIRA_RICA · `cam:` LONGE · `dur:` 12s · `gravar:` SIM

**Ação:** Djanira entra no carro e desce o morro. A cidade acesa lá embaixo. Corte para o preto no fim.

**NARRAÇÃO** — DJANIRA — 10s *(sem boca)*:
> "Eu passei três anos achando que eu queria vingança. Eu só queria os meus mil reais."

**NARRADOR** — 9s:
> "A Caloteira do Oeste. Uma história sobre o que mil reais são capazes de fazer com a vida de uma pessoa. Em breve, a segunda temporada."

---

# 🎞️ A VINHETA FINAL

> Grave **uma vez só**. Entra no fim dos 15 episódios.

### VINHETA_01 · OS CRÉDITOS
`local:` PRAIA_DELPERRO · `hora:` 18:30 · `clima:` limpo
`elenco:` DJANIRA_RICA, DINJER, DONA_IRACI, O_CHEFE · `cam:` MEDIA · `dur:` 20s · `gravar:` SIM

**Ação:** Os quatro dançando, um de cada vez, 5 segundos cada. Danças diferentes por personagem.

| Personagem | Dança |
|---|---|
| DJANIRA_RICA | `curtindo` |
| DINJER | `requebrando` |
| DONA_IRACI | `duro de dançar` *(é a piada)* |
| O_CHEFE | `deslizando` |

**Os créditos eu monto no CapCut:**

```
A CALOTEIRA DO OESTE
Uma novela de FELIPE SANTIAGO
Roteiro — FELIPE SANTIAGO
Direção — FELIPE SANTIAGO
Fotografia — FELIPE SANTIAGO
Dono do computador — FELIPE SANTIAGO
Quem pagou o GTA — FELIPE SANTIAGO
Agradecimento especial — FELIPE SANTIAGO
```

---

# PARTE 5 — RESUMO DE PRODUÇÃO

## Contagem

| Item | Quantidade |
|---|---|
| Episódios | 15 |
| Cenas a gravar | 94 + 1 vinheta |
| Locais a marcar | 21 |
| Personagens | 5 principais + figurantes |
| Cenas com diálogo *(boca mexendo)* | 29 |
| Cenas só com narração *(sem boca)* | 65 |

> 💡 **Dois terços das cenas não precisam de animação facial nenhuma.** Isso reduz muito o seu trabalho e o risco de bug. Comece pelas cenas de narração, que são mais simples, e deixe as de diálogo para depois que a boca estiver funcionando.

### Cenas por episódio

| Ep | Cenas | Ep | Cenas | Ep | Cenas |
|---|---|---|---|---|---|
| 1 | 9 | 6 | 6 | 11 | 5 |
| 2 | 7 | 7 | 5 | 12 | 5 |
| 3 | 7 | 8 | 6 | 13 | 6 |
| 4 | 6 | 9 | 6 | 14 | 5 |
| 5 | 5 | 10 | 7 | 15 | 9 |

## Ordem de produção sugerida

Agrupe por **local e figurino**, não por episódio. Assim eu teleporto e troco de roupa o mínimo possível.

| Bloco | Personagem | Locais | Cenas |
|---|---|---|---|
| 1 | DJANIRA_POBRE | ROCA_GRAPESEED, CASA_VO | Eps 1,2,3,4,5,8,9 |
| 2 | DONA_IRACI | CASA_VO, HOSPITAL_SANDY, CASSINO_FORA | Eps 1,2,4,5,8 |
| 3 | DINJER | AEROPORTO, CASA_DINJER | Eps 1,3,4,5 |
| 4 | DJANIRA_POBRE | ESTRADA_SENORA, cidade | Eps 3,4,8,9,10 |
| 5 | Noite, boate | BOATE_FORA, BOATE_DENTRO | Eps 2,6,12,13 |
| 6 | Avião e queda | AEROPORTO_AR, CANAL_LS | Eps 1,7,8 |
| 7 | DJANIRA_RICA | BANCO_MAZE, MANSAO_DJANIRA | Eps 11,14 |
| 8 | Final | MANSAO_CHEFE | Ep 15 |
| 9 | Vinheta | PRAIA_DELPERRO | — |

## O que eu preciso que você me diga antes de começar

1. A ordem em que convém eu marcar os 21 locais, agrupada por região
2. Quais figurinos você monta sozinho e quais eu monto no Menyoo
3. Se o interior da boate abre no modo história, ou se precisamos do Open All Interiors
4. Quais animações de gesto você achou no `PedAnimList` e quais faltam
5. Se a queda do avião precisa de etapa própria antes das outras cenas
