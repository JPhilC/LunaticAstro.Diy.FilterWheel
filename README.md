An Arduino Nano controlled 3D printed filterwheel or astronomy.

This project was inspired by and builds on the work of chemistorge who's original repo can be found [here](https://github.com/chemistorge/Arduino-Motorised-Filter-Wheel-Xagyl-compatible-ASCOM-and-INDI-)

After printing the parts I struggled to get the original firmware to operate reliably. This project includes a reworking of that original firmware retaining the same Xagyl command set. I improved the home detection and the button de-bounce code. The firmware has only been tested with a unipolar 28BYJ-48 stepper motor and the common ULN2003 driver board.

After getting the firmware working I went looking for the Xagyl ASCOM driver only to find that it is nolonger available. The suggestions I found for alternatives didn't seem to work so I created my own ASCOM driver. This is in a separate repo [here] (https://github.com/JPhilC/LunaticAstro.FilterWheel.Ascom.Driver).

## Disclaimer

The firmware in this project is provided “as is”, without warranty of any kind, express or implied.
The author shall not be held liable for any damages, loss of data, hardware damage,
or any other issues arising from the use of this software. Use at your own risk.

## License

This project is licensed under the Creative Commons Attribution–NonCommercial 4.0 International License (CC BY-NC 4.0).

You may use, modify, and share the code for non-commercial purposes, provided you give appropriate credit.
Commercial use is strictly prohibited.

