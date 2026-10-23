# EP07 — "O VOO"

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
| EP07_C01 | 00:00:00,000 | 9s | `EP07/srt_base/EP07_C01.base.srt` |
| EP07_C02 | 00:00:09,000 | 16s | `EP07/srt_base/EP07_C02.base.srt` |
| EP07_C03 | 00:00:25,000 | 10s | `EP07/srt_base/EP07_C03.base.srt` |
| EP07_C04 | 00:00:35,000 | 34s | `EP07/srt_base/EP07_C04.base.srt` |
| EP07_C05 | 00:01:09,000 | 13s | `EP07/srt_base/EP07_C05.base.srt` |
| VINHETA_01 | 00:01:22,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP06_C04 (o convite) · EP06_C06 (o gesto)

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP07_C01

**Ação:** Djanira chega no hangar arrumada com a roupa de igreja. Expressão `feliz`.

- Roupa de igreja conflita com DJANIRA_POBRE fixo; o prompt prevalece, ação/texto original permanecem. preservar origem e implementar regra do prompt explicitamente

### EP07_C02

**Ação:** Djanira escondida atrás de um contêiner, escutando. O Chefe conversa com dois homens perto de umas caixas. Filme de longe, pouca luz, nada claramente visível.

### EP07_C03

**Ação:** Os três embarcam. Djanira `feliz`, Dinjer `chateado` — primeira vez que ela não está convencida. O avião decola.

### EP07_C04

**Ação:** Dentro do avião. O Chefe servindo, simpático. Dinjer em pé no fundo, calada. No meio da conversa o Chefe para de sorrir.

> 💡 Na fala 6 a Dinjer **não olha para a Djanira**. Olha para o chão. É o detalhe que o público vai lembrar no Episódio 15.

### EP07_C05


> 💡 **Reaproveite a EP01_C01.** É a mesma queda, agora explicada. Se quiser regravar com outro ângulo, melhor ainda.

- Estimativa 10000ms menor que blocos declarados 13000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

> 💡 **Reaproveite a EP01_C01.** É a mesma queda, agora explicada. Se quiser regravar com outro ângulo, melhor ainda.

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
