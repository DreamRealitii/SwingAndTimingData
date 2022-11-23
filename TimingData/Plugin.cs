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
        private const float minHitboxDistance = 0.559f, maxHitboxDistance = 0.640f; // Distance in meters from yz/xz hitbox to center.

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
        }


        private void OnNoteCut(NoteController nc, NoteCutInfo nci) {
            AddTimingData(nci);
        }

        private void OnLevelEnd(object o, LevelFinishedEventArgs lfea) {
            CalculateAverageData();
            PrintTimingData();
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
            float time = (nci.timeDeviation - (Mathf.Lerp(minHitboxDistance, maxHitboxDistance, nci.cutDirDeviation / 90) / nci.saberSpeed)) * 1000f;
            if (nci.saberType == SaberType.SaberA) {
                leftHandTimings[row, col].Add(time);
                if (IsCenteredNote(nci.saberType, row, col, nci.noteData.cutDirection))
                    centeredLeftHandTimings[row, col].Add(time);
            }
            else {
                rightHandTimings[row, col].Add((nci.timeDeviation - (minHitboxDistance / nci.saberSpeed)) * 1000f);
                if (IsCenteredNote(nci.saberType, row, col, nci.noteData.cutDirection))
                    centeredRightHandTimings[row, col-1].Add(time);
            }
        }

        // Tells if the note can be cut straight from the hand's position (excluding vision-blocking notes).
        private static Boolean IsCenteredNote(SaberType st, int row, int col, NoteCutDirection ncd) {
            if (st == SaberType.SaberA && row == 0 && col == 0 && (ncd == NoteCutDirection.Left || ncd == NoteCutDirection.UpLeft || ncd == NoteCutDirection.Up)) return true;
            if (st == SaberType.SaberA && row == 0 && col == 1 && (ncd == NoteCutDirection.UpLeft || ncd == NoteCutDirection.Up || ncd == NoteCutDirection.UpRight)) return true;
            if (st == SaberType.SaberA && row == 0 && col == 2 && (ncd == NoteCutDirection.Up || ncd == NoteCutDirection.UpRight || ncd == NoteCutDirection.Right)) return true;
            if (st == SaberType.SaberA && row == 1 && col == 0 && (ncd == NoteCutDirection.DownLeft || ncd == NoteCutDirection.Left || ncd == NoteCutDirection.UpLeft)) return true;
            if (st == SaberType.SaberA && row == 1 && col == 2 && (ncd == NoteCutDirection.UpRight || ncd == NoteCutDirection.Right || ncd == NoteCutDirection.DownRight)) return true;
            if (st == SaberType.SaberA && row == 2 && col == 0 && (ncd == NoteCutDirection.Down || ncd == NoteCutDirection.DownLeft || ncd == NoteCutDirection.Left)) return true;
            if (st == SaberType.SaberA && row == 2 && col == 1 && (ncd == NoteCutDirection.DownRight || ncd == NoteCutDirection.Down || ncd == NoteCutDirection.DownLeft)) return true;
            if (st == SaberType.SaberA && row == 2 && col == 2 && (ncd == NoteCutDirection.Right || ncd == NoteCutDirection.DownRight || ncd == NoteCutDirection.Down)) return true;
            if (st == SaberType.SaberB && row == 0 && col == 1 && (ncd == NoteCutDirection.Left || ncd == NoteCutDirection.UpLeft || ncd == NoteCutDirection.Up)) return true;
            if (st == SaberType.SaberB && row == 0 && col == 2 && (ncd == NoteCutDirection.UpLeft || ncd == NoteCutDirection.Up || ncd == NoteCutDirection.UpRight)) return true;
            if (st == SaberType.SaberB && row == 0 && col == 3 && (ncd == NoteCutDirection.Up || ncd == NoteCutDirection.UpRight || ncd == NoteCutDirection.Right)) return true;
            if (st == SaberType.SaberB && row == 1 && col == 1 && (ncd == NoteCutDirection.DownLeft || ncd == NoteCutDirection.Left || ncd == NoteCutDirection.UpLeft)) return true;
            if (st == SaberType.SaberB && row == 1 && col == 3 && (ncd == NoteCutDirection.UpRight || ncd == NoteCutDirection.Right || ncd == NoteCutDirection.DownRight)) return true;
            if (st == SaberType.SaberB && row == 2 && col == 1 && (ncd == NoteCutDirection.Down || ncd == NoteCutDirection.DownLeft || ncd == NoteCutDirection.Left)) return true;
            if (st == SaberType.SaberB && row == 2 && col == 2 && (ncd == NoteCutDirection.DownRight || ncd == NoteCutDirection.Down || ncd == NoteCutDirection.DownLeft)) return true;
            if (st == SaberType.SaberB && row == 2 && col == 3 && (ncd == NoteCutDirection.Right || ncd == NoteCutDirection.DownRight || ncd == NoteCutDirection.Down)) return true;
            return false;
        }

        private static void CalculateAverageData() {
            // Average timing for each note position.
            float leftSum, rightSum;
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 4; j++) {
                    leftSum = 0f; rightSum = 0f;
                    foreach (float n in leftHandTimings[i, j])
                        leftSum += n;
                    foreach (float n in rightHandTimings[i, j])
                        rightSum += n;
                    if (leftHandTimings[i, j].Count > 0)
                        leftHandAverageTiming[i, j] = leftSum / leftHandTimings[i, j].Count;
                    if (rightHandTimings[i, j].Count > 0)
                        rightHandAverageTiming[i, j] = rightSum / rightHandTimings[i, j].Count;
                }
            }
            // Average timing for centered notes.
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 3; j++) {
                    leftSum = 0f; rightSum = 0f;
                    foreach (float n in centeredLeftHandTimings[i, j])
                        leftSum += n;
                    foreach (float n in centeredRightHandTimings[i, j])
                        rightSum += n;
                    if (centeredLeftHandTimings[i, j].Count > 0)
                        centeredLeftHandAverageTiming[i, j] = leftSum / centeredLeftHandTimings[i, j].Count;
                    if (centeredRightHandTimings[i, j].Count > 0)
                        centeredRightHandAverageTiming[i, j] = rightSum / centeredRightHandTimings[i, j].Count;
                }
            }
            // Average timing overall.
            leftSum = 0f; rightSum = 0f;
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 4; j++) {
                    leftSum += leftHandAverageTiming[i, j];
                    rightSum += rightHandAverageTiming[i, j];
                }
            }
            leftHandOverallTiming = leftSum / 12;
            rightHandOverallTiming = rightSum / 12;
            // Average centered timing.
            leftSum = 0f; rightSum = 0f;
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 3; j++) {
                    leftSum += centeredLeftHandAverageTiming[i, j];
                    rightSum += centeredRightHandAverageTiming[i, j];
                }
            }
            centeredLeftHandOverallTiming = leftSum / 8;
            centeredRightHandOverallTiming = rightSum / 8;
            // Average unstable rate.
            leftSum = 0f;
            rightSum = 0f;
            int leftCount = 0, rightCount = 0;
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
            Log.Info(string.Format("Left hand overall timing: {0:F1}", leftHandOverallTiming));
            Log.Info(string.Format("Right hand overall timing: {0:F1}", rightHandOverallTiming));
            Log.Info(string.Format("Centered left hand overall timing: {0:F1}", centeredLeftHandOverallTiming));
            Log.Info(string.Format("Centered right hand overall timing: {0:F1}", centeredRightHandOverallTiming));
            Log.Info(string.Format("Left hand unstable rate: {0:F1}", leftHandUnstableRate));
            Log.Info(string.Format("Right hand unstable rate: {0:F1}", rightHandUnstableRate));

            string rowTimings;
            Log.Info("-");
            Log.Info("Left hand timings minus average: ");
            for (int i = 0; i < 3; i++) {
                rowTimings = "";
                for (int j = 0; j < 4; j++)
                    rowTimings += string.Format("{0:F1} ", (leftHandAverageTiming[i, j] - leftHandOverallTiming));
                Log.Info(rowTimings);
            }
            Log.Info("-");
            Log.Info("Right hand timings minus average: ");
            for (int i = 0; i < 3; i++) {
                rowTimings = "";
                for (int j = 0; j < 4; j++)
                    rowTimings += string.Format("{0:F1} ", (rightHandAverageTiming[i, j] - rightHandOverallTiming));
                Log.Info(rowTimings);
            }
            Log.Info("-");
            Log.Info("Centered Left hand timings minus average: ");
            for (int i = 0; i < 3; i++)
            {
                rowTimings = "";
                for (int j = 0; j < 3; j++)
                    rowTimings += string.Format("{0:F1} ", (centeredLeftHandAverageTiming[i, j] - centeredLeftHandOverallTiming));
                Log.Info(rowTimings);
            }
            Log.Info("-");
            Log.Info("Centered right hand timings minus average: ");
            for (int i = 0; i < 3; i++)
            {
                rowTimings = "";
                for (int j = 0; j < 3; j++)
                    rowTimings += string.Format("{0:F1} ", (centeredRightHandAverageTiming[i, j] - centeredRightHandOverallTiming));
                Log.Info(rowTimings);
            }
        }
    }
}
