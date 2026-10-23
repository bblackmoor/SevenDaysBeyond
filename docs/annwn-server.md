# ANNWN baseline server

## Installed state

The native Linux dedicated server is installed under `/srv/seven-days-beyond` and runs as the dedicated `chatgpt` Ubuntu account. No Docker is used.

The installed Steam build is `25661908`; the server identifies itself as **V 3.3.0 (b18)**. Its `Assembly-CSharp.dll` SHA256 matches the dependency lock used for the phase 1 diagnostic build.

| Item | Location or value |
| --- | --- |
| Server executable and shipped files | `/srv/seven-days-beyond/server/` |
| Disposable baseline configuration | `/srv/seven-days-beyond/config/baseline.xml` |
| User data and saves | `/srv/seven-days-beyond/data/` |
| Server log | `/srv/seven-days-beyond/logs/server.log` |
| SteamCMD | `/srv/seven-days-beyond/tools/steamcmd/steamcmd.sh` |
| Configuration helper | `/srv/seven-days-beyond/tools/configure-test-server.py` |
| Systemd unit | `seven-days-beyond.service` |
| Game port | `26900` |
| World and save | `Navezgane` / `SDB_Baseline` |
| Maximum simultaneous players | `6` |
| Server browser listing | Disabled |
| EAC | Disabled for forthcoming mod tests |
| Telnet and web dashboard | Disabled |

The first test is limited to the home LAN: UFW permits TCP 26900 and UDP 26900–26903 from the LAN subnet. No router forwarding was changed. There is no game password for this LAN-only disposable baseline.

The service is currently running but is **not enabled at boot** while this is a disposable test. It restarts on failure, with a bounded restart policy. Its stop signal is SIGINT, with up to 180 seconds for shutdown; shutdown/save behavior has not yet been exercised with a connected player.

## Verification performed

- SteamCMD completed installation and validation of app `294420`.
- The systemd service is active, running as `chatgpt`, with no observed restarts.
- Logs report `GameServer.Init successful`, `StartGame done`, and successful Steam registration.
- TCP and UDP game listeners are present.
- The configuration stores saves outside the Steam-updated installation directory.
- Initial idle memory consumption was approximately 2 GB. This is a single-world baseline, not a MultiWorld capacity measurement.

No player connection or manual world-transfer test has been performed. Only the server's shipped mods are installed; LITW MultiWorld and the diagnostic companion are not yet enabled. Verify the baseline client connection before adding them.

## Service operations

```sh
sudo systemctl status seven-days-beyond.service
sudo systemctl stop seven-days-beyond.service
sudo systemctl start seven-days-beyond.service
tail -n 80 /srv/seven-days-beyond/logs/server.log
```

Before updating the server, stop it and back up `/srv/seven-days-beyond/data` and `/srv/seven-days-beyond/config`. Updating Steam files must not replace the separate baseline configuration.

## Installation recipe

The upstream SteamCMD Linux installer was downloaded from `https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz` and extracted as the normal `chatgpt` user. The first app download reported `Missing configuration`; explicitly refreshing the app metadata and selecting Linux succeeded:

```sh
/srv/seven-days-beyond/tools/steamcmd/steamcmd.sh \
  +@sSteamCmdForcePlatformType linux \
  +force_install_dir /srv/seven-days-beyond/server \
  +login anonymous \
  +app_info_update 1 \
  +app_update 294420 validate \
  +quit
```

`tools/configure-test-server.py` derives the baseline XML from the shipped `serverconfig.xml` and prepares the systemd unit. It refuses to overwrite an existing baseline configuration and does not itself start services or change the firewall. The unit sets the library path used by the shipped startup script. The Steam client library is linked under `/home/chatgpt/.steam/sdk64/` for the dedicated server's Steam integration.

## Next verification

From a LAN gaming PC running V 3.3.0, use the game's direct-IP connection to ANNWN's LAN address on port 26900. Confirm entry into the world, then disconnect normally. After that succeeds, add MultiWorld and the companion, using separate disposable saves for the two-world tests in [development.md](development.md).
