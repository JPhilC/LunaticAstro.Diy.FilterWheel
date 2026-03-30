## An Arduino Nano controlled 3D printed filterwheel for astronomy.

This project was inspired by, and builds, on the work of chemistorge who's original repo can be found [here](https://github.com/chemistorge/Arduino-Motorised-Filter-Wheel-Xagyl-compatible-ASCOM-and-INDI-).

After printing the parts I struggled to get the original firmware to operate reliably so I wrote new firmware, roughly based on the original but with improved home detection and button de-bounce code. I also changed the command set to make it more compatible with a new ASCOM driver that is also included in this project meaning that **the new firmware is not compatible with the old Xagly ASCOM driver**.

The firmware has only been tested with a unipolar 28BYJ-48 stepper motor, the common ULN2003 driver board and a KY-003 analogue hall sensor.

The original Xagly ASCOM driver appears not be supported or further developed so I wrote a new ASCOM driver that is better suited to the capabilities of the hardware.

The Ardiuno sketch is in the Firmware folder and the ASCOM source code is in the ASCOM Driver folder. 

The ASCOM drive passes ASCOMs Universal Conform tool and a log of the results is included in the ASCOM driver folder.

## Disclaimer

The firmware and software in this project is provided “as is”, without warranty of any kind, express or implied.
The author shall not be held liable for any damages, loss of data, hardware damage,
or any other issues arising from the use of this software. Use at your own risk.

## License

This project is licensed under the Creative Commons Attribution–NonCommercial 4.0 International License (CC BY-NC 4.0).

You may use, modify, and share the code for non-commercial purposes, provided you give appropriate credit.
Commercial use is strictly prohibited.

