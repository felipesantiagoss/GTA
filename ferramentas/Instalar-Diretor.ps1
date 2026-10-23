[CmdletBinding()]
param(
    [string]$PastaJogo = 'C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V'
)

$ErrorActionPreference = 'Stop'
# Usar somente onde a execução normal de scripts PowerShell está autorizada.
# A CLI Python também funciona diretamente, sem alterar a política de execução.
$directorCli = Join-Path $PSScriptRoot 'diretor.py'
if (!(Test-Path -LiteralPath $directorCli -PathType Leaf)) {
    throw "CLI não encontrada: $directorCli"
}
& python '-X' 'utf8' $directorCli 'instalar' '--pasta-jogo' $PastaJogo
if ($LASTEXITCODE -ne 0) {
    throw 'A instalação foi interrompida pela CLI. Consulte o erro e os backups informados acima.'
}
