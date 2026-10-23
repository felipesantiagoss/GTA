# EP09 — "VINTE E UM"

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
| EP09_C01 | 00:00:00,000 | 13s | `EP09/srt_base/EP09_C01.base.srt` |
| EP09_C02 | 00:00:13,000 | 9s | `EP09/srt_base/EP09_C02.base.srt` |
| EP09_C03 | 00:00:22,000 | 6s | `EP09/srt_base/EP09_C03.base.srt` |
| EP09_C04 | 00:00:28,000 | 24s | `EP09/srt_base/EP09_C04.base.srt` |
| EP09_C05 | 00:00:52,000 | 10s | `EP09/srt_base/EP09_C05.base.srt` |
| EP09_C06 | 00:01:02,000 | 19s | `EP09/srt_base/EP09_C06.base.srt` |
| VINHETA_01 | 00:01:21,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP08_C03 (a herança) · EP08_C04 (o cassino) · EP08_C05 (o papel)

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP09_C01

**Ação:** Djanira sozinha na frente de uma cova. Ninguém mais em cena. É a solidão que conta a história.

- A ação exige Djanira sozinha; a narração cita outras pessoas. Não criar figurantes contrariando a ação. preservar origem e implementar regra do prompt explicitamente

### EP09_C02

**Ação:** Djanira abre o papel dobrado. Close no papel e no rosto dela.

> 🔴 **Plantado aqui, colhido no Episódio 11.** Ela não lê a segunda linha. O público também não vê.

### EP09_C03

**Ação:** Djanira parada na calçada, olhando o prédio enorme. Câmera baixa — o prédio gigante, ela minúscula.

### EP09_C04

**Ação:** Djanira sentada na frente de um atendente.

### EP09_C05

**Ação:** Djanira sai de casa com uma sacola, de costas. Câmera parada. Ela some do quadro sozinha.

### EP09_C06

**Ação:** Djanira esperando na beira da estrada com a sacola. Expressão `chateado`.

- Estimativa 12000ms menor que blocos declarados 19000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

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
