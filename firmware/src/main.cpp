// CubeMeter: shows Claude / Codex usage limits pushed by CubeBridge on a HelloCubic-Lite.
//
// HTTP API (port 80, cube.local):
//   POST /api/usage   {"c":{"ok":true,"s":21,"sr":142,"w":7,"wr":9876},"x":{...},"ts":...}
//   GET  /api/status  firmware / wifi / heap info
//   GET  /update      firmware upload page (always available, also in safe mode)

#include <Arduino.h>
#include <ArduinoJson.h>
#include <ArduinoOTA.h>
#include <ESP8266HTTPUpdateServer.h>
#include <ESP8266WebServer.h>
#include <ESP8266mDNS.h>
#include <WiFiManager.h>

#include "lcd.h"
#include "ui.h"
#include "usage.h"

static constexpr const char* HOSTNAME = "cube";
static constexpr const char* SETUP_AP = "CubeSetup";
static constexpr uint32_t STALE_AFTER_MS = 5UL * 60 * 1000;
static constexpr uint32_t REDRAW_EVERY_MS = 60UL * 1000;
static constexpr uint32_t BOOT_STABLE_MS = 60UL * 1000;
static constexpr uint32_t MAX_CRASH_BOOTS = 3;

static ESP8266WebServer server(80);
static ESP8266HTTPUpdateServer updater;
static UsageState state;
static bool safeMode = false;
static bool dirty = true;
static bool wasStale = false;
static uint32_t lastDrawMs = 0;

// ---- boot-loop guard ------------------------------------------------------
// Crash resets are counted in RTC memory (survives soft resets, not power loss).
// After MAX_CRASH_BOOTS in a row the display is skipped and only WiFi + /update run,
// so a bad build can always be replaced over the air.
struct BootGuard {
    uint32_t magic;
    uint32_t crashes;
};
static constexpr uint32_t BOOT_MAGIC = 0xC0BE0001;
static bool bootCleared = false;

static void checkBootLoop() {
    BootGuard g{};
    ESP.rtcUserMemoryRead(0, reinterpret_cast<uint32_t*>(&g), sizeof g);
    if (g.magic != BOOT_MAGIC) g = {BOOT_MAGIC, 0};

    uint32_t reason = ESP.getResetInfoPtr()->reason;
    bool crashed = reason == REASON_EXCEPTION_RST || reason == REASON_WDT_RST || reason == REASON_SOFT_WDT_RST;
    g.crashes = crashed ? g.crashes + 1 : 0;
    safeMode = g.crashes >= MAX_CRASH_BOOTS;
    ESP.rtcUserMemoryWrite(0, reinterpret_cast<uint32_t*>(&g), sizeof g);

    Serial.printf("reset reason %u, crash boots %u%s\n", reason, g.crashes, safeMode ? " -> SAFE MODE" : "");
}

static void clearBootGuard() {
    BootGuard g{BOOT_MAGIC, 0};
    ESP.rtcUserMemoryWrite(0, reinterpret_cast<uint32_t*>(&g), sizeof g);
    bootCleared = true;
}

// ---- HTTP handlers --------------------------------------------------------
static void readProvider(JsonVariantConst src, ProviderUsage& dst) {
    dst = ProviderUsage{};
    if (src.isNull()) return;
    dst.present = true;
    dst.ok = src["ok"] | false;
    dst.session = src["s"] | -1;
    dst.sessionResetMin = src["sr"] | -1;
    dst.weekly = src["w"] | -1;
    dst.weeklyResetMin = src["wr"] | -1;
    strlcpy(dst.error, src["e"] | "", sizeof dst.error);
}

static void handleUsage() {
    JsonDocument doc;
    DeserializationError err = deserializeJson(doc, server.arg("plain"));
    if (err) {
        server.send(400, "application/json", "{\"ok\":false,\"error\":\"bad json\"}");
        return;
    }
    readProvider(doc["c"], state.claude);
    readProvider(doc["x"], state.codex);
    state.receivedAtMs = millis();
    state.everReceived = true;
    dirty = true;
    server.send(200, "application/json", "{\"ok\":true}");
}

static void handleStatus() {
    JsonDocument doc;
    doc["version"] = FW_VERSION;
    doc["safeMode"] = safeMode;
    doc["ip"] = WiFi.localIP().toString();
    doc["rssi"] = WiFi.RSSI();
    doc["heap"] = ESP.getFreeHeap();
    doc["uptimeS"] = millis() / 1000;
    doc["lastUpdateAgoS"] = state.everReceived ? (long)((millis() - state.receivedAtMs) / 1000) : -1;
    String out;
    serializeJson(doc, out);
    server.send(200, "application/json", out);
}

static void handleRoot() {
    String html = F("<!doctype html><meta name=viewport content='width=device-width'><title>CubeMeter</title>"
                    "<body style='font-family:sans-serif;max-width:480px;margin:2em auto'>"
                    "<h2>CubeMeter " FW_VERSION "</h2>");
    if (safeMode) html += F("<p style='color:#c00'><b>SAFE MODE</b>: firmware crashed repeatedly, upload a fixed build.</p>");
    html += F("<p><a href='/update'>Firmware update</a> &middot; <a href='/api/status'>Status JSON</a></p></body>");
    server.send(200, "text/html", html);
}

// ---- setup / loop ---------------------------------------------------------
void setup() {
    Serial.begin(115200);
    Serial.println("\nCubeMeter " FW_VERSION);
    checkBootLoop();

    if (!safeMode) {
        lcd::begin();
        ui::splash("Connecting", "WiFi...");
    }

    WiFi.hostname(HOSTNAME);
    WiFiManager wm;
    wm.setHostname(HOSTNAME);
    std::vector<const char*> menu = {"wifi", "info", "update", "restart"};
    wm.setMenu(menu);  // the setup portal has its own firmware upload too
    wm.setConfigPortalTimeout(300);
    wm.setAPCallback([](WiFiManager*) {
        if (!safeMode) ui::splash("Join WiFi:", SETUP_AP, "192.168.4.1");
    });
    if (!wm.autoConnect(SETUP_AP)) ESP.restart();  // portal timed out: retry from scratch

    String ip = WiFi.localIP().toString();
    Serial.printf("WiFi OK, ip %s\n", ip.c_str());

    ArduinoOTA.setHostname(HOSTNAME);
    ArduinoOTA.onStart([] {
        if (!safeMode) ui::splash("Updating...", "do not unplug");
    });
    ArduinoOTA.begin();  // also starts mDNS as cube.local
    MDNS.addService("http", "tcp", 80);

    updater.setup(&server, "/update");
    server.on("/", HTTP_GET, handleRoot);
    server.on("/api/status", HTTP_GET, handleStatus);
    server.on("/api/usage", HTTP_POST, handleUsage);
    server.begin();

    if (!safeMode) ui::splash("Waiting for", "CubeBridge...", ip.c_str());
}

void loop() {
    server.handleClient();
    ArduinoOTA.handle();
    MDNS.update();

    uint32_t now = millis();
    if (!bootCleared && now > BOOT_STABLE_MS) clearBootGuard();
    if (safeMode) return;

    bool stale = state.everReceived && now - state.receivedAtMs > STALE_AFTER_MS;
    if (stale != wasStale) {
        wasStale = stale;
        dirty = true;
    }
    if (state.everReceived && (dirty || now - lastDrawMs > REDRAW_EVERY_MS)) {
        ui::dashboard(state, stale, WiFi.localIP().toString());
        lastDrawMs = now;
        dirty = false;
    }
}
