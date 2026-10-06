# v0.20 Display Discovery

## Scope

This checkpoint adds read-only identification of active display paths while keeping the working v0.14.1 mirroring core unchanged.

The implementation uses Windows CCD APIs:

- `GetDisplayConfigBufferSizes`
- `QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)`
- `DisplayConfigGetDeviceInfo`

For every active path VQM records:

- source GDI name,
- monitor friendly name,
- monitor device path,
- adapter device path,
- adapter/target-derived stable identifier,
- desktop position,
- current source resolution,
- connector/output technology,
- EDID availability,
- virtual / physical / unknown classification and the reason.

## Classification policy

Classification is deliberately conservative.

Strong virtual evidence:

- `INDIRECT_VIRTUAL` output technology,
- known virtual-display markers in friendly/device/adapter names,
- root-enumerated display adapter path such as `ROOT#DISPLAY`.

Strong physical evidence:

- internal/embedded laptop-panel connector,
- a normal direct physical connector such as HDMI / DisplayPort / DVI,
- valid EDID when no stronger virtual evidence exists.

`INDIRECT_WIRED` is **not automatically treated as physical** because it can represent an indirect software path or a real USB-connected display. Without another signal it remains `Unknown`.

## Current automatic candidate rule

Virtual source candidate:

- classified as Virtual,
- active source resolution is exactly 2560 × 1440.

Physical destination candidate:

- classified as Physical,
- external rather than internal panel,
- active resolution is exactly 1920 × 1080,
- not Miracast or an indirect output.

Selection behavior:

- exactly one candidate: it may be recommended,
- zero candidates: missing,
- two or more candidates: ambiguous; do not choose automatically.

This checkpoint does not start mirroring from the new .NET shell yet.

## Multiple physical FHD monitors

Two FHD physical monitors do not automatically mean that both should mirror the same virtual display.

There are at least three distinct future modes:

1. **One virtual source → one selected physical destination**  
   Default/simple VQM behavior.

2. **One virtual source → multiple physical destinations**  
   Duplicates the same virtual desktop on several monitors. It does not create more desktop workspace.

3. **Multiple virtual QHD sources → multiple physical FHD destinations**  
   Each physical FHD monitor becomes its own high-resolution workspace. This is the likely advanced mode when a user owns two FHD monitors and wants both to behave like independent QHD displays.

The public default must not guess between these modes. When two or more FHD destination candidates exist, the current discovery layer reports all of them and leaves destination selection unresolved.

## Validation needed on the target PC

Run the v0.20 .NET shell with the current IDD driver installed and verify:

- laptop panel is Physical + internal,
- actual external FHD monitor is Physical,
- current IDD virtual monitor is Virtual,
- source resolution and desktop coordinates match Windows Display Settings,
- two external FHD monitors produce `MultipleCandidates`, not an arbitrary automatic selection.

If the installed IDD reports `INDIRECT_WIRED` with no recognizable name/device path, capture its displayed adapter/monitor fields before broadening the classifier.
