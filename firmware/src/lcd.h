#pragma once

#include <Arduino_GFX_Library.h>

// HelloCubic-Lite wiring (reverse-engineered by Times-Z/GeekMagic-Open-Firmware)
static constexpr int8_t LCD_DC = 0;        // boot strap pin: only driven after boot
static constexpr int8_t LCD_RST = 2;       // boot strap pin: only driven after boot
static constexpr int8_t LCD_BACKLIGHT = 5; // active LOW
static constexpr int16_t LCD_SIZE = 240;
// The cube shows the panel through a prism, so the image must be mirrored (Arduino_GFX rotation 4..7).
static constexpr uint8_t LCD_ROTATION = 4;

constexpr uint16_t rgb565(uint8_t r, uint8_t g, uint8_t b) {
    return (uint16_t)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));
}

namespace lcd {
void begin();
void backlight(bool on);
Arduino_GFX& gfx();
}  // namespace lcd
