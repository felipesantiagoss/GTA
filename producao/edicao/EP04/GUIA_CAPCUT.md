# EP04 — "NO TRABALHO DELA"

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
| EP04_C01 | 00:00:00,000 | 12s | `EP04/srt_base/EP04_C01.base.srt` |
| EP04_C02 | 00:00:12,000 | 27s | `EP04/srt_base/EP04_C02.base.srt` |
| EP04_C03 | 00:00:39,000 | 9s | `EP04/srt_base/EP04_C03.base.srt` |
| EP04_C04 | 00:00:48,000 | 6s | `EP04/srt_base/EP04_C04.base.srt` |
| EP04_C05 | 00:00:54,000 | 14s | `EP04/srt_base/EP04_C05.base.srt` |
| EP04_C06 | 00:01:08,000 | 16s | `EP04/srt_base/EP04_C06.base.srt` |
| VINHETA_01 | 00:01:24,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP03_C05 (a fuga) · EP03_C07 (os trinta dias)

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP04_C01

**Ação:** Djanira desce de um ônibus e anda pelo terminal, deslocada no meio de gente bem vestida.

### EP04_C02

**Ação:** Dinjer aparece com colegas. Vê a Djanira e muda de expressão. Os colegas param para olhar. Dinjer percebe a plateia e vira o jogo.

- Estimativa 26000ms menor que blocos declarados 27000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

### EP04_C03

**Ação:** Dinjer vai embora andando na passarela com os colegas, rindo. Djanira fica parada no meio do saguão.

### EP04_C04

**Ação:** Djanira andando sozinha na estrada, câmera o mais longe possível.

### EP04_C05

**Ação:** A avó deitada, muito mal. Djanira sentada do lado.

### EP04_C06

**Ação:** Close na Djanira segurando uma caixa de remédio vazia. Expressão `machucado`.

- Estimativa 8000ms menor que blocos declarados 16000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

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
