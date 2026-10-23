[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Caminho
)

# Entregável para ambientes onde arquivos .ps1 são permitidos. Não mudar políticas
# nem executar seu texto para contornar a restrição da sessão atual.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$schemaPath = Join-Path $projectRoot 'esquemas\cena-v1.xsd'
if ([string]::IsNullOrWhiteSpace($Caminho)) {
    $Caminho = Join-Path $projectRoot 'cenas\teste_dupla.xml'
}
$erros = [System.Collections.Generic.List[string]]::new()
$reader = $null
$schemaReader = $null

function Adicionar-Erro([string]$Mensagem) {
    [void]$erros.Add($Mensagem)
}

function Conferir-Identificador([string]$Valor, [string]$Campo) {
    if (![regex]::IsMatch($Valor, '\A[a-z][a-z0-9_]{0,47}\z')) {
        Adicionar-Erro "$Campo deve começar com letra minúscula e conter até 48 letras minúsculas, números ou sublinhados."
    }
}

function Conferir-Numero([System.Xml.XmlElement]$Elemento, [string]$Campo,
                        [double]$Minimo, [double]$Maximo) {
    $texto = $Elemento.GetAttribute($Campo)
    [double]$numero = 0
    $estilo = [System.Globalization.NumberStyles]::Float
    $cultura = [System.Globalization.CultureInfo]::InvariantCulture
    if (![double]::TryParse($texto, $estilo, $cultura, [ref]$numero) -or
        [double]::IsNaN($numero) -or [double]::IsInfinity($numero) -or
        $numero -lt $Minimo -or $numero -gt $Maximo) {
        $minimoTexto = $Minimo.ToString($cultura)
        $maximoTexto = $Maximo.ToString($cultura)
        Adicionar-Erro "$($Elemento.Name).$Campo deve ser um número finito de $minimoTexto a $maximoTexto, usando ponto para decimais."
    }
}

try {
    if (!(Test-Path -LiteralPath $schemaPath -PathType Leaf)) {
        throw "Esquema não encontrado: $schemaPath"
    }
    if (!(Test-Path -LiteralPath $Caminho -PathType Leaf)) {
        throw "Cena não encontrada: $Caminho"
    }

    $validationHandler = [System.Xml.Schema.ValidationEventHandler] {
        param($origem, $evento)
        $linha = $evento.Exception.LineNumber
        $coluna = $evento.Exception.LinePosition
        Adicionar-Erro "A estrutura XML não atende ao esquema (linha $linha, coluna $coluna): $($evento.Message)"
    }
    $schemaSettings = [System.Xml.XmlReaderSettings]::new()
    $schemaSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $schemaSettings.XmlResolver = $null
    $schemaReader = [System.Xml.XmlReader]::Create($schemaPath, $schemaSettings)
    $schemas = [System.Xml.Schema.XmlSchemaSet]::new()
    $schemas.XmlResolver = $null
    $schemas.add_ValidationEventHandler($validationHandler)
    [void]$schemas.Add('', $schemaReader)
    $schemas.Compile()
    $schemaReader.Dispose()
    $schemaReader = $null
    if ($erros.Count -gt 0) { throw 'O esquema local é inválido; revise os erros de estrutura.' }

    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.ValidationType = [System.Xml.ValidationType]::Schema
    $settings.Schemas = $schemas
    $settings.ValidationFlags = [System.Xml.Schema.XmlSchemaValidationFlags]::ProcessIdentityConstraints -bor
                                [System.Xml.Schema.XmlSchemaValidationFlags]::ReportValidationWarnings
    $settings.add_ValidationEventHandler($validationHandler)
    $reader = [System.Xml.XmlReader]::Create([System.IO.Path]::GetFullPath($Caminho), $settings)
    $document = [System.Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    $document.Load($reader)
    $reader.Dispose()
    $reader = $null

    # XSD garante ordem, campos obrigatórios, atributos permitidos e referências.
    # As conferências abaixo reforçam limites e regras não expressas pelo XSD 1.0.
    foreach ($elemento in $document.SelectNodes('//*')) {
        if ($elemento.NamespaceURI -ne '') {
            Adicionar-Erro 'A cena não permite namespaces XML.'
        }
        foreach ($atributo in $elemento.Attributes) {
            if ($atributo.NamespaceURI -ne '' -or $atributo.Prefix -ne '' -or $atributo.Name -eq 'xmlns') {
                Adicionar-Erro 'A cena não permite atributos nem declarações de namespace XML.'
            }
        }
    }

    $cena = $document.DocumentElement
    if ($null -ne $cena -and $cena.Name -eq 'Cena' -and $cena.NamespaceURI -eq '') {
        Conferir-Identificador $cena.GetAttribute('id') 'Cena.id'
        if ([string]::IsNullOrWhiteSpace($cena.GetAttribute('nome'))) {
            Adicionar-Erro 'Cena.nome é obrigatório e não pode conter somente espaços.'
        }

        $ambiente = $cena.SelectSingleNode('Ambiente')
        if ($null -ne $ambiente) {
            Conferir-Numero $ambiente 'hora' 0 23
            Conferir-Numero $ambiente 'minuto' 0 59
            Conferir-Numero $ambiente 'densidadeTransito' 0 1
            Conferir-Numero $ambiente 'densidadePedestres' 0 1
            if ($ambiente.GetAttribute('bloquearPolicia') -cnotin @('true', 'false')) {
                Adicionar-Erro 'Ambiente.bloquearPolicia deve ser true ou false.'
            }
        }
        $local = $cena.SelectSingleNode('Local')
        if ($null -ne $local) { Conferir-Numero $local 'distanciaFrente' 1 10 }

        $personagens = $cena.SelectNodes('Personagens/Personagem')
        if ($personagens.Count -lt 2 -or $personagens.Count -gt 4) {
            Adicionar-Erro 'Personagens deve conter de 2 a 4 personagens.'
        }
        $ids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($personagem in $personagens) {
            $id = $personagem.GetAttribute('id')
            Conferir-Identificador $id 'Personagem.id'
            if (!$ids.Add($id)) { Adicionar-Erro "O identificador de personagem '$id' está repetido." }
            if ([string]::IsNullOrWhiteSpace($personagem.GetAttribute('nome'))) {
                Adicionar-Erro "O nome do personagem '$id' é obrigatório e não pode conter somente espaços."
            }
            if (![regex]::IsMatch($personagem.GetAttribute('modelo'), '\A[a-z0-9_]{1,64}\z')) {
                Adicionar-Erro "O modelo do personagem '$id' deve conter de 1 a 64 letras minúsculas, números ou sublinhados."
            }
            Conferir-Numero $personagem 'direita' -10 10
            Conferir-Numero $personagem 'frente' -10 10
            Conferir-Numero $personagem 'altura' -2 2
            Conferir-Identificador $personagem.GetAttribute('olharPara') "Personagem '$id'.olharPara"
        }
        foreach ($personagem in $personagens) {
            $id = $personagem.GetAttribute('id')
            $alvo = $personagem.GetAttribute('olharPara')
            if ($alvo -ceq $id) {
                Adicionar-Erro "O personagem '$id' deve olhar para outro personagem."
            } elseif (!$ids.Contains($alvo)) {
                Adicionar-Erro "O alvo de olhar '$alvo' do personagem '$id' não existe na cena."
            }
        }
    }
} catch {
    Adicionar-Erro "Não foi possível validar a cena: $($_.Exception.Message)"
} finally {
    if ($null -ne $reader) { $reader.Dispose() }
    if ($null -ne $schemaReader) { $schemaReader.Dispose() }
}

if ($erros.Count -gt 0) {
    foreach ($mensagem in ($erros | Select-Object -Unique)) {
        [Console]::Error.WriteLine("Cena inválida: $mensagem")
    }
    exit 1
}
Write-Output 'Estrutura válida; modelos/coordenadas exigem teste no jogo.'
exit 0
