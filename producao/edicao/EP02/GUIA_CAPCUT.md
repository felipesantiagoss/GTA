# EP02 — "O DINHEIRO SUMIU"

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
| EP02_C01 | 00:00:00,000 | 8s | `EP02/srt_base/EP02_C01.base.srt` |
| EP02_C02 | 00:00:08,000 | 9s | `EP02/srt_base/EP02_C02.base.srt` |
| EP02_C03 | 00:00:17,000 | 8s | `EP02/srt_base/EP02_C03.base.srt` |
| EP02_C04 | 00:00:25,000 | 18s | `EP02/srt_base/EP02_C04.base.srt` |
| EP02_C05 | 00:00:43,000 | 12s | `EP02/srt_base/EP02_C05.base.srt` |
| EP02_C06 | 00:00:55,000 | 16s | `EP02/srt_base/EP02_C06.base.srt` |
| EP02_C07 | 00:01:11,000 | 13s | `EP02/srt_base/EP02_C07.base.srt` |
| VINHETA_01 | 00:01:24,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE** *(reaproveite, não grave)*: EP01_C01 (a queda) · EP01_C08 (o pedido)

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP02_C01

**Ação:** Dinjer bebendo, dançando, tirando foto. Expressão `convencido`.

⚠️ Se o interior não abrir, use `BOATE_FORA` com ela entrando pela porta, à noite, com neon.

### EP02_C02

**Ação:** Dinjer sentada numa mesa reservada com quatro homens de terno. Um deles entrega um envelope para ela. Ela guarda rápido.

### EP02_C03

**Ação:** Djanira no escuro, mexendo no celular, esperando resposta. Expressão `estressado`.

### EP02_C04

**Ação:** A avó chama a Djanira para perto e avisa. Djanira ouve de pé.

### EP02_C05

**Ação:** Djanira com o celular na mão. Rosto fecha. Expressão muda para `com raiva`.

### EP02_C06

**Ação:** A avó mostra o celular dela para a Djanira. As duas olham a tela.

### EP02_C07

**Ação:** Djanira parada olhando o horizonte. Câmera baixa, olhando para cima — ela parece maior.

- Estimativa 6000ms menor que blocos declarados 13000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

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
