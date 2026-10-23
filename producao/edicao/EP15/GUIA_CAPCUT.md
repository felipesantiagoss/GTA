# EP15 — GRANDE FINAL

Material de preparação para dublagem/CapCut. Áudios sugeridos ainda não existem; vídeos ainda não são confirmados por este guia.

A ordem abaixo é a do roteiro. A vinheta gravada uma única vez entra no final. Planos/SRTs fonte permanecem intactos.

## Referência de tempo e sincronismo

A entrada de voz no CSV tem três referências: conteúdo(sem contagem), F7(com3s de contagem) e episódio preliminar. O SRT base é relativo a F7, sem inferir o começo do MP4.

No CapCut, identifique o início real da ação após a contagem e retire toda a preparação anterior. Se a captura começou antes deF7, remover apenas3s do começo do MP4 dá erro. Use imagem/contagem para ajustar o offset de cada tomada; data de criação do arquivo não sincroniza voz.

O SRT de continuidade assume clipes com as durações planejadas abaixo, já sem contagem. ANTERIORMENTE/cartelas/flashbacks extras, transições e recortes de duração indefinida ficam fora dessa conta. Ao inseri-los, desloque/revise os tempos no CapCut. Não tratar esta duração como medida de um filme real.

Reservas de tempo do cabeçalho não são falas nem silêncios escritos. Vozes e pausas explícitas nunca devem ser comprimidas para caber na estimativa.

## Ordem de montagem

| Cena | Entrada base | Duração planejada | SRT fonte |
|---|---|---|---|
| EP15_C01 | 00:00:00,000 | 7s | `EP15/srt_base/EP15_C01.base.srt` |
| EP15_C02 | 00:00:07,000 | 22s | `EP15/srt_base/EP15_C02.base.srt` |
| EP15_C03 | 00:00:29,000 | 26s | `EP15/srt_base/EP15_C03.base.srt` |
| EP15_C04 | 00:00:55,000 | 34s | `EP15/srt_base/EP15_C04.base.srt` |
| EP15_C05 | 00:01:29,000 | 30s | `EP15/srt_base/EP15_C05.base.srt` |
| EP15_C06 | 00:01:59,000 | 14s | `EP15/srt_base/EP15_C06.base.srt` |
| EP15_C07 | 00:02:13,000 | 12s | `EP15/srt_base/EP15_C07.base.srt` |
| EP15_C08 | 00:02:25,000 | 12s | `EP15/srt_base/EP15_C08.base.srt` |
| EP15_C09 | 00:02:37,000 | 19s | `EP15/srt_base/EP15_C09.base.srt` |
| VINHETA_01 | 00:02:56,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP14_C03 ("você sabia") · EP14_C04 (de joelhos) · EP14_C05 ("ele mandou te chamar")

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP15_C01

**Ação:** Um carro subindo a estrada do morro até a mansão, à noite, com a cidade acesa lá embaixo.

### EP15_C02

**Ação:** Jantar. O Chefe de um lado, a Djanira do outro. Dinjer em pé ao fundo com uma bandeja.

> 🔴 **Deixe a DINJER em quadro, ao fundo, parada, de uniforme, durante a cena inteira.** O público esquece que ela está ali. Quando a Djanira se virar para ela na cena C05, o susto dobra.

### EP15_C03

**Ação:** Djanira tira um papel dobrado do bolso e coloca na mesa.

- Intenção 'machucado' fora dos cinco gestos catalogados; pesquisar e testar sem substituir silenciosamente. preservar origem e implementar regra do prompt explicitamente

> 🔴 **Depois da fala 1, deixe 3 segundos de silêncio.** O silêncio é a cena.

### EP15_C04

**Ação:** O Chefe se levanta e anda até a janela, de costas.

> 🔴 **O primeiro grande twist da temporada.**

### EP15_C05

**Ação:** Djanira se levanta devagar e vira para trás, onde a Dinjer está.

- Intenção 'machucado' fora dos cinco gestos catalogados; pesquisar e testar sem substituir silenciosamente. preservar origem e implementar regra do prompt explicitamente

- Nota pede manter a câmera na fala 4 por 12s; a voz dura 8s. Preservar ambos, não estender/comprimir a fala. preservar origem e implementar regra do prompt explicitamente

> 🔴 **O segundo twist, e o mais cruel.** A tomada da fala 4 é a mais longa da temporada: 12 segundos no rosto da Dinjer, sem cortar.

### EP15_C06

**Ação:** Djanira parada no meio da sala, entre o pai e a amiga. Sem reação.

### EP15_C07

**Ação:** Djanira anda até a porta e sai, sem olhar para trás.

### EP15_C08

**Ação:** Duas tomadas de 4 segundos, sem música, só som ambiente. Primeiro o Chefe sentado sozinho na mesa enorme. Depois a Dinjer ainda em pé, no mesmo lugar, de uniforme, com a bandeja na mão.

- Duas tomadas de 4s totalizam 8s; narração de 10s e estimativa de 12s permanecem separadas, montagem a definir. preservar origem e implementar regra do prompt explicitamente

### EP15_C09

**Ação:** Djanira entra no carro e desce o morro. A cidade acesa lá embaixo. Corte para o preto no fim.

- Estimativa 12000ms menor que blocos declarados 19000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

### EP15_C05 — câmera na Dinjer

> 🔴 **O segundo twist, e o mais cruel.** A tomada da fala 4 é a mais longa da temporada: 12 segundos no rosto da Dinjer, sem cortar.

Manter a câmera por12s desde o começo da fala4 (00:00:11,000–00:00:23,000 do conteúdo). A voz dessa fala dura8s. O quadro atravessa falas seguintes; não adicionar4s de silêncio nem alterar o texto/duração das vozes.

### VINHETA_01 — reutilizar no final

Uma gravação, quatro tomadas de5s; danças ainda exigem clipes reais/testados. Créditos montados no CapCut:

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

## Dublagem

O CSV inclui todos os blocos originais, inclusive pausas/ação final sem voz. ArquivoAudioSugerido é somente um nome reservado na preparação: nenhum WAV/MP3 foi gerado. Narração e NARRADOR ficam emoff, sem boca; diálogos usam a atuação de fala. Não dublar os silêncios.

## Conferência antes da exportação

- Conferir visual/ações/props/câmera na tomada real, sem substituir ações ausentes.
- Alinhar cada voz ao marcador visual do clipe e conferir os silêncios.
- Rever o SRT depois de inserir recap/cartelas/flashbacks/transições.
- Conferir o vídeo horizontal16:9 e o nome real da tomada; preservar regravações `_v2` etc.
