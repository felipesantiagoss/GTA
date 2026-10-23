[CmdletBinding()]
param(
    [int]$Linhas = 80,
    [string]$PastaJogo = 'C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V'
)

$ErrorActionPreference = 'Stop'
if ($Linhas -lt 1) { throw 'A quantidade de linhas deve ser maior que zero.' }
$directorCli = Join-Path $PSScriptRoot 'diretor.py'
# Somente onde PowerShell tem execução normal autorizada; não alterar políticas.
& python '-X' 'utf8' $directorCli 'logs' '--pasta-jogo' $PastaJogo '--linhas' $Linhas
if ($LASTEXITCODE -ne 0) {
    throw 'A leitura dos logs foi interrompida pela CLI. Consulte o erro acima.'
}
