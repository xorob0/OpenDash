using System;
using System.Collections.Generic;
using System.Globalization;

namespace IrsdkEmulator
{
    /// <summary>
    /// Built-in catalogue of iRacing telemetry variables: type, count, unit, default value and description
    /// (names/types/units follow the live sim's var headers). A scenario can pull the whole catalogue in
    /// with "includeCatalog": true and only override values, or define variables one by one.
    /// Every variable that SimHub's IRacingManager / iRacingSDK.Telemetry reads is present.
    /// </summary>
    public static class Catalog
    {
        public sealed class Entry
        {
            public VarDef Def;
            public double Default;
        }

        // name|type|count|unit|default|description
        private const string Table = @"
SessionTime|double|1|s|0|Seconds since session start
SessionTick|int|1||0|Current update number
SessionNum|int|1||0|Session number
SessionState|int|1|irsdk_SessionState|4|Session state
SessionUniqueID|int|1||1|Session ID
SessionFlags|bitField|1|irsdk_Flags|4|Session flags
SessionTimeRemain|double|1|s|604800|Seconds left till session ends
SessionLapsRemain|int|1||32767|Old laps left till session ends use SessionLapsRemainEx
SessionLapsRemainEx|int|1||32767|New improved laps left till session ends
SessionTimeTotal|double|1|s|604800|Total number of seconds in session
SessionLapsTotal|int|1||32767|Total number of laps in session
SessionJokerLapsRemain|int|1||0|Joker laps remaining to be taken
SessionOnJokerLap|bool|1||0|Player is currently completing a joker lap
SessionTimeOfDay|float|1|s|50400|Time of day in seconds
RadioTransmitCarIdx|int|1||-1|The car index of the current person speaking on the radio
RadioTransmitRadioIdx|int|1||-1|The radio index of the current person speaking on the radio
RadioTransmitFrequencyIdx|int|1||-1|The frequency index of the current person speaking on the radio
DisplayUnits|int|1||1|Default units for the user interface 0 = english 1 = metric
DriverMarker|bool|1||0|Driver activated flag
PushToTalk|bool|1||0|Push to talk button state
PushToPass|bool|1||0|Push to pass button state
ManualBoost|bool|1||0|Hybrid manual boost state
ManualNoBoost|bool|1||0|Hybrid manual no boost state
IsOnTrack|bool|1||1|1=Car on track physics running with player in car
IsReplayPlaying|bool|1||0|0=replay not playing  1=replay playing
ReplayFrameNum|int|1||0|Integer replay frame number (60 per second)
ReplayFrameNumEnd|int|1||0|Integer replay frame number from end of tape
IsDiskLoggingEnabled|bool|1||0|0=disk based telemetry turned off  1=turned on
IsDiskLoggingActive|bool|1||0|0=disk based telemetry file not being written  1=being written
FrameRate|float|1|fps|60|Average frames per second
CpuUsageFG|float|1|%|0.35|Percent of available tim fg thread took with a 1 sec avg
GpuUsage|float|1|%|0.5|Percent of available tim gpu took with a 1 sec avg
ChanAvgLatency|float|1|s|0.03|Communication average latency
ChanLatency|float|1|s|0.03|Communication latency
ChanQuality|float|1|%|1|Communication quality
ChanPartnerQuality|float|1|%|1|Partner communication quality
CpuUsageBG|float|1|%|0.2|Percent of available tim bg thread took with a 1 sec avg
ChanClockSkew|float|1|s|0|Communication server clock skew
MemPageFaultSec|float|1||0|Memory page faults per second
MemSoftPageFaultSec|float|1||0|Memory soft page faults per second
PlayerCarPosition|int|1||0|Players position in race
PlayerCarClassPosition|int|1||0|Players class position in race
PlayerCarClass|int|1||0|Player car class id
PlayerTrackSurface|int|1|irsdk_TrkLoc|3|Players car track surface type
PlayerTrackSurfaceMaterial|int|1|irsdk_TrkSurf|1|Players car track surface material type
PlayerCarIdx|int|1||0|Players carIdx
PlayerCarTeamIncidentCount|int|1||0|Players team incident count for this session
PlayerCarMyIncidentCount|int|1||0|Players own incident count for this session
PlayerCarDriverIncidentCount|int|1||0|Teams current drivers incident count for this session
PlayerCarWeightPenalty|float|1|kg|0|Players weight penalty
PlayerCarPowerAdjust|float|1|%|0|Players power adjust
PlayerCarDryTireSetLimit|int|1||0|Players dry tire set limit
PlayerCarTowTime|float|1|s|0|Players car is being towed if time is greater than zero
PlayerCarInPitStall|bool|1||0|Players car is properly in there pitstall
PlayerCarPitSvStatus|int|1|irsdk_PitSvStatus|0|Players car pit service status bits
PlayerTireCompound|int|1||0|Players car current tire compound
PlayerFastRepairsUsed|int|1||0|Players car number of fast repairs used
CarIdxLap|int|64||-1|Laps started by car index
CarIdxLapCompleted|int|64||-1|Laps completed by car index
CarIdxLapDistPct|float|64|%|-1|Percentage distance around lap by car index
CarIdxTrackSurface|int|64|irsdk_TrkLoc|-1|Track surface type by car index
CarIdxTrackSurfaceMaterial|int|64|irsdk_TrkSurf|-1|Track surface material type by car index
CarIdxOnPitRoad|bool|64||0|On pit road between the cones by car index
CarIdxPosition|int|64||0|Cars position in race by car index
CarIdxClassPosition|int|64||0|Cars class position in race by car index
CarIdxClass|int|64||0|Cars class id by car index
CarIdxF2Time|float|64|s|0|Race time behind leader or fastest lap already completed
CarIdxEstTime|float|64|s|0|Estimated time to reach current location on track
CarIdxLastLapTime|float|64|s|-1|Cars last lap time
CarIdxBestLapTime|float|64|s|-1|Cars best lap time
CarIdxBestLapNum|int|64||-1|Cars best lap number
CarIdxTireCompound|int|64||-1|Cars current tire compound
CarIdxQualTireCompound|int|64||-1|Cars Qual tire compound
CarIdxQualTireCompoundLocked|bool|64||0|Cars Qual tire compound is locked-in
CarIdxFastRepairsUsed|int|64||0|How many fast repairs each car has used
CarIdxSessionFlags|bitField|64|irsdk_Flags|0|Session flags for each player
CarIdxPaceLine|int|64||-1|What line cars are pacing in  or -1 if not pacing
CarIdxPaceRow|int|64||-1|What row cars are pacing in  or -1 if not pacing
CarIdxPaceFlags|bitField|64|irsdk_PaceFlags|0|Pacing status flags for each car
CarIdxSteer|float|64|rad|0|Steering wheel angle by car index
CarIdxRPM|float|64|revs/min|0|Engine rpm by car index
CarIdxGear|int|64||0|-1=reverse  0=neutral  1..n=current gear by car index
CarIdxP2P_Status|bool|64||0|Push2Pass active or not
CarIdxP2P_Count|int|64||0|Push2Pass count of usage (or remaining in Race)
PaceMode|int|1|irsdk_PaceMode|4|Are we pacing or not
OnPitRoad|bool|1||0|Is the player car on pit road between the cones
SteeringWheelAngle|float|1|rad|0|Steering wheel angle
Throttle|float|1|%|1|0=off throttle to 1=full throttle
Brake|float|1|%|0|0=brake released to 1=max pedal force
Clutch|float|1|%|1|0=disengaged to 1=fully engaged
Gear|int|1||0|-1=reverse  0=neutral  1..n=current gear
RPM|float|1|revs/min|0|Engine rpm
Lap|int|1||0|Laps started count
LapCompleted|int|1||0|Laps completed count
LapDist|float|1|m|0|Meters traveled from S/F this lap
LapDistPct|float|1|%|0|Percentage distance around lap
RaceLaps|int|1||0|Laps completed in race
LapBestLap|int|1||0|Players best lap number
LapBestLapTime|float|1|s|0|Players best lap time
LapLastLapTime|float|1|s|0|Players last lap time
LapCurrentLapTime|float|1|s|0|Estimate of players current lap time as shown in F3 box
LapLasNLapSeq|int|1||0|Player num consecutive clean laps completed for N average
LapLastNLapTime|float|1|s|0|Player last N average lap time
LapBestNLapLap|int|1||0|Player last lap in best N average lap time
LapBestNLapTime|float|1|s|0|Player best N average lap time
LapDeltaToBestLap|float|1|s|0|Delta time for best lap
LapDeltaToBestLap_DD|float|1|s/s|0|Rate of change of delta time for best lap
LapDeltaToBestLap_OK|bool|1||1|Delta time for best lap is valid
LapDeltaToOptimalLap|float|1|s|0|Delta time for optimal lap
LapDeltaToOptimalLap_DD|float|1|s/s|0|Rate of change of delta time for optimal lap
LapDeltaToOptimalLap_OK|bool|1||1|Delta time for optimal lap is valid
LapDeltaToSessionBestLap|float|1|s|0|Delta time for session best lap
LapDeltaToSessionBestLap_DD|float|1|s/s|0|Rate of change of delta time for session best lap
LapDeltaToSessionBestLap_OK|bool|1||1|Delta time for session best lap is valid
LapDeltaToSessionOptimalLap|float|1|s|0|Delta time for session optimal lap
LapDeltaToSessionOptimalLap_DD|float|1|s/s|0|Rate of change of delta time for session optimal lap
LapDeltaToSessionOptimalLap_OK|bool|1||1|Delta time for session optimal lap is valid
LapDeltaToSessionLastlLap|float|1|s|0|Delta time for session last lap
LapDeltaToSessionLastlLap_DD|float|1|s/s|0|Rate of change of delta time for session last lap
LapDeltaToSessionLastlLap_OK|bool|1||1|Delta time for session last lap is valid
Speed|float|1|m/s|0|GPS vehicle speed
Yaw|float|1|rad|0|Yaw orientation
YawNorth|float|1|rad|0|Yaw orientation relative to north
Pitch|float|1|rad|0|Pitch orientation
Roll|float|1|rad|0|Roll orientation
EnterExitReset|int|1||2|Indicate action the reset key will take 0 enter 1 exit 2 reset
TrackTemp|float|1|C|31.5|Deprecated  set to TrackTempCrew
TrackTempCrew|float|1|C|31.5|Temperature of track measured by crew around track
AirTemp|float|1|C|22.8|Temperature of air at start/finish line
TrackWetness|int|1|irsdk_TrackWetness|1|How wet is the average track surface
Skies|int|1||1|Skies (0=clear/1=p cloudy/2=m cloudy/3=overcast)
AirDensity|float|1|kg/m^3|1.18|Density of air at start/finish line
AirPressure|float|1|Pa|100800|Pressure of air at start/finish line
WindVel|float|1|m/s|2.1|Wind velocity at start/finish line
WindDir|float|1|rad|0.8|Wind direction at start/finish line
RelativeHumidity|float|1|%|0.55|Relative Humidity at start/finish line
FogLevel|float|1|%|0|Fog level at start/finish line
Precipitation|float|1|%|0|Precipitation at start/finish line
SolarAltitude|float|1|rad|0.9|Sun angle above horizon in radians
SolarAzimuth|float|1|rad|3.2|Sun angle clockwise from north in radians
WeatherDeclaredWet|bool|1||0|The steward says rain tires can be used
DCLapStatus|int|1||0|Status of driver change lap requirements
DCDriversSoFar|int|1||1|Number of team drivers who have run a stint
OkToReloadTextures|bool|1||1|True if it is ok to reload car textures at this time
LoadNumTextures|bool|1||0|True if the car_num texture will be loaded
CarLeftRight|int|1|irsdk_CarLeftRight|1|Notify if car is to the left or right of driver
PitsOpen|bool|1||1|True if pit stop is allowed for the current player
VidCapEnabled|bool|1||0|True if video capture system is enabled
VidCapActive|bool|1||0|True if video currently being captured
PitRepairLeft|float|1|s|0|Time left for mandatory pit repairs if repairs are active
PitOptRepairLeft|float|1|s|0|Time left for optional repairs if repairs are active
PitstopActive|bool|1||0|Is the player getting pit stop service
FastRepairUsed|int|1||0|How many fast repairs used so far
FastRepairAvailable|int|1||0|How many fast repairs left  255 is unlimited
LFTiresUsed|int|1||1|How many left front tires used so far
RFTiresUsed|int|1||1|How many right front tires used so far
LRTiresUsed|int|1||1|How many left rear tires used so far
RRTiresUsed|int|1||1|How many right rear tires used so far
LeftTireSetsUsed|int|1||1|How many left tire sets used so far
RightTireSetsUsed|int|1||1|How many right tire sets used so far
FrontTireSetsUsed|int|1||1|How many front tire sets used so far
RearTireSetsUsed|int|1||1|How many rear tire sets used so far
TireSetsUsed|int|1||1|How many tire sets used so far
LFTiresAvailable|int|1||3|How many left front tires are remaining  255 is unlimited
RFTiresAvailable|int|1||3|How many right front tires are remaining  255 is unlimited
LRTiresAvailable|int|1||3|How many left rear tires are remaining  255 is unlimited
RRTiresAvailable|int|1||3|How many right rear tires are remaining  255 is unlimited
LeftTireSetsAvailable|int|1||3|How many left tire sets are remaining  255 is unlimited
RightTireSetsAvailable|int|1||3|How many right tire sets are remaining  255 is unlimited
FrontTireSetsAvailable|int|1||3|How many front tire sets are remaining  255 is unlimited
RearTireSetsAvailable|int|1||3|How many rear tire sets are remaining  255 is unlimited
TireSetsAvailable|int|1||3|How many tire sets are remaining  255 is unlimited
CamCarIdx|int|1||0|Active camera's focus car index
CamCameraNumber|int|1||1|Active camera number
CamGroupNumber|int|1||1|Active camera group number
CamCameraState|bitField|1|irsdk_CameraState|80|State of camera system
IsOnTrackCar|bool|1||1|1=Car on track physics running
IsInGarage|bool|1||0|1=Car in garage physics running
SteeringWheelAngleMax|float|1|rad|7.85|Steering wheel max angle
ShiftPowerPct|float|1|%|0|Friction torque applied to gears when shifting or grinding
ShiftGrindRPM|float|1|RPM|0|RPM of shifter grinding noise
ThrottleRaw|float|1|%|1|Raw throttle input 0=off throttle to 1=full throttle
BrakeRaw|float|1|%|0|Raw brake input 0=brake released to 1=max pedal force
ClutchRaw|float|1|%|1|Raw clutch input 0=disengaged to 1=fully engaged
HandbrakeRaw|float|1|%|0|Raw handbrake input 0=handbrake released to 1=max force
BrakeABSactive|bool|1||0|true if abs is currently reducing brake force pressure
EngineWarnings|bitField|1|irsdk_EngineWarnings|0|Bitfield for warning lights
FuelLevelPct|float|1|%|0|Percent fuel remaining
PitSvFlags|bitField|1|irsdk_PitSvFlags|0|Bitfield of pit service checkboxes
PitSvLFP|float|1|kPa|172|Pit service left front tire pressure
PitSvRFP|float|1|kPa|172|Pit service right front tire pressure
PitSvLRP|float|1|kPa|172|Pit service left rear tire pressure
PitSvRRP|float|1|kPa|172|Pit service right rear tire pressure
PitSvFuel|float|1|l|0|Pit service fuel add amount
PitSvTireCompound|int|1||0|Pit service pending tire compound
SteeringWheelPctTorque|float|1|%|0|Force feedback % max torque on steering shaft unsigned
SteeringWheelPctTorqueSign|float|1|%|0|Force feedback % max torque on steering shaft signed
SteeringWheelPctTorqueSignStops|float|1|%|0|Force feedback % max torque on steering shaft signed stops
SteeringWheelPctSmoothing|float|1|%|0|Force feedback % max smoothing
SteeringWheelPctDamper|float|1|%|0|Force feedback % max damping
SteeringWheelLimiter|float|1|%|0|Force feedback limiter strength limits impacts and oscillation
SteeringWheelMaxForceNm|float|1|N*m|20|Value of strength or max force slider in Nm for FFB
SteeringWheelPeakForceNm|float|1|N*m|-1|Peak torque mapping to direct input units for FFB
SteeringWheelUseLinear|bool|1||0|True if steering wheel force is using linear mode
ShiftIndicatorPct|float|1|%|0|DEPRECATED use DriverCarSLBlinkRPM instead
ReplayPlaySpeed|int|1||1|Replay playback speed
ReplayPlaySlowMotion|bool|1||0|0=not slow motion  1=replay is in slow motion
ReplaySessionTime|double|1|s|0|Seconds since replay session start
ReplaySessionNum|int|1||0|Replay session number
TireLF_RumblePitch|float|1|Hz|0|Players LF Tire Sound rumblestrip pitch
TireRF_RumblePitch|float|1|Hz|0|Players RF Tire Sound rumblestrip pitch
TireLR_RumblePitch|float|1|Hz|0|Players LR Tire Sound rumblestrip pitch
TireRR_RumblePitch|float|1|Hz|0|Players RR Tire Sound rumblestrip pitch
IsGarageVisible|bool|1||0|1=Garage screen is visible
SteeringWheelTorque|float|1|N*m|0|Output torque on steering shaft
VelocityZ|float|1|m/s|0|Z velocity
VelocityY|float|1|m/s|0|Y velocity
VelocityX|float|1|m/s|0|X velocity
YawRate|float|1|rad/s|0|Yaw rate
PitchRate|float|1|rad/s|0|Pitch rate
RollRate|float|1|rad/s|0|Roll rate
VertAccel|float|1|m/s^2|9.81|Vertical acceleration (including gravity)
LatAccel|float|1|m/s^2|0|Lateral acceleration (including gravity)
LongAccel|float|1|m/s^2|0|Longitudinal acceleration (including gravity)
dcStarter|bool|1||0|In car trigger car starter
dcPitSpeedLimiterToggle|bool|1||0|In car pit speed limiter toggle
dcDashPage|float|1||0|In car dash display page adjustment
dcHeadlightFlash|bool|1||0|In car headlight flash toggle
dcToggleWindshieldWipers|bool|1||0|In car turn wipers on or off
dcTriggerWindshieldWipers|bool|1||0|In car momentarily turn on wipers
dcLowFuelAccept|bool|1||0|In car low fuel accept
dpFastRepair|float|1||0|Pitstop fast repair set
dpLFTireChange|float|1||1|Pitstop lf tire change request
dpRFTireChange|float|1||1|Pitstop rf tire change request
dpLRTireChange|float|1||1|Pitstop lr tire change request
dpRRTireChange|float|1||1|Pitstop rr tire change request
dpFuelFill|float|1||1|Pitstop fuel fill flag
dpWindshieldTearoff|float|1||0|Pitstop windshield tearoff
dpFuelAddKg|float|1|kg|40|Pitstop fuel add amount
dpFuelAutoFillEnabled|float|1||0|Pitstop auto fill fuel system enabled
dpFuelAutoFillActive|float|1||0|Pitstop auto fill fuel next stop flag
dpLFTireColdPress|float|1|Pa|172000|Pitstop lf tire cold pressure adjustment
dpRFTireColdPress|float|1|Pa|172000|Pitstop rf cold tire pressure adjustment
dpLRTireColdPress|float|1|Pa|172000|Pitstop lr tire cold pressure adjustment
dpRRTireColdPress|float|1|Pa|172000|Pitstop rr cold tire pressure adjustment
dcBrakeBias|float|1||54.5|In car brake bias adjustment
dcABS|float|1||2|In car abs adjustment
dcTractionControl|float|1||3|In car traction control adjustment
dcFuelMixture|float|1||1|In car fuel mixture adjustment
dcThrottleShape|float|1||1|In car throttle shape adjustment
FuelUsePerHour|float|1|kg/h|0|Engine fuel used instantaneous
Voltage|float|1|V|13.8|Engine voltage
WaterTemp|float|1|C|88|Engine coolant temp
WaterLevel|float|1|l|5|Engine coolant level
FuelPress|float|1|bar|4.2|Engine fuel pressure
OilTemp|float|1|C|102|Engine oil temperature
OilPress|float|1|bar|5.5|Engine oil pressure
OilLevel|float|1|l|7|Engine oil level
ManifoldPress|float|1|bar|1|Engine manifold pressure
FuelLevel|float|1|l|0|Liters of fuel remaining
Engine0_RPM|float|1|revs/min|0|Engine0Engine rpm
RFbrakeLinePress|float|1|bar|0|RF brake line pressure
RFcoldPressure|float|1|kPa|190|RF tire cold pressure  as set in the garage
RFtempCL|float|1|C|85|RF tire left carcass temperature
RFtempCM|float|1|C|85|RF tire middle carcass temperature
RFtempCR|float|1|C|85|RF tire right carcass temperature
RFwearL|float|1|%|0.97|RF tire left percent tread remaining
RFwearM|float|1|%|0.97|RF tire middle percent tread remaining
RFwearR|float|1|%|0.97|RF tire right percent tread remaining
LFbrakeLinePress|float|1|bar|0|LF brake line pressure
LFcoldPressure|float|1|kPa|190|LF tire cold pressure  as set in the garage
LFtempCL|float|1|C|85|LF tire left carcass temperature
LFtempCM|float|1|C|85|LF tire middle carcass temperature
LFtempCR|float|1|C|85|LF tire right carcass temperature
LFwearL|float|1|%|0.97|LF tire left percent tread remaining
LFwearM|float|1|%|0.97|LF tire middle percent tread remaining
LFwearR|float|1|%|0.97|LF tire right percent tread remaining
RRbrakeLinePress|float|1|bar|0|RR brake line pressure
RRcoldPressure|float|1|kPa|190|RR tire cold pressure  as set in the garage
RRtempCL|float|1|C|85|RR tire left carcass temperature
RRtempCM|float|1|C|85|RR tire middle carcass temperature
RRtempCR|float|1|C|85|RR tire right carcass temperature
RRwearL|float|1|%|0.97|RR tire left percent tread remaining
RRwearM|float|1|%|0.97|RR tire middle percent tread remaining
RRwearR|float|1|%|0.97|RR tire right percent tread remaining
LRbrakeLinePress|float|1|bar|0|LR brake line pressure
LRcoldPressure|float|1|kPa|190|LR tire cold pressure  as set in the garage
LRtempCL|float|1|C|85|LR tire left carcass temperature
LRtempCM|float|1|C|85|LR tire middle carcass temperature
LRtempCR|float|1|C|85|LR tire right carcass temperature
LRwearL|float|1|%|0.97|LR tire left percent tread remaining
LRwearM|float|1|%|0.97|LR tire middle percent tread remaining
LRwearR|float|1|%|0.97|LR tire right percent tread remaining
LFshockDefl|float|1|m|0.02|LF shock deflection
LFshockVel|float|1|m/s|0|LF shock velocity
RFshockDefl|float|1|m|0.02|RF shock deflection
RFshockVel|float|1|m/s|0|RF shock velocity
LRshockDefl|float|1|m|0.02|LR shock deflection
LRshockVel|float|1|m/s|0|LR shock velocity
RRshockDefl|float|1|m|0.02|RR shock deflection
RRshockVel|float|1|m/s|0|RR shock velocity
";

        private static List<Entry> _entries;
        private static Dictionary<string, Entry> _byName;

        public static IList<Entry> Entries
        {
            get { EnsureLoaded(); return _entries; }
        }

        public static Entry Find(string name)
        {
            EnsureLoaded();
            return _byName.TryGetValue(name, out var e) ? e : null;
        }

        private static void EnsureLoaded()
        {
            if (_entries != null) return;
            var list = new List<Entry>();
            var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var raw in Table.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var f = line.Split('|');
                if (f.Length < 5) throw new FormatException("bad catalog line: " + line);
                var e = new Entry
                {
                    Def = new VarDef
                    {
                        Name = f[0].Trim(),
                        Type = IrType.Parse(f[1]),
                        Count = int.Parse(f[2].Trim(), CultureInfo.InvariantCulture),
                        Unit = f[3].Trim(),
                        Desc = f.Length > 5 ? f[5].Trim() : "",
                    },
                    Default = double.Parse(f[4].Trim(), CultureInfo.InvariantCulture),
                };
                if (map.ContainsKey(e.Def.Name)) throw new FormatException("duplicate catalog entry " + e.Def.Name);
                map[e.Def.Name] = e;
                list.Add(e);
            }
            _entries = list;
            _byName = map;
        }
    }
}
