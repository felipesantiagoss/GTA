# EP14 — "VOCÊ SABIA"

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
| EP14_C01 | 00:00:00,000 | 7s | `EP14/srt_base/EP14_C01.base.srt` |
| EP14_C02 | 00:00:07,000 | 20s | `EP14/srt_base/EP14_C02.base.srt` |
| EP14_C03 | 00:00:27,000 | 24s | `EP14/srt_base/EP14_C03.base.srt` |
| EP14_C04 | 00:00:51,000 | 29s | `EP14/srt_base/EP14_C04.base.srt` |
| EP14_C05 | 00:01:20,000 | 30s | `EP14/srt_base/EP14_C05.base.srt` |
| VINHETA_01 | 00:01:50,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP13_C05 (a foto) · EP13_C06 (no chão)

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP14_C01

**Ação:** Dinjer batendo no portão, desesperada, ainda de uniforme. Expressão `estressado`.

- A indicação sem maquiagem conflita com visual fixo de Dinjer. preservar origem e implementar regra do prompt explicitamente

### EP14_C02


> 💡 Posicione a Djanira **mais alta** que a Dinjer — em cima de um degrau, por exemplo. A câmera deve mostrar isso.

### EP14_C03


> 🔴 **Depois da fala 2, deixe 4 segundos de silêncio absoluto antes da fala 3.** O silêncio é a cena.

### EP14_C04

**Ação:** Dinjer cai de joelhos.

- Estimativa 28000ms menor que blocos declarados 29000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

### EP14_C05

**Ação:** Dinjer já na porta, para e fala sem se virar.

- Estimativa 22000ms menor que blocos declarados 30000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

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
