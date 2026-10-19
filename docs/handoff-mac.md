# Handoff: ทำ CubeMeter ต่อบน MacBook

> สำหรับ Claude Code บน Mac — อ่านไฟล์นี้ + `README.md` + `docs/flashing.md` ก่อนเริ่ม
> ผู้ใช้ต้องการให้ตอบเป็นภาษาไทย

## เป้าหมาย

HelloCubic-Lite (ESP8266 + ST7789 240×240) แสดง usage limit ของ **Claude** และ **Codex** (5 ชั่วโมง + รายสัปดาห์)
โดย **MacBook เป็นตัวกลาง**: Mac ต่อเน็ตบริษัทผ่าน UGREEN (LAN ที่ลงทะเบียนแล้ว) และปล่อย WiFi ผ่าน
Internet Sharing ให้ cube, แล้วรัน CubeBridge บน Mac

```
เน็ตบริษัท ──LAN── UGREEN ── MacBook ──WiFi (Internet Sharing, 2.4GHz)── cube (192.168.2.x)
                               └─ CubeBridge (macOS, launchd) ──POST /api/usage ทุก 60s──▶ cube
```

ทำไมต้องเป็น Mac: PC Windows ต่อแค่สาย LAN ไม่มี WiFi และอยู่คนละวงกับ WiFi บริษัท (น่าจะติด firewall)
usage เป็นของบัญชี ไม่ใช่ของเครื่อง — ผู้ใช้ยืนยันว่า Mac login Claude Code + Codex บัญชีเดียวกันแล้ว และเปิดทั้งวัน

## สถานะตอนนี้

| ส่วน | สถานะ |
|---|---|
| `firmware/` | build ผ่าน (424KB, RAM 47%) **ยังไม่เคยรันบน cube จริง** |
| `bridge/` | ทดสอบบน Windows กับ API จริงแล้ว, `dotnet test bridge` ผ่าน 8/8 — **ยังเป็น `net9.0-windows` (WinForms tray) รันบน Mac ไม่ได้** |
| cube | ยังเป็น stock firmware 7.0.17, ยังไม่ได้ต่อ WiFi ไหน (AP `GIFTV`, `http://192.168.4.1`) |

## ข้อเท็จจริงที่ตรวจแล้ว (อย่าเปลี่ยนโดยไม่มีเหตุผล)

- **Endpoint usage (ทดสอบแล้ว 200 OK):**
  - Claude: `GET https://api.anthropic.com/api/oauth/usage` + `Authorization: Bearer <accessToken>` + `anthropic-beta: oauth-2025-04-20`
    → `five_hour` / `seven_day` `{utilization, resets_at}` (fallback: `limits[]` `{group: session|weekly, percent, resets_at}`)
  - Codex: `GET https://chatgpt.com/backend-api/wham/usage` + Bearer `tokens.access_token` + header `ChatGPT-Account-Id: tokens.account_id`
    → `rate_limit.primary_window` (5h) / `secondary_window` (weekly) `{used_percent, reset_at (unix s)}`
- **ห้าม refresh OAuth token เอง** — refresh token หมุนทุกครั้ง ถ้า bridge refresh จะทำให้ Claude Code/Codex หลุด login
  ให้อ่านไฟล์/keychain ใหม่ทุกรอบ และถ้าหมดอายุให้รายงาน error (จอขึ้น `NO DATA`)
- **ESP8266WebServer อ่าน chunked body ไม่ได้** — ต้องส่ง POST แบบมี `Content-Length` (ดู `bridge/CubeBridge/CubeClient.cs`)
- **Flash layout = `eagle.flash.4m3m.ld` เหมือน stock** (LittleFS stock ที่ 0x100000–0x3FA000) → OTA ของเราไม่แตะ FS ของ stock
  และย้อนกลับ stock ได้โดยอัป `docs/backup/stock-app-7.0.17.bin` ผ่าน `/update`
- **งบขนาด firmware ≤ 0x86000 (548,864 bytes)** สำหรับการแฟลชครั้งแรกจาก stock — `firmware/scripts/check_size.py` บังคับไว้แล้ว
- **LCD:** SPI_MODE3 (CS ต่อ GND), DC=0, RST=2, BL=5 (active LOW), vendor init sequence ใน `firmware/src/lcd.cpp`
  `LCD_ROTATION = 4` (MADCTL MX) ตาม Times-Z — **ยังไม่ยืนยันบนเครื่องจริง** ถ้าตัวหนังสือกลับซ้ายขวาเทียบกับ stock ให้ใช้ 0
- Payload ที่ cube รับ: `{"c":{"ok":true,"s":21,"sr":142,"w":7,"wr":9876},"x":{...},"ts":...}` (s/w = %, sr/wr = นาทีถึง reset, `e` = error สั้น ๆ เมื่อ ok=false)

## งานที่ต้องทำ (ตามลำดับ)

### 1. เตรียม Mac
```bash
git clone https://github.com/riwjung/CubeProject.git && cd CubeProject
python3 -m venv .venv && .venv/bin/pip install platformio
.venv/bin/pio run -e hellocubic -d firmware      # ต้องได้ firmware.bin ≤ OTA budget
brew install --cask dotnet-sdk                    # ถ้ายังไม่มี .NET 9 SDK
```

### 2. ทำ CubeBridge ให้รันบน macOS
- เปลี่ยนเป็น multi-target: `net9.0` (console, ทุก OS) + `net9.0-windows` (มี tray, `UseWindowsForms` เฉพาะ target นี้)
  ห่อโค้ด tray / `AttachConsole` ใน `Program.cs` ด้วย `#if WINDOWS`; บน net9.0 ให้รันแบบ console เสมอ
- **Claude credentials บน macOS อยู่ใน Keychain ไม่ใช่ไฟล์** (service `Claude Code-credentials` — ตรวจด้วย
  `security find-generic-password -s "Claude Code-credentials" -w` ควรได้ JSON ที่มี `claudeAiOauth`)
  → `ClaudeUsageProvider` บน macOS: env `CLAUDE_CODE_OAUTH_TOKEN` → keychain (เรียก `security` CLI) → `~/.claude/.credentials.json`
  ครั้งแรกที่ launchd agent เรียก `security` อาจมี dialog ขอสิทธิ์ keychain ให้ผู้ใช้กด **Always Allow**
- Codex บน Mac: ตรวจว่า `~/.codex/auth.json` มีอยู่ (ถ้า Codex ตั้งให้เก็บใน keyring ต้องปรับเหมือน Claude)
- เพิ่ม test ของ path เลือก credential เท่าที่ทำได้ แล้ว `dotnet test bridge` ต้องผ่านทั้งหมด
- ทดสอบ: `dotnet run --project bridge/CubeBridge -f net9.0 -- --dry-run` → ตัวเลขต้องตรงกับ `/usage` ใน Claude Code และ `/status` ใน Codex
- เพิ่ม `bridge/install-macos.sh`: `dotnet publish -f net9.0 -c Release -o ~/Library/Application\ Support/CubeBridge`,
  ตั้ง `Cube:Host`, สร้าง `~/Library/LaunchAgents/com.cubemeter.bridge.plist` (RunAtLoad + KeepAlive, log ไป `~/Library/Logs/CubeBridge.log`),
  `launchctl bootstrap gui/$(id -u) ...`; มี `--uninstall`
- **ห้ามลบ/ทำลายของฝั่ง Windows** (`install-startup.ps1`, tray) — ต้องยังใช้ได้

### 3. ตั้ง Internet Sharing + ต่อ cube (ผู้ใช้ทำ, Claude อธิบายทีละขั้น)
1. เสียบ UGREEN → Mac ต่อเน็ตบริษัทผ่าน LAN
2. System Settings → General → Sharing → Internet Sharing: share from **USB 10/100/1000 LAN** → to **Wi-Fi**
   ตั้งชื่อ/รหัส WiFi, ถ้าเลือก channel ได้ให้ใช้ 1/6/11 (2.4GHz — ESP8266 ไม่รองรับ 5GHz)
3. ต่อมือถือเข้า `GIFTV` → `http://192.168.4.1` → เลือก WiFi ของ Mac → SAVE
4. หา IP ของ cube: ดูบนจอ cube หรือบน Mac `cat /var/db/dhcpd_leases` (ปกติ `192.168.2.x`)
5. ตั้ง Mac ไม่ให้ sleep ตอนเสียบไฟ (Internet Sharing หยุดเมื่อ sleep): System Settings → Battery/Energy → Prevent automatic sleeping

### 4. แฟลช firmware (**ต้องถามผู้ใช้ยืนยันก่อนทุกครั้ง**)
1. ตรวจว่าเข้าถึงได้: `ping <ip>`, `curl -sI http://<ip>/update` (ถ้าไม่มีหน้า `/update` → **หยุด** แจ้งผู้ใช้)
2. แฟลช: เปิด `http://<ip>/update` ใน browser แล้วเลือก `firmware/.pio/build/hellocubic/firmware.bin`
   (หรือ `curl -F "firmware=@firmware/.pio/build/hellocubic/firmware.bin" http://<ip>/update` — เช็กชื่อ field ของหน้า stock ก่อน)
   บอกผู้ใช้ **ห้ามถอดสาย ~30 วินาที**
3. cube ควรขึ้น `CubeMeter` → `Connecting WiFi...` → `Waiting for CubeBridge...` + IP
   (ถ้าขึ้น `Join WiFi: CubeSetup` ให้ต่อ WiFi `CubeSetup` แล้วเลือก WiFi ของ Mac ใหม่)
4. **ยืนยัน OTA รอบสอง** ผ่าน `/update` ของ CubeMeter เอง ก่อนทำอย่างอื่น
5. ทดสอบจอ:
   ```bash
   curl -X POST http://<ip>/api/usage -H 'Content-Type: application/json' \
     -d '{"c":{"ok":true,"s":72,"sr":95,"w":31,"wr":5000},"x":{"ok":true,"s":12,"sr":200,"w":90,"wr":700}}'
   curl http://<ip>/api/status
   ```
   ขอรูปจากผู้ใช้ เช็กทิศทางภาพ / สี (เขียว <60, เหลือง 60–85, แดง ≥85) / ตัวหนังสือไม่ล้นจอ
   ถ้าต้องแก้ rotation หรือ layout → แก้ `firmware/src/lcd.h` / `ui.cpp` → build → OTA ใหม่

### 5. เปิดใช้จริง
- `bridge/install-macos.sh <ip หรือ cube.local>` (Mac resolve `cube.local` ผ่าน mDNS ได้เอง)
- ยืนยันว่าจออัปเดตภายใน ~60 วินาที และ `tail ~/Library/Logs/CubeBridge.log` ไม่มี error
- อัปเดต `README.md` ให้มีวิธีติดตั้งบน macOS แล้ว commit + push (commit message ภาษาอังกฤษ, ถามผู้ใช้ก่อน push)

## ทางหนีไฟ

| อาการ | ทำอะไร |
|---|---|
| firmware crash วน | หลัง 3 รอบเข้า safe mode อัตโนมัติ (ไม่มีจอ, มี WiFi + `/update`) → อัป build ที่แก้แล้ว |
| ต่อ WiFi ไม่ได้ | cube เปิด AP `CubeSetup` (192.168.4.1) มีเมนู WiFi + Update |
| อยากกลับ stock | อัป `docs/backup/stock-app-7.0.17.bin` ที่ `/update` |
| เปิดไม่ขึ้นเลย (แฟลชพังกลางทาง) | ต้องใช้ USB-TTL 3.3V + `docs/backup/hellocubic-lite-7.0.17_backup_full.bin` (ดู `docs/flashing.md`) |
| `NO DATA` บนจอ | token หมดอายุ → เปิด Claude Code / Codex บน Mac หนึ่งครั้ง; ดู raw response ล่าสุดใน debug folder |

## อ้างอิง
- Pinout / LCD init / factory backup: https://github.com/Times-Z/GeekMagic-Open-Firmware
- ตัวอย่างแนวคิดเดียวกัน: https://github.com/giovi321/clawdmeter-daemon , https://github.com/bairachnyi/geektv
