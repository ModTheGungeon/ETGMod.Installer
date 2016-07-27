ModTheGungeon Installer (Core)
===================
So... copying some files and running a .bat / .sh is not your thing?
It's dangerous out there - take this!

This is just an installing utility for [ModTheGungeon](https://modthegungeon.github.io/), a mod for the game "Enter The Gungeon".

For issues with the installer, select the log box, CTRL+A, CTRL+C, create an issue here.

For issues with ModTheGungeon, f.e. crashes, go the EtG.exe folder, then EtG_Data, upload output_log.txt, create an issue [in this repo](https://github.com/ModTheGungeon/ETGMod).

Uses [MonoMod](https://github.com/0x0ade/MonoMod), Mono.Cecil and a binary blob repository as submodules and de4dot (in said repository) as dependency. Clone recursively!

Previous versions of Core shipped with de4dot. Due to licensing changes with the installer (switching from a weird clause-decided-GPLv3-or-MIT), the de4dot dependency has been replaced with a deobfuscation abstraction layer. See [the citrus flavour (GPLv3 licensed)](https://github.com/ModTheGungeon/ETGMod.Installer) for the "full" installer.
