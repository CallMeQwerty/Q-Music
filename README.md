# QMusic

A personal desktop music player that unifies multiple music sources into one fast, minimal interface.

## Stack

- **.NET 10** (LTS) with **Blazor Hybrid** via WPF + BlazorWebView
- **Clean Architecture** — Domain, Application, Infrastructure, Desktop layers
- **NAudio** for audio playback
- **YoutubeExplode** for YouTube audio stream extraction
- **YouTube Data API v3** for search

## Features

- Search YouTube Music and play tracks directly
- Real audio playback with play/pause/stop, seek, and volume control
- Dark-themed minimal UI with album art display
- Provider abstraction — designed for multiple music sources (Spotify planned)

## Getting Started

### Prerequisites

- .NET 10 SDK
- Windows 10/11 (WebView2 runtime, included with Windows 11)
- YouTube Data API v3 key ([Google Cloud Console](https://console.cloud.google.com/))

### Setup

1. Clone the repository
2. Copy the example settings file and add your API key:
   ```
   cp src/QMusic.Desktop/appsettings.local.example.json src/QMusic.Desktop/appsettings.local.json
   ```
   Edit `appsettings.local.json` and set your YouTube API key.
3. Build and run:
   ```
   dotnet build QMusic.slnx
   dotnet run --project src/QMusic.Desktop
   ```

## Architecture

```
QMusic.sln
├── src/
│   ├── QMusic.Domain/            — Entities, value objects, enums (no dependencies)
│   ├── QMusic.Application/       — Use cases, interfaces, DTOs (depends on Domain)
│   ├── QMusic.Infrastructure/    — Concrete implementations (depends on Application)
│   └── QMusic.Desktop/           — WPF host + Blazor UI (presentation layer)
└── tests/
    ├── QMusic.Domain.Tests/
    ├── QMusic.Application.Tests/
    └── QMusic.Infrastructure.Tests/
```

## License

This is a personal project. All rights reserved.
