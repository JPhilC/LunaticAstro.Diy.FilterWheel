# DIY Filter Wheel – Serial Command Reference
**Protocol Version 1.0**  
Compatible with Xagyl-style ASCOM drivers

This document describes the serial command protocol implemented by the DIY Filter Wheel firmware.  
The command set is intentionally compatible with the legacy Xagyl filter wheel protocol so that existing Xagyl ASCOM drivers can communicate with the device.

Commands fall into two categories:

- **Supported Commands** — These control real hardware behaviour.  
- **Spoofed Commands** — These return fixed or simulated values for Xagyl driver compatibility.  
  *(These may be removed or replaced when the custom ASCOM driver is complete.)*

---

# 1. Movement Commands

## **G1 .. G8 — Go To Filter Position**
**Syntax:** `Gn`  
Moves the wheel to filter position *n*.  
Filter **1** triggers a full homing cycle.

**Response:**
```text
P<n>
```

---

## **G0 — Save Configuration**
Saves all configuration settings (offsets, speed, etc.) to EEPROM.

**Response:**
```text
EEPROM Saved
```

---

# 2. Offset Commands

## **O1 .. O8 — Query Offset for Filter**
**Syntax:** `On`  
Returns the stored offset for filter *n*.

**Response:**
```text
P<n> Offset X
```

---

## **) — Increment Offset**
Increases the offset for the current filter by the configured resolution.  
Triggers a re-home and re-position.

**Response:**
```text
P<n> Offset X
```

---

## **( — Decrement Offset**
Decreases the offset for the current filter.  
Triggers a re-home and re-position.

**Response:**
```text
P<n> Offset X
```

---

## **Fxxxx — Set Absolute Offset**
**Syntax:** `F<number>`  
Sets the offset for the current filter to an explicit value.  
Triggers a re-home and re-position.

**Response:**
```text
P<n> Offset X
```

---

# 3. Information Commands (Supported)

These map directly to `handleInfo(char c1)`.

## **I0 — Product Name**
**Response:**
```text
Nano Filter Wheel
```

---

## **I1 — Firmware Version**
**Response:**
```text
FW1.0.0
```

---

## **I2 — Current Filter Position**
**Response:**
```text
P<n>
```

---

## **I3 — Serial Number**
**Response:**
```text
DIY
```

---

## **I4 — Maximum Speed**
**Response:**
```text
MaxSpeed <maxSpeed>
```

---

## **I5 — Unused**
**Response:**
```text
<Unused>
```

---

## **I6 — Current Filter Offset**
**Response:**
```text
P<n> Offset X
```

---

## **I7 — Threshold**
**Response:**
```text
Threshold <analogSensorThreshold>
```

---

## **I8 — Number of Filter Slots**
**Response:**
```text
FilterSlots <numberOfFilters>
```

---

## **I9 — Unused**
**Response:**
```text
<Unused>
```

---

# 4. Sensor Test Commands

These map to `handleSensorTest(char c1)`.

## **T0 / T1 — Sensor Digital State**
**Response:**
```text
Sensors <state> <state>
```

---

## **T2 — Sensor Type**
**Response:**
```text
Digital YES
```
or
```text
Digital NO
```

---

## **T3 — Sensor Polarity**
**Response:**
```text
Active High YES
```
or
```text
Active High NO
```

---

# 5. Reset / Calibration Commands

These map to `handleReset(char c1)`.

## **R1 — Re-Initialise**
Re-initialises the wheel (calls `initialise()`).

---

## **R2 — Reset All Offsets**
Clears all offsets, saves configuration, and re-homes.

**Response:**
```text
Calibration Removed
```

---

## **R3 — Jitter Preset (Spoofed)**
**Response:**
```text
Jitter 5
```

---

## **R4 — Reset Speed to 100%**
**Response:**
```text
MaxSpeed 100%
```

---

## **R5 — Re-Detect Sensor and Re-Initialise**
**Response:**
```text
Threshold <analogSensorThreshold>
```

---

## **R6 — Delay**
Performs a 1-second delay.  
_No response._

---

# 6. Speed Commands

## **Sxxx — Set Rotation Speed**
**Syntax:** `S<number>`  
Sets speed as a percentage of maximum.

**Response:**
```text
MaxSpeed <percent>%
```

---

# 7. Spoofed Commands (Xagyl Compatibility Only)

These do **not** affect hardware.

## **{0 / }0 — Threshold Adjustment (Spoofed)**
**Response:**
```text
Threshold 0
```

---

## **[0 / ]0 — Jitter Adjustment (Spoofed)**
**Response:**
```text
Jitter 0
```

---

## **M0 / N0 — Pulse Width Adjustment (Spoofed)**
**Response:**
```text
Pulse Width 0uS
```

---

## **T0 .. T3 — Sensor Test (Spoofed)**
Returns simulated values.

---

# 8. Unknown Commands
Unknown commands are silently ignored.