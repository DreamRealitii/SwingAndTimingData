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
    public class SwingData : MonoBehaviour
    {
        public static SwingData Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        // Positions are relative to the middle row and center columns, non-vertical swing rotations are rotated to line up with vertical swings.
        // Units are in centimeters and degrees.
        private static List<Vector3>[,] leftSaberPos = new List<Vector3>[3, 3], rightSaberPos = new List<Vector3>[3, 3], leftSaberRot = new List<Vector3>[3, 3], rightSaberRot = new List<Vector3>[3, 3];
        private static Vector3[,] averageLeftSaberPos = new Vector3[3, 3], averageRightSaberPos = new Vector3[3, 3], averageLeftSaberRot = new Vector3[3, 3], averageRightSaberRot = new Vector3[3, 3];
        private static Vector3 overallLeftSaberPos, overallRightSaberPos, overallLeftSaberRot, overallRightSaberRot;
        private static List<float> leftSaberRotZ, rightSaberRotZ; // Instead of saying how far forward the saber is pointing for Z rotation, give directional deviance.
        private static float overallLeftSaberRotZ, overallRightSaberRotZ;
        private static int[] prevLeftNotePos = new int[2], prevRightNotePos = new int[2];
        private static bool levelEnded = false; // Because BSEvents.LevelFinished sometimes gets played more than once.

        // These methods are automatically called by Unity, you should remove any you aren't using.
        #region Monobehaviour Messages
        /// <summary>
        /// Only ever called once, mainly used to initialize variables.
        /// </summary>
        private void Awake() {
            // For this particular MonoBehaviour, we only want one instance to exist at any time, so store a reference to it in a static property
            //   and destroy any that are created while one already exists.
            Log = Plugin.Log;
            if (Instance != null)
            {
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
                for (int j = 0; j < 3; j++) {
                    leftSaberPos[i, j] = new List<Vector3>();
                    rightSaberPos[i, j] = new List<Vector3>();
                    leftSaberRot[i, j] = new List<Vector3>();
                    rightSaberRot[i, j] = new List<Vector3>();
                }
            }
            leftSaberRotZ = new List<float>();
            rightSaberRotZ = new List<float>();
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
            AddSwingData(nci);
        }

        private void OnLevelEnd(object o, LevelFinishedEventArgs lfea) {
            if (levelEnded == false) {
                CalculateAverageData();
                PrintSwingData();
                ClearSwingData();
                levelEnded = true;
            }
        }

        private static void ClearSwingData() {
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 3; j++) {
                    leftSaberPos[i, j].Clear();
                    rightSaberPos[i, j].Clear();
                    leftSaberRot[i, j].Clear();
                    rightSaberRot[i, j].Clear();
                }
            }
            prevLeftNotePos[0] = 0; prevLeftNotePos[1] = 1; prevRightNotePos[0] = 0; prevRightNotePos[1] = 2;
            leftSaberRotZ.Clear();
            rightSaberRotZ.Clear();
        }

        // Only adds centered swings, so very inefficient.
        private static void AddSwingData(NoteCutInfo nci) {
            // Get note position and player height.
            int row, col;
            switch (nci.noteData.noteLineLayer) {
                case NoteLineLayer.Top: row = 0; break;
                case NoteLineLayer.Upper: row = 1; break;
                case NoteLineLayer.Base: row = 2; break;
                default: row = 0; break;
            }
            col = nci.noteData.lineIndex;

            // Get relative saber position and rotation.
            Vector3 saberPos = nci.saberMovementData.lastAddedData.bottomPos;
            Vector3 centerPos = new Vector3(nci.saberType == SaberType.SaberA ? -0.3f : 0.3f, CalculateMidRowHeight(row, nci.notePosition.y));
            saberPos = (saberPos - centerPos) * 100f;
            Vector3 saberRot = nci.saberMovementData.lastAddedData.topPos - nci.saberMovementData.lastAddedData.bottomPos;
            float saberRotZ = Vector2.SignedAngle(nci.notePosition - centerPos, saberRot);
            if (nci.saberType == SaberType.SaberA)
                saberRot = CalculateUpwardsSwingRotation(saberRot, nci.saberType, row, col).normalized * 90f;
            else saberRot = CalculateUpwardsSwingRotation(saberRot, nci.saberType, row, 3 - col).normalized * 90f;

            // Add swing data to correct lists.
            if (nci.saberType == SaberType.SaberA) {
                if (IsCenteredNote(row, col, nci.noteData.cutDirection) && IsOppositePosition(nci.saberType, row, col)) {
                    leftSaberPos[row, col].Add(saberPos);
                    leftSaberRot[row, col].Add(saberRot);
                    leftSaberRotZ.Add(saberRotZ);
                }
            }
            else {
                if (IsCenteredNote(row, 3 - col, nci.noteData.cutDirection.Mirrored()) && IsOppositePosition(nci.saberType, row, 3 - col)) {
                    rightSaberPos[row, col - 1].Add(saberPos);
                    rightSaberRot[row, col - 1].Add(saberRot);
                    rightSaberRotZ.Add(saberRotZ);
                }
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
        // Sets prev____NotePos to current note position.
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

        private static float CalculateMidRowHeight(int row, float noteHeight) {
            if (row == 0) return noteHeight - 0.6f;
            if (row == 2) return noteHeight + 0.55f;
            return noteHeight;
        }

        // Rotates saber rotation around the z axis so all swings are vertical and can be averaged together.
        private static Vector3 CalculateUpwardsSwingRotation(Vector3 rotation, SaberType type, int row, int col) {
            double correctionAngle = -Math.PI / 2;
            if (type == SaberType.SaberA)
                correctionAngle += Math.Atan2(-(row - prevLeftNotePos[0]), col - prevLeftNotePos[1]);
            else correctionAngle += Math.Atan2(-(row - prevRightNotePos[0]), col - prevRightNotePos[1]);
            // Negate rotation if swing is downwards.
            if (row + col >= 2 && !(row == 0 && col == 2))
                correctionAngle += Math.PI;
            return new Vector3((float)((rotation.x * Math.Cos(correctionAngle)) - (rotation.y * Math.Sin(correctionAngle))),
                               (float)((rotation.x * Math.Sin(correctionAngle)) + (rotation.y * Math.Cos(correctionAngle))), rotation.z);
        }

        private static void CalculateAverageData() {
            overallLeftSaberPos = AverageSwingCalculator(leftSaberPos, averageLeftSaberPos, false);
            overallRightSaberPos = AverageSwingCalculator(rightSaberPos, averageRightSaberPos, false);
            overallLeftSaberRot = AverageSwingCalculator(leftSaberRot, averageLeftSaberRot, true);
            overallRightSaberRot = AverageSwingCalculator(rightSaberRot, averageRightSaberRot, true);
            float leftRotZSum = 0f, rightRotZSum = 0f;
            foreach (float f in leftSaberRotZ)
                leftRotZSum += f;
            foreach (float f in rightSaberRotZ)
                rightRotZSum += f;
            overallLeftSaberRotZ = leftRotZSum / leftSaberRotZ.Count;
            overallRightSaberRotZ = rightRotZSum / rightSaberRotZ.Count;
        }

        // Sets average timing values for each position in averageResults, returns average for all positions.
        private static Vector3 AverageSwingCalculator(List<Vector3>[,] swings, Vector3[,] averageResults, bool rotations) {
            int centeredPositionsWithData = 0;
            Vector3 positionSum;
            for (int i = 0; i < swings.GetLength(0); i++) {
                for (int j = 0; j < swings.GetLength(1); j++) {
                    positionSum = new Vector3(0f, 0f, 0f);
                    foreach (Vector3 n in swings[i, j])
                        positionSum += n;
                    if (swings[i, j].Count > 0 && HasOppositeData(swings, i, j)) {
                        averageResults[i, j] = positionSum / swings[i, j].Count;
                        centeredPositionsWithData++;
                    }
                    else averageResults[i, j] = Vector3.zero;
                }
            }
            Vector3 totalSum = new Vector3(0f, 0f, 0f);
            for (int i = 0; i < swings.GetLength(0); i++)
                for (int j = 0; j < swings.GetLength(1); j++)
                    totalSum += averageResults[i, j];
            if (rotations)
                totalSum = totalSum.normalized * 90f;
            else totalSum /= centeredPositionsWithData;
            return totalSum;
        }

        // Data needs to exist on both sides of the center or else overall results will be heavily skewed.
        private static bool HasOppositeData(List<Vector3>[,] swings, int row, int col) {
            if (row == 0 && col == 0) return swings[2, 2].Count > 0;
            if (row == 0 && col == 1) return swings[2, 1].Count > 0;
            if (row == 0 && col == 2) return swings[2, 0].Count > 0;
            if (row == 1 && col == 0) return swings[1, 2].Count > 0;
            if (row == 1 && col == 2) return swings[1, 0].Count > 0;
            if (row == 2 && col == 0) return swings[0, 2].Count > 0;
            if (row == 2 && col == 1) return swings[0, 1].Count > 0;
            if (row == 2 && col == 2) return swings[0, 0].Count > 0;
            return false;
        }

        private static void PrintSwingData() {
            Log.Info("Printing Swing Data:");
            Log.Info(String.Format("Left Hand Overall Position:   X:  {0,6:F2}cm, Y:  {1,6:F2}cm, Z: {2,6:F2}cm", overallLeftSaberPos.x, overallLeftSaberPos.y, overallLeftSaberPos.z));
            Log.Info(String.Format("Right Hand Overall Position:  X:  {0,6:F2}cm, Y:  {1,6:F2}cm, Z: {2,6:F2}cm", overallRightSaberPos.x, overallRightSaberPos.y, overallRightSaberPos.z));
            Log.Info(String.Format("Left Hand Overall Direction:  X: {0,6:F2}deg, Y: {1,6:F2}deg, Z: {2,6:F2}deg", overallLeftSaberRot.x, overallLeftSaberRot.y, overallLeftSaberRotZ));
            Log.Info(String.Format("Right Hand Overall Direction: X: {0,6:F2}deg, Y: {1,6:F2}deg, Z: {2,6:F2}deg", overallRightSaberRot.x, overallRightSaberRot.y, overallRightSaberRotZ));
            Log.Info("- -");
        }
    }
}
