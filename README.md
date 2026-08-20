# WinToastRelay

WinToastRelay is a native Windows 11 app that relays Windows toast notifications to a webhook in real time.

WinToastRelay uses `Windows.UI.Notifications.Management.UserNotificationListener` and its `NotificationChanged` event. It does not use a timer or polling loop. The notification center is enumerated once at startup to establish a baseline; each later event is handled by its notification ID.

## Features

- WinUI 3 / Windows App SDK visual language with Mica and native Windows controls.
- Chinese and English UI, switchable from Settings.
- Bark delivery is the default: `/device-key/title/body?parameters` with configurable templates and arbitrary Bark parameters.
- HTTPS JSON webhook delivery with an optional Bearer token remains available (loopback HTTP is allowed for local development).
- Bearer token stored in Windows Credential Manager, not in the JSON settings file.
- Application allow-list filter.
- Delivery activity history with status and HTTP response details.
- Durable local delivery queue with exponential backoff for transient HTTP failures.
- System-tray operation: closing the window keeps the relay running; the tray menu can reopen or exit it.
- Optional Windows sign-in startup, using the packaged startup-task API.
- Automatic event listener startup after a valid destination is configured; no manual start button is required.
- Application filters presented as per-app switches for notification sources already observed by Windows.
- Single-project MSIX packaging with package identity; notification access is granted explicitly through `RequestAccessAsync`.

## Build

Requirements:

- Windows 10 1809 or later (Windows 11 recommended).
- .NET 9 SDK.
- A Windows App SDK-compatible development environment.

```powershell
dotnet restore .\WinToastRelay.csproj -r win-x64
dotnet build .\WinToastRelay.csproj -r win-x64 -p:Platform=x64
dotnet test .\tests\WinToastRelay.Tests\WinToastRelay.Tests.csproj
```

The project is intentionally packaged because the Windows notification listener needs an interactive packaged identity. On first use, press **Start listening** and approve notification access when Windows asks.

## Development MSIX

Create a local code-signing certificate once. The generated files are ignored by Git.

The package publisher display name is `RavelloH`, and the package requests exactly one capability: `runFullTrust`. This is required for the packaged WinUI 3 desktop executable and its optional Windows startup task; it does not grant notification-listener access. Access to Windows notifications is separately requested at runtime through `UserNotificationListener.RequestAccessAsync` and can be denied or revoked by the user. No AI-model capability is declared.

The Store submission uses the reserved package identity `RavelloH.WinToastRelay` and Publisher `CN=184C7048-0661-4259-8EE3-39EFE462DFBE`. This is different from the original local development identity. Microsoft Store will re-sign the submitted package; the temporary matching certificate below is only for producing a package that can be uploaded to Partner Center.

Create a temporary certificate whose subject matches the reserved Store Publisher and use it only during packaging:

```powershell
.\scripts\New-DevCertificate.ps1 `
  -Subject "CN=184C7048-0661-4259-8EE3-39EFE462DFBE" `
  -OutputName "WinToastRelay-store-upload" `
  -Password "choose-a-temporary-password" `
  -ValidYears 1
dotnet publish .\WinToastRelay.csproj -r win-x64 -p:Platform=x64 -p:Configuration=Release `
  -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Always -p:AppxBundlePlatforms=x64 `
  -p:PackageCertificateKeyFile="$PWD\certs\WinToastRelay-store-upload.pfx" `
  -p:PackageCertificatePassword="choose-a-temporary-password"
```

Upload the resulting `.msixbundle` from `AppPackages` under **管理程序包** in Partner Center. Do not upload the `.cer`; Microsoft Store replaces the package signature when the submission is accepted.

For local sideloading, run the generated `Add-AppDevPackage.ps1` from the package output directory and allow it to install the matching `.cer` into the **Local Computer → Trusted People** store. You can also import that `.cer` manually with administrator approval. The temporary certificate includes the non-CA Basic Constraints extension required by MSIX sideloading. Do not publish the temporary PFX or CER as a public signing identity; Microsoft Store replaces the signature for Store distribution.

### Certificate troubleshooting

Use the `.msix` and `.cer` from the same output directory. If Windows reports `0x800B0109` or `0x87e80034` during local sideloading, import the matching `.cer` into **Local Computer → Trusted People** and then install the matching `.msix`.

## Webhook payload

## Delivery modes

### Bark (default)

Enter a Bark server URL and device key. WinToastRelay sends the standard Bark URL form:

```text
https://api.day.app/{device-key}/{title}/{body}?sound=bell&group=work
```

Title and body templates support `{app}`, `{title}`, `{body}`, `{id}`, `{eventType}`, and `{createdAt}`. Add any Bark query parameters as `key=value`, one per line, in the app settings.

### Generic JSON webhook

```json
{
  "eventType": "notification.added",
  "deliveryId": "a-generated-id",
  "notification": {
    "id": 123,
    "app": "Example app",
    "title": "A title",
    "body": "Notification body",
    "createdAt": "2026-08-20T00:00:00Z"
  }
}
```

The `X-WinToastRelay-Delivery` header contains the same delivery ID to make receiver-side deduplication straightforward. Transient failures (timeouts, 429, and 5xx responses) are persisted locally and retried with exponential backoff. Other failed responses are persisted as dead letters and reported in the activity view for the active session.

## Privacy

Webhook delivery sends the visible notification text, source application display name, and creation time to the configured endpoint. Review the endpoint's retention and access policy before relaying sensitive notifications. The optional bearer token is stored only in Windows Credential Manager.

## Project status

This repository is an early working application. GitHub Release automation is available; a broadly distributable public release still needs Microsoft Store signing or a publicly trusted code-signing certificate.
