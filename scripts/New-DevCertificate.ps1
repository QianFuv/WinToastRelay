param(
    [string]$Password = "WinToastRelay-dev",
    [int]$ValidYears = 2
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$certificateDirectory = Join-Path $projectRoot "certs"
$pfxPath = Join-Path $certificateDirectory "WinToastRelay-dev.pfx"
$cerPath = Join-Path $certificateDirectory "WinToastRelay-dev.cer"

New-Item -ItemType Directory -Force $certificateDirectory | Out-Null

# MSIX sideloading requires a non-CA Basic Constraints extension.
$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=RavelloH" `
    -FriendlyName "RavelloH WinToastRelay Development Package Signing" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -HashAlgorithm SHA256 `
    -TextExtension @("2.5.29.19={text}CA=false") `
    -NotAfter (Get-Date).AddYears($ValidYears)

$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $securePassword -Force | Out-Null
Export-Certificate -Cert $certificate -FilePath $cerPath -Force | Out-Null

Write-Host "Created development certificate: $cerPath"
Write-Host "Thumbprint: $($certificate.Thumbprint)"
Write-Host "The .pfx and .cer files are intentionally ignored by Git."
