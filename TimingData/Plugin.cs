using IPA;
using IPA.Config;
using IPA.Config.Stores;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using IPALogger = IPA.Logging.Logger;
using BS_Utils.Utilities;

namespace TimingData
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        [Init]
        /// <summary>
        /// Called when the plugin is first loaded by IPA (either when the game starts or when the plugin is enabled if it starts disabled).
        /// [Init] methods that use a Constructor or called before regular methods like InitWithConfig.
        /// Only use [Init] with one Constructor.
        /// </summary>
        public void Init(IPALogger logger)
        {
            Instance = this;
            Log = logger;
            Log.Info("TimingData initialized.");
        }

        #region BSIPA Config
        //Uncomment to use BSIPA's config
        /*
        [Init]
        public void InitWithConfig(Config conf)
        {
            Configuration.PluginConfig.Instance = conf.Generated<Configuration.PluginConfig>();
            Log.Debug("Config loaded");
        }
        */
        #endregion

        // Uses units of millisecods.
        // Position [0, 0] = top left, [2, 3] = bottom right.
        private static List<float>[,] leftHandTimings = new List<float>[3, 4], rightHandTimings = new List<float>[3, 4];
        private static List<float>[,] centeredLeftHandTimings = new List<float>[3, 3], centeredRightHandTimings = new List<float>[3, 3];
        private static float[,] leftHandAverageTiming = new float[3, 4], rightHandAverageTiming = new float[3, 4];
        private static float[,] centeredLeftHandAverageTiming = new float[3, 3], centeredRightHandAverageTiming = new float[3, 3];
        private static float leftHandOverallTiming = 0f, rightHandOverallTiming = 0f, leftHandUnstableRate = 0f, rightHandUnstableRate = 0f;
        private static float centeredLeftHandOverallTiming = 0f, centeredRightHandOverallTiming = 0f;
        private static int[] prevLeftNotePos = new int[2], prevRightNotePos = new int[2];
        private static bool levelEnded = false; // Because BSEvents.LevelFinished sometimes gets played more than once.

        private const float minHitboxDistance = 0.559017f, maxHitboxDistance = 0.640312f; // Distance in meters from yz/xz hitbox to center.
        private const float degreesToRadians = 0.0174533f;

        [OnStart]
        public void OnApplicationStart()
        {
            Log.Debug("OnApplicationStart");
            new GameObject("TimingDataController").AddComponent<TimingDataController>();

            BSEvents.gameSceneLoaded += OnLevelStart;
            BSEvents.noteWasCut += OnNoteCut;
            BSEvents.LevelFinished += OnLevelEnd;
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 4; j++) {
                    leftHandTimings[i, j] = new List<float>();
                    rightHandTimings[i, j] = new List<float>();
                }
            }
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 3; j++) {
                    centeredLeftHandTimings[i, j] = new List<float>();
                    centeredRightHandTimings[i, j] = new List<float>();
                }
            }
        }

        [OnExit]
        public void OnApplicationQuit()
        {
            Log.Debug("OnApplicationQuit");

        }

        private void OnLevelStart() {
            ClearTimings();
            levelEnded = false;
        }


        private void OnNoteCut(NoteController nc, NoteCutInfo nci) {
            AddTimingData(nci);
        }

        private void OnLevelEnd(object o, LevelFinishedEventArgs lfea) {
            if (levelEnded == false) {
                CalculateAverageData();
                PrintTimingData();
                levelEnded = true;
            }
        }

        private static void ClearTimings() {
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 4; j++) {
                    leftHandTimings[i, j].Clear();
                    rightHandTimings[i, j].Clear();
                }
            }
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 3; j++) {
                    centeredLeftHandTimings[i, j].Clear();
                    centeredRightHandTimings[i, j].Clear();
                }
            }
            leftHandAverageTiming = new float[3, 4];
            rightHandAverageTiming = new float[3, 4];
            centeredLeftHandAverageTiming = new float[3, 3];
            centeredRightHandAverageTiming = new float[3, 3];
            leftHandOverallTiming = 0f;
            rightHandOverallTiming = 0f;
            centeredLeftHandOverallTiming = 0f;
            centeredRightHandOverallTiming = 0f;
            leftHandUnstableRate = 0f;
            rightHandUnstableRate = 0f;
            prevLeftNotePos[0] = 1; prevLeftNotePos[1] = 1;
            prevRightNotePos[0] = 1; prevRightNotePos[1] = 1;
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

            float distanceToCenter = (minHitboxDistance * (float)Math.Cos(nci.cutDirDeviation * degreesToRadians)) + (maxHitboxDistance * (float)Math.Sin(nci.cutDirDeviation * degreesToRadians));
            float time = (nci.timeDeviation - (distanceToCenter / nci.saberSpeed)) * -1000f;

            if (nci.saberType == SaberType.SaberA) {
                leftHandTimings[row, col].Add(time);
                if (IsCenteredNote(row, col, nci.noteData.cutDirection) && IsOppositePosition(nci.saberType, row, col))
                    centeredLeftHandTimings[row, col].Add(time);
            } else {
                rightHandTimings[row, col].Add(time);
                if (IsCenteredNote(row, 3-col, nci.noteData.cutDirection.Mirrored()) && IsOppositePosition(nci.saberType, row, 3-col))
                    centeredRightHandTimings[row, col-1].Add(time);
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

        // Tells if the current note is in the opposite position of the previous one (required to be a centered note).
        private static bool IsOppositePosition(SaberType st, int row, int col) {
            /*if (row == 0 && col == 0 && prevRow == 2 && prevCol == 2) return true;
              if (row == 1 && col == 0 && prevRow == 1 && prevCol == 2) return true;
              if (row == 2 && col == 0 && prevRow == 0 && prevCol == 2) return true;
              if (row == 0 && col == 1 && prevRow == 2 && prevCol == 1) return true;
              if (row == 2 && col == 1 && prevRow == 0 && prevCol == 1) return true;
              if (row == 0 && col == 2 && prevRow == 2 && prevCol == 0) return true;
              if (row == 1 && col == 2 && prevRow == 1 && prevCol == 0) return true;
              if (row == 2 && col == 2 && prevRow == 0 && prevCol == 0) return true;*/
            bool result = false;
            if (st == SaberType.SaberA) {
                if (row + prevLeftNotePos[0] == 2 && col + prevLeftNotePos[1] == 2) result = true;
                prevLeftNotePos[0] = row; prevLeftNotePos[1] = col;
            } else {
                if (row + prevRightNotePos[0] == 2 && col + prevRightNotePos[1] == 2) result = true;
                prevRightNotePos[0] = row; prevRightNotePos[1] = col;
            }
            return result;
        }

        private static void CalculateAverageData() {
            // Average timing for each note position.
            float leftSum, rightSum, totalLeftSum = 0f, totalRightSum = 0f;
            int leftCount = 0, rightCount = 0;
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 4; j++) {
                    leftSum = 0f; rightSum = 0f;
                    foreach (float n in leftHandTimings[i, j]) {
                        leftSum += n;
                        totalLeftSum += n;
                    }
                    foreach (float n in rightHandTimings[i, j]) {
                        rightSum += n;
                        totalRightSum += n;
                    }
                    leftCount += leftHandTimings[i, j].Count;
                    rightCount += rightHandTimings[i, j].Count;
                    if (leftHandTimings[i, j].Count > 0)
                        leftHandAverageTiming[i, j] = leftSum / leftHandTimings[i, j].Count;
                    if (rightHandTimings[i, j].Count > 0)
                        rightHandAverageTiming[i, j] = rightSum / rightHandTimings[i, j].Count;
                }
            }
            // Average timing overall.
            leftHandOverallTiming = totalLeftSum / leftCount;
            rightHandOverallTiming = totalRightSum / rightCount;
            // Average timing for centered notes.
            totalLeftSum = 0f; totalRightSum = 0f;
            leftCount = 0; rightCount = 0;
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 3; j++) {
                    leftSum = 0f; rightSum = 0f;
                    foreach (float n in centeredLeftHandTimings[i, j]) {
                        leftSum += n;
                        totalLeftSum += n;
                    }
                    foreach (float n in centeredRightHandTimings[i, j]) {
                        rightSum += n;
                        totalRightSum += n;
                    }
                    if (centeredLeftHandTimings[i, j].Count > 0)
                        centeredLeftHandAverageTiming[i, j] = leftSum / centeredLeftHandTimings[i, j].Count;
                    if (centeredRightHandTimings[i, j].Count > 0)
                        centeredRightHandAverageTiming[i, j] = rightSum / centeredRightHandTimings[i, j].Count;
                    leftCount += centeredLeftHandTimings[i, j].Count;
                    rightCount += centeredRightHandTimings[i, j].Count;
                }
            }
            // Average centered timing.
            centeredLeftHandOverallTiming = totalLeftSum / leftCount;
            centeredRightHandOverallTiming = totalRightSum / rightCount;
            // Average unstable rate.
            leftSum = 0f; rightSum = 0f;
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 4; j++) {
                    foreach (float n in leftHandTimings[i, j])
                        leftSum += (n - leftHandOverallTiming) * (n - leftHandOverallTiming);
                    foreach (float n in rightHandTimings[i, j])
                        rightSum += (n - rightHandOverallTiming) * (n - rightHandOverallTiming);
                    leftCount += leftHandTimings[i, j].Count;
                    rightCount += rightHandTimings[i, j].Count;
                }
            }
            leftHandUnstableRate = leftSum / leftCount;
            rightHandUnstableRate = rightSum / rightCount;
        }

        private static void PrintTimingData() {
            Log.Info(string.Format("Left hand overall timing: {0:F1}ms", leftHandOverallTiming));
            Log.Info(string.Format("Right hand overall timing: {0:F1}ms", rightHandOverallTiming));
            Log.Info(string.Format("Centered left hand overall timing: {0:F1}ms", centeredLeftHandOverallTiming));
            Log.Info(string.Format("Centered right hand overall timing: {0:F1}ms", centeredRightHandOverallTiming));
            Log.Info(string.Format("Left hand unstable rate: {0:F1}ms^2", leftHandUnstableRate));
            Log.Info(string.Format("Right hand unstable rate: {0:F1}ms^2", rightHandUnstableRate));

            string rowTimings;
            Log.Info("-");
            Log.Info("Left hand timings relative to average: ");
            for (int i = 0; i < 3; i++) {
                rowTimings = "";
                for (int j = 0; j < 4; j++) {
                    if (leftHandAverageTiming[i, j] == 0.0f)
                        rowTimings += "N/A ";
                    else
                        rowTimings += string.Format("{0:F1} ", leftHandAverageTiming[i, j] - leftHandOverallTiming);
                }
                Log.Info(rowTimings);
            }
            Log.Info("-");
            Log.Info("Right hand timings relative to average (mirrored): ");
            for (int i = 0; i < 3; i++) {
                rowTimings = "";
                for (int j = 3; j >= 0; j--) {
                    if (rightHandAverageTiming[i, j] == 0.0f)
                        rowTimings += "N/A ";
                    else
                        rowTimings += string.Format("{0:F1} ", rightHandAverageTiming[i, j] - rightHandOverallTiming);
                }
                Log.Info(rowTimings);
            }
            Log.Info("-");
            Log.Info("Centered Left hand timings relative to average: ");
            for (int i = 0; i < 3; i++)
            {
                rowTimings = "";
                for (int j = 0; j < 3; j++) {
                    if (centeredLeftHandAverageTiming[i, j] == 0.0f)
                        rowTimings += "N/A ";
                    else
                        rowTimings += string.Format("{0:F1} ", centeredLeftHandAverageTiming[i, j] - centeredLeftHandOverallTiming);
                }
                Log.Info(rowTimings);
            }
            Log.Info("-");
            Log.Info("Centered right hand timings relative to average (mirrored): ");
            for (int i = 0; i < 3; i++)
            {
                rowTimings = "";
                for (int j = 2; j >= 0; j--) {
                    if (centeredRightHandAverageTiming[i, j] == 0.0f)
                        rowTimings += "N/A ";
                    else
                        rowTimings += string.Format("{0:F1} ", centeredRightHandAverageTiming[i, j] - centeredRightHandOverallTiming);
                }
                Log.Info(rowTimings);
            }
        }
    }
}
