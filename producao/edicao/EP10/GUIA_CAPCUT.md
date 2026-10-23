# EP10 — "TRÊS ANOS"

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
| EP10_C01 | 00:00:00,000 | 10s | `EP10/srt_base/EP10_C01.base.srt` |
| EP10_C02 | 00:00:10,000 | 9s | `EP10/srt_base/EP10_C02.base.srt` |
| EP10_C03 | 00:00:19,000 | 8s | `EP10/srt_base/EP10_C03.base.srt` |
| EP10_C04 | 00:00:27,000 | 7s | `EP10/srt_base/EP10_C04.base.srt` |
| EP10_C05 | 00:00:34,000 | 9s | `EP10/srt_base/EP10_C05.base.srt` |
| EP10_C06 | 00:00:43,000 | 14s | `EP10/srt_base/EP10_C06.base.srt` |
| EP10_C07 | 00:00:57,000 | 10s | `EP10/srt_base/EP10_C07.base.srt` |
| VINHETA_01 | 00:01:07,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP09_C04 ("três anos") · EP09_C05 (a saída de casa)

> 💡 **O episódio mais fácil de gravar da temporada.** Um personagem só, zero diálogo, tudo narração. Oito tomadas curtas em lugares diferentes, montadas com música. **Varie muito a hora do dia** — é a mudança de luz que vende os três anos passando.

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP10_C01

**Ação:** Djanira dormindo num banco, abraçada na sacola.

### EP10_C02

**Ação:** Djanira num ponto de ônibus, esperando, de uniforme de faxina.

- Uniforme de faxina conflita com DJANIRA_POBRE fixo do prompt. preservar origem e implementar regra do prompt explicitamente

### EP10_C03

**Ação:** Djanira andando sozinha na rua vazia, ainda escuro.

### EP10_C04

**Ação:** Djanira andando embaixo do viaduto à noite.

### EP10_C05

**Ação:** Djanira andando rápido na calçada, no meio de outras pessoas, sem ninguém olhar para ela.

### EP10_C06

**Ação:** Close na Djanira riscando algo com a mão. O calendário em si você monta no CapCut.

### EP10_C07

**Ação:** Close no rosto dela. Um sorriso mínimo, o primeiro em três anos.

- Estimativa 4000ms menor que blocos declarados 10000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

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
