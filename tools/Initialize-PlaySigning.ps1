param(
    [string]$SigningDirectory = (Join-Path $env:LOCALAPPDATA 'Dingous\AndroidSigning\br.com.dingous.orcaai'),
    [string]$Alias = 'dingous-orcaai-upload'
)

$ErrorActionPreference = 'Stop'

function Resolve-JavaTool([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $searchRoots = @(
        (Join-Path $env:ProgramFiles 'Android\openjdk'),
        (Join-Path $env:ProgramFiles 'Microsoft'),
        (Join-Path $env:ProgramFiles 'Java')
    ) | Where-Object { $_ -and (Test-Path $_ -ErrorAction SilentlyContinue) }

    foreach ($root in $searchRoots) {
        $candidate = Get-ChildItem -Path $root -Recurse -Filter "$Name.exe" -File -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName -First 1
        if ($candidate) { return $candidate }
    }

    throw "$Name.exe não foi encontrado. Instale o workload Android/.NET MAUI ou um JDK 21."
}

$keytool = Resolve-JavaTool 'keytool'
New-Item -ItemType Directory -Force -Path $SigningDirectory | Out-Null

$keyStore = Join-Path $SigningDirectory 'upload.keystore'
$passwordFile = Join-Path $SigningDirectory 'upload-password.dpapi'

if (Test-Path -LiteralPath $keyStore) {
    throw "Já existe uma chave de upload em $keyStore. Preserve a chave usada na primeira publicação."
}

$random = New-Object byte[] 36
[Security.Cryptography.RandomNumberGenerator]::Fill($random)
$password = [Convert]::ToBase64String($random).TrimEnd('=').Replace('+','A').Replace('/','B')
$securePassword = ConvertTo-SecureString $password -AsPlainText -Force
$securePassword | ConvertFrom-SecureString | Set-Content -LiteralPath $passwordFile -Encoding UTF8

try {
    $keyArgs = @(
        '-genkeypair', '-v',
        '-keystore', $keyStore,
        '-storepass', $password,
        '-keypass', $password,
        '-alias', $Alias,
        '-keyalg', 'RSA',
        '-keysize', '4096',
        '-validity', '10000',
        '-dname', 'CN=Dingous OrçaAI, OU=Mobile, O=Dingous, L=Uberlandia, ST=Minas Gerais, C=BR'
    )
    & $keytool @keyArgs

    if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar a chave de upload.' }

    Write-Host ''
    Write-Host 'Chave de upload criada com sucesso.' -ForegroundColor Green
    Write-Host "Keystore: $keyStore"
    Write-Host "Senha protegida por DPAPI: $passwordFile"
    Write-Host ''
    Write-Host 'Fingerprint SHA-256:' -ForegroundColor Cyan
    & $keytool -list -v -keystore $keyStore -storepass $password -alias $Alias |
        Select-String 'SHA256:'
}
finally {
    $password = $null
    $securePassword = $null
}
