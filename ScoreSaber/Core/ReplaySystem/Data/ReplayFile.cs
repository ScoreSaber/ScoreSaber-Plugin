using System;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable IDE1006 // Naming Styles
namespace ScoreSaber.Core.ReplaySystem.Data
{
    internal class ReplayFile
    {
        internal Metadata metadata;
        internal List<VRPoseGroup> poseKeyframes;
        internal List<HeightEvent> heightKeyframes;
        internal List<NoteEvent> noteKeyframes;
        internal List<ScoreEvent> scoreKeyframes;
        internal List<ComboEvent> comboKeyframes;
        internal List<MultiplierEvent> multiplierKeyframes;
        internal List<EnergyEvent> energyKeyframes;

        internal ReplayFile() {

            poseKeyframes = new List<VRPoseGroup>();
            heightKeyframes = new List<HeightEvent>();
            noteKeyframes = new List<NoteEvent>();
            scoreKeyframes = new List<ScoreEvent>();
            comboKeyframes = new List<ComboEvent>();
            multiplierKeyframes = new List<MultiplierEvent>();
            energyKeyframes = new List<EnergyEvent>();
        }
    }

    internal struct Metadata
    {
        internal Version Version;
        internal string LevelID;
        internal int Difficulty;
        internal string Characteristic;
        internal string Environment;
        internal string[] Modifiers;
        internal float NoteSpawnOffset;
        internal bool LeftHanded;
        internal float InitialHeight;
        internal float RoomRotation;
        internal VRPosition RoomCenter;
        internal float FailTime;
        internal Hive.Versioning.Version GameVersion;
        internal Version PluginVersion;
        internal string Platform; // Quest or PC
    };

    internal struct ScoreEvent
    {
        public int Score;
        public float Time;
        public int? ImmediateMaxPossibleScore;
    };

    internal struct ComboEvent
    {
        internal int Combo;
        internal float Time;
    };

    internal struct NoteEvent
    {
        internal NoteID NoteID;
        internal NoteEventType EventType;
        internal VRPosition CutPoint;
        internal VRPosition CutNormal;
        internal VRPosition SaberDirection;
        internal int SaberType;
        internal bool DirectionOK;
        internal float SaberSpeed;
        internal float CutAngle;
        internal float CutDistanceToCenter;
        internal float CutDirectionDeviation;
        internal float BeforeCutRating;
        internal float AfterCutRating;
        internal float Time;
        internal float UnityTimescale;
        internal float TimeSyncTimescale;

        // stored but not replayed
        internal float? TimeDeviation;
        internal VRRotation? WorldRotation;
        internal VRRotation? InverseWorldRotation;
        internal VRRotation? NoteRotation;
        internal VRPosition? NotePosition;
    };

    internal enum NoteEventType
    {
        None,
        GoodCut,
        BadCut,
        Miss,
        Bomb
    }

    internal enum ScoringType_pre1_40 {
        Ignore = -1,
        NoScore,
        Normal,
        SliderHead,
        SliderTail,
        BurstSliderHead,
        BurstSliderElement
    }

    internal static class RelevantGameVersions {
        public static readonly Hive.Versioning.Version Version_1_40 = new Hive.Versioning.Version("1.40.0");
    }

    internal struct NoteID : IEquatable<NoteID>
    {
        internal float Time;
        internal int LineLayer;
        internal int LineIndex;
        internal int ColorType;
        internal int CutDirection;
        internal int? GameplayType;
        internal int? ScoringType;
        internal float? CutDirectionAngleOffset;

        public static bool operator ==(NoteID a, NoteID b) {
            return Mathf.Approximately(a.Time, b.Time) && a.LineIndex == b.LineIndex && a.LineLayer == b.LineLayer && a.ColorType == b.ColorType && a.CutDirection == b.CutDirection
                && a.GameplayType == b.GameplayType && a.ScoringType == b.ScoringType && a.CutDirectionAngleOffset == b.CutDirectionAngleOffset;
        }

        public static bool operator !=(NoteID a, NoteID b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            return Time.GetHashCode() ^ LineLayer ^ LineIndex;
        }

        public override bool Equals(object obj) {
            return Equals((NoteID)obj);
        }

        public bool Equals(NoteID other) {
            return this == other;
        }

        internal bool MatchesScoringType(NoteData.ScoringType comparedScoringType, Hive.Versioning.Version gameVersion) {
            if (ScoringType is int scoringType) {
                if(gameVersion == null || gameVersion < RelevantGameVersions.Version_1_40) {
                    switch((ScoringType_pre1_40)scoringType) {
                        case ScoringType_pre1_40.Ignore: return comparedScoringType == NoteData.ScoringType.Ignore;
                        case ScoringType_pre1_40.NoScore: return comparedScoringType == NoteData.ScoringType.NoScore;
                        case ScoringType_pre1_40.Normal: return comparedScoringType == NoteData.ScoringType.Normal;
                        case ScoringType_pre1_40.SliderHead:
                            if (comparedScoringType == NoteData.ScoringType.ArcHeadArcTail) return true;
                            if (comparedScoringType == NoteData.ScoringType.ChainLinkArcHead) return true;
                            return comparedScoringType == NoteData.ScoringType.ArcHead;
                        case ScoringType_pre1_40.SliderTail:
                            if (comparedScoringType == NoteData.ScoringType.ArcHeadArcTail) return true;
                            if (comparedScoringType == NoteData.ScoringType.ChainHeadArcTail) return true;
                            return comparedScoringType == NoteData.ScoringType.ArcTail;
                        case ScoringType_pre1_40.BurstSliderHead:
                            if (comparedScoringType == NoteData.ScoringType.ChainHeadArcTail) return true;
                            return comparedScoringType == NoteData.ScoringType.ChainHead;
                        case ScoringType_pre1_40.BurstSliderElement:
                            if (comparedScoringType == NoteData.ScoringType.ChainLinkArcHead) return true;
                            return comparedScoringType == NoteData.ScoringType.ChainLink;
                    }
                }

                // if it's none of the special versions handled above the scoring types should be compatible to our current scoring type.
                return scoringType == (int)comparedScoringType;
            }
            return true;
        }
    };

    internal struct EnergyEvent
    {
        internal float Energy;
        internal float Time;
    };

    internal struct HeightEvent
    {
        internal float Height;
        internal float Time;
    };

    internal struct MultiplierEvent
    {
        internal int Multiplier;
        internal float NextMultiplierProgress;
        internal float Time;
    };

    internal struct VRPoseGroup
    {
        internal VRPose Head;
        internal VRPose Left;
        internal VRPose Right;
        internal int FPS;
        internal float Time;
    };

    internal struct VRPose
    {
        internal VRPosition Position;
        internal VRRotation Rotation;
    };

    internal struct VRPosition
    {
        internal float X;
        internal float Y;
        internal float Z;

        internal static VRPosition None() {
            return new VRPosition() { X = 0, Y = 0, Z = 0 };
        }
    };

    internal struct VRRotation
    {
        internal float X;
        internal float Y;
        internal float Z;
        internal float W;
    };
}
