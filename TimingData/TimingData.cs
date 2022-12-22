using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;
using BS_Utils.Utilities;

namespace SwingAndTimingData
{
    /// <summary>
    /// Monobehaviours (scripts) are added to GameObjects.
    /// For a full list of Messages a Monobehaviour can receive from the game, see https://docs.unity3d.com/ScriptReference/MonoBehaviour.html.
    /// </summary>
    public class TimingData : MonoBehaviour
    {
        public static TimingData Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        // Uses units of millisecods.
        // Position [0, 0] = top left, [2, 3] = bottom right.
        private static List<float>[,] timings = new List<float>[3, 4], leftTimings = new List<float>[3, 4], rightTimings = new List<float>[3, 4];
        private static List<float>[,] rowTimings = new List<float>[3, 1], colTimings = new List<float>[1, 4];
        private static List<float>[,] centeredLeftTimings = new List<float>[3, 3], centeredRightTimings = new List<float>[3, 3];
        private static float[,] averageTiming = new float[3, 4], leftAverageTiming = new float[3, 4], rightAverageTiming = new float[3, 4];
        private static float[,] averageRowTiming = new float[3, 1], averageColTiming = new float[1, 4];
        private static float[,] centeredLeftAverageTiming = new float[3, 3], centeredRightAverageTiming = new float[3, 3];
        private static float overallTiming = 0f, leftOverallTiming = 0f, rightOverallTiming = 0f;
        private static float unstableRate = 0f, leftUnstableRate = 0f, rightUnstableRate = 0f;
        private static float centeredLeftOverallTiming = 0f, centeredRightOverallTiming = 0f;
        private static int[] prevLeftNotePos = new int[2], prevRightNotePos = new int[2];
        private static bool levelEnded = false; // Because BSEvents.LevelFinished sometimes gets played more than once.

        private const float minHitboxDistance = 0.559017f, maxHitboxDistance = 0.640312f; // Distance in meters from yz/xz hitbox to center.
        private const float degreesToRadians = 0.0174533f;

        // These methods are automatically called by Unity, you should remove any you aren't using.
        #region Monobehaviour Messages
        /// <summary>
        /// Only ever called once, mainly used to initialize variables.
        /// </summary>
        private void Awake() {
            // For this particular MonoBehaviour, we only want one instance to exist at any time, so store a reference to it in a static property
            //   and destroy any that are created while one already exists.
            Log = Plugin.Log;
            if (Instance != null) {
                Plugin.Log?.Warn($"Instance of {GetType().Name} already exists, destroying.");
                GameObject.DestroyImmediate(this);
                return;
            }
            GameObject.DontDestroyOnLoad(this); // Don't destroy this object on scene changes
            Instance = this;
            Plugin.Log?.Debug($"{name}: Awake()");

            BSEvents.gameSceneLoaded += OnLevelStart;
            BSEvents.noteWasCut += OnNoteCut;
            BSEvents.LevelFinished += OnLevelEnd;
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 4; j++) {
                    timings[i, j] = new List<float>();
                    leftTimings[i, j] = new List<float>();
                    rightTimings[i, j] = new List<float>();
                }
            }
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 3; j++) {
                    centeredLeftTimings[i, j] = new List<float>();
                    centeredRightTimings[i, j] = new List<float>();
                }
            }
            for (int i = 0; i < 3; i++)
                rowTimings[i, 0] = new List<float>();
            for (int i = 0; i < 4; i++)
                colTimings[0, i] = new List<float>();
            prevLeftNotePos[0] = 0; prevLeftNotePos[1] = 1; prevRightNotePos[0] = 0; prevRightNotePos[1] = 2;
        }

        /// <summary>
        /// Called when the script is being destroyed.
        /// </summary>
        private void OnDestroy() {
            Plugin.Log?.Debug($"{name}: OnDestroy()");
            if (Instance == this)
                Instance = null; // This MonoBehaviour is being destroyed, so set the static instance property to null.

        }
        #endregion

        private void OnLevelStart() {
            levelEnded = false;
        }


        private void OnNoteCut(NoteController nc, NoteCutInfo nci) {
            AddTimingData(nci);
        }

        private void OnLevelEnd(object o, LevelFinishedEventArgs lfea) {
            if (levelEnded == false) {
                CalculateAverageData();
                PrintTimingData();
                ClearTimingData();
                levelEnded = true;
            }
        }

        private static void ClearTimingData() {
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 4; j++) {
                    timings[i, j].Clear();
                    leftTimings[i, j].Clear();
                    rightTimings[i, j].Clear();
                }
            }
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 3; j++) {
                    centeredLeftTimings[i, j].Clear();
                    centeredRightTimings[i, j].Clear();
                }
            }
            for (int i = 0; i < 3; i++)
                rowTimings[i, 0].Clear();
            for (int i = 0; i < 4; i++)
                colTimings[0, i].Clear();
            averageTiming = new float[3, 4];
            leftAverageTiming = new float[3, 4]; rightAverageTiming = new float[3, 4];
            averageRowTiming = new float[3, 1]; averageColTiming = new float[1, 4];
            centeredLeftAverageTiming = new float[3, 3]; centeredRightAverageTiming = new float[3, 3];
            overallTiming = 0f;
            leftOverallTiming = 0f; rightOverallTiming = 0f;
            centeredLeftOverallTiming = 0f; centeredRightOverallTiming = 0f;
            unstableRate = 0f;
            leftUnstableRate = 0f; rightUnstableRate = 0f;
            prevLeftNotePos[0] = 0; prevLeftNotePos[1] = 1; prevRightNotePos[0] = 0; prevRightNotePos[1] = 2;
        }

        private static void AddTimingData(NoteCutInfo nci) {
            int row, col;
            switch (nci.noteData.noteLineLayer) {
                case NoteLineLayer.Top: row = 0; break;
                case NoteLineLayer.Upper: row = 1; break;
                case NoteLineLayer.Base: row = 2; break;
                default: row = 0; break;
            }
            col = nci.noteData.lineIndex;

            //In Beat Saber code, NoteCutInfo.timeDeviation = (note time - current time) in seconds.
            float distanceToCenter = (minHitboxDistance * (float)Math.Cos(nci.cutDirDeviation * degreesToRadians)) + (maxHitboxDistance * (float)Math.Sin(nci.cutDirDeviation * degreesToRadians));
            float time = (-nci.timeDeviation + (distanceToCenter / nci.saberSpeed)) * 1000f;

            timings[row, col].Add(time);
            rowTimings[row, 0].Add(time);
            colTimings[0, col].Add(time);
            if (nci.saberType == SaberType.SaberA) {
                leftTimings[row, col].Add(time);
                if (IsCenteredNote(row, col, nci.noteData.cutDirection) && IsOppositePosition(nci.saberType, row, col))
                    centeredLeftTimings[row, col].Add(time);
            }
            else {
                rightTimings[row, col].Add(time);
                if (IsCenteredNote(row, 3 - col, nci.noteData.cutDirection.Mirrored()) && IsOppositePosition(nci.saberType, row, 3 - col))
                    centeredRightTimings[row, col - 1].Add(time);
            }
        }

        // Tells if the note can be cut straight from the hand's position and isn't an invert note (excluding vision-blocking notes).
        private static bool IsCenteredNote(int row, int col, NoteCutDirection ncd) {
            if (row == 0 && col == 0 && (ncd == NoteCutDirection.Left || ncd == NoteCutDirection.UpLeft || ncd == NoteCutDirection.Up)) return true;
            if (row == 0 && col == 1 && (ncd == NoteCutDirection.UpLeft || ncd == NoteCutDirection.Up || ncd == NoteCutDirection.UpRight)) return true;
            if (row == 0 && col == 2 && (ncd == NoteCutDirection.Up || ncd == NoteCutDirection.UpRight || ncd == NoteCutDirection.Right)) return true;
            if (row == 1 && col == 0 && (ncd == NoteCutDirection.DownLeft || ncd == NoteCutDirection.Left || ncd == NoteCutDirection.UpLeft)) return true;
            if (row == 1 && col == 2 && (ncd == NoteCutDirection.UpRight || ncd == NoteCutDirection.Right || ncd == NoteCutDirection.DownRight)) return true;
            if (row == 2 && col == 0 && (ncd == NoteCutDirection.Down || ncd == NoteCutDirection.DownLeft || ncd == NoteCutDirection.Left)) return true;
            if (row == 2 && col == 1 && (ncd == NoteCutDirection.DownRight || ncd == NoteCutDirection.Down || ncd == NoteCutDirection.DownLeft)) return true;
            if (row == 2 && col == 2 && (ncd == NoteCutDirection.Right || ncd == NoteCutDirection.DownRight || ncd == NoteCutDirection.Down)) return true;
            return false;
        }

        // Tells if the current note is in the opposite position of the previous one (which is required to be a centered note).
        private static bool IsOppositePosition(SaberType st, int row, int col) {
            bool result = false;
            if (st == SaberType.SaberA) {
                if (row + prevLeftNotePos[0] == 2 && col + prevLeftNotePos[1] == 2) result = true;
                prevLeftNotePos[0] = row; prevLeftNotePos[1] = col;
            }
            else {
                if (row + prevRightNotePos[0] == 2 && col + prevRightNotePos[1] == 2) result = true;
                prevRightNotePos[0] = row; prevRightNotePos[1] = col;
            }
            return result;
        }

        private static void CalculateAverageData() {
            overallTiming = AverageTimingCalculator(timings, averageTiming);
            leftOverallTiming = AverageTimingCalculator(leftTimings, leftAverageTiming);
            rightOverallTiming = AverageTimingCalculator(rightTimings, rightAverageTiming);
            AverageTimingCalculator(rowTimings, averageRowTiming);
            AverageTimingCalculator(colTimings, averageColTiming);

            centeredLeftOverallTiming = AverageTimingCalculator(centeredLeftTimings, centeredLeftAverageTiming);
            centeredRightOverallTiming = AverageTimingCalculator(centeredRightTimings, centeredRightAverageTiming);

            unstableRate = UnstableRateCalculator(timings, overallTiming);
            leftUnstableRate = UnstableRateCalculator(leftTimings, leftOverallTiming);
            rightUnstableRate = UnstableRateCalculator(rightTimings, rightOverallTiming);
        }

        // Sets average timing values for each position in averageResults, returns average for all positions.
        private static float AverageTimingCalculator(List<float>[,] timings, float[,] averageResults) {
            float positionSum, totalSum = 0f;
            int count = 0;
            for (int i = 0; i < timings.GetLength(0); i++) {
                for (int j = 0; j < timings.GetLength(1); j++) {
                    positionSum = 0f;
                    foreach (float n in timings[i, j]) {
                        positionSum += n;
                        totalSum += n;
                    }
                    count += timings[i, j].Count;
                    if (timings[i, j].Count > 0)
                        averageResults[i, j] = positionSum / timings[i, j].Count;
                }
            }
            return totalSum / count;
        }

        // Returns unstable rate, defined as the standard deviation of all timing values.
        private static float UnstableRateCalculator(List<float>[,] timings, float averageTiming) {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < timings.GetLength(0); i++) {
                for (int j = 0; j < timings.GetLength(1); j++) {
                    foreach (float n in timings[i, j])
                        sum += (n - averageTiming) * (n - averageTiming);
                    count += timings[i, j].Count;
                }
            }
            return sum / count;
        }

        private static void PrintTimingData() {
            Log.Info("Printing Timing Data:");
            Log.Info(string.Format("Overall timing:                     {0:F2}ms", overallTiming));
            Log.Info(string.Format("Left hand overall timing:           {0:F2}ms", leftOverallTiming));
            Log.Info(string.Format("Right hand overall timing:          {0:F2}ms", rightOverallTiming));
            Log.Info(string.Format("Centered left hand overall timing:  {0:F2}ms", centeredLeftOverallTiming));
            Log.Info(string.Format("Centered right hand overall timing: {0:F2}ms", centeredRightOverallTiming));
            Log.Info(string.Format("Unstable rate:                      {0:F0}ms^2", unstableRate));
            Log.Info(string.Format("Left hand unstable rate:            {0:F0}ms^2", leftUnstableRate));
            Log.Info(string.Format("Right hand unstable rate:           {0:F0}ms^2", rightUnstableRate));

            PrintGrid("Overall timings for each position:", averageTiming);
            PrintGrid("Overall timings for each row:", averageRowTiming);
            PrintGrid("Overall timings for each col:", averageColTiming);
            PrintGrid("Left hand timings relative to average:", leftAverageTiming, leftOverallTiming);
            PrintGrid("Right hand timings relative to average:", rightAverageTiming, rightOverallTiming);
            PrintGrid("Centered Left hand timings relative to average:", centeredLeftAverageTiming, centeredLeftOverallTiming);
            PrintGrid("Centered Right hand timings relative to average:", centeredRightAverageTiming, centeredRightOverallTiming);
        }

        private static void PrintGrid(string message, float[,] timings, float average = 0f) {
            string rowTimings;
            Log.Info("-");
            Log.Info(message);
            for (int i = 0; i < timings.GetLength(0); i++) {
                rowTimings = "";
                for (int j = 0; j < timings.GetLength(1); j++) {
                    if (timings[i, j] == 0.0f)
                        rowTimings += "N/A ";
                    else
                        rowTimings += string.Format("{0,-4:F0} ", timings[i, j] - average);
                }
                Log.Info(rowTimings);
            }
        }
    }
}
