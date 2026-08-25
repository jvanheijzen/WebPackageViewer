param([switch]$Force)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$licenseKeysSource = Join-Path $repoRoot "WebPackageViewer\Licensing\LicenseKeys.cs"

if (-not (Test-Path $licenseKeysSource)) {
    throw "Could not find: $licenseKeysSource"
}

$keyFolder = Join-Path $env:LOCALAPPDATA "WebPackageViewer\LicenseGenerator"
$privateKeyPath = Join-Path $keyFolder "PrivateKey.protected"
$publicKeyPath = Join-Path $keyFolder "PublicKey.xml"

if ((Test-Path $privateKeyPath) -and -not $Force) {
    Write-Host ""
    Write-Host "Signing keys already exist. No changes were made." -ForegroundColor Yellow
    Write-Host "Key folder: $keyFolder"
    Write-Host ""
    Write-Host "Replacing the key would invalidate all licenses issued with it."
    Write-Host "Use -Force only if you intentionally want a new signing identity."
    exit 1
}

New-Item -ItemType Directory -Path $keyFolder -Force | Out-Null
Add-Type -AssemblyName System.Security

$rsa = [System.Security.Cryptography.RSACryptoServiceProvider]::new(2048)

try {
    $rsa.PersistKeyInCsp = $false
    $privateXml = $rsa.ToXmlString($true)
    $publicXml = $rsa.ToXmlString($false)

    $privateBytes = [System.Text.Encoding]::UTF8.GetBytes($privateXml)
    $protectedBytes = [System.Security.Cryptography.ProtectedData]::Protect(
        $privateBytes,
        $null,
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser)

    [System.IO.File]::WriteAllBytes($privateKeyPath, $protectedBytes)
    [System.IO.File]::WriteAllText(
        $publicKeyPath, $publicXml, [System.Text.UTF8Encoding]::new($false))

    $source = [System.IO.File]::ReadAllText($licenseKeysSource)
    $pattern = 'public\s+const\s+string\s+PublicKeyXml\s*=\s*".*?";'

    if (-not [regex]::IsMatch(
        $source, $pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        throw "Could not locate PublicKeyXml in $licenseKeysSource"
    }

    $replacement = 'public const string PublicKeyXml = "' + $publicXml + '";'

    $updated = [regex]::Replace(
        $source,
        $pattern,
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)
            return $replacement
        },
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    [System.IO.File]::WriteAllText(
        $licenseKeysSource, $updated, [System.Text.UTF8Encoding]::new($true))

    Write-Host ""
    Write-Host "Offline license signing initialized." -ForegroundColor Green
    Write-Host "Private key: $privateKeyPath"
    Write-Host "Public key:  $publicKeyPath"
    Write-Host "Viewer source updated: $licenseKeysSource"
    Write-Host ""
    Write-Host "Rebuild WebPackageViewer and rebuild licensed course EXEs."
}
finally {
    $rsa.Dispose()
}
