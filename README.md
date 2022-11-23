# TimingData
Measures timing statistics of a Beat Saber play and makes multiple calculations for both hands:
- Average time difference between cuts and notes.
- Average unstable rate (using standard deviation).
- Time difference between cuts for each note position, relative to the average.
- "Centered" time difference, where TimingData only counts non-inverted swings on the note positions circling each hand.

Notes:
- Beat Saber calculates time difference when your saber touches the hitbox instead of passing through the center of the note, so the value is always earlier than it's supposed to be. I try to manually calculate the remaining difference using the hitbox size, cut angle difference, and saber speed.
- Because of above calculations, Pro Mode/Pro Mode + Small Cube values will be slightly late.
- Units are in milliseconds, where negative numbers mean early swings and positive numbers are late swings.
- Right now it just prints results into the console.
