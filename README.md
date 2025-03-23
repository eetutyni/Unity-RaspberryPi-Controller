## Raspberry Pi Controller - Unity

A Unity based application for discovering, connecting to, and controlling Raspberry Pi devices over a local network. This project allows users to detect available Raspberry Pis, connect via TCP, and toggle an LED on/off via a simple UI.

## Features
- **Auto Discovery**: Broadcasts a UDP message to find Raspberry Pis running a compatible listener.
- **Connection Management**: Establishes and maintains TCP connections with selected Raspberry Pi devices.
- **LED Control**: Sends commands to turn an LED on or off remotely. Can also be expanded to other commands, LED on/off is just an example.
- **UI**: Displays discovered devices and updates their connection status in real time.

## How It Works
1. The application sends a UDP broadcast (`DISCOVER_RASPBERRY_PI`) to detect Raspberry Pis.
2. Pis responding with `RASPBERRY_PI_RESPONSE` are added to the UI list.
3. Users can select a Raspberry Pi, establish a TCP connection, and toggle an LED on/off.
4. The app monitors connection status and updates the UI accordingly.

## Setup & Requirements
Unity
- Unity 2021.3+ (or newer)
## Pi
-  I personally used Raspberry Pi 4.

## Raspberry Pi
 Download the controller.py for the raspberry it's located in the root of the repo.
