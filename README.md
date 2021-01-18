# Differential Damage Calculator
Calculate damage dealt from the /age command in [Project: Gorgon](https://projectgorgon.com/). 

# Using the program
Copy [the latest release](https://github.com/dlebansais/PgDmgDiff/releases/download/v1.0.0/PgDmgDiff.exe) in a directory, then run it as administrator (If you don't want that, see below). This will create a little icon ![Icon](/Screenshots/Icon.png?raw=true "The taskbar icon") in the task bar.

Right-click the icon to pop a menu with the following items:

+ Load at startup. When checked, the application is loaded when a user logs in.
+ Exit

You also need to enable the `BookSaveToFile` option in the game special settings.

![Special Settings](/Screenshots/Settings.png?raw=true "The game special settings")

# How does it work?
Every time you type the /age command, then save it in a file with the `Save` button, this application reads the new file. It then compares values in it with previous values it has read in previous files, calculate the difference and saves a summary in the clipboard.

Currently, this application reads:

+ Total damage (from the `You have dealt X damage` line)
+ Number of kills (from the `You have killed X monsters` line) 

# Damage summary

In the game, just start chatting with return and press Ctrl+V, to get a summary similar to this:

	Total: 8650, Kills: 13, Dpm: 665

Dpm is the damage per monster.
 
# Screenshots

![Menu](/Screenshots/Menu.png?raw=true "The app menu")
