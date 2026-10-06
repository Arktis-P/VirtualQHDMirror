# v0.14 Portal Input

## Goal

Preserve the normal Windows 1 | 2 | 3 topology while making physical display 2 behave like an interactive local client for virtual display 3.

The user should not manage an ARMED/CAPTURED state during normal use.

## State

### Outside portal

The real Windows cursor is on display 1 or display 3 and behaves normally.

### Portal active

When the pointer enters the mirror window on display 2:

1. destination coordinates are mapped to display 3
2. the real Windows cursor moves to that source coordinate
3. Windows handles input natively on display 3
4. the source cursor is software-composited onto display 2
5. source bounds are constrained temporarily to keep native interactions stable

This uses the same useful principle as a remote-desktop client: presentation and native source input are separated.

## Seamless edge release

Portal mode does not require Ctrl+Alt+F11.

The edge used to enter the portal is temporarily blocked until the source cursor moves inward by a small hysteresis margin. This avoids immediate bounce.

When the cursor later reaches an unblocked source edge, VirtualQHDMirror checks whether a Windows monitor exists immediately outside the corresponding display-2 edge.

If a monitor exists:

- release the source constraint
- convert the source Y/X position back to display-2 space
- place the real cursor one pixel across the destination edge
- return to normal Windows navigation

This is intended to support:

- display 1 -> display 2
- display 2 -> display 1
- display 2 -> display 3
- display 3 -> display 2

without a hotkey.

## Native interaction coverage

While Portal is active, the actual OS cursor is on display 3. Therefore Windows itself owns:

- ordinary hover/click/double-click
- drag and resize
- taskbar grouped previews
- shell flyouts
- minimize/maximize/close
- Unity/Electron/Chromium custom chrome
- application-defined cursor changes
- wheel and XButtons

This removes most of the v0.13 special-case forwarding path from the default mode.

## MagSetInputTransform decision

The API was researched because it could theoretically keep the system cursor on display 2 while mapping input to display 3. Microsoft requires UIAccess for MagSetInputTransform, and UIAccess is a highly trusted accessibility mechanism. It is not adopted as the default v0.14 dependency.

## Fallbacks

- Captured: previous v0.14 ClipCursor + manual-release experiment
- Legacy: v0.13.1 message/native fallback
- archive/v0.13.1: frozen runnable daily-use baseline

## QA gate

- [ ] entry from display 1 does not bounce back
- [ ] entry from display 3 does not bounce back
- [ ] left-edge exit reaches display 1
- [ ] right-edge exit reaches display 3
- [ ] no Ctrl+Alt+F11 is required
- [ ] taskbar grouped previews stay open
- [ ] caption buttons work
- [ ] Unity custom caption controls work
- [ ] drag/resize works
- [ ] wheel/XButtons work
- [ ] cursor hotspot remains aligned
