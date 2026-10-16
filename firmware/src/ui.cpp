#include "ui.h"

#include "lcd.h"

// Layout (240x240): two 112px sections (Claude, Codex) and an 8px footer.
//   y+0   NAME                     status
//   y+26  5h  [==========      ]   24%
//   y+44      reset 3h17m
//   y+64  wk  [===             ]    8%
//   y+82      reset 6d 3h
static constexpr int16_t SECTION_H = 112;
static constexpr int16_t BAR_X = 34, BAR_W = 150, BAR_H = 14;

static const uint16_t BG = RGB565_BLACK;
static const uint16_t FG = RGB565_WHITE;
static const uint16_t DIM = rgb565(110, 110, 110);
static const uint16_t TRACK = rgb565(40, 40, 40);
static const uint16_t CLAUDE = rgb565(217, 119, 87);  // Claude orange
static const uint16_t CODEX = rgb565(120, 200, 255);
static const uint16_t OK_GREEN = rgb565(60, 200, 90);
static const uint16_t WARN_YELLOW = rgb565(240, 200, 40);
static const uint16_t HOT_RED = rgb565(240, 60, 50);

static uint16_t levelColor(int pct) { return pct >= 85 ? HOT_RED : pct >= 60 ? WARN_YELLOW : OK_GREEN; }

static void formatReset(char* out, size_t n, int minutes) {
    if (minutes < 0) snprintf(out, n, "reset ?");
    else if (minutes >= 1440) snprintf(out, n, "reset %dd %dh", minutes / 1440, (minutes % 1440) / 60);
    else if (minutes >= 60) snprintf(out, n, "reset %dh%02dm", minutes / 60, minutes % 60);
    else snprintf(out, n, "reset %dm", minutes);
}

static void drawBar(int16_t y, const char* label, int pct, int resetMin, bool stale) {
    auto& g = lcd::gfx();
    g.setTextSize(2);
    g.setTextColor(DIM, BG);
    g.setCursor(4, y);
    g.print(label);

    int16_t fill = pct < 0 ? 0 : (int16_t)((long)BAR_W * pct / 100);
    uint16_t color = stale ? DIM : levelColor(pct);
    g.fillRect(BAR_X, y, fill, BAR_H, color);
    g.fillRect(BAR_X + fill, y, BAR_W - fill, BAR_H, TRACK);

    char buf[20];
    if (pct < 0) snprintf(buf, sizeof buf, "  --");
    else snprintf(buf, sizeof buf, "%3d%%", pct);
    g.setTextColor(stale ? DIM : FG, BG);
    g.setCursor(BAR_X + BAR_W + 4, y);
    g.print(buf);

    formatReset(buf, sizeof buf, resetMin);
    g.setTextSize(1);
    g.setTextColor(DIM, BG);
    g.setCursor(BAR_X, y + 19);
    g.printf("%-18s", buf);
}

static void drawSection(int16_t y, const char* name, uint16_t accent, const ProviderUsage& p, int ageMin, bool stale) {
    auto& g = lcd::gfx();
    g.fillRect(0, y, LCD_SIZE, SECTION_H, BG);
    g.setTextSize(3);
    g.setTextColor(accent, BG);
    g.setCursor(4, y + 2);
    g.print(name);

    if (!p.present) {
        g.setTextSize(1);
        g.setTextColor(DIM, BG);
        g.setCursor(4, y + 40);
        g.print("disabled in bridge");
        return;
    }
    if (!p.ok) {
        g.setTextSize(2);
        g.setTextColor(HOT_RED, BG);
        g.setCursor(4, y + 34);
        g.print("NO DATA");
        g.setTextSize(1);
        g.setCursor(4, y + 58);
        g.print(p.error);
        return;
    }

    if (stale) {
        g.setTextSize(1);
        g.setTextColor(WARN_YELLOW, BG);
        g.setCursor(190, y + 10);
        g.print("STALE");
    }
    // Countdowns tick down locally between bridge updates.
    auto left = [&](int m) { return m < 0 ? -1 : max(0, m - ageMin); };
    drawBar(y + 32, "5h", p.session, left(p.sessionResetMin), stale);
    drawBar(y + 70, "wk", p.weekly, left(p.weeklyResetMin), stale);
}

namespace ui {

void splash(const char* line1, const char* line2, const char* line3) {
    auto& g = lcd::gfx();
    g.fillScreen(BG);
    g.setTextSize(3);
    g.setTextColor(CLAUDE, BG);
    g.setCursor(12, 40);
    g.print("CubeMeter");
    g.setTextSize(1);
    g.setTextColor(DIM, BG);
    g.setCursor(12, 70);
    g.print("v" FW_VERSION);

    const char* lines[] = {line1, line2, line3};
    int16_t y = 110;
    g.setTextSize(2);
    for (const char* l : lines) {
        if (!l) continue;
        g.setTextColor(FG, BG);
        g.setCursor(12, y);
        g.print(l);
        y += 28;
    }
}

void dashboard(const UsageState& s, bool stale, const String& ip) {
    auto& g = lcd::gfx();
    if (!s.everReceived) {
        splash("Waiting for", "CubeBridge...", ip.c_str());
        return;
    }
    int age = minutesSince(s.receivedAtMs);
    drawSection(0, "Claude", CLAUDE, s.claude, age, stale);
    drawSection(SECTION_H + 4, "Codex", CODEX, s.codex, age, stale);

    g.fillRect(0, 230, LCD_SIZE, 10, BG);
    g.setTextSize(1);
    g.setTextColor(DIM, BG);
    g.setCursor(4, 231);
    g.print(ip);
}

}  // namespace ui
