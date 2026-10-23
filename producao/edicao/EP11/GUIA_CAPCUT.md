# EP11 — "A HERDEIRA"

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
| EP11_C01 | 00:00:00,000 | 8s | `EP11/srt_base/EP11_C01.base.srt` |
| EP11_C02 | 00:00:08,000 | 12s | `EP11/srt_base/EP11_C02.base.srt` |
| EP11_C03 | 00:00:20,000 | 14s | `EP11/srt_base/EP11_C03.base.srt` |
| EP11_C04 | 00:00:34,000 | 8s | `EP11/srt_base/EP11_C04.base.srt` |
| EP11_C05 | 00:00:42,000 | 27s | `EP11/srt_base/EP11_C05.base.srt` |
| VINHETA_01 | 00:01:09,000 | 20s | `compartilhados/VINHETA_01.base.srt` |

## Reaproveitamento, cartelas e edição

**ANTERIORMENTE**: EP10_C01 (dormindo na rua) · EP10_C06 (o calendário) · EP10_C07 ("faltou um")

Reutilizar tomadas existentes; a duração desta montagem não foi inventada no SRT de continuidade.

### EP11_C01

**Ação:** Djanira entrando no banco com a roupa velha e o chinelo. O segurança a segue com o olhar.

- Elenco/ação do roteiro usa Djanira pobre no episódio11; prompt determina rica desde o início. DJANIRA_RICA; elenco e texto originais continuam arquivados

> ⚠️ Ainda é a **DJANIRA_POBRE**. A troca acontece só na próxima cena.

### EP11_C02

**Ação:** O atendente olha a tela e muda de expressão.

- Elenco/ação do roteiro usa Djanira pobre no episódio11; prompt determina rica desde o início. DJANIRA_RICA; elenco e texto originais continuam arquivados

### EP11_C03

**Ação:** Montagem de 5 ou 6 tomadas curtas: saindo do carro, na frente da mansão, no portão, andando de salto.

- O prompt determina DJANIRA_RICA desde o início do episódio11, embora este bloco diga que a troca só acontece aqui. preservar origem e implementar regra do prompt explicitamente

> 🔴 **Aqui troca o figurino.** A partir desta cena é sempre `DJANIRA_RICA`, andar `na passarela`, expressão `convencido`.

### EP11_C04

**Ação:** Djanira sentada sozinha, no chão, numa sala enorme e vazia.

### EP11_C05

**Ação:** Djanira recebe um envelope pardo. Lê. Pega o papel da avó e lê a segunda linha, que ela nunca tinha lido. Para de ler no meio. Começa a rir — um riso errado.

- Estimativa 20000ms menor que blocos declarados 27000ms. Preservar todos os tempos; reserva de gravação aumentada, sem comprimir vozes.

> 🔴 **O gancho de toda a segunda metade da temporada.**

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
