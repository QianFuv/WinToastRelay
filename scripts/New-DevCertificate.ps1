param(
    [string]$Password = "WinToastRelay-store-upload",
    [int]$ValidYears = 2,
    [string]$Subject = "CN=184C7048-0661-4259-8EE3-39EFE462DFBE",
    [string]$OutputName = "WinToastRelay-store-upload"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$certificateDirectory = Join-Path $projectRoot "certs"
$pfxPath = Join-Path $certificateDirectory "$OutputName.pfx"
$cerPath = Join-Path $certificateDirectory "$OutputName.cer"

New-Item -ItemType Directory -Force $certificateDirectory | Out-Null

# MSIX sideloading requires a non-CA Basic Constraints extension.
$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -FriendlyName "WinToastRelay package signing ($Subject)" `
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
