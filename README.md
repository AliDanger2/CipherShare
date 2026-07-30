# CipherShare

A lightweight LAN file-sharing app for Windows, built with WPF and .NET 8. Devices on the same network find each other automatically and can send files and folders directly, with no server, no account, and no internet connection required.

## Features

- **Automatic device discovery** — instances on the same LAN find each other over UDP broadcast, no manual pairing or IP entry needed.
- **Direct file transfers** — files and whole folders move straight over TCP, with real-time progress and speed.
- **Integrity verification** — every received file is checked against a SHA-256 hash computed by the sender.
- **Transfer controls** — configurable concurrent transfer limits, bandwidth throttling, and chunked streaming.
- **Trust & confirmation levels** — require confirmation for every incoming transfer, skip it for trusted devices only, or disable it entirely.
- **Transfer history** — a persisted log of past sends and receives.
- **Desktop notifications** — optional alerts for discovered devices, incoming transfers, and completed, failed, or lost connections.
- **Custom, modern UI** — a hand-styled window with its own title bar, built entirely in WPF/XAML.

## How it works

CipherShare has two independent pieces of networking:

| | Protocol | Purpose |
|---|---|---|
| **Discovery** | UDP broadcast | Each instance periodically announces itself (name, OS, device ID, transfer port) on the LAN and listens for announcements from others. |
| **Transfers** | TCP | The sender opens a connection to the receiver, sends a file header, waits for accept/decline, then streams file bytes followed by a SHA-256 trailer for verification. |

All application data — settings, known devices, transfer history, and this installation's identity — is stored as plain JSON under:

```
%AppData%\CipherShare\
```

There is no cloud service and no external database; that folder is the entire persistence layer.

## Requirements

- Windows 10 or later
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or the SDK, if building from source)

## Getting started

### Build from source

```bash
git clone https://github.com/<your-username>/CipherShare.git
cd CipherShare
dotnet build
```

### Run

```bash
dotnet run --project CipherShare.csproj
```

Or open `CipherShare.csproj` in Visual Studio and run it directly (F5).

### Using the app

1. Launch CipherShare on two or more devices on the same LAN.
2. Devices that are running CipherShare appear automatically in the **Devices** view.
3. Select a device, choose files or a folder to send, and confirm.
4. On the receiving end, accept the incoming transfer (unless the device is trusted or confirmation is disabled in Settings).

## Configuration

All settings are available from the **Settings** view in the app, including:

- Download location
- Auto-discovery on/off, and broadcast interval
- Network port used for both discovery and transfers
- Maximum simultaneous transfers and bandwidth limit
- Security level (confirmation behavior for incoming transfers)
- Whether partial files are kept or discarded when a transfer fails

## Project structure

```
CipherShare/
├── Models/          Data models (devices, transfers, settings, notifications)
├── ViewModels/       MVVM view models backing each screen
├── Views/            WPF views (Home, Devices, Transfers, History, Settings)
├── Services/         Discovery, transfer, storage, and networking logic
├── Converters/       XAML value converters
├── Common/           Shared MVVM infrastructure (ObservableObject, RelayCommand, etc.)
└── Themes/           Colors, icons, and styles
```

## Known limitations

- Discovery relies on IPv4 UDP broadcast and is limited to a single LAN segment; it will not cross routers or subnets that block broadcast traffic.
- Resuming an interrupted transfer from a byte offset isn't supported yet; a failed transfer can be kept as a partial file or discarded, but not resumed in place.
- File contents are transferred in plaintext over TCP; integrity is verified via SHA-256, but transfers are not encrypted in transit. Use CipherShare on networks you trust.

## License

Licensed under the [MIT License](LICENSE).

## Authors

Erfan Mokhtari & Mohamad Reza Mokhtari
