#include "lcd.h"

#include <SPI.h>

// CS is tied to GND on this board, so no CS pin; SPI_MODE3 is required for the panel to latch.
static Arduino_HWSPI bus(LCD_DC, GFX_NOT_DEFINED, &SPI, true);
static Arduino_ST7789 panel(&bus, GFX_NOT_DEFINED, 0, true, LCD_SIZE, LCD_SIZE);

// Vendor init sequence for the HelloCubic-Lite panel: {command, data length, data...}, 0xFF terminates.
static const uint8_t INIT[] PROGMEM = {
    0xB2, 5, 0x1F, 0x1F, 0x00, 0x33, 0x33,  // porch
    0x35, 1, 0x00,                          // tearing effect off
    0x36, 1, 0x00,                          // MADCTL
    0x3A, 1, 0x05,                          // RGB565
    0xB7, 1, 0x00,
    0xBB, 1, 0x36,
    0xC0, 1, 0x2C,
    0xC2, 1, 0x01,
    0xC3, 1, 0x13,
    0xC4, 1, 0x20,
    0xC6, 1, 0x13,
    0xD6, 1, 0xA1,
    0xD0, 2, 0xA4, 0xA1,
    0xD6, 1, 0xA1,
    0xE0, 14, 0xF0, 0x08, 0x0E, 0x09, 0x08, 0x04, 0x2F, 0x33, 0x45, 0x36, 0x13, 0x12, 0x2A, 0x2D,
    0xE1, 14, 0xF0, 0x0E, 0x12, 0x0C, 0x0A, 0x15, 0x2E, 0x32, 0x44, 0x39, 0x17, 0x18, 0x2B, 0x2F,
    0xE4, 3, 0x1D, 0x00, 0x00,
    0x21, 0,                                // inversion on (IPS)
    0x29, 0,                                // display on
    0xFF,
};

namespace lcd {

void backlight(bool on) {
    pinMode(LCD_BACKLIGHT, OUTPUT);
    digitalWrite(LCD_BACKLIGHT, on ? LOW : HIGH);
}

void begin() {
    bus.begin(40000000, SPI_MODE3);

    pinMode(LCD_RST, OUTPUT);
    digitalWrite(LCD_RST, HIGH);
    delay(120);
    digitalWrite(LCD_RST, LOW);
    delay(120);
    digitalWrite(LCD_RST, HIGH);
    delay(120);

    bus.beginWrite();
    bus.writeCommand(0x11);  // sleep out
    delay(120);
    for (const uint8_t* p = INIT; pgm_read_byte(p) != 0xFF;) {
        bus.writeCommand(pgm_read_byte(p++));
        for (uint8_t n = pgm_read_byte(p++); n > 0; n--) bus.write(pgm_read_byte(p++));
    }
    bus.endWrite();
    delay(10);

    panel.setRotation(LCD_ROTATION);
    panel.fillScreen(RGB565_BLACK);
    backlight(true);
}

Arduino_GFX& gfx() { return panel; }

}  // namespace lcd
