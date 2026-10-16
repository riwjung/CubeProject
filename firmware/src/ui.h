#pragma once

#include "usage.h"

namespace ui {
void splash(const char* line1, const char* line2 = nullptr, const char* line3 = nullptr);
// Full redraw of the usage dashboard; stale = no payload from the bridge for a while.
void dashboard(const UsageState& s, bool stale, const String& ip);
}  // namespace ui
