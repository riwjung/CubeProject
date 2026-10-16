# Fails the build when firmware.bin is too big to be installed over the air.
# Stock 7.0.17 occupies 0x7A000 of the 1MB app region, leaving 0x86000 for the first
# install; staying under that also guarantees CubeMeter -> CubeMeter OTA always fits.
Import("env")  # noqa: F821  (provided by PlatformIO)

import os

OTA_LIMIT = 0x86000


def check(source, target, env):
    path = target[0].get_abspath()
    size = os.path.getsize(path)
    padded = (size + 0xFFF) & ~0xFFF
    print(f"firmware.bin {size} bytes ({padded / OTA_LIMIT:.0%} of OTA budget {OTA_LIMIT})")
    if padded > OTA_LIMIT:
        env.Exit(f"firmware.bin too large for OTA install ({padded} > {OTA_LIMIT})")


env.AddPostAction("$BUILD_DIR/${PROGNAME}.bin", check)  # noqa: F821
