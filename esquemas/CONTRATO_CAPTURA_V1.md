# Contrato de captura e organização v1

A camada de organização não inicia nem para gravação. A Game Bar é acionada manualmente com Win+Alt+R; Home arma a associação antes de começar a captura. F7 escreve a legenda e inicia a contagem de três segundos. Ao terminar a cena, o Diretor chama o organizador em processo oculto e acompanha o resultado. A tomada usa um GUID novo a cada armação. Delete, F10, recarga e mudança de cena cancelam uma ação incompleta sem apagar o vídeo parcial. Uma sessão cujo último quadro já foi registrado permanece recuperável, mesmo apósF10/Reload; não marcar cancelamento de associação concluída ainda não organizada.

Antes do primeiro teste, desmarcar **“Desabilitar tecla Windows” no Rockstar Games Launcher**. O [suporte da Microsoft](https://support.microsoft.com/pt-br/accessibility/windows/use-a-screen-reader-to-record-your-screen-with-xbox-game-bar) documenta Win+Alt+R, MP4 e nomes que identificam aplicativo/jogo e data. A presença da Game Bar não prova que a gravação funciona.

## Entrada XML

Arquivo UTF-8 em `Dados/logs/capturas/<guidN>/sessao.xml`, publicado por arquivo temporário e renomeação atômica. `Dados` deve ser exatamente `%USERPROFILE%/GTA_Diretor`. GUID: 32 caracteres hexadecimais minúsculos, sem hífens. Campos desconhecidos, DTD, entidades e XML acima de 1 MiB são rejeitados.

```xml
<?xml version="1.0" encoding="utf-8"?>
<captura versao="1" id="0123456789abcdef0123456789abcdef" estado="concluida"
 cenaId="teste_fila_01" episodio="0" numero="0"
 pastaDados="C:\Users\usuario\GTA_Diretor"
 pastaCapturas="C:\Users\usuario\Videos\Captures"
 armadaUtc="2026-10-03T23:00:00.0000000Z"
 contagemUtc="2026-10-03T23:00:05.0000000Z"
 fimUtc="2026-10-03T23:00:20.0000000Z"
 prazoUtc="2026-10-03T23:02:20.0000000Z" duracaoMs="15000">
 <srt caminho="C:\Users\usuario\GTA_Diretor\cenas\teste_fila_01.srt"
      sha256="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" />
 <antes>
  <arquivo nome="GTA5 2026-10-02 20-00-00.mp4" tamanho="12000"
           criadoUtc="2026-10-02T23:00:00Z" alteradoUtc="2026-10-02T23:00:20Z" />
 </antes>
</captura>
```

`antes` é o snapshot de todos os MP4 diretamente na pasta real quando Home arma a tomada, antes de Win+Alt+R. Não tirar esse snapshot só em F7: o gravador já pode ter criado o arquivo. Nomes antigos permanecem excluídos mesmo se seu tamanho ou horário mudar depois. Não associar “o vídeo mais recente”.

`contagemUtc` corresponde a F7 e ao início da legenda; `fimUtc` corresponde ao último quadro da cena. Datas UTC aceitam de zero a sete casas decimais. Exigir `armadaUtc ≤ contagemUtc ≤ fimUtc ≤ prazoUtc`; prazo até dez minutos após o fim, janela inteira até uma hora. `duracaoMs` inclui a contagem: 3001–183000 ms. O organizador espera no máximo 120 s por execução; End pode repetir a mesma sessão após resultado `aguardando` ou `erro`, conservando baseline e legenda. Atualizar o prazo, se necessário, com backup do XML anterior; nunca reciclar o GUID em outra tomada.

End após recarga procura somente uma sessão concluída elegível dos últimos dez minutos, sem marcador de cancelamento e sem resultado salvo. Duas candidatas causam recusa, sem escolher a mais recente. `--repetir` primeiro valida todos os dados da sessão e, sob lock exclusivo, amplia o prazo até o limite com backup doXML; conservaGUID, baseline, legenda e tempos da tomada. Não recupera uma cena que foi interrompida antes do último quadro nem amplia a busca para vídeos sem associação.

Episódio de produção: `EP01_C01`–`EP15_C99`, episódio e número iguais ao código, destino `Dados/Serie/A_CALOTEIRA_1_TEMP/EPnn`. Testes `teste_json`, `teste_fila_01`–`teste_fila_99` e XML legados `teste_dupla`, `teste_falas`, `teste_camera`, `teste_cortes`, `teste_ambiente`: episódio/número zero, pasta `TESTES`. `VINHETA_01`: episódio/número zero, pasta `COMPARTILHADOS`. A legenda de origem é o caminho exato retornado por WriteSrt, diretamente em `Dados/cenas`, com nome `<id>.srt` ou `<id>_vN.srt`, N≥2, e SHA256 conferido. A legenda de origem continua preservada.

## Seleção, finalização e preservação

Também são IDs técnicos explícitos de episódio/número zero `teste_elenco_pobre`, `teste_elenco_rica` e `teste_coreografia`; seus MP4/SRT usam a pasta `TESTES`. Outros nomes técnicos são rejeitados até extensão declarada do contrato.

Aceitar exatamente um MP4 novo, nome começando por `GTA5` ou `Grand Theft Auto V`, ausente do snapshot, criado entre armação e prazo e com última escrita entre o fim da cena e o prazo. A tolerância na borda inicial da última escrita é 100 ms para a resolução do horário. Dois candidatos causam erro de ambiguidade e não são movidos. Outro aplicativo, vídeo antigo, captura parada antes da cena ou tomada cancelada não são associados. O padrão de nome precisa ser confirmado na primeira captura real; nome diferente exige ajuste explícito, sem ampliar a busca silenciosamente.

Antes da cópia, aguardar três observações iguais de tamanho/última escrita, abrir o arquivo exclusivamente e conferir as caixas MP4 `ftyp`, `moov`, `mdat` e seus limites. Ler `mvhd` v0/v1 e exigir duração de mídia pelo menos igual a `duracaoMs`, com tolerância máxima de 100 ms para arredondamento e últimos quadros. A disposição desses campos foi conferida no [leitor oficial do FFmpeg](https://github.com/FFmpeg/FFmpeg/blob/master/libavformat/mov.c); nenhuma dependência FFmpeg é instalada. O [contrato de CreateFile](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew) permite abertura sem compartilhamento, recusando arquivo ainda ocupado pelo gravador. A conferência lê metadados; não decodifica mídia nem comprova a imagem, o áudio ou seu alinhamento ao F7.

Pasta de origem precisa coincidir com a registrada no Windows. Consultar `User Shell Folders` e o registro local `FolderDescriptions/Captures`; não presumir a pasta do workspace nem um caminho do OneDrive. A identificação local da pasta Captures utiliza o GUID registrado `{EDC0FE71-98D8-4F4A-B920-C8DC133CB165}`; ele não é apresentado como constante pública do SDK. Aceitar somente discos locais fixos, sem UNC, ADS, links, junctions ou outros reparse points em qualquer ancestral, incluindo as raízes. Arquivos com múltiplos hard links são recusados. Conferir o caminho final do handle aberto.

Um lock exclusivo de sessão e o handle exclusivo do MP4 impedem dois organizadores de consumir a mesma origem. Reservar conjuntamente o nome de vídeo e legenda, sem sobrescrever: `<id>.mp4/.srt`, depois `_v2`, `_v3` etc. Se qualquer integrante do par já existir, escolher uma nova versão para ambos. Reservas de processos interrompidos também não são reutilizadas.

Antes de copiar ao destino, guardar MP4 e SRT em `Dados/backups/capturas/<guid>-<sufixo>/`, conferir SHA256 e escrever `manifesto.json`. Criar os destinos de forma exclusiva; conferir hashes de ambos. Registrar o par em `conclusao.json` com estado `par_conferido` e em `compromisso.json`, conservando identidade do arquivo, destinos e hashes, antes de remover o MP4 de origem pelo mesmo handle exclusivo. O SRT de origem nunca é removido. Se falhar a remoção, o original permanece; se falhar a escrita de `resultado.xml` depois da remoção, o retry recupera o par pelo registro sem criar nova versão. Cancelamento anterior à remoção conserva o original e o backup; arquivos de destino parciais ficam registrados para revisão, sem apagar tomadas antigas.

## Cancelamento e resultado

Para cancelar, criar atomicamente `cancelada.xml` na pasta da sessão. Sua presença basta; não substitui nem remove o XML de entrada. O organizador consulta o marcador antes de backup, cópia final e remoção da origem. Um cancelamento posterior à conclusão não desfaz um vídeo já salvo. Enquanto uma associação estiver pendente, o Diretor bloqueia nova tomada/navegação ou cancela explicitamente o contexto antes de outra operação.

Resultado UTF-8 em `resultado.xml`, escrito atomicamente. Substituição em retry primeiro conserva o resultado anterior com nome exclusivo na própria pasta. O processo termina com código 0=`salvo`, 1=`erro`, 2=`aguardando`, 3=`cancelado`. Processo concorrente não substitui o resultado do organizador ativo. XML de entrada inválido pode impedir a escrita de resultado seguro; nesse caso o consumidor usa o código de saída e registra erro.

```xml
<resultado versao="1" id="0123456789abcdef0123456789abcdef" status="salvo"
 mensagem="Tomada salva: teste_fila_01.mp4"
 video="C:\Users\usuario\GTA_Diretor\Serie\A_CALOTEIRA_1_TEMP\TESTES\teste_fila_01.mp4"
 legenda="C:\Users\usuario\GTA_Diretor\Serie\A_CALOTEIRA_1_TEMP\TESTES\teste_fila_01.srt"
 manifesto="C:\Users\usuario\GTA_Diretor\backups\capturas\guid-sufixo\manifesto.json"
 sha256Video="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
 sha256Legenda="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
 versaoTomada="1" />
```

`status`: `salvo`, `erro`, `aguardando`, `cancelado`. Somente `salvo` traz caminhos, hashes e versão. Para `aguardando`, parar manualmente com Win+Alt+R e usar End para repetir. Um resultado `salvo` com hashes ainda iguais torna o retry idempotente. `compromisso.json` permite recuperar a conclusão quando o processo terminou depois de conferir o par e antes de escrever o resultado; se a origem ainda existir, sua identidade e hash precisam coincidir antes da remoção.

## Linha de tempo e próxima camada

O SRT começa no F7, com deslocamento de três segundos antes da cena. Na gravação manual, pode haver preparação antes do F7. **Não deduzir esse intervalo da data de criação do MP4**: ela não identifica o primeiro quadro capturado. No CapCut, cortar o trecho antes do F7 ou alinhar manualmente vídeo e SRT. Conservar o arquivo original da legenda e registrar `InicioVideoRelativoF7=nao_inferido` no manifesto.

A camada de automação usa somente [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput), após o primeiro MP4/SRT real salvo e conferido nesta sessão e com o processo GTA em primeiro plano. `HasVerifiedSave` só é ativado depois de verificar hashes do par real. O INI mantém `Modo=manual`; `AutomatizarAposPrimeira=true` permite a camada posterior à primeira captura manual. Reload reinicia essa confirmação; Home armado escolhe uma tomada manual mesmo depois da habilitação.

F7 automático arma baseline/sessão e vincula a legenda antes de enviar uma vez Win+Alt+R; o fim da ação envia uma vez o atalho de parada. Cancelamento/erro/recarga tentam parar a captura iniciada por esta camada. Win+Alt+R é alternância: número de eventos enviados não comprova gravação, e o código não repete o toggle após falha. Se o envio ou a organização da tomada automática falhar, a automação fica suspensa até a próxima sessão; o modo manual permanece. Restrições UIPI, políticas ou launcher não são contornadas. Nenhuma captura automática foi confirmada apenas por esses testes de arquivos/compilação.

CLI independente, sem instalar dependências:

```text
python -X utf8 ferramentas/organizar_capturas.py --diagnosticar
python -X utf8 ferramentas/organizar_capturas.py --sessao "<Dados>/logs/capturas/<guid>/sessao.xml"
python -X utf8 ferramentas/organizar_capturas.py --sessao "<Dados>/logs/capturas/<guid>/sessao.xml" --repetir
```

`--diagnosticar` só lê registro/pacote Game Bar e retorna JSON. Consulta sem resultado no sandbox é `não verificado`, não prova desinstalação. O diagnóstico e os testes estruturais não comprovam a primeira captura real no GTA.
