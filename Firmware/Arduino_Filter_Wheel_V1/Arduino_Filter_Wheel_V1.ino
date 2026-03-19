#include <AccelStepper.h>
#include <EEPROM.h>

// =========================================================
// CONFIGURATION
// =========================================================

#define DEBUG 1  // Set to 1 for verbose debug output

// Pins
#define SENSOR A3
#define hallPower A2
#define hallCom A1
#define upButton 4
#define downButton 5
#define buzzerPin 2
#define ledPin 3

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
#define maxNumberOfFilters 8

#define offSetResolution 5
#define backlashOvershoot 200
#define SENSOR_OFFSET 70


// Optional peripherals
#define buzzerConnected 0
#define ledConnected 0

// Motor type (fixed)
#define motorType 1  // 1 = unipolar 28BYJ-48

// =========================================================
// GLOBALS
// =========================================================

#if motorType == 1
AccelStepper stepper(4, IN2, IN4, IN1, IN3);
#endif

int numberOfFilters = 5;
int filterPos[maxNumberOfFilters + 1];
int16_t posOffset[maxNumberOfFilters + 1];

bool Error = false;
int currPos = 0;

// Sensor characteristics (EEPROM-backed)
bool sensorIsDigital = false;
bool activeHigh = true;
int analogSensorThreshold = 650;


// =========================================================
// EEPROM HELPERS
// =========================================================

// EEPROM addresses
#define EE_THRESH 10
#define EE_DIGITAL 12
#define EE_ACTIVEHIGH 13
#define EE_OFFSET_BASE 52

void writeInt(int addr, int value) {
  EEPROM.write(addr, value >> 8);
  EEPROM.write(addr + 1, value & 0xFF);
}

int readInt(int addr) {
  return (EEPROM.read(addr) << 8) | EEPROM.read(addr + 1);
}

void saveSensorSettings() {
  writeInt(EE_THRESH, analogSensorThreshold);
  EEPROM.write(EE_DIGITAL, sensorIsDigital ? 1 : 0);
  EEPROM.write(EE_ACTIVEHIGH, activeHigh ? 1 : 0);
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
  for (int i = 1; i <= maxNumberOfFilters; i++) {
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
  }
}



// =========================================================
// SENSOR AUTO-DETECT
// =========================================================

void detectSensorType() {
  Serial.println("EEPROM invalid → running auto-detect…");

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

  if (DEBUG) {
    Serial.print("min=");
    Serial.print(minVal);
    Serial.print(" max=");
    Serial.println(maxVal);
  }

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

    Serial.println("Sensor is ANALOG");
    Serial.print("Analog threshold = ");
    Serial.println(analogSensorThreshold);
    Serial.print("Polarity: active ");
    Serial.println(activeHigh ? "HIGH" : "LOW");
  }

  // Rewind
  stepper.runToNewPosition(stepper.currentPosition() - stepsPerRevolution);
  stepper.setCurrentPosition(0);

  saveSensorSettings();
  Serial.println("Auto-detect complete. Values saved.");
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

void goToLocation(int newPos) {
  if (newPos < 1 || newPos > numberOfFilters) return;
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
}

// =========================================================
// SERIAL PROTOCOL (unchanged from original)
// =========================================================

void serialFlush() {
  if (Serial.available()) Serial.read();
}

void sendSerial(String s) {
  Serial.println(s);
}

void (*resetFunc)(void) = 0;

void handleSerial(char firstChar, char secondChar) {
  int number = int(secondChar - 48);
  int tmpPoss;

  switch (toupper(firstChar)) {

    case 'B':
      tmpPoss = currPos;
      for (int i = 0; i < number; i++) {
        tmpPoss--;
        if (tmpPoss == 0) tmpPoss = numberOfFilters;
      }
      goToLocation(tmpPoss);
      sendSerial("P" + String(currPos));
      break;

    case 'F':
      tmpPoss = currPos;
      for (int i = 0; i < number; i++) {
        tmpPoss++;
        if (tmpPoss == numberOfFilters + 1) tmpPoss = 1;
      }
      goToLocation(tmpPoss);
      sendSerial("P" + String(currPos));
      break;

    case 'G':
      goToLocation(number);
      sendSerial("P" + String(currPos));
      break;

    case 'P':
      // Return the current position
      sendSerial("P" + String(currPos));
      break;

    case 'R':
      switch (number) {
        case 0:
        case 1:
          resetFunc();
          break;

        case 2:
          for (int i = 1; i < numberOfFilters; i++) posOffset[i] = 0;
          saveConfigSettings();
          Error = homeWheel();
          if (!Error) goToLocation(1);
          sendSerial("Calibration Removed");
          break;

        case 6:
          saveConfigSettings();
          break;
      }
      break;

    case '(':
      tmpPoss = currPos;

      // Increase offset
      posOffset[tmpPoss] += offSetResolution;

      // Clamp to safe range
      if (posOffset[tmpPoss] > 1000) posOffset[tmpPoss] = 1000;
      if (posOffset[tmpPoss] < -1000) posOffset[tmpPoss] = -1000;

      // Remove back-lash
      stepper.runToNewPosition(stepper.currentPosition() - backlashOvershoot);
      goToLocation(tmpPoss);

      // Save to EEPROM
      saveConfigSettings();

      // Report in steps
      sendSerial("P" + String(currPos) + " Offset " + String(posOffset[currPos]));
      break;

    case ')':
      tmpPoss = currPos;

      // Decrease offset
      posOffset[tmpPoss] -= offSetResolution;

      // Clamp to safe range
      if (posOffset[tmpPoss] > 1000) posOffset[tmpPoss] = 1000;
      if (posOffset[tmpPoss] < -1000) posOffset[tmpPoss] = -1000;

      // Remove back-lash
      stepper.runToNewPosition(stepper.currentPosition() - backlashOvershoot);
      goToLocation(tmpPoss);

      // Save to EEPROM
      saveConfigSettings();

      // Report in steps
      sendSerial("P" + String(currPos) + " Offset " + String(posOffset[currPos]));
      break;


    default:
      Serial.println("Command Unknown");
  }
}

// =========================================================
// SETUP
// =========================================================

void setup() {
  Serial.begin(9600);

  pinMode(hallCom, OUTPUT);
  pinMode(hallPower, OUTPUT);
  pinMode(SENSOR, INPUT);
  pinMode(IN1, OUTPUT);
  pinMode(IN2, OUTPUT);
  pinMode(IN3, OUTPUT);
  pinMode(IN4, OUTPUT);
  pinMode(upButton, INPUT_PULLUP);
  pinMode(downButton, INPUT_PULLUP);
  pinMode(buzzerPin, OUTPUT);
  pinMode(ledPin, OUTPUT);

  digitalWrite(hallCom, LOW);
  digitalWrite(hallPower, HIGH);

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
  if (!Error) goToLocation(1);
}

// =========================================================
// LOOP
// =========================================================


// --- Debounce state ---
const unsigned long debounceTime = 50;  // 50ms is ideal for mechanical buttons

unsigned long lastUpPress = 0;
unsigned long lastDownPress = 0;

bool lastUpState = HIGH;
bool lastDownState = HIGH;

void loop() {

  // ============================
  // UP BUTTON (edge-triggered)
  // ============================
  bool upState = digitalRead(upButton);

  // Detect falling edge (button pressed)
  if (upState == LOW && lastUpState == HIGH) {
    unsigned long now = millis();
    if (now - lastUpPress > debounceTime) {

      if (DEBUG) {
        Serial.print("UP: currPos = ");
        Serial.print(currPos);
        Serial.print(" of ");
        Serial.println(numberOfFilters);
      }

      if (currPos < numberOfFilters) {
        goToLocation(currPos + 1);
        sendSerial("P" + String(currPos));
      }

      lastUpPress = now;
    }
  }

  lastUpState = upState;



  // ============================
  // DOWN BUTTON (edge-triggered)
  // ============================
  bool downState = digitalRead(downButton);

  if (downState == LOW && lastDownState == HIGH) {
    unsigned long now = millis();
    if (now - lastDownPress > debounceTime) {

      if (DEBUG) {
        Serial.print("DOWN: currPos = ");
        Serial.print(currPos);
        Serial.print(" of ");
        Serial.println(numberOfFilters);
      }

      if (currPos > 1) {
        goToLocation(currPos - 1);
        sendSerial("P" + String(currPos));
      }

      lastDownPress = now;
    }
  }

  lastDownState = downState;


  if (Serial.available() > 0) {
    delay(10);
    if (Serial.available() >= 2) {
      char one = Serial.read();
      char two = Serial.read();
      if (!isDigit(one) && isDigit(two)) handleSerial(one, two);
      else serialFlush();
    } else serialFlush();
  }

  if (Error) {
    Serial.println("Homing Failed");
    Error = false;
  }
}