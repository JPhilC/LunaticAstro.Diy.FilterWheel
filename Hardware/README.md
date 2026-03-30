The hardware for this project is the same as [Arduino-Motorised-Filter-Wheel-Xagyl-compatible-ASCOM-and-INDI-](https://github.com/chemistorge/Arduino-Motorised-Filter-Wheel-Xagyl-compatible-ASCOM-and-INDI-). 

Rather than copying the details here, please refer to the original project for the original print files and also for details of the electrical components. 

## Important

The firmware in this project expects the following Arduino Nano pin outs:
### Analogue pins
- A1 - Hall sensor (common -)
- A2 - Hall sensor (power (centre pin on sensor))
- A3 - Hall sensor (sensor S)
### Digital pins
- 3 LED
- 4 Up button
- 5 Down button
- 6 Stepper IN2
- 7 Stepper IN4
- 8 Stepper IN3
- 9 Stepper IN1

## The Hall Sensor
The firmware has only been tested with a KY-003 analogue hall sensor. This is the one I used. This is the one I used [KY-003 Hall Effect Magnetic Sensor Module For Arduino](https://www.ebay.co.uk/itm/303361490827)
