# Wacom CTL-472 Ultra-Low Latency Driver — Technical Architecture & Deep Dive

This document details the internal engineering, hardware communication protocols, and architectural mechanics of the dedicated **Wacom CTL-472 Ultra-Low Latency osu! Driver**.

---

## Table of Contents
1. [Overview & Engineering Goals](#1-overview--engineering-goals)
2. [Wacom CTL-472 HID Hardware Protocol & Unlock Mechanism](#2-wacom-ctl-472-hid-hardware-protocol--unlock-mechanism)
   - [Default Mouse Emulation Mode vs Raw Digitizer Mode](#default-mouse-emulation-mode-vs-raw-digitizer-mode)
   - [Vendor Feature Report Unlock (`0x02` & `0x03`)](#vendor-feature-report-unlock-0x02--0x03)
3. [Low-Latency Win32 I/O Pipeline](#3-low-latency-win32-io-pipeline)
   - [Collection Discovery & `GENERIC_READ` Validation](#collection-discovery--generic_read-validation)
   - [Direct Win32 `ReadFile` & High-Priority Thread Loop](#direct-win32-readfile--high-priority-thread-loop)
4. [Coordinate Transformations & Mathematical Mapping](#4-coordinate-transformations--mathematical-mapping)
   - [Active Area Scaling & Offsets](#active-area-scaling--offsets)
   - [Orientation: Rotate 180° & Left-Handed Mode](#orientation-rotate-180--left-handed-mode)
   - [Normalized Absolute Pointer Injection (`SendInput`)](#normalized-absolute-pointer-injection-sendinput)
5. [Performance Modes: Pure 200Hz Raw vs 1000Hz Sub-Frame Engine](#5-performance-modes-pure-200hz-raw-vs-1000hz-sub-frame-engine)
   - [Pure 200Hz Raw Mode (5.0 ms Hardware Pass-Through)](#pure-200hz-raw-mode-50-ms-hardware-pass-through)
   - [1000Hz Sub-Frame Interpolation Engine (Microsecond High-Res Loop)](#1000hz-sub-frame-interpolation-engine-microsecond-high-res-loop)
6. [Physical USB Mouse Release & Handover System](#6-physical-usb-mouse-release--handover-system)
7. [System Timer Resolution & Process Scheduling](#7-system-timer-resolution--process-scheduling)

---

## 1. Overview & Engineering Goals

The primary goal of this application is to deliver **absolute minimum input latency (Sub-millisecond)** and **maximum positioning accuracy** for Wacom One (CTL-472) digitizer tablets specifically optimized for playing *osu!*.

Unlike generic tablet drivers that layer multiple abstraction wrappers (such as WPF, heavy .NET HID libraries, or intermediate mouse filters), this driver communicates directly with the Windows Kernel using native **Win32 P/Invoke APIs**, processing raw 10-byte USB HID packets immediately as they reach the USB host controller.

---

## 2. Wacom CTL-472 HID Hardware Protocol & Unlock Mechanism

### Default Mouse Emulation Mode vs Raw Digitizer Mode
When a Wacom CTL-472 tablet is connected to a Windows system without Wacom's official service active:
* The tablet firmware defaults to **Mouse Emulation Mode**.
* In this default state, the hardware digitizer interface (`COL02`) remains **completely silent (0 packets sent)**.
* Windows' built-in system driver (`Windows Ink / HID-compliant Pen`) attaches to `COL01` with exclusive access, causing standard `CreateFile` calls on `COL01` to fail with `ERROR_ACCESS_DENIED` (Error 5).

### Vendor Feature Report Unlock (`0x02` & `0x03`)
To bypass Windows Ink and wake up the raw digitizer streaming interface on `COL02`, the driver sends specific Wacom Vendor Feature Reports over the HID handle upon connection:

```csharp
private void SendWacomInitFeatureReports(SafeFileHandle handle)
{
    // Feature Report 0x02: Switches tablet firmware from Mouse Emulation -> Raw Digitizer Mode
    byte[] feat2 = new byte[] { 0x02, 0x02 };
    HidD_SetFeature(handle, feat2, (uint)feat2.Length);

    // Feature Report 0x03: Initializes high-speed packet streaming
    byte[] feat3 = new byte[] { 0x03, 0x01 };
    HidD_SetFeature(handle, feat3, (uint)feat3.Length);
}
```

Upon receiving `Feature Report 0x02`, the Wacom CTL-472 firmware immediately exits Mouse Emulation and begins streaming raw **10-byte HID digitizer packets** over `COL02`.

#### Wacom CTL-472 10-Byte Packet Structure:
| Byte Index | Field Description | Hex Sample |
| :--- | :--- | :--- |
| `data[0]` | Report ID | `0x02` |
| `data[1]` | Proximity & Status Bits (`0xC0` = In Proximity) | `0xC0` |
| `data[2]..data[3]` | Raw X Coordinate (Little-Endian, range `0..15200`) | `6B 2F` -> `12139` |
| `data[4]..data[5]` | Raw Y Coordinate (Little-Endian, range `0..9500`) | `30 13` -> `4912` |
| `data[6]..data[7]` | Reserved / Secondary Flags | `00 00` |
| `data[8]..data[9]` | Tip Pressure (Little-Endian, range `0..2047`) | `0D 00` -> `13` |

---

## 3. Low-Latency Win32 I/O Pipeline

### Collection Discovery & `GENERIC_READ` Validation
The driver enumerates all HID device interfaces matching Wacom's Vendor ID (`0x056A`) and Product ID (`0x037A`) using `setupapi.dll`. To avoid locked interfaces (`COL01`), it attempts a non-blocking `CreateFile` with `GENERIC_READ` rights prior to selecting the active path:

```csharp
private bool CanOpenRead(string path)
{
    SafeFileHandle testHandle = CreateFile(
        path,
        GENERIC_READ,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        IntPtr.Zero,
        OPEN_EXISTING,
        0,
        IntPtr.Zero);

    if (!testHandle.IsInvalid)
    {
        testHandle.Close();
        return true;
    }
    return false;
}
```

### Direct Win32 `ReadFile` & High-Priority Thread Loop
Once `COL02` is opened, a dedicated background thread (`_driverThread`) running at `ThreadPriority.Highest` enters a synchronous `ReadFile` loop:

```csharp
while (_isRunning && IsTabletConnected)
{
    uint bytesRead = 0;
    bool readSuccess = ReadFile(handle, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero);
    if (readSuccess && bytesRead > 0)
    {
        ProcessHidPacket(buffer, (int)bytesRead);
    }
}
```

---

## 4. Coordinate Transformations & Mathematical Mapping

### Active Area Scaling & Offsets
Raw physical coordinates ($X_{raw} \in [0, 15200]$, $Y_{raw} \in [0, 9500]$) are converted to millimeters ($152.0 \text{ mm} \times 95.0 \text{ mm}$):

$$X_{mm} = X_{raw} \times \left(\frac{152.0}{15200}\right)$$
$$Y_{mm} = Y_{raw} \times \left(\frac{95.0}{9500}\right)$$

Active area offsets and bounds are normalized to range $[0.0, 1.0]$:

$$\text{Norm}_X = \text{Clamp}\left(\frac{X_{trans} - \text{Offset}_X}{\text{Area}_W}, 0.0, 1.0\right)$$
$$\text{Norm}_Y = \text{Clamp}\left(\frac{Y_{trans} - \text{Offset}_Y}{\text{Area}_H}, 0.0, 1.0\right)$$

### Orientation: Rotate 180° & Left-Handed Mode
* **Rotate 180°**:
  $$X_{trans} = 152.0 - X_{mm}$$
  $$Y_{trans} = 95.0 - Y_{mm}$$
* **Left-Handed Mode (Horizontal Flip)**:
  $$X_{trans} = 152.0 - X_{mm}$$

### Normalized Absolute Pointer Injection (`SendInput`)
In Absolute Mode, normalized coordinates $[0.0, 1.0]$ are mapped to Windows virtual desktop coordinates ($0..65535$) and injected using Win32 `SendInput`:

```csharp
INPUT[] inputs = new INPUT[1];
inputs[0].type = INPUT_MOUSE;
inputs[0].mi.dx = (int)Math.Round(normX * 65535.0);
inputs[0].mi.dy = (int)Math.Round(normY * 65535.0);
inputs[0].mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_MOVE;

SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
SetCursorPos(targetScreenX, targetScreenY);
```

---

## 5. Performance Modes: Pure 200Hz Raw vs 1000Hz Sub-Frame Engine

### Pure 200Hz Raw Mode (5.0 ms Hardware Pass-Through)
* **Hardware Native Polling**: Wacom CTL-472 EMR digitizer hardware samples at **133 Hz** when hovering and **200 Hz (5.0 ms)** when pressed/dragging.
* **Force 200Hz Engine**: When `Force200Hz` is enabled, a high-resolution 5.0ms timer spin-wait loop guarantees a rock-solid **200 Hz (5.0 ms)** output stream even when hovering, bypassing all software smoothing filters for zero-latency raw hardware feel.

### 1000Hz Sub-Frame Interpolation Engine (Microsecond High-Res Loop)
For high-refresh-rate monitors ($144\text{Hz}, 240\text{Hz}, 360\text{Hz}+$):
* Runs a microsecond-precision `Stopwatch` loop stepping every **1.000 ms (1000 Hz)**.
* Performs sub-millisecond linear/Hermite interpolation between hardware packets to eliminate visual micro-stuttering during fast aim jumps in *osu!*.

```csharp
Stopwatch sw = Stopwatch.StartNew();
long ticksPerMs = Stopwatch.Frequency / 1000;
long nextTick = sw.ElapsedTicks;

while (_isRunning)
{
    nextTick += ticksPerMs;
    // Perform sub-frame interpolation and SendInput injection...
    while (sw.ElapsedTicks < nextTick)
    {
        Thread.SpinWait(10);
    }
}
```

---

## 6. Physical USB Mouse Release & Handover System

A critical issue in background tablet drivers is pointer locking: if the driver continuously calls `SetCursorPos` or `SendInput` when the pen is lifted, the physical USB mouse becomes trapped at the pen's last position.

To resolve this, the driver monitors proximity status on every incoming packet:

```csharp
bool isZeroCoord = (rawX_us == 0 && rawY_us == 0);
_lastInProximity = inProximity && !isZeroCoord;

if (!_lastInProximity)
{
    // Pen is lifted / out of range: IMMEDIATELY halt cursor injection.
    // Physical USB mouse regains 100% free movement across Windows.
}
```

---

## 7. System Timer Resolution & Process Scheduling

Upon startup (`Program.cs` and `DriverCore.cs`), the application configures Windows system timing and process scheduling for maximum responsiveness:

1. **1ms System Timer Resolution**: Calls `timeBeginPeriod(1)` via `winmm.dll`, forcing the Windows kernel scheduler granularity down from 15.6ms to **1.0ms**.
2. **High Priority Process**: Sets `Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High`, ensuring OS input scheduling prioritizes tablet packet processing over background applications.

---

*Documentation maintained by Antigravity AI Code Team.*
