# Wacom CTL-472 Ultra-Low Latency osu! Driver v1.0.0 ⚡🎮

First official release of the dedicated, high-performance C# driver for **Wacom One (CTL-472)**, specifically engineered for *osu!* players.

---

## ⚡ What's New in v1.0.0

- **🚀 Direct Win32 Low-Latency Kernel Read**: Bypasses heavy OS wrappers for sub-millisecond hardware packet response.
- **🔓 Wacom Digitizer Feature Report Unlock**: Unlocks CTL-472 firmware from Mouse Emulation mode to high-speed Raw Digitizer Mode using `HidD_SetFeature` (`0x02` / `0x03`).
- **🔥 Pure 200 Hz Raw Mode (5.0 ms)**: Hardware pass-through mode with 0ms buffering for raw, unfiltered input response.
- **⚡ 1000 Hz Sub-Frame Ultra-Smooth Engine**: Microsecond-precision Stopwatch loop for ultra-smooth tracking on 144Hz / 240Hz / 360Hz+ monitors.
- **🎯 Absolute 1:1 Positioning**: Direct virtual desktop mapping via Win32 `SendInput` (`MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK`).
- **🎨 Interactive Active Area Canvas**: Live visual click-and-drag area resizing with millimeter display.
- **🔄 Orientation & Handedness**: 180° Rotate (Upside-down tablet) and Left-Handed Mode (Horizontal flip).
- **🖐️ Physical USB Mouse Auto-Release**: Instantly releases cursor hold when pen is out of range, allowing normal mouse usage.

---

## 📦 Download Assets
- Attach **`CTL472_OsuDriver.exe`** (Self-contained standalone executable, no installation required).

---

# 🇹🇭 รายละเอียดการปล่อยเวอร์ชัน v1.0.0 (ภาษาไทย)

โปรแกรมไดรเวอร์เมาส์ปากกา **Wacom One (CTL-472)** เวอร์ชันแรกอย่างเป็นทางการ สร้างขึ้นมาเพื่อเกมเมอร์ *osu!* โดยเฉพาะ

- **🚀 ดึงข้อมูลระดับ Win32 Kernel**: อ่านพิกัดจาก USB โดยตรงเพื่อความเร็วสูงสุดแบบ Sub-millisecond
- **🔓 ปลุกชิป Wacom 100%**: ปลุกเฟิร์มแวร์เมาส์ปากกา CTL-472 เข้าสู่ Raw Digitizer Mode สปีดสูงสุดด้วยรหัส `0x02` และ `0x03`
- **🔥 โหมดพลังดิบ 200 Hz (5.0 ms)**: รับค่าพิกัดดิบจากฮาร์ดแวร์โดยตรง 0ms Buffering
- **⚡ 1000 Hz Sub-Frame Engine**: เอนจินเพิ่มความเนียนลื่นระดับ 1ms สำหรับจอ 144Hz / 240Hz / 360Hz+
- **🎯 Absolute Mode 1:1**: แมปพิกัดตรงด้วย Win32 `SendInput`
- **🎨 Visual Canvas**: คลิกลากขยาย/ย้ายขอบ Active Area บนหน้าจอจำลองได้ทันที
- **🔄 Rotate 180° & Left-Handed Mode**: โหมดหมุนกลับหัวและโหมดกลับด้านสำหรับคนถนัดมือซ้าย
- **🖐️ Physical Mouse Release**: คายการจับเมาส์อัตโนมัติเมื่อยกปากกาออก ทำให้เมาส์ปกติเลื่อนได้อิสระ
