# Poses iniciais da série

Esta extensão prepara atores sentados/deitados **antes da contagem e da gravação**, sobre um objeto próprio que existe e foi assentado no chão real. Não executa sentar/levantar/deitar durante a tomada e não substitui uma transição física por teleporte. Cenas que exigem transição, abraço, entrega, janela/porta, chão sem móvel ou outro efeito continuam com essas dependências; a pose inicial sozinha não as completa.

Proposta JSON: `poses`, opcional, lista0–5 somente em `encenacao=serie_v1`. Cada objeto requer `personagem`(código do elenco), `objeto`(ID de prop declarado), `recurso`(código real de corpos) e `tipo`(`sentada|deitada`). Opcionais: `superficie:{direita,frente}`(XY local ao modelo) e `rotacao` em±180graus relativa ao móvel. Sem `superficie`, usar centroXY dos bounds realmente medidos como ponto **candidato**; não usar maxZ do bbox como assento. Z vem exclusivamente do raycast que atingiu o próprio móvel, nunca do JSON. Campos desconhecidos, booleano como número, infinito, IDs repetidos/ausentes e tipo/recurso divergente rejeitam leitura.

Um ator tem no máximo uma pose inicial. O objeto precisa ser do tipo `apoio`, visível, assentado, sem attachment ou eventos que o movam/ocultem; somente mostrar é aceito porque o apoio já nasce visível. Ações legadas e direção corporal normal continuam proibidas nesse ator; boca e gestos upper-body/secondary de fala continuam permitidos. A exceção `direcao.tipo=clipe,sobrePoseSentada=true` exige `corpoInteiro=false,loop=false,manterPoseFinal=false`, sem `grupoPareado`, sobre `sentada_cadeira_livre_f` ou `sentado_cadeira_m`. Somente `mp_common/givetake1_a`, `mp_common/givetake1_b` e `mp_common_miss/put_away_coke` são candidatos aceitos. Não inferir do sufixo os papéis de entregar/receber ou afirmar adequação visual do gesto de guardar antes do teste. Nenhuma fila/cena atual muda seu comportamento por omitir `poses`.

Whitelist canônica conferida na PedAnimList.txt instalada:

| Recurso | Tipo | Dicionário | Clipe |
|---|---|---|---|
| sentada_cadeira_f | sentada | amb@prop_human_seat_chair@female@arms_folded@base | base |
| sentada_cadeira_livre_f | sentada | amb@prop_human_seat_chair@female@proper@base | base |
| sentado_cadeira_m | sentada | amb@prop_human_seat_chair@male@generic@base | base |
| sentada_meio_fio_f | sentada | amb@world_human_seat_steps@female@hands_by_sides@base | base |
| deitada_cama_f | deitada | mp_bedmid | f_sleep_l_loop_bighouse |
| sono_f | deitada | mp_sleep | sleep_loopfemale |

Modelo/variante são conferidos por `IS_PED_MALE`. Posição de meio-fio em cadeira não comprova adequação visual; animação de sono não comprova doença. O piso sem móvel e `sentar_chao_candidato` ficam fora desta primeira API, pois o nome do clipe não prova postura de chão. Não inventar pares ou publicar recurso “testado”.

## Superfície real

`ObjetosSerieDiretor.RequestSurface(propId,localX,localY)->int probeId` inicia raio vertical assíncrono nos limites reais do modelo. `PollLoading()` resolve as medições por Tick; `TryGetMeasuredSurface(probeId,out point,out normal)->bool` retornafalse enquanto pendente. O raio precisa atingir **o handle do próprio prop**, com normalZ≥0,98 e dentro dos boundsZ reais. Falha/timeout2s aborta. Medição só ocorre antes de `Update`, não durante a filmagem. `Reset` invalida probes anteriores; pedir novamente após restaurar os objetos. Máximo16probes por preparo. O pontoXY candidato pode exigir ajuste após teste; um raio sobre encosto/poste não é prova de assento útil.

## API C#

`PosesSerieDiretor(log)`; `PoseSpec` contém `Ator`, `Objeto`, `Recurso`, `Tipo`, `Dicionario`, `Clipe`, `OrigemArquivo`, `OrigemSha256`, `OrigemLinha`, `SuperficieRelativa`(Vector3 XY eZ0), `UsarCentroMedido`(true por padrão), `RotacaoRelativa`(float). Ator/objeto são normalizados para maiúsculos. Dicionário/clipe/arquivo/linha/hash vêm exclusivamente de `corpos_serie.json`, não da cena.

`GetCatalogDictionary`, `GetCatalogClip`, `GetCatalogType` e `VerifySourceEvidence(IEnumerable<PoseSpec>)` são funções estáticas puras de dados/arquivos, sem natives. Validam whitelist, tipos, IDs e par/linha/SHA da lista real. O parser usa essas funções antes de limpar a cena atual.

`Prepare(dictOwnedActors,listPoseSpec,ownProps)` só é chamado após `ownProps.IsReady`. Confere/copia specs e captura posição/heading/congelamento originais dos atores uma vez; pede dicionários e superfícies. `PollLoading()` verifica `GET_ANIM_DURATION` positivo, inicia loop primário fora da captura, aguarda o blend e alinha o pivô do corpo ao contato real em no máximo4correções. Correção total fica até3m da marca original. Sem correção baseada em altura fictícia.

Sentada: contato do pivô pelvis com superfície medida, cabeça acima da pelvis, joelhos realmente flexionados e pés sem atravessar o chão. Deitada: referência inferior de pelvis/cabeça/pés no plano medido, eixo corporal horizontal e cabeça/pés dentro dos limites reais da cama. São verificações de **ossos/geometria**, não prova de contato da malha/roupa; roupa, quadril, colchão e posição fina ainda precisam de confirmação visual. Falha geométrica recusa preparo, não troca o clipe por ator em pé.

`IsReady` só após esses checks. `Update(elapsedMs)` verifica identidade, suporte visível/congelado, animação primária e geometria real dos ossos, sem reposicionar/limpar ator e sem tocar facial/gesto secundário. Se a pose se perder, abortar e repetir; não corrigir no filme. `Reset` e `ReleaseActorsForReset()` restauram o snapshot fora da captura. A sequência é interromper captura/ação → liberar poses → restaurar atores/objetos → `ownProps.Reset()` → `poses.Reset()` → aguardar `PollLoading/IsReady` → contagem → ação. Não aplicar `ClearAllImmediately`/`StandStill` no ator depois da pose pronta; fala usa apenas upper-body/secondary.

`AcoesSerieDiretor.BindPoses(poses)` associa o helper após `Prepare` das poses. Antes e durante cada gesto sentado, `RequireSeatedOverlay` exige a mesma identidade, primária, apoio e joelhos/ossos; início não chama ClearAll e fim para somente o clipe secundário. A aplicação usa UpperBodyOnly|Secondary. Fonte, carga e duração real continuam validadas; compatibilidade dos braços/roupa/envelope permanece visual pendente. Nenhum fallback troca a pose por ator em pé.

`Cleanup`/`Dispose` interrompem somente tarefas dos atores com identidade conferida, restauram snapshot quando vivos e liberam dicionários pedidos. Prop é limpo pelo seu próprio helper. Ator removido/dead é tratado sem declarar sucesso visual ou apagar entidade alheia. Sem `Script.Wait`, sem nova classe Script, sem mudança de DNA/figurino.

Estado: helper implementado e compilação C#5/avisos como erros concluída; nenhuma pose, apoio ou figurino novo confirmado no jogo. Cenas de transição continuam exigindo implementação específica. A fonte de API é a documentação XML local da nightly191 e seus natives enumerados, com compilação separada do teste no GTA.
