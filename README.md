# Wacom CTL-472 Ultra-Low Latency osu! Driver ⚡🎮

A high-performance, ultra-low latency C# Windows Forms application designed specifically for the **Wacom One (CTL-472)** digitizer tablet, optimized for playing ***osu!*** with zero input lag, 1:1 hardware mapping, interactive active area selection, and raw 200Hz / interpolated 1000Hz performance modes.

---

## ⚡ Highlights & Key Features

- **🚀 Ultra-Low Latency Direct HID Read**: Reads raw 10-byte digitizer packets straight from Win32 kernel handles without heavy framework overhead.
- **🔓 Hardware Vendor Feature Unlock**: Wakes up Wacom CTL-472 firmware from default Mouse Emulation mode to high-speed Raw Digitizer Mode using `HidD_SetFeature` (`0x02` & `0x03`).
- **🔥 Pure 200 Hz Raw Mode (5.0 ms)**: Locked 200 Hz hardware pass-through mode with 0ms buffering for raw, unfiltered input response.
- **⚡ 1000 Hz Sub-Frame Ultra-Smooth Engine**: High-resolution 1ms microsecond spin-wait loop for buttery-smooth cursor tracking on 144Hz / 240Hz / 360Hz+ monitors.
- **🎯 1:1 Absolute Mode**: Direct hardware coordinate mapping using Win32 `SendInput` (`MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK`).
- **🎨 Interactive Active Area Canvas**: Click and drag to resize or move your tablet active area visually with live millimeter display.
- **🔄 Orientation & Handedness Controls**: Custom 2D Rotation Angle ($0^\circ - 360^\circ$ for tilted/angled tablet setups), 180° Quick Rotate, and Left-Handed Mode (Horizontal flip).
- **🖐️ Physical USB Mouse Release**: Automatically releases OS cursor hold when pen is out of range, allowing seamless physical mouse usage.
- **🎯 Aim & Latency Test Arena**: Built-in interactive Aim Arena to test positioning accuracy and latency directly within the app.

---

## 🛠️ Performance Modes Comparison

| Mode | Target Rate | Latency | Recommended For |
| :--- | :--- | :--- | :--- |
| **🔥 Force Constant 200 Hz Raw Mode** | **200 Hz (5.0 ms)** | **0ms (Pure Raw)** | Competitive *osu!* players wanting unfiltered hardware response. |
| **⚡ 1000 Hz Sub-Frame Engine** | **1000 Hz (1.0 ms)** | **Sub-millisecond** | High refresh rate monitors (144Hz/240Hz/360Hz+) for ultra-smooth aim. |

---

## 💻 Building from Source

This project is built using native Windows C# compiler (`csc.exe`) and requires no heavy IDE or third-party dependencies.

### Command Line Build:
Double-click `build.bat` or run:

```cmd
build.bat
```

Or manually compile using the native .NET C# compiler:

```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:CTL472_OsuDriver.exe /opt+ Program.cs MainForm.cs DriverCore.cs ConfigManager.cs
```

---

## 📖 Deep Technical Architecture

For detailed information regarding HID report protocols, `HidD_SetFeature` unlock commands, Win32 P/Invoke pipelines, and coordinate math, please read [TECHNICAL_DETAILS.md](TECHNICAL_DETAILS.md).

---
---

# 🇹🇭 ภาษาไทย (Thai Documentation)

# Wacom CTL-472 Ultra-Low Latency osu! Driver ⚡🎮

โปรแกรมไดรเวอร์สำหรับเมาส์ปากกา **Wacom One (CTL-472)** โดยเฉพาะ พัฒนาขึ้นด้วยภาษา C# และ Win32 API ออกแบบมาเพื่อการเล่นเกม ***osu!*** โดยเน้นความเร็วระดับไร้ความหน่วง (Ultra-Low Latency), การแมปพิกัดแบบ Absolute Mode 1:1, การเลือกพื้นที่ใช้งาน (Active Area) บนหน้าจอแบบ Interactive, และโหมดความถี่ **200Hz พลังดิบ** / **1000Hz ความละเอียดสูง**.

---

## ⚡ คุณสมบัติเด่นของโปรแกรม

- **🚀 Ultra-Low Latency Direct HID Read**: ดึงพิกัดดิบจากสาย USB ตรงผ่าน Win32 Kernel ReadFile โดยไม่ผ่านแรปเปอร์หนัก ๆ
- **🔓 ปลุกชิปฮาร์ดแวร์ Wacom (Feature Unlock)**: ยิงรหัส `HidD_SetFeature` (`0x02` & `0x03`) ปลุกเมาส์ปากกา CTL-472 จากโหมดเมาส์ปกติเข้าสู่ Raw Digitizer Mode สปีดสูงสุด
- **🔥 Force Constant 200 Hz Raw Mode**: โหมดพลังดิบจากฮาร์ดแวร์ 200Hz (5.0ms) นิ่งสนิท 0ms Buffering
- **⚡ 1000 Hz Sub-Frame Ultra-Smooth Engine**: ลูปสร้างพิกัดย่อยความแม่นยำสูง 1ms ดัน Polling Rate ขึ้น 1000Hz ลื่นไหลเนียนตาบนจอ 144Hz / 240Hz / 360Hz+
- **🎯 1:1 Absolute Mode**: ยิงพิกัดตรงผ่าน `SendInput` (`MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK`)
- **🎨 Interactive Active Area Canvas**: ใช้เมาส์คลิกลากเพื่อย้ายหรือย่อ/ขยายขอบ Active Area ได้ทันทีพร้อมแสดงมิลลิเมตร
- **🔄 Rotate 180° & Left-Handed Mode**: สลับกลับหัวแท็บเล็ต 180 องศา และโหมดกลับด้านสำหรับคนถนัดมือซ้าย
- **🖐️ Physical USB Mouse Release**: คายการจับเคอร์เซอร์ทันทีเมื่อยกปากกาออกจากแผ่น ทำให้เมาส์ปกติใช้งานได้อิสระ
- **🎯 Aim & Latency Test Arena**: มีหน้าต่างทดสอบเล็ง Aim Arena ในตัวเพื่อวัดความแม่นยำและความหน่วง

---

## 📖 เอกสารวิศวกรรมระดับลึก

รายละเอียดเชิงเทคนิค รหัสปลุกฮาร์ดแวร์ Wacom การคำนวณเมทริกซ์พิกัด และโครงสร้าง Win32 API ทั้งหมด สามารถอ่านเพิ่มเติมได้ที่ไฟล์ [TECHNICAL_DETAILS.md](TECHNICAL_DETAILS.md).

---

### License
Open-source under the MIT License. Developed for the *osu!* community.
