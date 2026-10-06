/*
 * Stand-ins for libva.so.2 and libva-drm.so.2, built by build-desktop.sh into out/linux-x64/fallback.
 *
 * ELF has no optional dependencies: the bundled FFmpeg libraries need libva because they were built with VA-API, so
 * without libva on the system they would not load at all. The app loads these stubs only when the system has no
 * libva; FFmpeg then binds to them, VA-API device creation fails (vaGetDisplayDRM returns no display) and decode and
 * encode use software.
 *
 * Every entry point FFmpeg imports is here (build-desktop.sh's payload check compares the lists). The parameters are
 * left out: on x86-64 a caller's extra arguments are harmless, and only the return value matters.
 *
 * LIBVA_STUB_CORE builds libva.so.2, LIBVA_STUB_DRM builds libva-drm.so.2.
 */

#include <stddef.h>

#define EXPORT __attribute__((visibility("default")))

/* VA_STATUS_ERROR_UNIMPLEMENTED */
#define UNIMPLEMENTED 0x00000014

#define STATUS(name) EXPORT int name(void) { return UNIMPLEMENTED; }
#define COUNT(name) EXPORT int name(void) { return 0; }
#define POINTER(name) EXPORT void *name(void) { return NULL; }
#define TEXT(name, text) EXPORT const char *name(void) { return text; }

#ifdef LIBVA_STUB_CORE
STATUS(vaInitialize)
STATUS(vaTerminate)
STATUS(vaSetDriverName)
STATUS(vaCreateConfig)
STATUS(vaDestroyConfig)
STATUS(vaGetConfigAttributes)
STATUS(vaQueryConfigProfiles)
STATUS(vaQueryConfigEntrypoints)
STATUS(vaCreateContext)
STATUS(vaDestroyContext)
STATUS(vaCreateSurfaces)
STATUS(vaDestroySurfaces)
STATUS(vaQuerySurfaceAttributes)
STATUS(vaSyncSurface)
STATUS(vaSyncBuffer)
STATUS(vaExportSurfaceHandle)
STATUS(vaCreateBuffer)
STATUS(vaDestroyBuffer)
STATUS(vaMapBuffer)
STATUS(vaUnmapBuffer)
STATUS(vaAcquireBufferHandle)
STATUS(vaReleaseBufferHandle)
STATUS(vaBeginPicture)
STATUS(vaRenderPicture)
STATUS(vaEndPicture)
STATUS(vaCreateImage)
STATUS(vaDeriveImage)
STATUS(vaDestroyImage)
STATUS(vaGetImage)
STATUS(vaPutImage)
STATUS(vaQueryImageFormats)
COUNT(vaMaxNumProfiles)
COUNT(vaMaxNumEntrypoints)
COUNT(vaMaxNumImageFormats)
POINTER(vaSetErrorCallback)
POINTER(vaSetInfoCallback)
TEXT(vaErrorStr, "VA-API not available: no libva on this system")
TEXT(vaQueryVendorString, NULL)
TEXT(vaProfileStr, "<unknown profile>")
TEXT(vaEntrypointStr, "<unknown entrypoint>")
#endif

#ifdef LIBVA_STUB_DRM
POINTER(vaGetDisplayDRM)
#endif
