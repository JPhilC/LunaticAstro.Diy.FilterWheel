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


Pn

---

## **G0 — Save Configuration**
Saves all configuration settings (offsets, speed, etc.) to EEPROM.

**Response:**


EEPROM Saved

---

# 2. Offset Commands

## **O1 .. O8 — Query Offset for Filter**
**Syntax:** `On`  
Returns the stored offset for filter *n*.

**Response:**


Pn Offset X

---

## **) — Increment Offset**
Increases the offset for the current filter by the configured resolution.  
Triggers a re-home and re-position.

**Response:**


Pn Offset X

---

## **( — Decrement Offset**
Decreases the offset for the current filter.  
Triggers a re-home and re-position.

**Response:**


Pn Offset X

---

## **Fxxxx — Set Absolute Offset**
**Syntax:** `F<number>`  
Sets the offset for the current filter to an explicit value.  
Triggers a re-home and re-position.

**Response:**


Pn Offset X

---

# 3. Information Commands (Supported)

## **I0 — Product Name**
Returns the firmware product identifier.

Example:


Rayzwheel b.097

---

## **I1 — Firmware Version**
Example:


FW3.1.5

---

## **I2 — Current Filter Position**


Pn

---

## **I3 — Serial Number**
Returns a fixed serial number string.

---

## **I4 — Maximum Speed**
Returns the configured maximum rotation speed.

---

## **I6 — Current Filter Offset**


Pn Offset X

---

## **I8 — Number of Filter Slots**
Returns the number of available filter positions.

---

# 4. Reset / Calibration Commands

## **R1 — Re-Home Wheel**
Performs a full homing cycle.

**Response:**


P1

---

## **R2 — Reset All Offsets**
Clears all filter offsets, saves configuration, and re-homes.

**Response:**


Calibration Removed

---

## **R3, R4, R5, R6 — Spoofed**
These commands return fixed values for Xagyl compatibility.  
They do **not** affect hardware.

---

# 5. Speed Commands

## **Sxxx — Set Rotation Speed**
**Syntax:** `S<number>`  
Sets the wheel rotation speed as a percentage of maximum.

**Response:**


MaxSpeed X%

---

# 6. Spoofed Commands (Xagyl Compatibility Only)

These commands do **not** affect hardware.  
They return fixed values to satisfy the expectations of the Xagyl ASCOM driver.

## **{0 / }0 — Threshold Adjustment (Spoofed)**
Returns a fixed threshold value.

---

## **[0 / ]0 — Jitter Adjustment (Spoofed)**
Returns a fixed jitter value.

---

## **M0 / N0 — Pulse Width Adjustment (Spoofed)**
Returns a fixed pulse width.

---

## **T0 .. T3 — Sensor Test (Spoofed)**
Returns simulated sensor values.

---

# 7. Unknown Commands
Any unrecognised command is silently ignored, matching Xagyl behaviour.




