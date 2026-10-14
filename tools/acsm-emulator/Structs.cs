// Structs.cs: the four Assetto Corsa shared-memory pages, laid out exactly as SimHub reads them.
//
// Copied field for field from SimHub 9.12.6's ACSharedMemory.dll (ACSharedMemory.Physics, Graphics,
// StaticInfo and ACSharedMemory.MMFModels.SPageFileCrewChief, decompiled with ilspycmd), including the
// attributes, because the emulator writes a page with Marshal.StructureToPtr and SimHub reads it with
// Marshal.PtrToStructure: the same declaration on both sides is what makes the bytes agree. Sequential,
// Pack = 4, Unicode strings, and a C# bool marshalled as a four-byte BOOL.
//
// Do not reorder, rename or retype a field. SelfCheck.cs holds the sizes and a few offsets to the
// numbers worked out by hand from these declarations, so an edit that moves a byte fails there.
using System;
using System.Runtime.InteropServices;

namespace AcsmEmulator
{
    public enum AC_STATUS
    {
        AC_OFF,
        AC_REPLAY,
        AC_LIVE,
        AC_PAUSE
    }

    public enum AC_SESSION_TYPE
    {
        AC_UNKNOWN = -1,
        AC_PRACTICE,
        AC_QUALIFY,
        AC_RACE,
        AC_HOTLAP,
        AC_TIME_ATTACK,
        AC_DRIFT,
        AC_DRAG
    }

    /// <summary>One value at a time: AC shows a single flag, and SimHub compares Graphics.Flags for equality.</summary>
    public enum AC_FLAGS
    {
        AC_NO_FLAG = 0,
        AC_BLUE_FLAG = 1,
        AC_YELLOW_FLAG = 2,
        AC_BLACK_FLAG = 3,
        AC_WHITE_FLAG = 4,
        AC_CHECKERED_FLAG = 5,
        AC_PENALTY_FLAG = 6
    }

    public struct Coordinates
    {
        public float X;
        public float Y;
        public float Z;
    }

    /// <summary><c>Local\acpmf_physics</c>.</summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    public struct Physics
    {
        public int PacketId;
        public float Gas;
        public float Brake;
        public float Fuel;
        public int Gear;
        public int Rpms;
        public float SteerAngle;
        public float SpeedKmh;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] Velocity;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] AccG;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] WheelSlip;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] WheelLoad;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] WheelsPressure;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] WheelAngularSpeed;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreWear;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreDirtyLevel;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreCoreTemperature;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] CamberRad;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] SuspensionTravel;
        public float Drs;
        public float TC;
        public float Heading;
        public float Pitch;
        public float Roll;
        public float CgHeight;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)] public float[] CarDamage;
        public int NumberOfTyresOut;
        public int PitLimiterOn;
        public float Abs;
        public float KersCharge;
        public float KersInput;
        public int AutoShifterOn;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] RideHeight;
        public float Turbo;
        public float Ballast;
        public float AirDensity;
        public float AirTemp;
        public float RoadTemp;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] LocalAngularVelocity;
        public float FinalFF;
        public float PerformanceMeter;
        public int EngineBrake;
        public int ErsRecoveryLevel;
        public int ErsPowerLevel;
        public int ErsHeatCharging;
        public int ErsisCharging;
        public float KersCurrentKJ;
        public int DrsAvailable;
        public int DrsEnabled;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] BrakeTemp;
        public float Clutch;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreTempI;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreTempM;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreTempO;
        public int IsAIControlled;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public Coordinates[] TyreContactPoint;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public Coordinates[] TyreContactNormal;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public Coordinates[] TyreContactHeading;
        public float BrakeBias;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] LocalVelocity;
    }

    /// <summary><c>Local\acpmf_graphics</c>.</summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    public struct Graphics
    {
        public int PacketId;
        public AC_STATUS Status;
        public AC_SESSION_TYPE Session;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string CurrentTime;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string LastTime;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string BestTime;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string Split;
        public int CompletedLaps;
        public int Position;
        public int iCurrentTime;
        public int iLastTime;
        public int iBestTime;
        public float SessionTimeLeft;
        public float DistanceTraveled;
        public int IsInPit;
        public int CurrentSectorIndex;
        public int LastSectorTime;
        public int NumberOfLaps;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string TyreCompound;
        public float ReplayTimeMultiplier;
        public float NormalizedCarPosition;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] CarCoordinates;
        public float PenaltyTime;
        public AC_FLAGS Flags;
        public int idealLineOn;
        public int isInPitLane;
        public float surfaceGrip;
        public int MandatoryPitDone;
    }

    /// <summary><c>Local\acpmf_static</c>.</summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    public struct StaticInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string SMVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)] public string ACVersion;
        public int NumberOfSessions;
        public int NumCars;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string CarModel;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string Track;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string PlayerName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string PlayerSurname;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string PlayerNick;
        public int SectorCount;
        public float MaxTorque;
        public float MaxPower;
        public int MaxRpm;
        public float MaxFuel;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] SuspensionMaxTravel;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] TyreRadius;
        public float MaxTurboBoost;
        public float Deprecated1;
        public float Deprecated2;
        public int PenaltiesEnabled;
        public float AidFuelRate;
        public float AidTireRate;
        public float AidMechanicalDamage;
        public bool AidAllowTyreBlankets;
        public float AidStability;
        public bool AidAutoClutch;
        public bool AidAutoBlip;
        public int HasDRS;
        public int HasERS;
        public int HasKERS;
        public float KersMaxJ;
        public int EngineBrakeSettingsCount;
        public int ErsPowerControllerCount;
        public float TrackSPlineLength;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string TrackConfiguration;
        public float ErsMaxJ;
        public int IsTimedRace;
        public int HasExtraLap;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string carSkin;
        public int ReversedGridPositions;
        public int PitWindowStart;
        public int PitWindowEnd;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct acsVec3
    {
        public float x;
        public float y;
        public float z;
    }

    /// <summary>One car of SimHub's own AC app page.</summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct acsVehicleInfo
    {
        public int carId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] driverName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] carModel;
        public float speedMS;
        public int bestLapMS;
        public int lapCount;
        public int currentLapInvalid;
        public int currentLapTimeMS;
        public int lastLapTimeMS;
        public acsVec3 worldPosition;
        public int isCarInPitline;
        public int isCarInPit;
        public int carLeaderboardPosition;
        public int carRealTimeLeaderboardPosition;
        public float spLineLength;
        public int isConnected;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] suspensionDamage;
        public float engineLifeLeft;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreInflation;
    }

    /// <summary>
    /// <c>Local\acpmf_simhub_v2</c>, which SimHub's own Python app writes from inside AC (the CrewChief layout). It
    /// is the only source of opponents SimHub's AC reader has; without it the leaderboard is the player alone.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SPageFileCrewChief
    {
        public int numVehicles;
        public int focusVehicle;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)] public byte[] serverName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public acsVehicleInfo[] vehicle;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)] public byte[] acInstallPath;
        public int isInternalMemoryModuleLoaded;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] pluginVersion;
    }
}
