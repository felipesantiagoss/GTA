[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$api = Join-Path $projectRoot 'dependencias\ScriptHookVDotNet-v3.7.0-nightly.191\ScriptHookVDotNet3.dll'
$mainSource = Join-Path $projectRoot 'src\DiretorGTA.3.cs'
$helperSources = @('CameraDiretor.cs', 'AmbienteDiretor.cs', 'LocaisDiretor.cs', 'CenaJsonDiretor.cs', 'CapturaDiretor.cs', 'VisualDiretor.cs', 'AtalhoGravacaoDiretor.cs', 'ObjetosDiretor.cs', 'ElencoAutomaticoDiretor.cs', 'PreparacaoElencoDiretor.cs', 'PilotoPedidoDiretor.cs', 'EnsaioAcaoDiretor.cs', 'AcoesSerieDiretor.cs', 'TemporadaDiretor.cs', 'InterpretacaoDiretor.cs', 'CenaQuedaDiretor.cs', 'ObjetosSerieDiretor.cs', 'VeiculosSerieDiretor.cs', 'PosesSerieDiretor.cs', 'EpisodioUmDiretor.cs', 'VariantesDjaniraDiretor.cs', 'OperacaoLocalDiretor.cs', 'OperacaoProducaoDiretor.cs', 'EpisodiosTresSeteDiretor.cs', 'PortasSerieDiretor.cs', 'CenaVooTemporadaDiretor.cs', 'CenaEmpurraoAviaoDiretor.cs', 'FigurinoTransitorioDiretor.cs', 'CenaTemporadaDiretor.cs', 'ArmarioSerieDiretor.cs', 'LuzesBoateDiretor.cs', 'EntradaSentadaAutorDiretor.cs', 'CartaFisicaDiretor.cs', 'MesaColisaoTrialDiretor.cs', 'LimpezaMesaDiretor.cs', 'GorjetaFisicaTrialDiretor.cs') | ForEach-Object { Join-Path $projectRoot ('src\' + $_) }
$source = Join-Path $projectRoot 'dist\DiretorGTA.3.cs'
$output = Join-Path $projectRoot 'dist\DiretorGTA.dll'

foreach ($required in (@($compiler, $api, $mainSource) + $helperSources)) {
    if (!(Test-Path -LiteralPath $required -PathType Leaf)) { throw "Arquivo necessário não encontrado: $required" }
}
# Igual ao Python: UTF-8 sem BOM, finais de linha LF e main antes dos helpers.
$utf8 = New-Object System.Text.UTF8Encoding($false)
$mainText = [IO.File]::ReadAllText($mainSource, $utf8).Replace("`r`n", "`n").Replace("`r", "`n").TrimEnd()
$parts = @($mainText)
foreach ($helperSource in $helperSources) {
    $helperText = [IO.File]::ReadAllText($helperSource, $utf8).Replace("`r`n", "`n").Replace("`r", "`n").TrimEnd()
    if ($helperText -match '(?m)^\s*using\s+(?!\()[^;\r\n]+;') {
        throw "O helper deve usar nomes completos, sem diretivas using: $helperSource"
    }
    $parts += $helperText
}
$combinedText = ($parts -join "`n`n") + "`n"
$scriptClasses = [regex]::Matches($combinedText, '(?m)^\s*(?:(?:public|internal|sealed|abstract|partial)\s+)*class\s+[A-Za-z_]\w*\s*:\s*(?:GTA\.)?Script\b')
if ($scriptClasses.Count -ne 1) {
    throw 'A fonte combinada deve conter exatamente uma classe que herda Script.'
}
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'dist') -Force | Out-Null
$backupDir = $null
# Preservar fonte gerada e DLL anteriores antes de alterar qualquer uma.
foreach ($existing in @($source, $output)) {
    if (!(Test-Path -LiteralPath $existing)) { continue }
    $item = Get-Item -LiteralPath $existing
    if (!$item.PSIsContainer -and (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0)) {
        if ($null -eq $backupDir) {
            $backupName = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N')
            $backupDir = Join-Path $projectRoot ('backups\compilacao\' + $backupName)
            New-Item -ItemType Directory -Path $backupDir | Out-Null
        }
        $backupFile = Join-Path $backupDir $item.Name
        $previousHash = (Get-FileHash -LiteralPath $existing -Algorithm SHA256).Hash
        Copy-Item -LiteralPath $existing -Destination $backupFile
        if ((Get-FileHash -LiteralPath $backupFile -Algorithm SHA256).Hash -ne $previousHash) {
            throw "O backup da compilação não confere: $backupFile"
        }
    } else {
        throw "O destino não é um arquivo comum; revise antes de compilar: $existing"
    }
}
[IO.File]::WriteAllText($source, $combinedText, $utf8)

# O compilador do .NET Framework já presente verifica a mesma API usada no jogo.
& $compiler '/nologo' '/codepage:65001' '/target:library' '/platform:anycpu' '/optimize+' '/langversion:5' '/warn:4' '/warnaserror+' "/reference:$api" '/reference:System.dll' '/reference:System.Core.dll' '/reference:System.Windows.Forms.dll' '/reference:System.Xml.dll' '/reference:System.Drawing.dll' "/out:$output" $source
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação. Corrija os erros acima antes de instalar.' }
[pscustomobject]@{Resultado='Fonte única com câmera, ambiente, locais e JSON compilada sem erros; teste no jogo ainda necessário';Arquivo=$output;SHA256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash;FontePublicavel=$source;FonteSHA256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash}
