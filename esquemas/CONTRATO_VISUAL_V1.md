# Visuais v1 — Menyoo e aplicação no Diretor

O pedido mais recente autoriza o Diretor a escolher os visuais e registrá-los para reutilizar na segunda temporada. Importar arquivos Menyoo continua sendo uma opção; não é uma exigência de seleção ou aprovação manual. Nenhum XML ausente é fabricado pela ferramenta de importação. A geração automática usa o jogo e será registrada separadamente; desenvolvimento/compilação não comprovam o resultado visual.

## Leitura da aparência

`src/VisualDiretor.cs` é um helper C#5 sem `using` e sem classe `Script`.

- `VisualDiretor.Load(string path, string expectedModel)` devolve `VisualDiretor.Visual`, sem executar natives. Campos públicos readonly: `Model`, `Sha256`, `FaceSha256`, `ModelHash` (uint) e `IsFreemode`.
- `Apply(GTA.Ped ped, Visual visual)` verifica modelo e todas as variações disponíveis antes de alterar a aparência. NPC nativo termina imediatamente. Freemode inicia o DNA e registra uma aplicação pendente.
- `IsReady(GTA.Ped ped)` deve ser chamada entre frames. Espera `HAS_PED_HEAD_BLEND_FINISHED` e conclui traços, cores, overlays, roupas e acessórios uma vez. Timeout de10s; não bloqueia o Tick. `true` indica conclusão das chamadas e conferências disponíveis, não aprovação visual.
- `Forget(GTA.Ped ped)` remove a aplicação pendente; a limpeza deve chamá-la antes de `Delete` para evitar reuso de handle.

Arquivo XML original Menyoo `OutfitPedData`, máximo1MiB/100000 caracteres e32 níveis, sem namespace/DTD/entidade externa. Respeitar sua codificação declarada (incluindo ISO-8859-1). `Type=1`, `ModelHash` original hexadecimal ou decimal deve corresponder ao modelo conhecido, por Jenkins calculado do identificador. Root/PedProperties não permitem campos duplicados. Campos de aparência possuem estrutura estrita e obrigatória; outros metadados Menyoo são ignorados e nunca executados.

`PedProperties/PedComps` exige `_0` até `_11`; `PedProps` exige `_0` até `_9`. Cada valor é `drawable,textura`, índices brutos originais. Componentes:0–10000; acessórios drawable/textura:-1–10000, ausência somente `-1,-1` ou `-1,0`. Acessório presente exige textura não negativa. As faixas de arquivo são limites técnicos; a existência real é conferida no modelo antes da aplicação. Componentes sem variações aceitam apenas o par neutro0,0.

Freemode exige `HeadFeatures WasInArray="true"` e conteúdo completo:

- `ShapeAndSkinTone`: seis campos `ShapeFatherId/ShapeMotherId/ShapeOverrideId/ToneFatherId/ToneMotherId/ToneOverrideId`, inteiros0–45 para cabeças nativas sem addon; `ShapeVal/ToneVal/OverrideVal` finitos0–1; `IsP`0/1 ou false/true.
- `HairColour/HairColourStreaks`: inteiros0–255, conferidos pela quantidade real de cores do jogo; `EyeColour`:0–32, faixa usada pelo autor, com conferência do getter após aplicação.
- `FacialFeatures`: `_0` até `_19`, números finitos-1–1.
- `Overlays`: `_0` até `_12`, somente atributos `index`0–255 (`255` significa ausente), `colour/colourSecondary`-1–255 e `opacity` finita0–1. Sem conteúdo/subelementos; conferir quantidade real de cada overlay no jogo. O tipo da paleta segue o importador Menyoo:1 nos índices1/2/4/10;2 em5/8;0 nos demais.

Em NPC nativo, um `HeadFeatures` genérico de exportação legada é ignorado: o rosto pertence ao próprio modelo. Isso não enfraquece a exigência de rosto completo para freemode. Posição, direção, arma, tarefas, anexos, scripts, estados de missão e outros atributos do XML nunca são aplicados. O Diretor continua responsável pelas entidades e pelo roteiro.

Importante: `PedComponent.SetVariation` usa índice bruto; `PedProp.SetVariation` usa bruto+1 e0 remove o acessório. O XML preserva o índice bruto. Usar `Style[PedComponentType]` e `Style[PedPropAnchorPoint]`, não o indexador obsoleto `PedPropType`.

## Identidade e integridade

`Sha256` é o SHA256 uppercase dos bytes originais, sem reformatar o XML. `FaceSha256` é o SHA256 UTF-8 da representação canônica terminada em newline:

1. `VisualDiretor.Face.v1`;
2. identificador do modelo;
3. par do componente0;
4. seis IDs do DNA separados por vírgula;
5. três misturas em IEEE754 float32 little endian, hexadecimal uppercase, separadas por vírgula;
6. IsP normalizado0/1;
7. vinte traços com a mesma representação float32;
8. cor dos olhos.

Valores de zero são normalizados para +0. NPC nativo usa somente as primeiras três linhas. A representação evita diferenças de cultura, precisão textual ou notação exponencial entre C# e Python. Djanira pobre/rica devem compartilhar esse fingerprint. Conforme o pedido mais recente, apenas roupa, acessórios, penteado e cor de cabelo podem mudar; modelo, DNA, traços, componente facial, máscara, olhos e os13 overlays completos continuam iguais. `VisualDiretor.SameFaceDetails` e `mesma_identidade_djanira` também comparam máscara e overlays, fora do fingerprint v1. As comparações verificam os dados salvos, não a imagem renderizada.

## Ferramenta Python e arquivos

`ferramentas/importar_visuais.py` usa somente a biblioteca padrão. `ler_visual(caminho, expected_model=...)` devolve dict com `modelo/modelHash/sha256/faceSHA256/freemode/componentes/acessorios/rosto`; erros são `ErroVisual`, derivada de `ValueError`.

Comandos:

```text
python -X utf8 ferramentas/importar_visuais.py --status
python -X utf8 ferramentas/importar_visuais.py --importar
```

Aceita `--origem`, `--destino` e `--ped-list`. Origem padrão: `menyooStuff/Outfit` do GTA instalado. Importação mantém destino padrão `C:\GTA_Diretor\catalogos\visuais` e escrita confinada ao workspace. Status padrão lê `%USERPROFILE%\GTA_Diretor\catalogos\visuais` quando o perfil existe, com fallback ao workspace se USERPROFILE não estiver disponível. Status também pode ler `--destino` fora do workspace, sem escrever; recusa `..`, links, junctions e reparse points, aplica os mesmos limites de XML/DTD/hashes e confere `visualSHA256`/modelo contra `catalogos/personagens.json` quando declarados. XML registrado no destino tem precedência sobre original Menyoo. A importação não publica no runtime nem altera `personagens.json`.

Reconhece os13 códigos reais: DJANIRA_POBRE/RICA, DINJER, DONA_IRACI, O_CHEFE, CAPANGA_1–4, COLEGA_1–3 e ATENDENTE. Arquivos recebem exatamente `CODIGO.xml`. Os três primeiros exigem `mp_f_freemode_01`; para os demais, o hash deve resolver unicamente a um modelo humano registrado no `PedList.xml` local. Isso confere o identificador e não prova idade, roupa ou adequação artística.

Status apenas lê e informa arquivos ausentes/inválidos/importados ou já registrados no destino; não cria XML nem marca aprovação. Quando não existe original na pasta Menyoo, também examina o XML registrado no catálogo (incluindo geração real pelo jogo). Importar valida todos os arquivos presentes antes de qualquer escrita e compara Djanira contra a outra variante presente na origem ou já importada. Divergência rejeita tudo. Uma variante isolada pode ser guardada com comparação conjunta pendente. Conservar outros visuais já importados. `visualArquivo` no manifesto é somente `CODIGO.xml`, relativo à pasta `catalogos/visuais`.

Copiar bytes autênticos, conferir SHA256 de origem/destino, fazer backup único timestamp+GUID antes de substituir arquivo existente (incluindo manifesto), verificar o backup e usar temporário GUID exclusivo+substituição atômica por arquivo. Somente temporário próprio pode ser removido. Uma falha de I/O não promete transação atômica do lote inteiro: saídas já concluídas têm hashes e backups conferidos; repetir é permitido. `importacao_visuais.json` registra códigos/modelos/nomes/hash/fingerprint/origem/UTC e `aprovado=false`, sem tornar personagens prontos ou alegar teste visual. Ativação no catálogo/runtime pertence ao fluxo do Diretor.

## Evidências e estado inicial

Menyoo instalado: `PedList.xml:9` confirma `mp_f_freemode_01`; `Outfit/GhostRider.xml` registra um exemplo completo masculino com12 componentes,10 acessórios,20 traços e13 overlays. Não é visual da série e não foi aplicado pelo teste. Nenhum dos13 XML da série existia no levantamento inicial. `TESTE_AMBIENTE` é a única marca real inicial; não inventar ROCA_GRAPESEED ou outros locais.

Fonte do autor para salvar/importar aparência: [Menyoo FileManagement.cpp](https://github.com/itsjustcurtis/MenyooSP/blob/master/Solution/source/Submenus/Spooner/FileManagement.cpp). A cabeça completa só é serializada quando está no registro interno (`WasInArray=true`). O suporte de cabeça se restringe aos dois freemode; paletas e limites vêm de [PedComponentChanger.cpp](https://github.com/itsjustcurtis/MenyooSP/blob/master/Solution/source/Submenus/PedComponentChanger.cpp). Conversão de acessórios: [SHVDN PedProp.cs](https://github.com/scripthookvdotnet/scripthookvdotnet/blob/main/source/scripting_v3/GTA/Entities/Peds/PedProp.cs), também confirmada por leitura do IL da DLL191 local. Natives atuais foram conferidas nos metadados dessa DLL, sem invocação.

As natives faciais usadas são `SET_PED_HEAD_BLEND_DATA`, `HAS_PED_HEAD_BLEND_FINISHED`, `SET_PED_MICRO_MORPH`, `SET_PED_HAIR_TINT`, `SET_HEAD_BLEND_EYE_COLOR`, `SET_PED_HEAD_OVERLAY` e `SET_PED_HEAD_OVERLAY_TINT`. Compilação isolada e testes Python são evidências técnicas; nenhuma DLL/harness/native foi executada fora do jogo.

## Geração automática real

`ElencoAutomaticoDiretor.Generate(Ped,codigo,seed)` usa somente o ped real criado no jogo. `IsReady(Ped)` conclui entre frames; `Save(path,Ped,codigo,seed)` grava XML novo exclusivamente e devolve `VisualDiretor.Visual`; `Forget(Ped)` cancela o estado antes da remoção. `SetDjaniraBase(Visual)` reutiliza uma identidade anterior, copiando DNA,20 traços, olhos, face, máscara e todos13 overlays, sem referências mutáveis compartilhadas.

Roupas e acessórios vêm de contagens/texturas do modelo e `IsVariationValid`; cada escolha inspeciona no máximo64 drawables por slot e usa a variação atual válida como fallback. Face/máscara/bolsa/colete são preservados; capacetes e óculos não são sorteados. Cores de cabelo vêm do RGB real da paleta. A seleção de cabeças usa os ranges femininos base/DLC retornados por [GetPedHeadBlendFirstIndex](https://github.com/citizenfx/natives/blob/master/PED/GetPedHeadBlendFirstIndex.md) e [GetPedHeadBlendNumHeads](https://github.com/citizenfx/natives/blob/master/PED/GetPedHeadBlendNumHeads.md), restritos à faixa nativa0–45 suportada pelo contrato. Olhos7 (azul) e4 (castanho) têm origem na documentação primária [SetPedEyeColor](https://github.com/citizenfx/natives/blob/master/PED/SetPedEyeColor.md); o getter confere aceitação pelo jogo.

O sorteio usa PRNG xorshift32/seed+versão+personagem, sem `GetHashCode` dependente de processo. Djanira usa uma única chave de rosto e duas de figurino. XML registra seed, versão, UTC, estado pendente, origem e fingerprint. Os12 componentes e10 acessórios são lidos de volta do ped antes de salvar. DNA e os20 traços são os parâmetros exatos aplicados; não se inventa um getter de traços inexistente. Temporário GUID é validado pelo mesmo leitor e movido sem substituir arquivos anteriores. O manager persiste semente e manifesto para reutilização na segunda temporada.

Variações existentes não demonstram short/chinelo/afro/uniforme, tom de pele específico ou ausência de sobreposição de peças. O Wardrobe do autor expõe Type/Texture, sem nomes de roupa suficientes para provar esses detalhes automaticamente. A geração registra essa limitação e mantém `gerado no jogo; teste visual pendente`; não promete aprovação estética.
