
<h1 align="center">Orange Launcher Linux</h1>
<p align="center">
  <img src="https://github.com/user-attachments/assets/e4d63dcb-6537-4453-9375-3c8c0b3b5a50" alt="orange">
</p>

<h1 align="center">Orange Launcher Win10</h1>
<p align="center">
  <img src="https://github.com/user-attachments/assets/bb8af391-de1a-41fa-8498-345e056ba023" alt="orange">
</p>

<h1 align="center">Orange Launcher Win11</h1>
<p align="center">
  <img src="https://github.com/user-attachments/assets/6eac6ddc-77c4-457b-916b-33adc0b505cd" alt="orange">
</p>

# Installation 

## Arch Linux x64
[![Source](https://github.com/Orang-Studio/OrangLaunch/actions/workflows/buildsrc.yml/badge.svg)](https://github.com/Orang-Studio/OrangLaunch/actions/workflows/buildsrc.yml) [![Binary](https://github.com/Orang-Studio/OrangLaunch/actions/workflows/build.yml/badge.svg)](https://github.com/Orang-Studio/OrangLaunch/actions/workflows/build.yml)

Install with [**yay**](https://github.com/Jguer/yay). There is two options for btw users:<br>
Prebuild binary (Fast)<br>
```shell
yay -Sy oranglauncher-bin
```

Build from source py (Slower, can fail)<br>
```shell
yay -Syu oranglauncher
```
<br>

## Fedora x64 
Install with **dnf**
```bash
sudo dnf install oranglauncher-bin
```
for the open source build version:

```bash
sudo dnf install oranglauncher
```

## Linux Other x64 
Download zip: [launcher_x64_linux_x.x.x.tar.gz](https://github.com/Orang-Studio/OrangLaunch/releases)


## Windows 10-11 x86-x64
Download installer: [OrangLauncher_Windows.msi](https://github.com/Orang-Studio/OrangLaunch/releases)<br>
### arm32-arm64
Download zip: [launcher_arms_win_x.x.x.zip](https://github.com/Orang-Studio/OrangLaunch/releases)

# Terminal Mode

The launcher includes a terminal/CLI mode for headless servers or users who prefer command-line interfaces.

## Usage

Run the launcher with the `--terminal` flag:

```bash
python launcher.py --terminal
```

This will:
1. Display a list of your configured Minecraft instances
2. Let you select an instance by number
3. Show available game profiles (accounts)
4. Let you select a profile/account
5. Prompt for RAM allocation (default: 4G)
6. Launch the game directly

### Example:
```
==================================================
OrangLauncher - Terminal Mode
==================================================

Available Instances:
  1. Fabulously Optimized (MC 26.1.2, fabric)
  2. gh (MC 1.20.1, forge)
  3. New Profile (MC 1.21.1, vanilla)

Select instance (1-3): 1
Available Profiles:
  1. MyAccount (Microsoft)
  2. OfflinePlayer (Offline)

Select profile (1-2): 1
Enter RAM amount (default: 4G): 6G

[Launcher] Launching Minecraft 26.1.2 (fabric) as MyAccount...
```

# About This Minecraft Launcher
This launcher is designed to be a modular, modern, and highly customizable Minecraft launcher experience. It uses Python for backend that keeps everything lightweight, fast, and fully scriptable.

# Accounts 
## Microsoft (mojang)
This is achieved by using localhost and microsoft official page. If that fails, then it will use browser to connect. It's safe for you to use this, because the launcher gets the token from the Microsoft servers, not from me! Token is required to indentify if you are legit owner of your account.

## Offline (cracked)
Simply enter username and boom you have that account, and you can use it only on cracked servers (`offline-mode=false`) and singleplayer. Though, you can't have a custom skin bc you cracked it! 
I'm not resposible for any legal damage, nor im Mojang or Xbox studios, I use methods that mojang provides to give offline mode.

## News Hub

The launcher includes a news page that displays every updates, announcements, and community info. It refreshes automatically every start in a clean way.

## Modding Panel

A dedicated modding tab provides:
- Easy mod installation and management
- Version‑aware mod compatibility handling
- Automatic folder structuring for cleaner organization
- MrPack file support from modrith

## Profiles System

You can create and switch between multiple game profiles, each with its own:

- Name
- Game version
- Loader
- Version of loader
- Ram
And more!


## Resource Packs & Shaders

The launcher features a tab for visual enhancements:

Enable, disable, and reorder resource packs.
Manage shaders with eaay toggling.
Add/remove shaderpacks and resourcepacks

## Settings

The settings interface lets you modify:

- Launcher general
- Accounts page
- About page
And more!

## Language Support

The entire application supports changeable languages, including community‑driven translations. 

## For the developers section:

Go to GitHub Wiki up in the GitHub website of this repo, and open Wiki. You will find all tutorials and examples about this launcher, and even build your own version!
