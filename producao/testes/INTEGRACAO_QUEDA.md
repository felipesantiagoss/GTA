# Integração da queda — preparação técnica

`src/CenaQuedaDiretor.cs` é um helper novo incluído no agregador pelo responsável pela integração, ainda sem cena ativa conectada. Compilação C#5 com `/warnaserror+` passou; nenhuma DLL/harness foi executada. Não houve teste no GTA desta capacidade. A produção continua pendente de integração, marcas reais e confirmação visual.

Foram lidos os95 documentos integrais da série, incluindo85 pendentes na leitura inicial, os46 clipes e10 objetos pesquisados. A queda é compartilhada por `EP01_C01` e `EP07_C05`. Poses `free_forward` e `swimclimbup_40` não criam voo, física ou rampa; este helper usa tarefas/física do GTA.

A abertura conserva14s de narração. A reprise estima10s, mas seus blocos somam6+7=13s. A queda de4s é uma meta técnica, sem bloco explícito desse tempo no roteiro. `ObservedFallMs` mede saída efetiva do assento até `IsInWater`; `ObservedDurationMs` mede a sequência inteira. Filmar por3s de contagem mais `max(vozesOriginais, ObservedDurationMs)`, registrar tempo extra e manter o SRT/texto original. Não comprimir falas nem forçar impacto pelo relógio. Se forem necessários cortes no CapCut, registrar os takes físicos e a diferença de tempo; nenhum vídeo está pronto.

`template_queda_continua.json` é um descritor de integração fora de `cenas`, sem XYZ. Não é cena JSON executável. As falas/tempos/raw originais foram copiados por leitura UTF8 e conferidos por igualdade com os documentos de execução. Os XMLs reais atuais têm somente `TESTE_AMBIENTE`; falta `CANAL_LS`. A água e a direção precisam ser capturadas no jogo. Para completar a abertura, também são obrigatórias a entrada/topo da rampa e o fim da corrida. A interface de captura desses subpontos precisa ser conectada no main. Nunca substituir a marca por coordenadas presumidas ou pedir XYZ digitado.

O avião começa70m antes e30m acima da água capturada: deslocamentos relativos de ensaio, sem simular uma marca de `AEROPORTO_AR` nem decolagem. Embarque/decolagem de `EP07_C03` continuam fora deste helper.

```csharp
var queda = new CenaQuedaDiretor(WriteLog);
var spec = new CenaQuedaDiretor.QuedaSpec();
spec.Cena = "EP01_C01";
spec.Agua = marcaAguaReal;
spec.Direcao = direcaoDaMarcaReal;
spec.CompletarFuga = true;
spec.EntradaRampa = entradaReal;
spec.TopoRampa = topoReal;
spec.Fuga = fugaReal;
queda.Prepare(djaniraCriadaPeloDiretor, spec);
// Tick: PollLoading(); proteger OwnedEntities da limpeza ambiental.
// Depois do assentamento: Reset(); se false, tentar no próximo Tick.
// Ceder câmera/tarefas primárias, contar3s, Start(), Update(elapsedMs).
// Encerrar só depois de IsSuccess e do fim das vozes originais.
// Cancelar captura antes de Reset/Cleanup; limpeza pendente exige outro frame.
```

Para `EP07_C05`, usar `DuracaoSolicitadaMs=13000` e, se desejar outro ângulo, `OlhosDesdeInicio=true`. A mesma filmagem pode ser reaproveitada na edição; não declarar reaproveitamento concluído sem MP4 real.

`Reset()` usa Warp apenas fora da filmagem e retorna `false` enquanto a saída dos assentos estiver pendente. Guarda a posição assentada na primeira restauração. `Start()` dá velocidade física inicial ao avião e chama `StartPlaneMission(...GoTo...)` no piloto próprio. A saída usa `BailOut | DontWaitForVehicleToStop | DontDefaultWarpIfDoorBlocked`. `Update` não atribui posições nem usa Warp. Ragdoll produz a queda; água e emersão são observadas. Nado, rampa e corrida usam `FollowNavMeshTo` com timeout de Warp=-1. Rota bloqueada ou prazo excedido retorna erro, sem fabricar chegada. `CompletarFuga=false` é ensaio parcial: não torna a abertura completa.

O plano largo acompanha o avião, o corte da queda usa a cabeça/direção da Djanira, depois a câmera acompanha da margem. Os planos laterais mantêm o lado escolhido do corredor. A visão durante ragdoll, enquadramento, clipping e obstáculos da câmera precisam de teste. Ao fim físico, fixar o último quadro e manter as vozes restantes. Main conserva HUD, horário/clima, polícia, ocultação/restauração do jogador, legendas/SRT e captura. Suspender `CameraDiretor.Update` e outros controladores de tarefas primárias desse ator enquanto o helper possuir a câmera/ação.

O helper cria somente avião/piloto próprios e recebe Djanira própria persistente. Recusa o jogador e valida handle, modelo e endereço de memória. `ObjetosDiretor` valida a identidade do avião. O piloto `s_m_m_pilot_01` está no `PedList.xml` instalado, linha949; `velum` está no enum da DLL191 e seu tipo/disponibilidade é conferido no runtime. A estética continua pendente. São228 consultas de água entre frames num corredor de300m com12m de largura; trecho estreito, curto ou com ponte pode ser recusado. Isso não autoriza inventar outra locação.

`Cleanup` restaura a câmera anterior válida quando ainda é a câmera própria que está renderizando; não apaga câmeras alheias. Restaura flags e marca da Djanira, sem ressuscitar para esconder falha. Não apaga avião ocupado por entidade alheia; preserva referências quando saída/exclusão precisa ser repetida. Não muda globais de paraquedas, relógio, clima ou população. Voo, nado e rampa não foram confirmados visualmente.

Fonte SHA256: `05CB34A6F4A450B25B5DBA56F2781F146A33233F6CDB23D22757B84453A078D1`.

Biblioteca de verificação final: `backups/compilacao/helpers-transporte-final-20261004T0211543049192Z/Transporte.Verificacao.dll`, exit0, sem execução. A última revisão recusa recriar piloto quando uma identidade registrada mudou. Metadados/XML da API191 e compilação confirmaram assinaturas. O XML cita `DefaultNavmeshFinalHeading`, ausente da API pública desta DLL; foi usado o default do próprio método. O enum de navegação público usa `Default`, não `None`. Backups das revisões estão em `backups/edicoes/queda-*`; a revisão de encoding também preservou os arquivos anteriores em `veiculos-docs-api-*`.
