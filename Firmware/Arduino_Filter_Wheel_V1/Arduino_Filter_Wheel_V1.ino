#include <AccelStepper.h>
#include <EEPROM.h>

/* ==========================================================================  
   Lunatic Astro Filter Wheel Firmware  
   Copyright © 2026 Phil Crompton, Lunatic Astro  
   All rights reserved.

   This firmware is provided for use with Lunatic Astro hardware projects.  
   Redistribution or modification is permitted only with explicit permission  
   from the copyright holder.

   For project information, updates, and documentation, visit Lunatic Astro.  
   ========================================================================== */


// =========================================================
// CONFIGURATION
// =========================================================

#define DEBUG 0  // Set to 1 for verbose debug output
#define FIRMWARE_VERSION "2.0"
// Pins
#define SENSOR A3
#define hallPower A2
#define hallCom A1
#define upButton 4
#define downButton 5
#define ledPin 10

// Motor pins (28BYJ-48 + ULN2003)
#define IN1 9
#define IN2 6
#define IN3 8
#define IN4 7

// Motor settings
#define maxSpeed 400
#define Speed 400
#define setAccel 1000
#define stepsPerRevolution 2048

// Filter wheel settings
#define MAX_FILTERS 8
#define NAME_SLOT_SIZE 31

#define offSetResolution 5
#define backlashOvershoot 200
#define SENSOR_OFFSET 70


// Optional peripherals
#define ledConnected 1

// Motor type (fixed)
#define motorType 1  // 1 = unipolar 28BYJ-48

// =========================================================
// GLOBALS
// =========================================================

#if motorType == 1
AccelStepper stepper(4, IN2, IN4, IN1, IN3);
#endif

int numberOfFilters = 5;
int filterPos[MAX_FILTERS + 1];
int16_t posOffset[MAX_FILTERS + 1];

bool Error = false;
int currPos = 0;

// Sensor characteristics (EEPROM-backed)
bool sensorIsDigital = false;
bool activeHigh = true;
int analogSensorThreshold = 650;
char filterNames[MAX_FILTERS + 1][NAME_SLOT_SIZE];
const char DEFAULT_FILTER_NAME[] = "<empty>";

// LED flash state machine
unsigned long ledLastToggle = 0;
int ledFlashCount = 0;
int ledFlashTarget = 0;
int ledInterval = 0;
bool ledState = false;
bool ledActive = false;


// =========================================================
// EEPROM HELPERS
// =========================================================

// EEPROM addresses
#define EE_THRESH 10
#define EE_DIGITAL 12
#define EE_ACTIVEHIGH 13
#define EE_OFFSET_BASE 52
#define EE_NAME_BASE (EE_OFFSET_BASE + (MAX_FILTERS * 2))  // 52 + 16 = 68


void writeInt(int addr, int value) {
  EEPROM.write(addr, value >> 8);
  EEPROM.write(addr + 1, value & 0xFF);
}

int readInt(int addr) {
  return (EEPROM.read(addr) << 8) | EEPROM.read(addr + 1);
}

void writeStringFixed(int addr, const char *str, int maxLen) {
  for (int i = 0; i < maxLen; i++) {
    char c = str[i];
    if (c == '\0') {
      EEPROM.write(addr + i, 0);
      // Fill the rest with 0
      for (int j = i + 1; j < maxLen; j++)
        EEPROM.write(addr + j, 0);
      return;
    }
    EEPROM.write(addr + i, c);
  }
}

void readStringFixed(int addr, char *buffer, int maxLen) {
  for (int i = 0; i < maxLen; i++) {
    buffer[i] = EEPROM.read(addr + i);
    if (buffer[i] == 0) {
      return;
    }
  }
  buffer[maxLen - 1] = 0;  // ensure null termination
}


void saveSensorSettings() {
  writeInt(EE_THRESH, analogSensorThreshold);
  EEPROM.write(EE_DIGITAL, sensorIsDigital ? 1 : 0);
  EEPROM.write(EE_ACTIVEHIGH, activeHigh ? 1 : 0);
  if (DEBUG) Serial.println("Sensor settings saved.");
}

// Save configuration settings
void saveConfigSettings() {

  // --- Save number of filters ---
  EEPROM.write(20, numberOfFilters);

  // --- Save offsets dynamically ---
  for (int i = 1; i <= numberOfFilters; i++) {
    int16_t raw = posOffset[i];  // must be unsigned
    EEPROM.write(EE_OFFSET_BASE + (i - 1) * 2, highByte(raw));
    EEPROM.write(EE_OFFSET_BASE + (i - 1) * 2 + 1, lowByte(raw));
  }

  // --- Save filter names ---
  for (int i = 1; i <= numberOfFilters; i++) {
    int addr = EE_NAME_BASE + (i - 1) * NAME_SLOT_SIZE;
    writeStringFixed(addr, filterNames[i], NAME_SLOT_SIZE);
  }

  if (DEBUG) Serial.println("Calibration settings saved.");
}

bool loadSensorSettings() {
  int t = readInt(EE_THRESH);
  int d = EEPROM.read(EE_DIGITAL);
  int a = EEPROM.read(EE_ACTIVEHIGH);

  // Validate
  if (t < 0 || t > 1023) return false;  // <-- widened range
  if (d != 0 && d != 1) return false;
  if (a != 0 && a != 1) return false;

  analogSensorThreshold = t;
  sensorIsDigital = d;
  activeHigh = a;

  if (DEBUG) {
    Serial.println("Loaded sensor settings from EEPROM:");
    Serial.print("Threshold=");
    Serial.println(analogSensorThreshold);
    Serial.print("Digital=");
    Serial.println(sensorIsDigital);
    Serial.print("ActiveHigh=");
    Serial.println(activeHigh);
  }

  return true;
}

void loadConfigSettings() {

  // --- Load number of filters ---
  numberOfFilters = EEPROM.read(20);

  // Validate range (3–8)
  if (numberOfFilters < 3 || numberOfFilters > 8) {
    numberOfFilters = 5;  // sensible default
  }

  // --- Initialise arrays to safe defaults ---
  for (int i = 1; i <= MAX_FILTERS; i++) {
    posOffset[i] = 0;
    filterPos[i] = 0;
  }

  // --- Compute ideal filter positions ---
  int stepsPerSlot = stepsPerRevolution / numberOfFilters;

  for (int i = 1; i <= numberOfFilters; i++) {
    filterPos[i] = (i - 1) * stepsPerSlot + SENSOR_OFFSET;
  }

  // --- Load offsets dynamically ---
  for (int i = 1; i <= numberOfFilters; i++) {
    uint8_t hi = EEPROM.read(EE_OFFSET_BASE + (i - 1) * 2);
    uint8_t lo = EEPROM.read(EE_OFFSET_BASE + (i - 1) * 2 + 1);
    int16_t val = word(hi, lo);

    // Detect uninitialised or absurd values
    if (val == 0x7FFF || val < -1000 || val > 1000) {
      val = 0;
    }


    posOffset[i] = val;
  }

  // --- Load filter names ---
  for (int i = 1; i <= numberOfFilters; i++) {
    int addr = EE_NAME_BASE + (i - 1) * NAME_SLOT_SIZE;
    readStringFixed(addr, filterNames[i], NAME_SLOT_SIZE);

    // Detect uninitialised or blank names
    if (filterNames[i][0] == 0xFF || filterNames[i][0] == 0) {
      // Set default
      strncpy(filterNames[i], DEFAULT_FILTER_NAME, NAME_SLOT_SIZE - 1);
      filterNames[i][NAME_SLOT_SIZE - 1] = 0;

      // Write back to EEPROM so it's persistent
      writeStringFixed(addr, filterNames[i], NAME_SLOT_SIZE);
    }
  }

  // --- Debug output ---
  if (DEBUG) {
    Serial.println("Loaded config settings from EEPROM:");
    Serial.print("Number of filters: ");
    Serial.println(numberOfFilters);

    for (int i = 1; i <= numberOfFilters; i++) {
      Serial.print("Offset[");
      Serial.print(i);
      Serial.print("] = ");
      Serial.println(posOffset[i]);

      Serial.print("Ideal FilterPos[");
      Serial.print(i);
      Serial.print("] = ");
      Serial.println(filterPos[i]);
    }

    for (int i = 1; i <= numberOfFilters; i++) {
      Serial.print("Name[");
      Serial.print(i);
      Serial.print("] = ");
      Serial.println(filterNames[i]);
    }
  }
}



// =========================================================
// SENSOR AUTO-DETECT
// =========================================================

void detectSensorType() {
  if (DEBUG) Serial.println("Sensor → running auto-detect…");

  int v, minVal = 1023, maxVal = 0;

  delay(50);

  // Back off to clear magnet
  stepper.runToNewPosition(stepper.currentPosition() - 1000);

  // Sweep one full revolution
  stepper.setAcceleration(setAccel);
  stepper.setMaxSpeed(maxSpeed);
  stepper.setSpeed(Speed);

  long target = stepper.currentPosition() + stepsPerRevolution;
  stepper.moveTo(target);

  while (stepper.distanceToGo() != 0) {
    stepper.run();
    v = analogRead(SENSOR);
    if (v < minVal) minVal = v;
    if (v > maxVal) maxVal = v;
  }

  if (DEBUG) Serial.println("min=" + String(minVal) + " max=" + String(maxVal));


  // Digital detection
  if (minVal < 50 || maxVal > 970) {
    sensorIsDigital = true;
    activeHigh = (maxVal > 970);
    Serial.println("Sensor is DIGITAL");
  } else {
    sensorIsDigital = false;
    activeHigh = (maxVal > minVal);

    // *** UPDATED THRESHOLD MARGIN ***
    analogSensorThreshold = activeHigh ? (maxVal - 40) : (minVal + 40);

    if (DEBUG) {
      Serial.println("Sensor is ANALOG");
      Serial.print("Polarity: active ");
      Serial.println(activeHigh ? "HIGH" : "LOW");
      Serial.println("Threshold " + String(analogSensorThreshold));
    }
  }

  // Rewind
  stepper.runToNewPosition(stepper.currentPosition() - stepsPerRevolution);
  stepper.setCurrentPosition(0);

  saveSensorSettings();
}
// =========================================================
// LED
// =========================================================
void startLedFlash(int flashes, int intervalMs) {
#if ledConnected
  ledActive = true;
  ledFlashCount = 0;
  ledFlashTarget = flashes * 2;  // ON+OFF = 2 toggles per flash
  ledInterval = intervalMs;
  ledLastToggle = millis();
  ledState = false;
  if (DEBUG) {
    Serial.println("LED LOW");
  }
  digitalWrite(ledPin, LOW);
#endif
}

void updateLed() {
#if ledConnected
  if (!ledActive) return;

  if (millis() - ledLastToggle >= ledInterval) {
    ledLastToggle = millis();
    ledState = !ledState;
    if (DEBUG) {
      Serial.print("LED: ");
      Serial.println(ledState);
    }
    digitalWrite(ledPin, ledState);

    ledFlashCount++;
    if (ledFlashCount >= ledFlashTarget) {
      ledActive = false;
      digitalWrite(ledPin, LOW);
      if (DEBUG) {
        Serial.println("LED: LOW");
      }
    }
  }
#endif
}


// =========================================================
// MOTOR + FEEDBACK HELPERS
// =========================================================

void motor_Off() {
  digitalWrite(IN1, LOW);
  digitalWrite(IN2, LOW);
  digitalWrite(IN3, LOW);
  digitalWrite(IN4, LOW);
}

// =========================================================
// HOMING
// =========================================================

bool homeWheel() {

  long safetyLimit = stepsPerRevolution * 2;
  long target;

  if (DEBUG) {
    Serial.println("=== HOMING START ===");
  }

  stepper.setAcceleration(setAccel);
  stepper.setMaxSpeed(maxSpeed);

  // ----------------------------------------------------
  // PHASE 1: Move until we are OFF the magnet
  // ----------------------------------------------------
  target = stepper.currentPosition() - safetyLimit;
  stepper.moveTo(target);

  while (stepper.distanceToGo() != 0) {
    stepper.run();

    int v = analogRead(SENSOR);
    bool triggered = activeHigh ? (v > analogSensorThreshold)
                                : (v < analogSensorThreshold);

    if (!triggered) {
      break;  // we are now OFF the magnet
    }
  }

  // Move a bit more to make sure
  target = stepper.currentPosition() - backlashOvershoot;
  stepper.moveTo(target);
  while (stepper.distanceToGo() != 0) {
    stepper.run();
  }


  // ----------------------------------------------------
  // PHASE 2: Now search for the magnet normally
  // ----------------------------------------------------
  // Continue in the SAME direction
  target = stepper.currentPosition() - safetyLimit;
  stepper.moveTo(target);

  while (stepper.distanceToGo() != 0) {
    stepper.run();

    int v = analogRead(SENSOR);
    bool triggered = activeHigh ? (v > analogSensorThreshold)
                                : (v < analogSensorThreshold);

    if (triggered) {
      stepper.stop();
      while (stepper.isRunning())
        stepper.run();

      stepper.setCurrentPosition(0);
      currPos = 0;

      if (DEBUG) Serial.println("Home found");
      return false;
    }
  }

  Serial.println("Home Sensor not found");
  return true;
}



// =========================================================
// MOVEMENT
// =========================================================

void goToLocation(int newPos, bool silent) {
  if (newPos < 1 || newPos > numberOfFilters) 
    return;
  
  if (DEBUG) {
    Serial.print("GoTo ");
    Serial.print(newPos);
    Serial.print(" filterPos[newPos] = ");
    Serial.print(filterPos[newPos]);
    Serial.print(" posOffset[newPos] = ");
    Serial.println(posOffset[newPos]);
  }

  if (newPos < currPos) {
    stepper.runToNewPosition(filterPos[newPos] + posOffset[newPos] - backlashOvershoot);
  }

  stepper.runToNewPosition(filterPos[newPos] + posOffset[newPos]);
  currPos = newPos;

  motor_Off();
  if (!silent) {
    Serial.println("P" + String(currPos));
  }
  startLedFlash(currPos, 120);  // N flashes, 120ms interval
}

void unwindBacklash() {
  stepper.runToNewPosition(stepper.currentPosition() - backlashOvershoot);
}

// =========================================================
// SERIAL PROTOCOL (unchanged from original)
// =========================================================

void sendSerial(String s) {
  Serial.println(s);
}


// =========================================================
// SETUP
// =========================================================

void setup() {
  Serial.begin(115200);

  pinMode(hallCom, OUTPUT);
  pinMode(hallPower, OUTPUT);
  pinMode(SENSOR, INPUT);
  pinMode(IN1, OUTPUT);
  pinMode(IN2, OUTPUT);
  pinMode(IN3, OUTPUT);
  pinMode(IN4, OUTPUT);
  pinMode(upButton, INPUT_PULLUP);
  pinMode(downButton, INPUT_PULLUP);
  pinMode(ledPin, OUTPUT);

  digitalWrite(hallCom, LOW);
  digitalWrite(hallPower, HIGH);
  digitalWrite(ledPin, LOW);
  sendSerial("CONNECTED");

  initialise();
}

// =========================================================
// LOOP
// =========================================================

void loop() {
  updateLed();
  handleButtons();
  handleSerialCommands();
  handleErrors();
}

void dispatchXagylCommand(const String &cmd) {

  if (cmd.length() == 0) return;

  char c0 = cmd.charAt(0);
  char c1 = cmd.length() > 1 ? cmd.charAt(1) : 0;

  switch (c0) {

    // ============================================================
    // GOTO FILTER (G1..G8)
    // ============================================================
    case 'G':
      if (c1 == '0') {
        // G0 = Save EEPROM
        saveConfigSettings();
        sendSerial("EEPROM Saved");
        return;
      } else {
        // G1..G8 = goto filter
        handleGoto(c1);
        return;
      }


    // ============================================================
    // OFFSET QUERY (O1..O8)
    // ============================================================
    case 'O':
      handleOffsetQuery(c1);
      return;


    // ============================================================
    // SET ABSOLUTE OFFSET (F<n> <xxxx>)
    // ============================================================
    case 'F':
      handleSetAbsoluteOffset(cmd);
      return;



    // ============================================================
    // NAME QUERY (Q1..Q8)
    // ============================================================
    case 'Q':
      handleNameQuery(cmd);
      return;


    // ============================================================
    // SET FILTER NAME (N<n> <cccccccccccccccccccccccccccccc>)
    // ============================================================
    case 'N':
      handleSetName(cmd);
      return;


    // ============================================================
    // INFORMATION COMMANDS (I0..I9)
    // ============================================================
    case 'I':
      handleInfo(c1);
      return;


    // ============================================================
    // SENSOR TEST COMMANDS (T0..T3)
    // ============================================================
    case 'T':
      handleSensorTest(c1);
      return;


    // ============================================================
    // RESET / CALIBRATION COMMANDS (R0..R6)
    // ============================================================
    case 'R':
      handleReset(c1);
      return;


    // ============================================================
    // SPEED SETTING (Sxxx)
    // ============================================================
    case 'S':
      handleSetSpeed(cmd);
      return;

      break;
  }

  // Unknown command → ignore silently (Xagyl behaviour)
}

// ============================
// COMMAND HANDLERS
// ============================
// --- Debounce state ---
const unsigned long debounceTime = 50;  // 50ms is ideal for mechanical buttons

unsigned long lastUpPress = 0;
unsigned long lastDownPress = 0;

bool lastUpState = HIGH;
bool lastDownState = HIGH;

void handleButtons() {

  // UP BUTTON
  bool upState = digitalRead(upButton);
  if (upState == LOW && lastUpState == HIGH) {
    unsigned long now = millis();
    if (now - lastUpPress > debounceTime) {

      if (currPos < numberOfFilters) {
        goToLocation(currPos + 1, false);
        sendSerial("P" + String(currPos));
      }
      else {
           startLedFlash(currPos, 120);
      }

      lastUpPress = now;
    }
  }
  lastUpState = upState;

  // DOWN BUTTON
  bool downState = digitalRead(downButton);
  if (downState == LOW && lastDownState == HIGH) {
    unsigned long now = millis();
    if (now - lastDownPress > debounceTime) {

      if (currPos > 1) {
        goToLocation(currPos - 1, false);
        sendSerial("P" + String(currPos));
      }
      else {
           startLedFlash(currPos, 120);
      }

      lastDownPress = now;
    }
  }
  lastDownState = downState;
}

void handleSerialCommands() {
  if (!Serial.available()) return;

  Serial.setTimeout(250);
  String cmd = Serial.readStringUntil('\n');

  if (cmd.length() == 0) return;

  dispatchXagylCommand(cmd);
}

void handleErrors() {
  if (Error) {
    Serial.println("Homing Failed");
    Error = false;
  }
}

// ============================
// OFFSET COMMAND HANDLERS
// ============================

void handleOffsetQuery(char c1) {
  int f = c1 - '0';
  if (f < 1 || f > numberOfFilters) return;

  sendSerial("P" + String(f) + " Offset " + String(posOffset[f]));
}

void handleSetAbsoluteOffset(const String &cmd) {
  // Expected format: "F<n> <offset>"
  // Example: "F3 120"

  // 1. Find the space separating filter number and offset
  int spaceIndex = cmd.indexOf(' ');
  if (spaceIndex < 0) {
    sendSerial("ERR Invalid Format");
    return;
  }

  // 2. Extract filter number
  int f = cmd.substring(1, spaceIndex).toInt();  // characters after 'F' up to space

  if (f < 1 || f > numberOfFilters) {
    sendSerial("ERR Invalid Filter");
    return;
  }

  // 3. Extract offset value
  int val = cmd.substring(spaceIndex + 1).toInt();

  // 4. Store offset
  posOffset[f] = val;
  writeInt(EE_OFFSET_BASE + (f - 1) * 2, posOffset[f]);

  // 5. Move wheel to new absolute position
  unwindAndGoto(f);

  // 6. Emit confirmation
  sendSerial("P" + String(f) + " Offset " + String(posOffset[f]));
}


void handleSetName(const String &cmd) {
  // cmd is the full command string, e.g. "L3 Red Filter"
  // First extract the slot number after 'L'

  int spaceIndex = cmd.indexOf(' ');
  if (spaceIndex == -1) {
    Serial.println("ERR Missing name");
    return;
  }


  // Extract slot number
  int slot = cmd.substring(1, spaceIndex).toInt();

  if (slot < 1 || slot > numberOfFilters) {
    Serial.println("ERR Slot out of range");
    return;
  }

  // Extract the name (everything after the first space)
  String newName = cmd.substring(spaceIndex + 1);

  // Trim whitespace
  newName.trim();

  // Enforce max length (30 chars)
  if (newName.length() > 30) {
    newName = newName.substring(0, 30);
  }

  // Copy into global array
  newName.toCharArray(filterNames[slot], NAME_SLOT_SIZE);

  // Save to EEPROM
  int addr = EE_NAME_BASE + (slot - 1) * NAME_SLOT_SIZE;
  writeStringFixed(addr, filterNames[slot], NAME_SLOT_SIZE);

  // Respond in protocol format
  Serial.print("P");
  Serial.print(slot);
  Serial.print(" Name ");
  Serial.println(filterNames[slot]);
}

void handleNameQuery(const String &cmd) {
  // cmd is the full command, e.g. "N3"
  // Extract the slot number after 'N'

  if (cmd.length() < 2) {
    Serial.println("ERR Missing slot");
    return;
  }

  int slot = cmd.substring(1).toInt();

  if (slot < 1 || slot > numberOfFilters) {
    Serial.println("ERR Slot out of range");
    return;
  }

  // Respond in protocol format:
  // P<n> Name <name>
  Serial.print("P");
  Serial.print(slot);
  Serial.print(" Name ");
  Serial.println(filterNames[slot]);
}

void handleGoto(char c1) {
  int f = c1 - '0';
  if (f < 1 || f > numberOfFilters) return;

  goToLocation(f, false);
}


void handleInfo(char c1) {
  switch (c1) {
    case '0': sendSerial("Lunatic Astro Nano Filter Wheel"); break;  // Product Name
    case '1': sendSerial(FIRMWARE_VERSION); break;                   // Firmware version
    case '2': sendSerial("P" + String(currPos)); break;              // Current filter position
    case '3': sendSerial("DIY"); break;                              // Serial number
    case '4': sendSerial("MaxSpeed " + String(maxSpeed)); break;     // Max speed
    case '5': sendOffsetReport(currPos); break;
    case '6': sendSerial("Threshold " + String(analogSensorThreshold)); break;
    case '7': sendSerial("FilterSlots " + String(numberOfFilters)); break;
  }
}


void handleSensorTest(char c1) {
  switch (c1) {
    case '0':
    case '1':
      sendSerial("Sensors " + String(digitalRead(SENSOR)) + " " + String(digitalRead(SENSOR)));
      break;

    case '2':
      sendSerial("Digital " + String(sensorIsDigital ? "YES" : "NO"));
      break;

    case '3':
      sendSerial("Active High " + String(activeHigh ? "YES" : "NO"));
      break;
  }
}

void handleSetSpeed(const String &cmd) {
  // Extract the numeric part after 'S'
  int newSpeed = cmd.substring(1).toInt();

  // Sanity‑check the range (Xagyl uses 0–100%)
  if (newSpeed < 0) newSpeed = 0;
  if (newSpeed > 100) newSpeed = 100;

  // Convert percentage to actual stepper speed
  // Your maxSpeed constant is the 100% value
  int scaledSpeed = (maxSpeed * newSpeed) / 100;

  // Apply to stepper
  stepper.setMaxSpeed(scaledSpeed);
  stepper.setSpeed(scaledSpeed);

  // Report back in Xagyl format
  sendSerial("MaxSpeed " + String(newSpeed) + "%");
}


void handleReset(char c1) {
  switch (c1) {
    case '1':
      initialise();
      break;
    case '2': resetOffsets(); break;
    case '3': handleSetSpeed("S100"); break;
    case '4':
      detectSensorType();
      initialise();
      Serial.println("Threshold " + String(analogSensorThreshold));
      break;
  }
}

void resetOffsets() {
  // Reset all offsets to zero
  for (int i = 1; i <= numberOfFilters; i++) {
    posOffset[i] = 0;
  }
  saveConfigSettings();

  // Re-home the wheel (Xagyl behaviour)
  Error = homeWheel();

  // Report completion
  sendSerial("Calibration Removed");
}

void handleSave() {
  saveConfigSettings();
  sendSerial("EEPROM Saved");
}

// ============================
// HELPERS
// ============================
void initialise() {
  stepper.setCurrentPosition(0);
  stepper.setMaxSpeed(maxSpeed);
  stepper.setSpeed(Speed);
  stepper.setAcceleration(setAccel);

  // Load filter geometry + offsets from EEPROM
  loadConfigSettings();

  // Load or auto-detect sensor settings
  if (!loadSensorSettings()) detectSensorType();

  // Home and move to filter 1
  Error = homeWheel();
  if (!Error) {
    goToLocation(1, true);
    sendSerial("READY");
  }
}


void clampOffset(int f) {
  if (posOffset[f] > 2000) posOffset[f] = 2000;
  if (posOffset[f] < -2000) posOffset[f] = -2000;
}

void saveOffsetToEEPROM(int f) {
  writeInt(EE_OFFSET_BASE + (f - 1) * 2, posOffset[f]);
}

void unwindAndGoto(int f) {
  unwindBacklash();
  goToLocation(f, false);
}

void sendOffsetReport(int f) {
  sendSerial("P" + String(f) + " Offset " + String(posOffset[f]));
}