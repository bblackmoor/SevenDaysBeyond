# Client testing tools (0.4.1)

## Debug-state transfer fix

MultiWorld unloads the client's world and creates a new local player for each map transition. The stock local-player setup resets flight, and the previous player's independent God Mode, collision and speed settings were not carried across.

The optional SevenDaysBeyond client companion captures God Mode, fly mode, noclip, flight speed and the debug-menu toggle immediately before an accepted map switch. It restores those independent values when the new local player is assigned, on its spawn-state callback and before a successful transfer result releases control. Recovery to the source retains the original snapshot. The snapshot is bound to the upstream transfer token and retained transport connection; a normal connection attempt clears it. Normal logins and unrelated spawns do not receive a saved snapshot. It does not enable God Mode for someone who had it off.

This code uses the stock player DataItem setters, not simulated keypresses or a blanket Q toggle. The unchanged upstream binary hash and client hook signatures are checked before installation. Errors log capture/restore failures without blocking upstream recovery. The server retains its own permission checks; the companion does not change administrator access.

Install the supplied client update with the game fully closed. Place its `SevenDaysBeyond` directory in the game's `Mods` directory, alongside the existing `LITW_MultiWorld` and shipped `0_TFP_Harmony` directories. Do not copy the server's `BoundaryTest.xml` to the client. Start without EAC as before. No server restart is needed for this client-only fix.

For a client check, enable `dm`, choose God Mode on and flight off in the debug settings, cross and verify both values. Repeat with flight/noclip on and a different flight speed. Turn the modes off, cross again, and verify they remain off. Normal reconnects intentionally keep the game's usual login behavior. Inspect `CLIENT_DEBUG_STATE_ENABLED`, `CLIENT_DEBUG_STATE_RESTORED` or the failure messages in the client log. The user subsequently confirmed God Mode persists across map transitions with this client update. The separate flight/noclip/speed combinations have not been explicitly confirmed.

## Border warning distances

The server now gives an early warning at 50 m and a close warning at 10 m before the playable travel line. The close warning bypasses the 15-second repeat throttle when entering the closer stage. Moving outside the early-warning zone rearms it. Messages report distance to that line.

The travel line remains 500 m inside the raw map extent, outside the unsafe outer terrain. Thus the warning positions are 550 m and 510 m inside that extent. Crossing, safe-ground checks, arrival placement and closed-edge retreat keep their existing positions. This change reduces notification distance; it does not move players into the outer radiation zone. Arrivals remain 100 m clear of the travel line, outside both warning stages.

The production geometry/landing/suspension-policy test passes 993 assertions, including exact warning boundaries and arrival clearance. The initial shared-clock check was reported working, but a later screenshot demonstrated a real HUD/calendar mismatch. See the 0.4.2 correction in shared-clock.md; God Mode success does not establish clock correctness.

## Deployment status

Gateway eventually completed its package check with no detection. Companion 0.4.1 was deployed on ANNWN after a clean stop at 21:39 America/New_York on 2026-10-21. The service restarted successfully. The prior companion, saves, configuration and log are backed up at `/srv/seven-days-beyond/backups/before-companion-update-20261022T013945328959Z.tar.gz`.

Deployed server package: `SevenDaysBeyond-Server-0.4.1.zip`, SHA256 `9ffd27ccd5bca341a11c6c2e4a252de3f40664ec34bc419a7de51b1158df755e`. Installed DLL SHA256: `ada779298b4f7b391f744953c8f77b5ffc2317785e4e81fdafb3de1fd6ee6f39`. Client package: `SevenDaysBeyond-Client-Debug-0.4.1.zip`, SHA256 `d058fcff569f0c456941ba35e366fbecc5f7d8235f5e595ada61f682a5083fa4`.

Both worlds reached ready state at 21:41:12 America/New_York. The upstream contract remained 12/12; both representative landing probes passed. Suspension self-tests passed at 21:41:41 and shared-calendar self-tests passed at 21:41:45. Both worlds remained suspended while the empty-server calendar paused. The installed DLL checksum matches the locally inspected binary, whose warning logic subtracts the 500 m travel-line margin and compares the remainder with 50 m and 10 m. The startup banner still contains an obsolete hardcoded `warning=700m` diagnostic string; it does not drive the runtime thresholds. Actual warning messages and `/sdb status` use the new behavior. An in-game warning-distance check remains pending.
