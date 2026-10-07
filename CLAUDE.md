# Working on Wtile

## Live-testing on this VM

This is a VM dedicated to Wtile development -- running the actual built `Wtile.exe` and driving
real windows/tags/focus to verify a fix is expected and encouraged, not just unit tests.

**Always finish with the user's terminal visible.** The user watches this terminal to know
whether you're done. If testing involved switching tags, moving focus to another window/monitor,
or spawning windows that ended up on a different tag than the terminal, switch back (e.g.
`view-tag`/`toggle-last-tag`, or restore the original foreground window) as the last step before
ending your turn. Never end a turn with the user's terminal hidden on some other tag with no way
to tell what happened.

**Before trusting any live test**, kill any running `Wtile.exe`, rebuild/republish, and confirm
the binary's timestamp actually changed before launching it again -- a stale binary from a
previous build will silently make a fix look untested or unfixed.
