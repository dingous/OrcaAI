param(
    [string]$Project = (Join-Path $PSScriptRoot '..\src\OrcaAI\OrcaAI.csproj'),
    [string]$SigningDirectory = (Join-Path $env:LOCALAPPDATA 'Dingous\AndroidSigning\br.com.dingous.orcaai'),
    [string]$Alias = 'dingous-orcaai-upload'
)

$ErrorActionPreference = 'Stop'

$keyStore = Join-Path $SigningDirectory 'upload.keystore'
$passwordFile = Join-Path $SigningDirectory 'upload-password.dpapi'

foreach ($path in @($Project, $keyStore, $passwordFile)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Arquivo necessário ausente: $path"
    }
}

$securePassword = Get-Content -LiteralPath $passwordFile -Raw | ConvertTo-SecureString
$password = [Net.NetworkCredential]::new('', $securePassword).Password
$tempPasswordFile = Join-Path $SigningDirectory ('publish-password-' + [Guid]::NewGuid().ToString('N') + '.txt')

try {
    Set-Content -LiteralPath $tempPasswordFile -Value $password -NoNewline -Encoding UTF8

    $publishArgs = @(
        'publish', $Project,
        '-f', 'net10.0-android36.0',
        '-c', 'Release',
        '-p:AndroidPackageFormats=aab',
        '-p:AndroidKeyStore=true',
        "-p:AndroidSigningKeyStore=$keyStore",
        "-p:AndroidSigningKeyAlias=$Alias",
        "-p:AndroidSigningKeyPass=file:$tempPasswordFile",
        "-p:AndroidSigningStorePass=file:$tempPasswordFile"
    )
    & dotnet @publishArgs

    if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o AAB de Release.' }

    $projectDirectory = Split-Path -Parent (Resolve-Path $Project)
    $publishDirectory = Join-Path $projectDirectory 'bin\Release\net10.0-android36.0\publish'
    $bundle = Get-ChildItem -LiteralPath $publishDirectory -Filter '*.aab' -File -ErrorAction Stop |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if (-not $bundle) { throw "Nenhum AAB foi encontrado em $publishDirectory." }

    Write-Host ''
    Write-Host 'AAB pronto para upload manual no Google Play Console:' -ForegroundColor Green
    Write-Host $bundle.FullName
    Write-Host ("SHA-256: " + (Get-FileHash -LiteralPath $bundle.FullName -Algorithm SHA256).Hash)
}
finally {
    if (Test-Path -LiteralPath $tempPasswordFile) {
        Remove-Item -LiteralPath $tempPasswordFile -Force
    }
    $password = $null
    $securePassword = $null
}
