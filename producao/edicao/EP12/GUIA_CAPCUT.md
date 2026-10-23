# EP12 — "A CLIENTE VIP"

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
| EP12_C01 | 00:00:00,000 | 8s | `EP12/srt_base/EP12_C01.base.srt` |
| EP12_C02 | 00:00:08,000 | 6s | `EP12/srt_base/EP12_C02.base.srt` |
| EP12_C03 | 00:00:14,000 | 41s | `EP12/srt_base/EP12_C03.base.srt` |
| EP12_C04 | 00:00:55,000 | 14s | `EP12/srt_base/EP12_C04.base.srt` |
| EP12_C05 | 00:01:09,000 | 7s | `EP12/srt_base/EP12_C05.base.srt` |
| VINHETA_01 | 00:01:16,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP11_C03 (a transformação) · EP11_C05 (a segunda linha)

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP12_C01

**Ação:** Um carro caro para na frente da boate. Djanira desce. O segurança abre a porta antes dela chegar perto.

> 🔴 **Use exatamente o mesmo enquadramento da EP06_C02.** Mesma posição de câmera, mesma distância, mesmo horário. Na edição eu coloco as duas lado a lado. **Este corte é o episódio inteiro.**

> 🔴 **Use exatamente o mesmo enquadramento da EP06_C02.** Mesma posição de câmera, mesma distância, mesmo horário. Na edição eu coloco as duas lado a lado. **Este corte é o episódio inteiro.**

### EP12_C02

**Ação:** Djanira sentada sozinha na área VIP — na mesma mesa onde o Chefe sentava no Episódio 6.

### EP12_C03

**Ação:** Dinjer chega para servir, com uma bandeja. Conversa de cliente e garçonete.

- Estimativa 36000ms menor que blocos declarados 41000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

> 💡 A Dinjer está com o mesmo uniforme de sempre. A Djanira mudou tudo menos o rosto. **A Dinjer olha e não reconhece.**

### EP12_C04

**Ação:** Djanira deixa um envelope grosso na mesa e vai embora sem olhar para trás. Dinjer abre, conta, e começa a chorar.

### EP12_C05

**Ação:** Close no envelope. Dentro, junto com o dinheiro, um papel escrito à mão. O texto você monta no CapCut.

**TEXTO NA TELA:**
> "Duas semanas. Juro."

- Estimativa 5000ms menor que blocos declarados 7000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

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
