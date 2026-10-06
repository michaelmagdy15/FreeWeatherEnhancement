# SkyWeave v0.7.0 — Beta Tester Quick-Start Guide ✈️

Welcome to the **SkyWeave Beta Test**! 
SkyWeave is a free, MIT-licensed real-weather injection engine and flight operations companion for **Microsoft Flight Simulator 2024**.

---

## 🚀 1. Installation (Under 1 Minute)

1. Download **`SkyWeave-Setup-0.7.0.exe`** from [Releases](https://github.com/michaelmagdy15/FreeWeatherEnhancement/releases).
2. Run the installer.
   - The installer will automatically detect your MSFS 2024 `Community` folder (both Microsoft Store / Xbox App and Steam editions) and install the in-sim bridge package (`SkyWeaveWeatherBridge`).
   - If you use a custom Community folder location, simply copy `bridge\SkyWeaveWeatherBridge` from your installation folder (`C:\Program Files\SkyWeave\bridge\SkyWeaveWeatherBridge`) into your MSFS `Community` folder.
3. Launch **SkyWeave** from your desktop or start menu shortcut.

---

## 🛩️ 2. How to Fly with SkyWeave

1. **Launch MSFS 2024** and start your flight at any airport.
2. In the MSFS weather dropdown, select **"Custom"** or select the **"SkyWeave"** preset.
3. In your MSFS top toolbar, ensure the **SkyWeave Weather Bridge** panel icon is active.
4. Launch **SkyWeave.App** on your desktop:
   - It will automatically connect to MSFS via SimConnect out-of-process.
   - SkyWeave continuously detects your aircraft's live GPS position and report station.
   - Weather will smoothly blend and inject into your simulator.

---

## 🌟 3. Key Features to Test

| Feature | Where to find it | What to check |
|---|---|---|
| **Smooth Weather Transitions** | In-flight | Notice that clouds do **not pop**, lighting does not stutter, and winds do not flip the aircraft. |
| **Hero Cockpit Weather & METAR** | `🛰️ FLIGHT DECK` tab | Compare the station KPI cards, decoded METAR, and sim readback against the real world. |
| **Radar & Online Traffic** | `🗺️ RADAR & SYNOPTIC` tab | Check RainViewer precipitation tiles, CartoDB dark basemap, and live VATSIM/IVAO aircraft symbols. |
| **Free AIP Charts & MSFS Planner** | `✈️ SIMBRIEF & DISPATCH` tab | Click `[Airmate]` or `[ChartFox]` for instant free aerodrome charts. Click `[🌐 Planner]` for MSFS 2024 Web Planner. |
| **SimBrief OFP & FMC Uplinks** | `✈️ SIMBRIEF & DISPATCH` tab | Enter your SimBrief username, fetch your OFP, and export winds aloft for PMDG / Fenix. |
| **GSX Pro Ground Ops & Deicing** | `🛰️ FLIGHT DECK` tab | When below 3°C with moisture, check the automated deicing recommendation and Holdover Time (HOT) countdown. |
| **Live AI Traffic Wake Turbulence** | In-flight near traffic | Fly behind large or heavy traffic (e.g. at major hubs) to test wake vortex encounters. |
| **Vertical Skew-T Sounding** | `📈 VERTICAL SOUNDING` tab | Review temperature lapse rate curves, freezing level, and cloud deck altitudes up to FL450. |
| **Sandbox Weather Studio** | `🛠️ WEATHER STUDIO` tab | Test the 6 extreme approach presets (Cat III Fog, 35G50kt Crosswind, Supercell Storm, etc.). |
| **Mobile/Tablet Web EFB** | Phone / Tablet Browser | Open `http://<your-pc-ip>:54170` on your iPad, phone, or second monitor for the companion EFB. |

---

## 📝 4. What Feedback We Need

Please file issues or feedback on GitHub:
👉 **https://github.com/michaelmagdy15/FreeWeatherEnhancement/issues**

We are particularly interested in:
1. **Visual Smoothness:** Any abrupt weather pops, flashing lighting, or frame drops when crossing weather boundaries.
2. **Autopilot Stability:** Any severe wind swings during climb, cruise, or approach.
3. **SimBrief & Charts:** Ease of use with Airmate / ChartFox free chart links and OFP imports.
4. **Hardware Performance:** Memory and CPU footprint (SkyWeave should remain <1% CPU and <250 MB RAM).

---

Thank you for helping make SkyWeave the best free weather engine for Microsoft Flight Simulator 2024!
