# Hikvision Voice Relay 🎤➡️📹

> PC microphone → G.711 A-law → Hikvision PTZ camera speaker.  
> HTTP API for web platform integration. One-click deploy.

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Windows-10%2F11%20x64-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

## ✨ Features

- **Zero install** — self-contained exe, runs on any Windows 10/11 x64
- **Web API** — `GET /open` `/close` `/status` on `127.0.0.1:8888`
- **G.711 A-law** software encoder (no extra codec needed)
- **8kHz mono 16-bit** PCM capture via `winmm`
- **Auto login fallback** — tries `NET_DVR_Login_V40` then `V30`

## 🚀 Quick Start

1. Download from [Releases]() → unzip
2. Double-click `VoiceRelay.exe`
3. Enter device IP / port / username / password
4. Click **Login** → **Start Relay**
5. Speak into your mic → camera speaker plays it

```bash
# Or use the HTTP API
curl http://127.0.0.1:8888/open   # start talking
curl http://127.0.0.1:8888/close  # stop
curl http://127.0.0.1:8888/status # check state
```

## 🏗️ Architecture

```
┌─────────────┐     PCM (8kHz)      ┌──────────────┐
│  Microphone  │ ──────────────────▶ │  G.711 Encoder │
└─────────────┘                     └──────┬───────┘
                                           │ G.711 frames
                                           ▼
                                  ┌────────────────┐
                                  │  HCNetSDK.dll   │
                                  │  VoiceComSend   │
                                  └───────┬────────┘
                                          │ Network (RTSP)
                                          ▼
                                  ┌────────────────┐
                                  │  PTZ Camera     │
                                  │  Speaker Output  │
                                  └────────────────┘
```

## 📦 Tech Stack

| Layer | Tech |
|-------|------|
| UI | C# WinForms (.NET 9.0) |
| Audio | `winmm.dll` waveIn (P/Invoke) |
| Codec | G.711 A-law (software, C#) |
| SDK | HCNetSDK 6.1.9.48 |
| HTTP | `TcpListener` (no admin required) |
| Deploy | `dotnet publish --self-contained` |

## 🔧 Build from Source

```bash
git clone https://github.com/liujiangyi217/hikvision-voice-relay.git
cd hikvision-voice-relay/src

# Install .NET 9.0 SDK first, then:
dotnet publish -c Release -r win-x64 --self-contained
```

## 📡 HTTP API

See [API文档.md](API文档.md) for full documentation (Chinese).

| Endpoint | Description |
|----------|-------------|
| `GET /open` | Login + start voice relay |
| `GET /close` | Stop voice relay |
| `GET /status` | `{"state":"open","loggedIn":true}` |

## 🌏 中文说明

海康威视语音对讲工具：PC 麦克风采集 → G.711 A-law 编码 → 发送到海康云台/摄像头扬声器。提供 HTTP API 供 Web 平台调用，支持自包含发布，复制即用。

## ⭐ Star History

If this project helps you, please ⭐ star it!

## 📄 License

MIT © [liujiangyi217](https://github.com/liujiangyi217)
