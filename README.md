# NextUI Setup Wizard

**A tool to help set up NextUI on your handheld gaming device**

![NextUI Setup Wizard](https://img.shields.io/badge/Status-Unofficial-orange) ![Platform](https://img.shields.io/badge/Platform-Windows-blue) ![Platform](https://img.shields.io/badge/Platform-macOS-blue)

## ⚠️ Important Disclaimer
- Always backup your important files before using this tool
- The setup process will involve erasing any files that might be on your SD card
- The developers of this tool are not responsible for any damage to your device or data loss

## What is NextUI?

[NextUI](https://nextui.loveretro.games/) is a custom firmware/interface for handheld gaming devices that provides an improved user experience for retro gaming. This setup wizard helps you prepare an SD card with NextUI and configure it with your BIOS files and ROMs.

## Who This Tool Is For

- **Beginners** who want to try NextUI but find the manual setup complex
- **Anyone** who wants to add BIOS/ROM files to an existing NextUI setup

## System Requirements

### Your Computer
- **Operating System**: Windows 10/11 x64 or macOS 15.0+ arm64
- **Storage**: At least 1GB free space for temporary files
- **SD Card Reader**: Built-in or USB SD card reader

### Your Handheld Device
This tool prepares SD cards for devices that support NextUI. Check the [official NextUI documentation](https://nextui.loveretro.games/docs/) for supported devices.

## Installation Instructions
Use the latest release: https://github.com/aaronhoogstraten/NextUI-Setup-Wizard/releases

### For Windows users: 
**Requirements:** 
- Windows 10/11, x64 

**Instructions:**
- Extract the **NextUI-Setup-Wizard-Windows-{version}.zip** and then open the `Launch NextUI Setup Wizard.bat` file to run the wizard.

### For Mac users:
**Requirements:** 
- macOS 15.0+, arm64 

**Instructions:**
- Extract the **NextUI-Setup-Wizard-macOS-{version}.zip** and then open the `NextUI Setup Wizard-1.0.pkg`.
- You will receive a popup that says the app cannot be opened. Follow the instructions here to proceed https://support.apple.com/en-us/102445#openanyway:
  1. Open System Settings.
  2. Click Privacy & Security, scroll down, and click the Open Anyway button to confirm your intent to open or install the app.
  3. The warning prompt reappears and, if you're absolutely sure that you want to open the app anyway, you can click Open.
  4. The app is now saved as an exception to your security settings, and you can open it in the future by double-clicking it, just as you can any authorized app.

---

## Setup Process Overview

### 1. SD Card Preparation
- Validates your SD card meets requirements
- Checks for existing NextUI installations
- Offers to wipe and prepare the card if needed

<img width="790" height="520" alt="Screenshot 2025-09-13 134544" src="https://github.com/user-attachments/assets/96ac33a7-a302-4738-b3b7-d2a4ceb33c7e" />

### 2. NextUI Download
- Downloads the latest NextUI release
- Verifies file integrity with SHA256 checksums
- Extracts files directly to your SD card
- Can be skipped if NextUI is already installed

<img width="790" height="520" alt="Screenshot 2025-09-13 134654" src="https://github.com/user-attachments/assets/7b97d713-ef0f-4756-8b8b-eb2b65028162" />

### 3. BIOS Configuration
- Helps you add BIOS files for various gaming systems
- Validates BIOS file formats and checksums
- Organizes files in the correct directory structure

<img width="790" height="520" alt="Screenshot 2025-09-13 134727" src="https://github.com/user-attachments/assets/550d2bc0-49ec-43af-9f19-89cf7a801b0d" />

### 4. ROM Configuration  
- Assists with adding ROM files for your games
- Supports multiple gaming systems (NES, SNES, Game Boy, etc.)
- Checks ROM file formats and organization

<img width="790" height="520" alt="Screenshot 2025-09-13 134739" src="https://github.com/user-attachments/assets/e19d62c5-9f18-4350-a64f-f05601ff4f44" />

### 5. Setup Complete
- Provides final instructions for using your device

<img width="790" height="520" alt="Screenshot 2025-09-13 152759" src="https://github.com/user-attachments/assets/60699829-55d2-4900-8913-f7dfdca45257" />

---
## NextUI Log Zipper Tool

To export all the `.userdata` logs from an existing NextUI installation, click the wrench icon button on the top left of the SD Card Preparation page and follow the instructions:

<img width="533" height="466" alt="Screenshot 2025-09-21 150121" src="https://github.com/user-attachments/assets/c9283c1b-a595-4915-8f42-cdb091bd0f00" />

---
## ADB Mode

NextUI Setup Wizard also supports ADB Mode which allows a user to connect their NextUI compatible device to their PC via USB cable and perform all the above operations without needing to remove the SD card from the device.
To access ADB Mode, click the wrench icon button in the top left and select ADB Mode:

<img width="229" height="161" alt="Screenshot 2025-10-12 134731" src="https://github.com/user-attachments/assets/279d0eba-9686-4a42-9dc8-22d366d5e05c" />

---

## Acknowledgements
- K-Wall for their generous time given in helping test the macOS version.
