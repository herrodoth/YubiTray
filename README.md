# YubiTray

A tiny Windows tray app that shows whether your YubiKey is connected to **this** PC.
Handy when you switch machines with a KVM and never know where the key currently is.

- Icon changes between connected / disconnected
- Tooltip shows the state (and the count, if more than one key is plugged in)
- Refresh interval: 0.5s / 1.5s / 5s (right-click menu)
- Optional "start with Windows" (per-user `Run` registry key)
- Single instance, no dependencies besides .NET Framework 4.8 (built into Windows 10/11)

## How it works

It polls WMI (`Win32_PnPEntity`) for devices with Yubico's USB vendor ID `VID_1050`
and counts the parent USB device of each key (a single YubiKey shows up as several
Windows devices: composite device, HID, smartcard reader, etc.).

## Build

Requires the .NET SDK (any recent version can build `net48` targets on Windows).

```powershell
cd YubiTray
dotnet build -c Release
```

Output: `YubiTray/bin/Release/net48/YubiTray2.exe`

## Files

| Path | Purpose |
|---|---|
| `YubiTray/Program.cs` | The whole app |
| `YubiTray/app.ico` | Exe icon |
| `YubiTray/ico/` | Tray icons (connected / disconnected), embedded in the exe |
