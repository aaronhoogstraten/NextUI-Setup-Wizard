# NextUI Setup Wizard

**A tool help set up NextUI on your handheld gaming device**

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
- Extract the **NextUI-Setup-Wizard-Windows-v1.0.0.zip** and then open the `Launch NextUI Setup Wizard.bat` file to run the wizard.

### For Mac users:
**Requirements:** 
- macOS 15.0+, arm64 

**Instructions:**
- Extract the **NextUI-Setup-Wizard-macOS-v1.0.0.zip** and then open the `NextUI Setup Wizard-1.0.pkg`.
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

### 2. NextUI Download
- Downloads the latest NextUI release
- Verifies file integrity with SHA256 checksums
- Extracts files directly to your SD card
- Can be skipped if NextUI is already installed

### 3. BIOS Configuration
- Helps you add BIOS files for various gaming systems
- Validates BIOS file formats and checksums
- Organizes files in the correct directory structure

### 4. ROM Configuration  
- Assists with adding ROM files for your games
- Supports multiple gaming systems (NES, SNES, Game Boy, etc.)
- Checks ROM file formats and organization

### 5. Setup Complete
- Provides final instructions for using your device

---
## Acknowledgements
- K-Wall for their generous time given in helping test the macOS version.
