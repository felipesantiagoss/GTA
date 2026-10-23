# GTA_Diretor — continuidade e conclusão de EP02 e EP03

**Entrega atual — 22/10/2026, 0.9.203:** briga `EP01_C07` refeita conforme o pedido de todos brigando ao mesmo tempo, substituindo a defesa sequencial das versões anteriores. Novo extra `EP01_C07_BRIGA_SIMULTANEA_EXTRA.mp4`, com 34 s de conteúdo (4 s de entrada, 18 s de falas originais, 10 s de briga e 2 s de rescaldo), e trecho separado `EP01_C07_BRIGA_SIMULTANEA_10S_EXTRA.mp4`, com os 10 s da briga. Ambos em 1920×1080 a 30 fps, silenciosos e sem textos na imagem, em `videos/EP01_SEM_LEGENDAS/` e no perfil `Serie/A_CALOTEIRA_1_TEMP/EP01/EP01_SEM_LEGENDAS/`. SHA256 do extra completo `472CFF2FA95616FAC122230E00D261BB5653EB00B0BB5390AD5A6807F9474C2C`; do trecho de 10 s `5221EBD172C3E81A8B1AF2DE63504BE0E87F3B7E104F5AC28E57FDC536A1E25A`. Dinjer/COLEGA_1 e COLEGA_2/COLEGA_3 atacam e reagem em duas duplas simultâneas entre 22 e 32 s, sem tarefas de espera ou combate de IA. Captura real OBS Studio 32.2.2/WGC/QuickSync, sessão `3f0363ba372040739cb6465192697dbb`, bruto `EP01_C07_v7.mp4`; o log mede 10,002 s de briga, seis ataques e cinco reações por ator, cinco ciclos por dupla e intervalo máximo de 66 ms entre avanços de fase por ator, com movimento real de mãos/cabeça registrado. Vinte amostras da briga, a cada 0,5 s, foram revistas pelo operador para conferir os quatro em movimento simultâneo. Falas, figurinos, roteiro e episódios preservados; os extras de 38 s e 32 s, rejeitados pelo usuário por pausas e espera dos colegas, continuam preservados, assim como C07_LIMPO e a versão ANTERIOR. [Publicação do script](backups/instalacao/20261022T234422600457Z-98997e9a/manifesto.json); [prova da entrega e preservação](backups/briga-simultanea-20261022/entrega-verificada.json). Avaliação artística do usuário pendente. Nenhuma ação do usuário necessária para obter os arquivos. Registros anteriores são históricos.

## ✅ Conclusão em 09/10/2026 — escopo EP02/EP03 encerrado

- **EP02:** final preservado sem alteração (`videos/EP02_SEM_LEGENDAS/EP02_MONTAGEM_LIMPA.mp4`, SHA `F51ADD7A…AF66`).
- **EP03:** `EP03_MONTAGEM_LIMPA.mp4`, 89,533 s, 1920×1080, silencioso, sem textos; SHA `D26C5B0B9ACBED1CCDB411F0A38E19856A3FCB2B03C3F9CE17BFF85C9C0B89DF`, idêntico em `videos/EP03_SEM_LEGENDAS/` e no perfil `Serie/A_CALOTEIRA_1_TEMP/EP03/`. Ordem C01 13,5 + C02 6 + C03 8 + C04 9 + C05 12 + C06 11 + C07 30 s. Prova: `backups/finalizar-ep03/20261009T124500727320Z-final/resultado.json`.
- **EP03_C07:** `EP03_C07_LIMPO.mp4` (30,033 s) = A9_v5 @5767 ms + A10 (`EP03_C07_v3.mp4`) @6667 + B11 (`EP03_C07.mp4`) @8617; sem regravação. Limitação: na A9 a mão fica encoberta na batida (sem aprovação estética). Prova: `backups/finalizar-ep03/20261009T122926189136Z-c07/resultado.json`.
- **EP03_C05:** gravada em 09/10 (`EP03_C05_v2.mp4`, sessão `97c9450e12dc4205bb4009bfb2c8deca`, offset 6700 ms; contagem visível até ~6,5 s). Adaptações registradas: sem abertura de porta (porta do trailer falhou antes), Dinjer descalça com sapatos na mão corre ~3,7 m para a direita até parar (destino original bloqueado por obstáculo; tolerância de chegada 0,40 m), câmera LONGE com `inverterEixo=true` (o lado padrão ficava dentro do trailer). Plano aberto noturno, atrizes pequenas. `EP03_C05.mp4` (v1, câmera dentro do trailer) não é usado. Backups da fonte/índice/seleção antes das mudanças: `backups/finalizar-ep03/c05-antes-20261009T123112Z/`.
- Ferramenta: `ferramentas/finalizar_ep03.py` (`.venv_edicao`). Melhorias ficam para extras pedidos pelo usuário.

O restante deste documento é o histórico de 08/10/2026.

Conferência de disco em **08/10/2026**. Horários técnicos abaixo são UTC; em São Paulo, subtrair três horas. Este documento registra o trabalho realizado e o ponto de parada. **Nesta etapa foi feita somente documentação, sem novas cenas, gravações, Reload ou edição de vídeos.**

## 1. Pedido atual e regra para a próxima execução

O usuário restringiu o trabalho a **terminar somente os episódios 2 e 3** e determinou:

> Se já foi gravado algo, não grava novamente. Foca em apenas completar todos os vídeos; as melhorias eu vou falando depois e peço um vídeo extra.

Portanto, reutilizar as gravações existentes. Não reiniciar a temporada, não regravar EP01 ou EP02, não abrir outra rodada de melhorias de câmera, mãos, objetos ou atuação. Uma limitação visual deve ser registrada honestamente e ficar para eventual extra solicitado depois. Corrigir apenas o necessário para conseguir terminar e exportar os dois episódios.

**EP02 já está completo. EP03 precisa de C05 e da pós-produção das cenas existentes, principalmente C07.** O pedido deste turno foi criar este MD; continuar a produção somente quando solicitado.

## 2. O que foi feito até aqui

O projeto implementa um diretor de cenas real para **GTA V Legacy, modo história**: leitura e validação de cenas JSON, filas, ambiente, elenco, figurinos persistidos, boca e gestos durante diálogos, ações físicas, objetos, portas, veículos, câmeras e organização de capturas da Game Bar.

Foram registrados **13 personagens/visuais**, com continuidade de rosto, roupas, acessórios e semente `20601003`. Djanira pobre e rica compartilham identidade; a rica usa a opção 1 aprovada. Dinjer mantém seu figurino registrado. Não regenerar esses arquivos.

As tomadas reais foram gravadas e organizadas por episódio, preservando versões. A edição usa recortes com PTS e snapshots de fonte, montagem por WinRT, revisão de quadros e cópia para workspace e perfil com hashes. O padrão de entrega é **1920×1080, horizontal 16:9, áudio silencioso, sem legendas queimadas, cartelas, vinheta ou créditos**. SRTs técnicos e versões antigas permanecem preservados.

O histórico registra **nove episódios integrais entregues: EP01, EP02, EP04, EP05, EP06, EP07, EP08, EP13 e EP14**. A briga nativa solicitada em EP01 já foi substituída somente naquele trecho; os dois extras de EP01 também foram entregues. EP07, EP08 e EP13 foram fechados aproveitando fontes e subtomadas reais. Há componentes adicionais de outros episódios, inclusive EP10 e EP15.

Os episódios EP09, EP10, EP11, EP12 e EP15 ainda têm pendências históricas, mas **estão fora do trabalho atual de EP02/EP03**. Não afirmar que a temporada inteira já foi concluída nem retomar essas pendências por causa das instruções antigas. O histórico detalhado continua em [RESUMO_CONTINUIDADE_CODEX.md](RESUMO_CONTINUIDADE_CODEX.md) e nos backups referenciados nele.

## 3. EP02 — entregue; não precisa gravar ou montar novamente

| Informação | Estado conferido |
|---|---|
| Final no workspace | `C:/GTA_Diretor/videos/EP02_SEM_LEGENDAS/EP02_MONTAGEM_LIMPA.mp4` |
| Final no perfil | `C:/Users/felipe.santiago/GTA_Diretor/Serie/A_CALOTEIRA_1_TEMP/EP02/EP02_SEM_LEGENDAS/EP02_MONTAGEM_LIMPA.mp4` |
| Duração e imagem | **88,033 s; 1920×1080; AVC/H264** |
| Tamanho de cada cópia | **90.813.245 bytes** |
| SHA256 das duas cópias, recalculado nesta conferência | `F51ADD7AFF164831C6F908E5EFD46AA851F8B87BA747F74E52001266E5CFAF66` |
| Última modificação UTC | `2026-10-06T00:03:21.8720602Z` |
| Pendência de produção | **Nenhuma encontrada** |

A subpasta adicional `EP02/EP02_SEM_LEGENDAS` no perfil é real. Não procurar apenas na raiz de EP02 e concluir que o final desapareceu.

As sete cenas estão incluídas, em ordem: C01 12 s, C02 9 s, C03 8 s, C04 18 s, C05 12 s, C06 16 s e C07 13 s; conteúdo total de 88 s. Os sete clipes limpos também existem em cada destino. A conferência atual recalculou os hashes dos **finais**, não de todos os clipes, e leu os átomos MP4 sem decodificar novamente o vídeo. O áudio silencioso e a revisão visual anterior constam da prova de publicação; não foi feita uma nova revisão artística neste turno.

Evidências existentes:

- [Publicação verificada de EP02](backups/montagem-video-episodios/20261006T000257220790Z-clips-limpos-exportar-c221118f/publicacao-verificada.json).
- [Seleção final das sete cenas](backups/montagem-video-episodios/20261006T000228900450Z-EP02-final-sete-61a57a9a/EP02.selecao-final.sete.json).
- [Manifesto integral](backups/montagem-video-episodios/20261006T000228900450Z-EP02-final-sete-61a57a9a/EP02.clips-limpos.final-sete.manifesto.json): `pendentes=[]`, `parcial=false`.
- [Validação final](backups/montagem-video-episodios/20261006T000228900450Z-EP02-final-sete-61a57a9a/validacao-final-sete.json).

**Para terminar EP02 no padrão limpo pedido, basta aproveitar esse final.** Vozes e edição autoral no CapCut são uma etapa externa, se o usuário quiser realizá-la depois. Flags antigas `gravado=false` em documentos de preparação não invalidam o MP4 entregue.

## 4. EP03 — inventário que deve ser reaproveitado

Não foi encontrado `EP03_MONTAGEM_LIMPA.mp4` final. O episódio tem sete cenas; o conteúdo atualmente previsto soma aproximadamente **89,5 s**, com pequena quantização de frames no render.

| Cena | Conteúdo | Material disponível | Próxima ação |
|---|---:|---|---|
| EP03_C01 | 13,5 s | `videos/EP03_SEM_LEGENDAS/EP03_C01_LIMPO.mp4`; carona com dois carros já gravada | Reutilizar |
| EP03_C02 | 6 s | Perfil `EP03/EP03_C02.mp4`; metadata fechada | Recortar/exportar a tomada existente |
| EP03_C03 | 8 s | `videos/EP03_SEM_LEGENDAS/EP03_C03_LIMPO.mp4`; degrau já gravado | Reutilizar |
| EP03_C04 | 9 s | Perfil `EP03/EP03_C04.mp4`; metadata fechada | Recortar/exportar a tomada existente |
| EP03_C05 | 12 s | Fonte preparada; **nenhum MP4 real localizado** | Preparar e gravar somente esta cena |
| EP03_C06 | 11 s | Perfil `EP03/EP03_C06_v2.mp4`; metadata fechada | Recortar/exportar a tomada existente |
| EP03_C07 | 30 s | Batida A9 em bruto; conversa A10 e reação B11 limpas | Fechar recortes e montar **9 + 10 + 11 s**, sem regravar |

Prefixo dos brutos: `C:/Users/felipe.santiago/GTA_Diretor/Serie/A_CALOTEIRA_1_TEMP/`. Os quatro clipes limpos já publicados em EP03 são C01, C03, C07_A10 e C07_B11; eles também existem no perfil EP03.

| Clipe limpo | Duração do arquivo | SHA256 |
|---|---:|---|
| C01 | 13,516233 s | `EC6C9B6D47CA130DF8B90513E82876FD1E1F1DCF601B41E729C27B2302BBC1C4` |
| C03 | 8,032970 s | `F091910C4201E2586A4611D25864C294B85E45567EDE54009A1BEA8D8A29F0C4` |
| C07_A10 | 10,033 s | `E6B9D0BDE84886FBA39BF593F36B8C4EBCA0E6AF4BE4CFBDF19D3272C123A04B` |
| C07_B11 | 11,033 s | `B145D636974DCA1CF33FD51F8DDE86EF063CF452D75453671428370E5F563D71` |

Metadata já fechada para os três brutos restantes, sob `backups/montagem-video-episodios/`:

- C02: `20261006T102926747189Z-calibracao-cpu-fechar-297a1af8/resultado.json`, sessão `d04c769c4f6a4247a7b37e987161ba61`, offset **6400 ms**, conteúdo **6000 ms**.
- C04: `20261006T102928485934Z-calibracao-cpu-fechar-a79c847c/resultado.json`, sessão `a956e2fbdd214d22b20d14b87f058e59`, offset **6584 ms**, conteúdo **9000 ms**.
- C06: `20261006T095315484017Z-calibracao-cpu-fechar-129e9bb5/resultado.json`, sessão `0573b976f5f244cd97846cdf60c9a62f`, offset **6400 ms**, conteúdo **11000 ms**.

Não repetir capturas, calibrações aprovadas ou auditorias completas desses materiais.

## 5. EP03_C07 — já foi gravada em partes; falta a montagem

O roteiro original pede um homem batendo à porta, a avó atendendo e Djanira ouvindo de dentro. **Não exige mostrar abertura e travessia da porta**; esses requisitos foram acrescentados por adaptações anteriores e não devem criar novas regravações.

1. **A9 — 9 s:** batida na porta, aproveitando `EP03_C07_v5.mp4` já existente.
2. **A10 — 10 s:** avó atendendo no alpendre e fala integral do capanga; já entregue como `EP03_C07_A10_LIMPO.mp4`.
3. **B11 — 11 s:** Djanira ouvindo de dentro, com narração de 5 s e Narrador de 6 s; já entregue como `EP03_C07_B11_LIMPO.mp4`.

### Última gravação real: A9, versão v5

| Campo | Registro |
|---|---|
| Sessão | `7057e76a334f42959b9ea0d65b5056f6` |
| SAVE UTC | **2026-10-08T03:22:00.7941943Z** |
| Bruto | `C:/Users/felipe.santiago/GTA_Diretor/Serie/A_CALOTEIRA_1_TEMP/EP03/EP03_C07_v5.mp4` |
| Tamanho | **22.931.076 bytes** |
| SHA256 atual do bruto | `39D686C0B1CC5FEBABB7C4DA2B1301F5AF4E7D5CCAC90A2E8B51D20095660A81` |
| Ação no log | **9025 ms**, janela declarada de **9000 ms**, seguida de SAVE |
| Snapshot antes de armar | `backups/operacao-sequencial/20261008T032135028753Z-589267ee/01-EP03_C07/EP03_C07.snapshot-prearm.json` |
| SHA256 da fonte filmada | `B92CE14A81D032845CEF1557DA7C9271BC8A6DE0718A06BA788B9AD5237B291E` |
| Primeiro quadro sem contagem | **PTS 57666666**, offset candidato **5767 ms** |
| Último quadro da contagem | **PTS 57166666** |

Calibração: `backups/montagem-video-episodios/20261008T032245704216Z-detectar-contagem-cpu-9c58e306/resultado.json`. Quadros adicionais: `20261008T032303828765Z-calibracao-cpu-quadros-c5bacabd/resultado.json` e `contato-pts.bmp`.

A imagem foi vista anteriormente, mas a mão ficou encoberta no momento da batida; isso motivou rejeição sob a exigência anterior de melhorar o contato. **A ordem mais recente é aproveitar o gravado e adiar melhorias: não gravar v6 para corrigir esse aspecto.** Registrar essa limitação na seleção da versão existente, sem afirmar contato perfeito, três impactos comprovados ou aprovação estética do usuário. A metadata da v5 ainda não foi fechada; seus campos de aprovação continuam falsos. A presença do snapshot anterior não prova hash durante a filmagem: `hashDuranteCapturaRegistrado` não deve ser inventado.

A10 boa usa sessão `b2f53b1fd1be4e20a82e264777eedc73`, fonte `5F12741CC9AEB9B12F24D79AD7A1351364A693078F5A135691A4107A8C5B0C0F`, offset **6667 ms** e metadata `20261008T024453691243Z-calibracao-cpu-fechar-7f072785/resultado.json`.

B11 boa usa sessão `c0b54681360345bc916d61fcef06618d`, fonte `028030AC5E2DF1358B275525402CC8CB717F2A4EE1B74006D6FA2092F7B771E7`, offset **8617 ms** e metadata `20261008T014056999896Z-calibracao-cpu-fechar-cac30473/resultado.json`.

**Não selecionar A10_v2**: o capanga ficou sobre o ar-condicionado. A10 boa é a versão já entregue acima. A9_v4 também permanece preservada, mas v5 é a referência mais recente para aproveitar, sem regravação.

Os assemblers privados já preparados estão em:

- `backups/retomada-componente-ep03-c07-a9/20261008T0322Z-braco-folha-futuro9/`.
- `backups/retomada-componente-ep03-c07-integral/20261008T0323Z-braco-folha-futuro30/`.

**Atenção:** esses helpers conservam exigências anteriores como `--contato-porta-root`, `--tres-batidas-root` e `batidasContatoPortaConferidas`. Não marcar provas inexistentes como verdadeiras para desbloquear o render. Adaptar a seleção/validação de forma explícita à ordem atual de usar a versão existente, mantendo sessão, fonte, duração, limites de corte e a limitação visual registrada. O arquivo `ROTA_ROOT.txt` nesses pacotes é histórico nesse ponto; não seguir sua instrução de gravar outra A9.

Montar os conteúdos exatos dos brutos em **9 + 10 + 11 = 30 s**, conservando a fala de 10 s uma vez e as duas narrações de B11 uma vez. Isso evita acumular os frames adicionais dos clipes limpos já exportados.

O rascunho existente de C07 está em `backups/retomada-componente-ep03-c07-integral/20261008T0323Z-braco-folha-futuro30/20261008T032226382380Z-rascunho-4fc1b5ba/EP03_C07.componente.manifesto.json`; conserva A10/B11 e ainda declara `exportavel=false`. **Não foi localizada uma seleção integral de EP03 pronta para exportar.** O exportador genérico `ferramentas/montar_video_episodio_limpo.py` aceita EP03, mas precisa da seleção completa. Preparar essa seleção e adaptar a composição dos componentes já entregues; isso é trabalho de montagem, não motivo para novas capturas.

## 6. EP03_C05 — única cena ainda sem MP4

Fonte preparada: [EP03_C05.json](producao/cenas_serie/EP03/EP03_C05.json). Documento do roteiro: [EP03_C05.execucao.json](producao/cenas_serie/EP03/EP03_C05.execucao.json). Na conferência atual, a cópia `runtime/cenas/EP03_C05.json` está ausente; preparar e validar antes de promovê-la.

**Cena original, 12 s, às 21:30:** porta dos fundos da casa de Dinjer abre; Dinjer sai correndo, descalça, com os sapatos na mão; Djanira permanece na rua vendo-a sumir. A narração completa de 12 s deve ser preservada, sem boca e sem texto queimado.

O candidato atual reserva abertura 0–2 s, travessia 2–5 s e corrida 5–12 s. Não foi comprovada uma captura completa desse comportamento. Os pontos concretos a resolver são:

- Usar a **porta dos fundos real**, com acesso e caminho até a rua. A marca atual `CASA_DINJER` é histórica de degraus; não presumir que identifica essa porta. A folha frontal `v_ilev_trevtraildr` teve falha de física em ensaio anterior; o portão `prop_cs4_10_tr_gd01` não é porta traseira.
- Aplicar o estado descalço somente nesta tomada, em memória; conservar o XML e o uniforme de Dinjer.
- Usar o recurso já registrado `sapato_mao` → `prop_cs_amanda_shoe`, sem regenerar figurino. Os offsets de mão do candidato ainda não provam encaixe visual; buscar uma versão funcional suficiente para a entrega, sem refinamento repetitivo.
- Conferir passagem e duração para evitar erro que cancele a gravação. Se necessário, organizar partes reais do mesmo ID, conservando os 12 s e a narração uma vez, sem apresentar teleporte como travessia.

Preparar o mínimo necessário para **uma captura completa**, salvar, organizar e seguir diretamente para a montagem de EP03. Não usar esta pendência para retestar os demais recursos da temporada.

## 7. Ponto exato de parada e estado atual

**Última entrega de arquivo limpo:** C07_A10, 10,033 s, copiada e conferida nos dois destinos. **Última captura:** A9_v5, SAVE 03:22:00 UTC, ainda sem recorte limpo entregue ou montagem C07. **Último episódio integral entregue:** EP08. EP03 continua sem montagem integral.

Após as capturas, o código **0.9.198** foi compilado, publicado e carregado. A entrada de autor de Modern1 foi corrigida; o ensaio confirmou piso em interior `227329`, mas a sala era mobiliada, sem nova captura de EP11. O pacote técnico de observação da carta99 foi preparado; o binding198 foi gerado em pasta privada, **sem aplicar INI99 ou executar o novo ensaio**. A telemetria199 da mão na porta ficou somente em candidato privado; **não foi integrada, publicada ou carregada**. Essas pesquisas não são necessárias para concluir EP02/EP03 sob a ordem atual de não refilmar.

| Camada | Estado real em disco/log |
|---|---|
| Fontes, dist e instalação | **0.9.198** |
| SHA256 da fonte combinada, dist = instalada | `6CFCFC11654A62BE2611005C5737E78B4DA198D1A31E8DEDA45E4491004E831A` |
| SHA256 do Main | `8A6EEAECD518D95304F560EB5190A6460BF3E30164ACDEE5DFFD31ABEDEEB69A` |
| Último LOAD/Tick registrado | **03:25:57.2936520 / 03:25:57.4288412 UTC** |
| INI em disco | Produção: `Inicial=teste_json.json`, `PilotoAoIniciar=false`, fila vazia, `Gravacao/Habilitada=true` |
| SHA256 do INI | `5D4650AA310828F50A5DCA2F83634B94229B1C2231EF781E277EEC33295B0523` |
| Runtime C07 | Original de 30 s restaurado, SHA `F1B06DD53251D080CB0D058E955A51C1C00239E764271F82C4D81C45ECE19C53` |
| Catálogo de marcas | `05255F4FC92706918C67A8A8837FD3D08AE2E01EEF3D00011DFF4FFD0805981A` |
| Última limpeza registrada | **03:31:15.0784628 UTC**, `Concluída=True`, entidades restantes 0, IPL/player restaurados |
| Processos na conferência deste documento | **GTA5, GameBar, Python e FFmpeg não encontrados**; nenhuma janela de GTA disponível ao operador de leitura |

O GTA que existia antes, PID `19184`, **não está aberto na conferência atual**. Não tratar o último LOAD histórico como uma instância ainda em memória. Não foi iniciado nenhum aplicativo durante a documentação.

`CASA_VO` foi restaurada em **03:25:18 UTC**, alterando somente seu nó. A marca exterior temporária da batida, H1,85065, está preservada em `backups/retomada-farm197/CASA_VO-restaurar-no-root/20261008T032518222223Z/antes.xml`; não está ativa no catálogo atual. `CASA_VO_INTERIOR` e as demais marcas foram preservadas. Não restaurar um XML inteiro antigo.

Integração198: `backups/retomada-conjunta198/20261008T032445472640Z-09d34b79/resultado.json`. Publicação: `backups/instalacao/20261008T032521839379Z-6d12635a/`. O log principal é `C:/Users/felipe.santiago/GTA_Diretor/logs/DiretorGTA.log`; sessões estão em `logs/capturas/GUID/`.

## 8. Sequência curta para terminar somente os dois episódios

1. **EP02:** localizar o final indicado e preservá-lo. Nenhuma gravação, nova montagem ou revisão geral necessária.
2. **EP03/C07:** aproveitar A9_v5 e as boas A10/B11. Fechar o recorte de 9 s com o offset medido, documentando a limitação visual e a ordem de adiar melhorias; adaptar os bloqueios antigos do assembler sem falsificar provas. Exportar C07 de 30 s.
3. **EP03/C02, C04 e C06:** utilizar as metadata fechadas indicadas. Reaproveitar C01/C03 limpas. Não refilmar.
4. **EP03/C05:** reabrir o GTA em modo história quando a produção for retomada, conferir o ambiente e preparar somente a cena de 12 s ainda ausente. A primeira captura da nova instância precisa do writer real da Game Bar antes da contagem; depois da organização confirmada, a automação pode ser usada normalmente.
5. Com C05 salva, montar EP03 em ordem C01→C07, velocidade 1, tempos e narrações preservados. Renderizar em pasta nova, conferir apenas limites essenciais e último quadro, e copiar o final para `videos/EP03_SEM_LEGENDAS/EP03_MONTAGEM_LIMPA.mp4` e perfil `Serie/A_CALOTEIRA_1_TEMP/EP03/EP03_MONTAGEM_LIMPA.mp4`, mantendo versões anteriores e comparando hashes.
6. Atualizar o checkpoint com EP02 preservado, EP03 entregue e caminhos dos dois finais. Encerrar esse escopo; melhorias posteriores ficam para extras pedidos pelo usuário.

Não publicar o candidato199, executar ensaios87/99, voltar à sala de EP11 ou retomar EP09/10/12/15 como parte desse fluxo. Consultar os comandos existentes apenas quando necessários; não repetir todas as verificações históricas.

Backup dos documentos anteriores: `backups/documentacao-ep02-ep03/20261008T173225796668Z/`. Este MD e o novo cabeçalho do resumo são as alterações de documentação desta etapa.
