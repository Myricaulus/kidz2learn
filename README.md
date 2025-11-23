# 🎮 Kidz4Learn  
Ein interaktives Lernspiel für Kinder – entwickelt mit **Blazor WebAssembly**, **.NET 8** und Spaß am Coden.
Dieses Repo wird automatisch auf netlify deployd und ist unter [text](https://kidz4learn.netlify.app/) erreichbar.

Kidz4Learn ist eine kleine, modulare Lern-App, die Kindern spielerisch verschiedene Aufgaben stellt.  
Sie kombiniert einfache Spielmechaniken (Punkte sammeln, Soundeffekte) mit pädagogischem Nutzen.

Diese Website sammelt Informationen über die Aufgaben im internen Browserspeicher um eine Lernkompetenz zu ermitteln, 
damit die Aufgaben besser ausgewählt werden können. (Leider nocht nicht implementiert, aber gesammelt wird schon)
Es werden keine Informationen nach aussen gegeben, auch ist nach dem Herunterladen keine Internetverbindung nötig.
Die Seite ist vollständig autark und verwendet keine cookies.. 

---

## ✨ Features

### 🧮 Lernspiele
- Einfache Matheaufgaben (Additionen, Subtraktionen)
- Zufällige Aufgaben-Generierung
- Punkte- und Levelsystem (halb implementiert, wird erweitert)

### 🔊 Sound & Musik
- Hintergrundmusik über ein **PlayerWidget**
- Soundeffekte für richtige Antworten (SidPlayer-Komponente)
- Automatische Lautstärkeanpassung je nach Seite

### 🧩 Blazor Components
- Saubere Trennung von Layout, Komponenten und Pages  
- Jede Lernaufgabe ist eine eigenständige Komponente
- Wiederverwendbare UI-Elemente

---

## 🧱 Projektstruktur
- /Components → UI-Bausteine (Buttons, PlayerWidget, PointsBadge …)
- /Layout → MainLayout + Navigationsstruktur
- /Pages → Mathe-Seiten, Startseite, Lernmodule
- /Services → UpdatePointsService, SoundService (PlayerWidget-Steuerung)
- App.razor → App-Root & Router
- Kidz2Learn.csproj


---

## 🧠 Architektur

### 🔹 Services statt Komponenten-Kommunikation  
Die App verwendet einen **UpdatePointsService**, um Punktestände global zu aktualisieren, statt direkten Component-Refs.  
Das reduziert Kopplung und sorgt für testbaren Code.

### 🔹 Soundsteuerung über eine zentrale Komponente  
Das **SidPlayerWidget** übernimmt:
- Laden der Musik
- Start/Stop
- Volume-Management

Pages können über Events/Services die Lautstärke verringern oder wiederherstellen.

### 🔹 Lifecycle-Fokus  
Die App nutzt Blazors Lifecycle-Ereignisse sinnvoll aus:  
- `OnInitialized()` → Services abonnieren  
- `OnAfterRenderAsync()` → Audio-Setup, das DOM benötigt  
- `IDisposable.Dispose()` → Event-Unsubscribe

---

## 🚀 Getting Started

### Voraussetzungen
- .NET 8 SDK  
- Ein Browser  
- Optional: VS Code oder Visual Studio

### Starten
```bash
dotnet run
