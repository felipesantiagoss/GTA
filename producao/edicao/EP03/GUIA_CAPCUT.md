# EP03 — "A COBRANÇA"

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
| EP03_C01 | 00:00:00,000 | 7s | `EP03/srt_base/EP03_C01.base.srt` |
| EP03_C02 | 00:00:07,000 | 6s | `EP03/srt_base/EP03_C02.base.srt` |
| EP03_C03 | 00:00:13,000 | 8s | `EP03/srt_base/EP03_C03.base.srt` |
| EP03_C04 | 00:00:21,000 | 9s | `EP03/srt_base/EP03_C04.base.srt` |
| EP03_C05 | 00:00:30,000 | 12s | `EP03/srt_base/EP03_C05.base.srt` |
| EP03_C06 | 00:00:42,000 | 11s | `EP03/srt_base/EP03_C06.base.srt` |
| EP03_C07 | 00:00:53,000 | 21s | `EP03/srt_base/EP03_C07.base.srt` |
| VINHETA_01 | 00:01:14,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP02_C04 (o aviso) · EP02_C05 (o bloqueio) · EP02_C06 (o story)

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP03_C01

**Ação:** Djanira andando na beira da estrada, acenando para carros que passam.

### EP03_C02

**Ação:** Djanira bate na porta três vezes. Espera. Ninguém atende.

### EP03_C03

**Ação:** Djanira sentada no meio-fio em frente à casa, esperando. Sol forte.

### EP03_C04

**Ação:** Djanira bate na porta de novo. Luz acesa dentro. Expressão `estressado`.

### EP03_C05

**Ação:** A porta dos fundos abre. Dinjer sai correndo, descalça, com os sapatos na mão. Djanira parada no meio da rua, vendo ela sumir.

- Dinjer descalça com sapatos na mão conflita com uniforme fixo; nenhuma troca é aprovada implicitamente. preservar origem e implementar regra do prompt explicitamente

### EP03_C06

**Ação:** Close na Djanira, parada, sem reação.

### EP03_C07

**Ação:** Um homem bate na porta. A avó atende. Djanira ouve de dentro.

- CAPANGA_1 mantém o terno fixo do prompt; preservar a indicação original 'sem terno' e sua fala, sem criar variante silenciosa. preservar origem e implementar regra do prompt explicitamente

- Estimativa 15000ms menor que blocos declarados 21000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

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
