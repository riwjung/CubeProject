#pragma once

#include <Arduino.h>

// One provider as pushed by CubeBridge: {"ok":true,"s":21,"sr":142,"w":7,"wr":9876} or {"ok":false,"e":"..."}
struct ProviderUsage {
    bool present = false;   // provider key was in the last payload
    bool ok = false;
    int session = -1;       // % used, 5-hour window (-1 unknown)
    int sessionResetMin = -1;
    int weekly = -1;        // % used, 7-day window
    int weeklyResetMin = -1;
    char error[25] = "";
};

struct UsageState {
    ProviderUsage claude;   // payload key "c"
    ProviderUsage codex;    // payload key "x"
    uint32_t receivedAtMs = 0;
    bool everReceived = false;
};

// Minutes elapsed since the payload arrived, used to count reset timers down locally.
inline int minutesSince(uint32_t ms) { return (int)((millis() - ms) / 60000UL); }
