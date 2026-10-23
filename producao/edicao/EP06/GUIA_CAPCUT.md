# EP06 — "A BOATE"

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
| EP06_C01 | 00:00:00,000 | 11s | `EP06/srt_base/EP06_C01.base.srt` |
| EP06_C02 | 00:00:11,000 | 7s | `EP06/srt_base/EP06_C02.base.srt` |
| EP06_C03 | 00:00:18,000 | 13s | `EP06/srt_base/EP06_C03.base.srt` |
| EP06_C04 | 00:00:31,000 | 46s | `EP06/srt_base/EP06_C04.base.srt` |
| EP06_C05 | 00:01:17,000 | 8s | `EP06/srt_base/EP06_C05.base.srt` |
| EP06_C06 | 00:01:25,000 | 7s | `EP06/srt_base/EP06_C06.base.srt` |
| VINHETA_01 | 00:01:32,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP05_C03 (a proposta) · EP05_C05 (o aviso da avó)

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP06_C01

**Ação:** Djanira desce do ônibus e olha os prédios. Câmera baixa, olhando para cima, os arranha-céus enormes em volta dela.

### EP06_C02

**Ação:** Djanira parada na fila, sendo olhada de cima a baixo pelo segurança. Expressão `chateado`.

> 🔴 **Guarde o enquadramento exato desta tomada.** No Episódio 12 você vai repetir a mesma câmera com a Djanira rica. O corte entre as duas é o momento mais forte da temporada.

### EP06_C03

**Ação:** Djanira vê, de longe, a Dinjer sentada com o Chefe e os capangas. Não é festa — é negócio.

⚠️ Se o interior não abrir, grave na fachada com as portas abertas ao fundo.

### EP06_C04

**Ação:** Dinjer vê a Djanira, levanta rápido e vai abraçar. Leva até a mesa do Chefe. Ele se levanta devagar.

- Estimativa 40000ms menor que blocos declarados 46000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

### EP06_C05

**Ação:** Djanira saindo da boate, sorrindo. Expressão `feliz` — a primeira vez na série.

### EP06_C06

**Ação:** Close no Chefe. Ele olha para a Dinjer e faz o gesto de **passar a mão no pescoço** — ordem de matar. Corta para a Dinjer: ela abaixa a cabeça e não diz nada. **Corte seco.**

- Estimativa 6000ms menor que blocos declarados 7000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

> 🔴 **A tomada mais importante da primeira metade da temporada. Sem fala nenhuma.**

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
