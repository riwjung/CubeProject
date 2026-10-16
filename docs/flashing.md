# แฟลช CubeMeter ลง HelloCubic-Lite (OTA อย่างเดียว ไม่ต้องแกะเครื่อง)

> ⚠️ ทำด้วยความเสี่ยงของตัวเอง เครื่องนี้ไม่มีทางกู้ผ่าน USB ถ้าไม่มี USB-TTL + หัวแร้ง
> ทุกขั้นด้านล่างออกแบบให้ "ย้อนกลับได้ผ่าน WiFi" เสมอ

## Flash layout (ตรวจจาก factory backup 7.0.17)

| ช่วง | stock 7.0.17 | CubeMeter |
|---|---|---|
| `0x000000–0x100000` | app (486KB) + พื้นที่ว่างสำหรับ OTA | app (~415KB) + พื้นที่ว่างสำหรับ OTA |
| `0x100000–0x3FA000` | LittleFS (web UI, GIF, config ของ stock) | **ไม่แตะ** (layout `eagle.flash.4m3m.ld` เหมือนกัน) |
| `0x3FB000–0x400000` | SDK config + WiFi ที่บันทึกไว้ | ใช้ WiFi เดิมต่อได้เลย |

ผลคือ:
- แฟลชครั้งแรกมีที่ว่างแค่ **548KB** → `scripts/check_size.py` จะ fail build ถ้า firmware ใหญ่เกิน
- ย้อนกลับ stock ได้ผ่าน `/update` ด้วย `docs/backup/stock-app-7.0.17.bin` เพราะ LittleFS ของ stock ยังอยู่ครบ

## 1. Build

```powershell
cd D:\source\repos\CubeProject
.venv\Scripts\pio run -e hellocubic -d firmware
# ได้ firmware\.pio\build\hellocubic\firmware.bin
```

## 2. หา IP ของ cube (firmware เดิม)

1. ถ้า cube ยังไม่ได้ต่อ WiFi บ้าน: ต่อ WiFi `GIFTV` → เปิด `http://192.168.4.1` → ตั้ง WiFi บ้าน
2. cube จะแสดง IP บนจอ (หรือดูจาก router) → เปิด `http://<ip>/update` ต้องเห็นหน้าอัปโหลด firmware
   - ถ้าไม่มีหน้า `/update` **หยุดตรงนี้** แล้วแจ้งกลับมา (firmware version อื่นอาจต่างกัน)

## 3. แฟลชครั้งแรก

1. เสียบ cube กับไฟที่เสถียร (อย่าใช้ USB hub ที่หลวม)
2. ที่ `http://<ip>/update` เลือก `firmware.bin` → Update → รอ ~30 วินาที **ห้ามถอดปลั๊ก**
3. cube รีบูต → จอขึ้น `CubeMeter` → `Connecting WiFi...`
   - ถ้าต่อ WiFi เดิมได้: ขึ้น `Waiting for CubeBridge...` + IP
   - ถ้าไม่ได้: ขึ้น `Join WiFi: CubeSetup` → ต่อ WiFi `CubeSetup` บนมือถือ → ตั้ง WiFi บ้าน
4. **ทดสอบ OTA รอบสองทันที** ก่อนใช้งานจริง: เปิด `http://<ip>/update` (หรือ `http://cube.local/update`)
   แล้วอัป `firmware.bin` เดิมซ้ำ → ต้องรีบูตกลับมาได้ = ยืนยันว่ามีทางอัปเดตเสมอ

## 4. ทดสอบจอโดยไม่ต้องใช้ bridge

```powershell
curl.exe -X POST http://<ip>/api/usage -H "Content-Type: application/json" `
  -d '{\"c\":{\"ok\":true,\"s\":72,\"sr\":95,\"w\":31,\"wr\":5000},\"x\":{\"ok\":true,\"s\":12,\"sr\":200,\"w\":90,\"wr\":700}}'
curl.exe http://<ip>/api/status
```

ถ้าภาพกลับด้าน/กระจก: แก้ `LCD_ROTATION` ใน `firmware/src/lcd.h` (ลอง 4–7 สำหรับแบบ mirror, 0–3 แบบปกติ) แล้ว OTA ใหม่

## ระบบกันพัง

- **Safe mode:** ถ้า firmware crash ติดกัน 3 รอบ จะไม่ init จอ เหลือแค่ WiFi + `/update` → อัป build ที่แก้แล้ว
- **Setup portal (`CubeSetup`)** มีเมนู Update ของตัวเองด้วย ใช้ได้แม้ยังไม่ได้ต่อ WiFi บ้าน
- **ArduinoOTA:** `.venv\Scripts\pio run -e hellocubic-ota -d firmware -t upload` (ต้อง resolve `cube.local` ได้ หรือแก้ `upload_port` เป็น IP)

## ย้อนกลับเป็น firmware เดิม

อัป `docs/backup/stock-app-7.0.17.bin` ที่ `http://<ip>/update` ของ CubeMeter → เครื่องกลับเป็น stock 7.0.17
พร้อม web UI/GIF เดิม (LittleFS ไม่ถูกแตะ)

`hellocubic-lite-7.0.17_backup_full.bin` คือ image เต็ม 4MB (จาก Times-Z/GeekMagic-Open-Firmware)
ใช้ได้เฉพาะกับ UART: `esptool.py --port COMx --baud 115200 write_flash 0x0 hellocubic-lite-7.0.17_backup_full.bin`
