# CubeMeter

แสดง usage limit ของ **Claude** และ **Codex** (5 ชั่วโมง + รายสัปดาห์) บน HelloCubic-Lite

```
CubeBridge (Windows tray, .NET 9)  ──POST /api/usage ทุก 60s──▶  CubeMeter firmware (ESP8266)
  ├─ Claude: api.anthropic.com/api/oauth/usage   (token จาก ~/.claude/.credentials.json)
  └─ Codex:  chatgpt.com/backend-api/wham/usage  (token จาก ~/.codex/auth.json)
```

token อยู่บน PC เท่านั้น cube ได้รับแค่ตัวเลข % กับนาทีที่เหลือจนรีเซ็ต
bridge ไม่ refresh token เอง (refresh token หมุนทุกครั้ง ถ้า refresh เองจะทำให้ Claude Code หลุด login)
ถ้า token หมดอายุ จอจะขึ้น `NO DATA` จนกว่าจะเปิดใช้ Claude Code / Codex อีกครั้ง

## เริ่มใช้

1. **Firmware:** ดู [docs/flashing.md](docs/flashing.md)
2. **Bridge ทดสอบ:** `dotnet run --project bridge/CubeBridge -- --dry-run` (แสดง payload ไม่ส่งไป cube)
3. **Bridge ใช้งานจริง:** `powershell -ExecutionPolicy Bypass -File bridge\install-startup.ps1 -CubeHost <ip-ของ-cube>`

## โครงสร้าง

| path | |
|---|---|
| `firmware/` | PlatformIO (Arduino, Arduino_GFX, WiFiManager) — `src/lcd.cpp` init จอ, `src/ui.cpp` หน้าจอ, `src/main.cpp` WiFi/HTTP/OTA/safe mode |
| `bridge/CubeBridge/` | Worker + tray; `Providers/` ดึง usage, `CubePayload.cs` แปลงเป็น JSON ของ cube |
| `bridge/CubeBridge.Tests/` | `dotnet test bridge` — parser + payload |
| `docs/backup/` | factory backup 7.0.17 และ stock app สำหรับย้อนกลับผ่าน OTA |

`bridge/CubeBridge/appsettings.json` → `Cube:Host`, `PollSeconds`, เปิด/ปิด `Claude` / `Codex`
ถ้า endpoint เปลี่ยนรูปแบบ ดู raw response ล่าสุดที่ `%LOCALAPPDATA%\CubeBridge\last-*.json`

ขอบคุณ [Times-Z/GeekMagic-Open-Firmware](https://github.com/Times-Z/GeekMagic-Open-Firmware) สำหรับ pinout, LCD init และ factory backup
