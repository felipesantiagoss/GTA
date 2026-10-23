# Prompt para o Codex — do teste atual até gravar uma cena completa

---

## 1. Onde estamos

Funcionou. Confirmei no jogo: apertei F6, apareceram um homem e uma mulher na minha frente, com a mensagem "Diretor: homem e mulher montados à sua frente. F10 limpa; F6 monta novamente." Sem erro vermelho. F10 limpou.

A **Etapa 2 está concluída**. O script carrega, cria personagens, mostra mensagem e limpa sem quebrar.

## 2. O que eu quero nesta fase

**Uma cena completa do meu roteiro, gravável do começo ao fim.** Eu aperto uma tecla, a cena se monta no lugar certo, com a hora e o clima certos, os personagens certos, sem gente estranha no quadro. Aperto outra tecla e a cena acontece: os personagens andam, se olham, falam com a boca mexendo, e a câmera corta sozinha entre os planos. Eu só gravo a tela.

No fim desta fase eu quero ter **o Episódio 1, Cena 4 gravada** — a cena em que a Dinjer pede o dinheiro emprestado.

## 3. Formato do vídeo — horizontal

**O vídeo final é horizontal, 16:9, do jeito que o jogo renderiza.** Eu posto no TikTok e o público gira o celular.

Isso significa que você **não precisa** de guia de enquadramento vertical, zona segura, nem de se preocupar com corte lateral. **Use a tela inteira.** Compor em 16:9 é o que vamos fazer, e é o formato nativo do jogo — nada se perde.

---

## 4. Leia o roteiro primeiro

Junto com este prompt está o arquivo **`ROTEIRO_A_CALOTEIRA_DO_OESTE_15_EPISODIOS.md`**. É a novela inteira: 15 episódios, 94 cenas, já estruturada para você.

**Leia o arquivo completo antes de escrever qualquer código.** A Parte 0 dele explica o formato de cada cena. A Parte 1 traz o elenco, a Parte 2 os 21 locais, a Parte 3 o vocabulário.

Cada cena já vem com local, hora, clima, elenco, câmera, duração e falas prontas. **Você não precisa inventar nada** — só transformar em arquivo de cena.

### 🔴 A regra que mais simplifica o seu trabalho

O roteiro separa dois tipos de texto, e eles se comportam de forma diferente:

| Tipo | Boca mexe? | O personagem |
|---|---|---|
| **NARRAÇÃO** | **NÃO** | só age: anda, olha, trabalha. A voz é minha, por cima da imagem |
| **DIÁLOGO** | **SIM** | fala com animação facial e gesto de corpo |

**Das 94 cenas, só 29 têm diálogo.** As outras 65 são narração e não precisam de animação facial nenhuma. O tipo está declarado em cada bloco — nunca presuma.

Comece implementando as cenas de narração, que são mais simples. Deixe as de diálogo para depois que a boca estiver funcionando e confirmada por mim.

### O que me dizer depois de ler

1. A ordem em que convém eu marcar os 21 locais, agrupada por região do mapa
2. Quais figurinos você monta sozinho e quais eu preciso montar no Menyoo
3. Se o interior da boate abre no modo história, ou se precisamos de um mod para isso
4. Quais animações de gesto você achou em `menyooStuff\PedAnimList` e quais faltam
5. Se a queda do avião precisa de etapa própria antes das demais cenas

### Regras fixas do roteiro

- **Só a Djanira troca de roupa** — `DJANIRA_POBRE` nos episódios 1 a 10, `DJANIRA_RICA` nos 11 a 15. O rosto é o mesmo nas duas. Todos os outros personagens usam a mesma roupa do começo ao fim.
- **A dívida é R$ 1.000.** Não altere valores, falas, nomes nem a ordem dos episódios.
- Os IDs de cena do roteiro viram os nomes de arquivo: a cena `EP01_C08` vira `EP01_C08.json`, `EP01_C08.mp4` e `EP01_C08.srt`.

### Não gere tudo de uma vez

1. Gere **só a cena `EP01_C08`** primeiro, para eu testar o ciclo inteiro.
2. Depois do meu "ok", gere o resto do Episódio 1.
3. Só então siga para os episódios seguintes, **um por vez**.

---

## 5. A CÂMERA — leia esta seção inteira antes de projetar

É a parte que mais decide se o vídeo presta.

### 5.1 Planos pré-definidos — eu não quero digitar coordenadas

Sou iniciante. Não quero escrever posição XYZ e rotação na mão. Quero escrever no arquivo de cena:

```
camera: PERTO em DJANIRA
```

E o script calcula a posição sozinho, **relativa ao personagem e à direção para onde ele está virado**.

Implemente estes planos como presets. Os valores são ponto de partida — ajuste se o teste mostrar que ficou ruim:

| Nome | Distância | Altura | Ângulo | FOV | Para que serve |
|---|---|---|---|---|---|
| `LONGE` | 8 a 12 m | 1,8 m | nível dos olhos | 50 | Estabelecer o lugar, mostrar solidão |
| `DOIS` | 4 a 5 m | 1,6 m | nível dos olhos | 45 | Os dois personagens no mesmo quadro |
| `MEDIA` | 3 a 4 m | 1,6 m | nível dos olhos | 40 | Plano padrão de diálogo |
| `PERTO` | 1,2 a 1,8 m | 1,65 m | nível dos olhos | 30 | Emoção, revelação, reação |
| `BAIXA` | 2 a 3 m | 0,4 m | olhando para cima | 35 | Deixa o personagem poderoso |
| `ALTA` | 3 a 4 m | 3,5 m | olhando para baixo | 35 | Deixa o personagem pequeno, derrotado |
| `ATRAS` | 1,5 m atrás, 0,6 m de lado | 1,7 m | sobre o ombro | 35 | Conversa, chegada, perseguição |
| `OLHOS` | primeira pessoa | — | — | 45 | O que o personagem está vendo |

Regras:
- Todo preset é calculado **relativo à direção em que o personagem está virado**, nunca ao norte do mapa.
- Todo preset aceita um **ajuste fino opcional** no arquivo de cena: deslocar para o lado, subir, descer, aproximar. Para quando o cenário atrapalhar.
- Enquadre usando a **regra dos terços**: o personagem não fica centralizado, fica em um terço do quadro, olhando para o espaço vazio do outro lado.

### 5.2 Corte automático no diálogo

O comportamento padrão: **quando um personagem começa a falar, a câmera corta para o plano dele.**

Eu não quero declarar isso em toda fala. Quero que seja automático, e poder sobrescrever quando quiser.

Exemplo do que o script faz sozinho numa cena com três falas:

```
0s  → LONGE          estabelece o lugar
2s  → MEDIA em DINJER     ela começa a falar
11s → PERTO em DJANIRA    ela responde
17s → MEDIA em DINJER     ela fala de novo
20s → DOIS                fecha com as duas
```

No arquivo de cena eu só escrevo as falas. Se eu quiser um plano diferente em uma fala específica, eu declaro só nela.

### 5.3 A regra do eixo — isto é obrigatório no corte automático

Numa conversa entre duas pessoas, existe uma linha imaginária ligando uma à outra. **A câmera precisa ficar sempre do mesmo lado dessa linha.**

Se o script cortar de um lado para o outro, os personagens parecem trocar de lugar na tela e a conversa fica confusa de assistir. Esse é o erro mais comum em corte automático.

Implemente assim:
- Ao montar a cena, calcule a linha entre os dois personagens.
- Escolha um lado (o padrão pode ser o lado onde a primeira câmera está).
- **Todos os planos daquela cena ficam nesse mesmo lado.**
- Deixe uma opção no arquivo de cena para inverter o lado, caso eu queira.

### 5.4 Marcar câmera no jogo

Além dos presets, quero uma tecla que **salve a posição e a rotação atuais da câmera livre** com um nome, no catálogo. Para quando eu achar um ângulo bom voando pelo cenário e quiser guardar.

### 5.5 O que mais a câmera precisa fazer

- **Esconder HUD e radar** automaticamente quando a cena começa, e devolver quando limpa.
- **Contagem de 3 segundos** parada antes da ação começar, para eu ter folga no corte.
- **Congelar no último quadro** quando a cena acaba, em vez de devolver o controle de repente.
- **Modo livre**: uma tecla que solta a câmera para eu voar e procurar ângulo, sem bagunçar a cena montada.
- **Transição entre planos**: corte seco é o padrão. Deixe disponível também um movimento suave de um ponto a outro, para quando eu quiser.

---

## 6. As falas e a boca mexendo

Requisito número um da série. Sem boca mexendo o vídeo não funciona.

### 6.1 A implementação

```
PlayFacialAnim(ped, "mic_chatter", "mp_facial")
```

É a mesma animação que o GTA Online usa quando um jogador fala no microfone.

Quando a fala termina, volte ao neutro:
- Homem: `mood_normal_1` do dicionário `facials@gen_male@variations@normal`
- Mulher: `mood_normal_1` do dicionário `facials@gen_female@variations@normal`

Regras:
- Peça os dicionários com `RequestAnimDict` e **espere carregar** antes de usar.
- A boca precisa mexer **durante toda a duração declarada da fala**. Se a animação acabar antes, reaplique em laço.
- Se `mic_chatter` não funcionar no teste, implemente como plano B as natives de fala ambiente (o personagem fala uma frase real do jogo, com animação facial completa) e eu desligo o áudio de diálogo nas configurações do jogo.

### 6.2 Gesto de corpo junto

Boca mexendo com o corpo parado fica estranho. Toda fala precisa de um gesto de conversa junto. Procure animações de conversa em `menyooStuff\PedAnimList` e monte um catálogo por intenção:

| Intenção | Quando usar |
|---|---|
| `neutro` | fala comum |
| `explicando` | quando o personagem convence ou enrola |
| `negando` | quando nega ou recusa |
| `implorando` | quando pede |
| `irritado` | quando confronta |

No arquivo de cena eu escolho pela intenção, não pelo nome técnico da animação.

### 6.3 Quem não fala, olha

Durante uma fala, os outros personagens precisam **virar a cabeça e olhar para quem está falando**. Sem isso parece que estão em cenas diferentes.

### 6.4 O arquivo .srt

Toda cena gera um arquivo `.srt` com os tempos exatos de cada fala, para eu alinhar as vozes no CapCut. Mesmo nome da cena, extensão `.srt`.

---

## 7. O ambiente — o quadro precisa estar limpo

Na minha captura de teste dá para ver pedestres aleatórios andando no fundo e um carro passando. **Isso não pode acontecer na gravação.**

Toda cena, antes de montar, precisa:

- **Teleportar** para o local marcado
- **Congelar a hora** no horário declarado
- **Travar o clima** no declarado
- **Zerar a densidade** de pedestres e de trânsito na área
- **Limpar** os NPCs e veículos que já estiverem no raio da cena
- **Bloquear a polícia** e eventos aleatórios
- **Esconder o jogador** — deixar o Franklin invisível e parado fora do quadro

Esse último é importante. Na captura de teste o Franklin está em pé no meio do quadro. Na cena de verdade ele não pode aparecer.

Deixe o raio de limpeza configurável no `.ini`. Meu computador é fraco e limpar área grande demais pode travar.

---

## 8. O formato do arquivo de cena

Projete um formato que eu consiga ler e editar sozinho, em português, sem saber programar. O que a cena precisa declarar:

```
Identificação:  episódio, número da cena, nome
Ambiente:       local (do catálogo), hora, clima, raio de limpeza
Elenco:         quais personagens, em qual marca de posição, virados para onde
Ações:          sequência por personagem — andar até, virar para, animação, esperar
Falas:          personagem, texto, duração em segundos, intenção do gesto,
                e opcionalmente o plano de câmera a usar
Câmera:         lista de planos com o tempo de cada corte,
                ou "automático" para seguir quem fala
```

Requisitos:
- **Validação** com mensagens de erro claras em português, dizendo qual campo está errado e o que era esperado.
- **Nunca inventar** nome de animação, hash de modelo ou coordenada. Se faltar no catálogo, me avise.
- Arquivos de cena em `%USERPROFILE%\GTA_Diretor\cenas`, fora de Program Files.

---

## 9. A gravação — pastas, nomes e automação

Esta seção é uma decisão de projeto importante. Leia inteira.

### 9.1 A estrutura de pastas

Crie e mantenha esta estrutura dentro de `%USERPROFILE%\GTA_Diretor\`:

```
%USERPROFILE%\GTA_Diretor\
├── cenas\                      ← os arquivos .json de cena
├── logs\                       ← o log do Diretor
├── catalogos\                  ← elenco, locais, roupas, animações
└── Serie\
    └── A_CALOTEIRA_1_TEMP\
        ├── EP01\
        │   ├── EP01_C01.mp4
        │   ├── EP01_C01.srt
        │   ├── EP01_C02.mp4
        │   ├── EP01_C02.srt
        │   └── ...
        ├── EP02\
        └── ... até EP15\
```

Regras:
- **Uma pasta por episódio**, criada automaticamente quando eu carrego a primeira cena daquele episódio.
- Cada cena gera **dois arquivos de mesmo nome**: o vídeo `.mp4` e a legenda `.srt`.
- O nome sai do próprio arquivo de cena, nunca é digitado por mim.
- Se eu regravar uma cena que já existe, **não sobrescreva**. Salve como `EP01_C08_v2.mp4`, `v3`, e assim por diante, e me avise na tela qual versão acabou de ser salva.

### 9.2 Um arquivo de vídeo por cena

**Cada cena vira um arquivo separado.** Não vou gravar o episódio inteiro de uma vez. Os motivos:

1. **Meu computador é fraco.** Gravação longa aumenta o risco de engasgo, e um engasgo no minuto 4 estraga tudo que veio antes.
2. **As cenas são em lugares diferentes.** Entre uma e outra tem teleporte e carregamento, que não podem aparecer no vídeo.
3. **Se uma tomada sair ruim, eu refaço só aquela.** Numa gravação única eu perderia o episódio inteiro.
4. **No CapCut eu monto** narração, cartelas e o "Anteriormente" entre as cenas. Clipes separados facilitam.

Projete o sistema para **montar, gravar, limpar, próxima cena** — não para encadear o episódio inteiro.

### 9.3 NÃO use a gravação da Steam

Eu tinha pensado em usar a gravação de jogo da Steam, mas ela não serve para este projeto:

- Ela **não salva MP4**. Salva fragmentos `.m4s` numa pasta interna do Steam.
- Para virar MP4 é preciso **exportar manualmente, um vídeo por vez**, pela interface do Steam.
- O recurso tem problemas conhecidos de exportação em lote.

Com cerca de 120 clipes na temporada, isso seria inviável.

### 9.4 Use a Barra de Jogos do Windows

Vamos usar a **Barra de Jogos do Windows** (Game Bar), que:

- **já vem instalada** no Windows 11, sem precisar instalar nada nem pedir administrador;
- **salva MP4 direto**, sem etapa de exportação;
- salva em `%USERPROFILE%\Videos\Capturas` (ou `Videos\Captures`).

Atalhos padrão:
- `Win + G` abre a barra
- `Win + Alt + R` começa e para a gravação

**Atenção a um conflito que eu preciso resolver:** no Rockstar Games Launcher está marcada a opção **"Desabilitar tecla Windows"**. Enquanto ela estiver marcada, `Win + Alt + R` não vai funcionar dentro do jogo. Me diga explicitamente para desmarcar essa opção antes do primeiro teste de gravação.

Se por algum motivo a Barra de Jogos não funcionar, a alternativa é o **OBS Studio** (gratuito, salva MP4 direto, aceita qualquer pasta e qualquer nome de arquivo). Só proponha isso se a Barra de Jogos falhar.

### 9.5 A automação — o que eu quero que o script faça sozinho

Quero que o script cuide da gravação inteira, sem eu precisar lembrar de nada. Implemente em duas camadas, nesta ordem:

**Camada 1 — organizar sozinho (faça primeiro, é a mais importante)**

Depois que a gravação terminar, o script:
1. Olha a pasta `%USERPROFILE%\Videos\Capturas`
2. Pega o **arquivo de vídeo mais recente**
3. **Renomeia** para o nome da cena, por exemplo `EP01_C08.mp4`
4. **Move** para `...\Serie\A_CALOTEIRA_1_TEMP\EP01\`
5. Gera o `.srt` junto, com o mesmo nome
6. Mostra na tela: "Salvo: EP01_C08.mp4"

Isso já resolve 90% do problema. Eu nunca mais organizo arquivo na mão, nem corro risco de misturar cenas.

**Camada 2 — apertar o botão sozinho (faça depois que a camada 1 funcionar)**

O script simula o atalho `Win + Alt + R` pelo Windows (P/Invoke das APIs de teclado do Windows), de modo que o ciclo fique:

1. Aperto **Ação**
2. O script liga a gravação sozinho
3. Conta 3 segundos parado
4. Roda a cena
5. Para a gravação sozinho
6. Renomeia e move o arquivo
7. Mostra "Salvo: EP01_C08.mp4"

Se a simulação de tecla não funcionar (por bloqueio do Windows, da empresa ou da opção do launcher), **não insista**. Me avise e deixe a camada 1 funcionando, que eu aperto `Win + Alt + R` na mão. Com a camada 1 pronta, apertar um atalho não é trabalho nenhum.

### 9.6 O ciclo que eu vou repetir centenas de vezes

| Passo | O que eu faço | O que o script faz |
|---|---|---|
| 1 | Aperto **Montar** | Teleporta, limpa a área, trava hora e clima, cria os personagens nas marcas, esconde o jogador, posiciona a primeira câmera, esconde o HUD |
| 2 | Confiro o enquadramento | Se eu quiser, solto a câmera livre e ajusto |
| 3 | Aperto **Ação** | Liga a gravação, conta 3 segundos, roda ações, falas e cortes de câmera |
| 4 | Espero acabar | Congela no último quadro, para a gravação, renomeia e move o arquivo |
| 5 | Ficou ruim → **Reiniciar tomada** | Volta todo mundo à posição inicial, pronto para repetir do passo 3 |
| 6 | Ficou bom → **Próxima cena** | Limpa e carrega a cena seguinte |

O passo 5 precisa ser instantâneo e confiável. É o que eu mais vou usar.

### 9.7 Fila de cenas

Quero carregar **todas as cenas de um episódio numa fila** e ir passando com uma tecla, sem voltar a menu nenhum. O script mostra na tela qual cena está carregada, por exemplo "EP01 — Cena 4 de 9".

### 9.8 Não usamos o Rockstar Editor

Meu computador tem vídeo Intel integrado. Renderizar no Rockstar Editor levaria tempo demais por clipe e inviabilizaria os 15 episódios. **A câmera é controlada pelo script**, e a gravação é da tela em tempo real.

As teclas do Diretor não podem conflitar com **F4** (console do SHVDN), **F8** (Menyoo), **F12** e **Shift+Tab** (Steam), **Win+Alt+R** (gravação) e **Alt+Tab**. Todas configuráveis no `.ini`.

---

## 10. A cena de teste desta fase

A cena **`EP01_C08`** do roteiro, chamada "O PEDIDO". Está escrita por inteiro no arquivo, com local, hora, clima, elenco, ação, narração e as cinco falas com duração e gesto.

É a cena certa para testar porque ela exerce **tudo de uma vez**: teleporte para um local marcado, dois personagens com figurinos diferentes, um bloco de narração sem boca, cinco falas com boca e gesto, corte automático de câmera, e gravação com arquivo nomeado e movido para a pasta do episódio.

Se eu conseguir montar, rodar e gravar a `EP01_C08`, o sistema está pronto para as outras 93 cenas.

---

## 11. Etapas desta fase

Uma de cada vez. Em cada uma: diga o que vai fazer, faça, me diga exatamente o que eu faço no jogo e o que devo ver, e **espere meu retorno**.

| # | Etapa | Como eu confirmo |
|---|---|---|
| **3** | Falas com boca mexendo e gesto de corpo, nos dois personagens que já funcionam | Vejo a boca mexer durante o tempo declarado e o outro olhando para quem fala |
| **4** | Câmera: presets, regra do eixo, HUD escondido, contagem de 3 segundos | Vejo a câmera nos ângulos certos, sem trocar de lado |
| **5** | Corte automático seguindo quem fala | A câmera corta sozinha quando muda o personagem que fala |
| **6** | Ambiente: teleporte, hora, clima, limpar área, esconder jogador | O quadro fica limpo, sem pedestres nem o Franklin |
| **7** | Formato de cena, validação e catálogos a partir do roteiro | Consigo editar um arquivo de cena e ver a mudança no jogo |
| **8** | Fila de cenas e reiniciar tomada | Passo de uma cena para outra e repito tomadas sem travar |
| **9** | Pastas da série e organização automática do arquivo gravado | Gravo uma vez e o MP4 aparece renomeado na pasta do episódio |
| **10** | A cena EP01_C08 completa, gravada | Mando o arquivo para você |

---

## 12. Como me responder

1. Uma linha por mudança que você fez.
2. O que você precisa que eu marque ou monte no jogo antes de testar.
3. O passo a passo: qual tecla, em que ordem, o que devo ver em cada uma.
4. O que você quer que eu confirme depois do teste.

**Lembre: você não vê o jogo.** Nada é considerado funcionando até eu confirmar. Separe sempre o que compilou, o que você supõe e o que eu confirmei.
