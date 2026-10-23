# Client testing tools (0.4.1)

## Debug-state transfer fix

MultiWorld unloads the client's world and creates a new local player for each map transition. The stock local-player setup resets flight, and the previous player's independent God Mode, collision and speed settings were not carried across.

The optional SevenDaysBeyond client companion captures God Mode, fly mode, noclip, flight speed and the debug-menu toggle immediately before an accepted map switch. It restores those independent values when the new local player is assigned, on its spawn-state callback and before a successful transfer result releases control. Recovery to the source retains the original snapshot. The snapshot is bound to the upstream transfer token and retained transport connection; a normal connection attempt clears it. Normal logins and unrelated spawns do not receive a saved snapshot. It does not enable God Mode for someone who had it off.

This code uses the stock player DataItem setters, not simulated keypresses or a blanket Q toggle. The unchanged upstream binary hash and client hook signatures are checked before installation. Errors log capture/restore failures without blocking upstream recovery. The server retains its own permission checks; the companion does not change administrator access.

Install the supplied client update with the game fully closed. Place its `SevenDaysBeyond` directory in the game's `Mods` directory, alongside the existing `LITW_MultiWorld` and shipped `0_TFP_Harmony` directories. Do not copy the server's `BoundaryTest.xml` to the client. Start without EAC as before. No server restart is needed for this client-only fix.

For a client check, enable `dm`, choose God Mode on and flight off in the debug settings, cross and verify both values. Repeat with flight/noclip on and a different flight speed. Turn the modes off, cross again, and verify they remain off. Normal reconnects intentionally keep the game's usual login behavior. Inspect `CLIENT_DEBUG_STATE_ENABLED`, `CLIENT_DEBUG_STATE_RESTORED` or the failure messages in the client log. Runtime behavior still needs a client test; a successful pinned-assembly build alone does not prove it.

## Border warning distances

The server now gives an early warning at 50 m and a close warning at 10 m before the playable travel line. The close warning bypasses the 15-second repeat throttle when entering the closer stage. Moving outside the early-warning zone rearms it. Messages report distance to that line.

The travel line remains 500 m inside the raw map extent, outside the unsafe outer terrain. Thus the warning positions are 550 m and 510 m inside that extent. Crossing, safe-ground checks, arrival placement and closed-edge retreat keep their existing positions. This change reduces notification distance; it does not move players into the outer radiation zone. Arrivals remain 100 m clear of the travel line, outside both warning stages.

The production geometry/landing/suspension-policy test passes 993 assertions, including exact warning boundaries and arrival clearance. The 0.4.0 shared-clock client check was reported working by the user; that result is independent of the debug-state bug.
