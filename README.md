# XenoIndustry
XenoIndustry is a KSP mod that connects the game to Factorio through a Clusterio master server, allowing the two games to interact.

## Features: 
- KSP vessels require Factorio items to be built in addition to funds
- Science gathered in KSP can be sent back to Factorio in the form of space science packs

## How to use:
- Download the latest release
- Find a running Clusterio master server, or install and run it yourself
- Edit the config.json file in XenoIndustry mod folder and edit the masterIP, masterPort, and masterAuthToken values to match that of your Clusterio server (masterAuthToken can be found in the secret-api-token.txt in your Clusterio directory)
- Run the game - XenoIndustry connects automatically upon loading a save file
