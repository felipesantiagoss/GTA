# EP05 — "O CONVITE"

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
| EP05_C01 | 00:00:00,000 | 12s | `EP05/srt_base/EP05_C01.base.srt` |
| EP05_C02 | 00:00:12,000 | 5s | `EP05/srt_base/EP05_C02.base.srt` |
| EP05_C03 | 00:00:17,000 | 34s | `EP05/srt_base/EP05_C03.base.srt` |
| EP05_C04 | 00:00:51,000 | 8s | `EP05/srt_base/EP05_C04.base.srt` |
| EP05_C05 | 00:00:59,000 | 31s | `EP05/srt_base/EP05_C05.base.srt` |
| VINHETA_01 | 00:01:30,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP04_C02 (a humilhação) · EP04_C05 (a avó doente) · EP04_C06 (a caixa vazia)

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP05_C01

**Ação:** Djanira capinando no sol forte, exausta. Expressão `chateado`.

### EP05_C02

**Ação:** O celular toca. Djanira olha a tela e atende.

### EP05_C03

**Ação:** As duas ao telefone, cada uma no seu lugar.

- Estimativa 30000ms menor que blocos declarados 34000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

> 💡 **Cena de telefone.** As duas estão em lugares diferentes. Grave cada uma separada, na sua locação, e alterne na edição. A Dinjer fica em `AEROPORTO_TERMINAL`.

### EP05_C04

**Ação:** Djanira parada olhando o guarda-roupa, quase vazio.

### EP05_C05

**Ação:** A avó chama a Djanira. Djanira ouve de pé, já decidida.

- Estimativa 18000ms menor que blocos declarados 31000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

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
