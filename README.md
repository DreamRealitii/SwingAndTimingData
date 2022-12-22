# SwingAndTimingData
Measures timing statistics of a Beat Saber play and makes multiple calculations for both hands:
- Average saber position and direction.
- Average time difference between cuts and notes.
- Average unstable rate (using standard deviation).
- Time difference between cuts for each note position, relative to the average.
- "Centered" time difference, where TimingData only counts non-inverted swings on the note positions circling each hand.

Notes:
- Swing data is calculated using only centered swings.
- For position and direction, Positive X means to the right, positive Y means up, positive Z means forward.
- Adjust Saber X direction by changing Saber Y rotation setting, Saber Y direction by changing Saber X rotation setting.
- Saber Z direction is not how far forward the Saber is pointing, but the difference between the direction of the saber, and the direction from the last note to the next note. A positive value means your swings are too clockwise.
- Data can be skewed by not standing in the center and facing exactly forward. It might be helpful to subtract the headset x/z position (which can be found in BeatLeader scores) form the saber x/z positions.
- Beat Saber calculates time difference when your saber touches the hitbox instead of passing through the center of the note, so the value is always earlier than it's supposed to be. I try to manually calculate the remaining difference using the hitbox size, cut angle difference, and saber speed.
- Because of above calculations, Pro Mode/Pro Mode + Small Cube values will be slightly late.
- Units are in milliseconds, where negative numbers mean early swings and positive numbers are late swings.
- Right now it just prints results into the console. To see the console, put --verbose into the Steam launch settings.
- Inverted notes skew the results into the negative, while far crossover swings skew the results into the positive.

What you want to get:
- Whether or not your saber Y position is centered on the middle row is up to you. Better to change height than saber Y position offset.
- For both hands, Saber Y and Z positions should be equal.
- All saber X position and direction values should be close to zero degrees, at least under five centimeters/degrees. If not, try changing your grip settings, but don't change both position and rotation at once, as changing one can affect the other. Saber Z direction cannot be changed with any settings as far as I know.
- Average timing values are somewhere between -20ms to 0ms. Try playing with Ghost Notes to sync your swings to the music.
- Timing values are the same for both hands. If not, try adjusting where/how you stand and change saber rotations for each hand.
- Timing values are about the same between the top/bottom rows. If values are generally higher on one row, try adjusting the height and/or saber x rotation.
- The unstable rate is as low as possible, preferably under 200ms^2. If not, try using Pro Mode.

WIP
- Show data in-game instead of the console.
- Detect when users are playing with Pro Mode/Pro Mode + Small Cube and adjust calculations.
