// SimCapabilitiesTests.cs: what each sim answers to the questions a dashboard may not ask by the game's name. #1101.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class SimCapabilitiesTests
    {
        /// <summary>The shape SimHub hands Assetto Corsa's and ACC's Graphics.Status in: the reader's own enum, boxed.</summary>
        private enum AcStatus
        {
            AC_OFF,
            AC_REPLAY,
            AC_LIVE,
            AC_PAUSE,
        }

        private const string GameData = "DataCorePlugin.GameData.";
        private const string Raw = "DataCorePlugin.GameRawData.";

        /// <summary>SimHub's properties for one frame, by full name; anything not listed is absent, as GetPropertyValue says.</summary>
        private static Func<string, object> Frame(params (string Name, object Value)[] properties)
        {
            var map = properties.ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
            return name =>
            {
                object value;
                return map.TryGetValue(name, out value) ? value : null;
            };
        }

        [Fact]
        public void On_iRacing_the_limiter_is_the_cars_own_control_and_damage_is_not_reported()
        {
            // A car with the control, switched off: it has a limiter, and it is not on.
            var gt3 = SimCapabilities.Read(SimCapabilities.IRacing, Frame(
                (Raw + "Telemetry.dcPitSpeedLimiterToggle", false),
                (GameData + "PitLimiterOn", 0),
                (Raw + "Telemetry.IsOnTrack", true)));
            Assert.True(gt3.CarHasPitLimiter);
            Assert.False(gt3.PitLimiterOn);
            Assert.False(gt3.DamageReported);
            Assert.True(gt3.DriverInCar);

            // A car without the control has no limiter, and a driver in the garage is not in the car.
            var mx5 = SimCapabilities.Read(SimCapabilities.IRacing, Frame(
                (GameData + "PitLimiterOn", 0),
                (Raw + "Telemetry.IsOnTrack", false)));
            Assert.False(mx5.CarHasPitLimiter);
            Assert.False(mx5.DriverInCar);

            // The limiter bit, as SimHub's int.
            var engaged = SimCapabilities.Read(SimCapabilities.IRacing, Frame(
                (Raw + "Telemetry.dcPitSpeedLimiterToggle", true),
                (GameData + "PitLimiterOn", 1)));
            Assert.True(engaged.PitLimiterOn);
        }

        [Fact]
        public void On_iRacing_every_answer_is_the_one_the_dashboards_fall_back_on()
        {
            // The fallbacks in values.ts are iRacing's readings, so the plugin there says what a package with no
            // plugin already reads: the control's presence, the limiter bit, no damage, IsOnTrack, SimHub's own flags
            // and the whole field.
            var frame = Frame(
                (Raw + "Telemetry.dcPitSpeedLimiterToggle", false),
                (GameData + "PitLimiterOn", 1),
                (Raw + "Telemetry.IsOnTrack", true));
            Assert.Same(SimReport.Of(true, true, false, true, null, null, null, null, true), SimCapabilities.Read("IRacing", frame));
        }

        [Fact]
        public void On_Assetto_Corsa_every_car_has_a_limiter_and_its_state_is_the_physics_pages()
        {
            // SimHub's own PitLimiterOn is the pit lane on AC (ACManager.GD_PitLimiterOn returns GD_IsInPitLane): in
            // the lane with the limiter off it reads 1, and the plugin answers from Physics.PitLimiterOn instead.
            var inTheLane = SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame(
                (GameData + "PitLimiterOn", 1),
                (Raw + "Physics.PitLimiterOn", 0),
                (Raw + "Graphics.Status", AcStatus.AC_LIVE)));
            Assert.True(inTheLane.CarHasPitLimiter);
            Assert.False(inTheLane.PitLimiterOn);
            Assert.True(inTheLane.DamageReported);
            Assert.True(inTheLane.DriverInCar);

            var engaged = SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame(
                (GameData + "PitLimiterOn", 0),
                (Raw + "Physics.PitLimiterOn", 1)));
            Assert.True(engaged.PitLimiterOn);
            Assert.True(engaged.CarHasPitLimiter);
        }

        [Fact]
        public void On_Assetto_Corsa_and_ACC_only_a_live_session_is_the_driver_in_the_car()
        {
            foreach (var game in new[] { SimCapabilities.AssettoCorsa, SimCapabilities.AssettoCorsaCompetizione })
            {
                Assert.True(SimCapabilities.Read(game, Frame((Raw + "Graphics.Status", AcStatus.AC_LIVE))).DriverInCar);
                Assert.False(SimCapabilities.Read(game, Frame((Raw + "Graphics.Status", AcStatus.AC_PAUSE))).DriverInCar);
                Assert.False(SimCapabilities.Read(game, Frame((Raw + "Graphics.Status", AcStatus.AC_REPLAY))).DriverInCar);
                Assert.False(SimCapabilities.Read(game, Frame((Raw + "Graphics.Status", AcStatus.AC_OFF))).DriverInCar);
                // A number or the enum's value written out, should SimHub ever hand it on that way.
                Assert.True(SimCapabilities.Read(game, Frame((Raw + "Graphics.Status", 2))).DriverInCar);
                Assert.Null(SimCapabilities.Read(game, Frame()).DriverInCar);
            }
        }

        [Fact]
        public void On_ACC_SimHubs_limiter_is_the_limiter_and_damage_is_reported()
        {
            // ACCManager.GD_PitLimiterOn reads Physics.PitLimiterOn, so SimHub's member is right on ACC.
            var acc = SimCapabilities.Read(SimCapabilities.AssettoCorsaCompetizione, Frame(
                (GameData + "PitLimiterOn", 1),
                (Raw + "Graphics.Status", AcStatus.AC_LIVE)));
            Assert.True(acc.CarHasPitLimiter);
            Assert.True(acc.PitLimiterOn);
            Assert.True(acc.DamageReported);
            Assert.True(acc.DriverInCar);
        }

        [Fact]
        public void On_Le_Mans_Ultimate_the_car_says_whether_it_has_a_limiter_and_damage_is_not_reported()
        {
            var hypercar = SimCapabilities.Read(SimCapabilities.LeMansUltimate, Frame(
                (Raw + "PlayerNativeTelemetry.mSpeedLimiterAvailable", (byte)1),
                (GameData + "PitLimiterOn", 0),
                (Raw + "LMUNativeTelemetry.scoring.scoringInfo.mInRealtime", true)));
            Assert.True(hypercar.CarHasPitLimiter);
            Assert.False(hypercar.PitLimiterOn);
            Assert.False(hypercar.DamageReported);
            Assert.True(hypercar.DriverInCar);

            var none = SimCapabilities.Read(SimCapabilities.LeMansUltimate, Frame(
                (Raw + "PlayerNativeTelemetry.mSpeedLimiterAvailable", (byte)0),
                (Raw + "LMUNativeTelemetry.scoring.scoringInfo.mInRealtime", false)));
            Assert.False(none.CarHasPitLimiter);
            Assert.False(none.DriverInCar);

            // LMU's page not there yet: no answer, so the dashboards fall back rather than read "no limiter".
            Assert.Null(SimCapabilities.Read(SimCapabilities.LeMansUltimate, Frame()).CarHasPitLimiter);
        }

        /// <summary>A Le Mans Ultimate frame: a race of 20 laps, green, the player on lap 5 in the first sector with no penalty.</summary>
        private static (string, object)[] LmuRace(params (string Name, object Value)[] overrides)
        {
            var scoring = Raw + "LMUNativeTelemetry.scoring.scoringInfo.";
            var player = Raw + "PlayerNativeScoring.";
            var frame = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                [scoring + "mSession"] = 10,
                [scoring + "mGamePhase"] = (byte)5,
                [scoring + "mMaxLaps"] = 20,
                [scoring + "mSessionTimeRemaining"] = 1800f,
                // The sector flags and the sector they are read for come from the rFactor 2 plugin's page, the only
                // one SimHub publishes them on (see LmuPropertiesSimHubDeclares).
                [PluginScoring + "mSectorFlag01"] = (sbyte)0,
                [PluginScoring + "mSectorFlag02"] = (sbyte)0,
                [PluginScoring + "mSectorFlag03"] = (sbyte)0,
                [PluginPlayer + "mSector"] = (sbyte)1,
                [player + "mTotalLaps"] = (short)4,
                [player + "mFinishStatus"] = (sbyte)0,
                [player + "mNumPenalties"] = (short)0,
                // Out on track, no stop under way.
                [player + "mPitState"] = (byte)0,
                // SimHub's own summaries, as LMUManager computes them from the same frame.
                [GameData + "Flag_Yellow"] = 0,
                [GameData + "Flag_Black"] = 0,
                [GameData + "Flag_White"] = 0,
                [GameData + "Flag_Checkered"] = 0,
            };
            foreach (var (name, value) in overrides) frame[name] = value;
            return frame.Select(p => (p.Key, p.Value)).ToArray();
        }

        private static SimReport Lmu(params (string Name, object Value)[] overrides) =>
            SimCapabilities.Read(SimCapabilities.LeMansUltimate, Frame(LmuRace(overrides)));

        private const string Scoring = Raw + "LMUNativeTelemetry.scoring.scoringInfo.";
        private const string Player = Raw + "PlayerNativeScoring.";
        private const string PluginScoring = Raw + "Data.";
        private const string PluginPlayer = Raw + "CurrentPlayer.";

        [Fact]
        public void On_Le_Mans_Ultimate_a_green_race_shows_no_flag_the_plugin_answers_for()
        {
            var green = Lmu();
            Assert.False(green.YellowFlagShown);
            Assert.False(green.BlackFlagShown);
            Assert.False(green.WhiteFlagShown);
            Assert.False(green.ChequeredFlagShown);
        }

        [Fact]
        public void On_Le_Mans_Ultimate_the_session_being_over_raises_the_chequer_SimHub_never_does()
        {
            // LMUManager.GD_Flag_Checkered is [NotAvailable]: SimHub reads 0 however the race ends.
            var over = Lmu((Scoring + "mGamePhase", (byte)8));
            Assert.True(over.ChequeredFlagShown);
            Assert.False(over.WhiteFlagShown);
            // The player across the line is shown it too, whatever the phase reads on that frame.
            Assert.True(Lmu((Player + "mFinishStatus", (sbyte)1)).ChequeredFlagShown);
            Assert.False(Lmu((Scoring + "mGamePhase", (byte)6)).ChequeredFlagShown);
        }

        [Fact]
        public void On_Le_Mans_Ultimate_a_retirement_raises_no_black_and_a_penalty_does()
        {
            // LMUManager.GD_Flag_Black is mFinishStatus 2 or 3: it lights for a car that has stopped, which is not
            // a flag shown to a driver still racing.
            var retired = Lmu((Player + "mFinishStatus", (sbyte)2), (GameData + "Flag_Black", 1));
            Assert.False(retired.BlackFlagShown);
            var disqualified = Lmu((Player + "mFinishStatus", (sbyte)3), (GameData + "Flag_Black", 1));
            Assert.False(disqualified.BlackFlagShown);
            Assert.True(Lmu((Player + "mNumPenalties", (short)1)).BlackFlagShown);
        }

        [Fact]
        public void On_Le_Mans_Ultimate_a_yellow_in_another_sector_raises_no_local_yellow()
        {
            // LMUManager.GD_Flag_Yellow is any mSectorFlag at 1. The player is in the first sector, mSector 1,
            // which is mSectorFlag02 in SimHub's one-based numbering; a yellow in the third is not theirs.
            var elsewhere = Lmu((PluginScoring + "mSectorFlag01", (sbyte)1), (GameData + "Flag_Yellow", 1));
            Assert.False(elsewhere.YellowFlagShown);
            Assert.False(Lmu((PluginScoring + "mSectorFlag03", (sbyte)1), (GameData + "Flag_Yellow", 1)).YellowFlagShown);
            var here = Lmu((PluginScoring + "mSectorFlag02", (sbyte)1), (GameData + "Flag_Yellow", 1));
            Assert.True(here.YellowFlagShown);
            // mSector 0 is the third sector, and its flag the first of the three.
            Assert.True(Lmu((PluginPlayer + "mSector", (sbyte)0), (PluginScoring + "mSectorFlag01", (sbyte)1)).YellowFlagShown);
            // With no sector to read, no answer: the dashboards fall back on SimHub's any-sector yellow.
            Assert.Null(Lmu((PluginPlayer + "mSector", null)).YellowFlagShown);
            // LMU's native page publishes no sector flag, so a name there is not one the plugin reads.
            Assert.False(Lmu((Scoring + "mSectorFlag02", (sbyte)1)).YellowFlagShown);
        }

        /// <summary>
        /// Every GameRawData name the Le Mans Ultimate row may read, as SimHub publishes it. DataCorePlugin declares
        /// GameRawData once, from LMUManager.GetDataSample, and declares nothing for an array that is null there
        /// (DeclareObject returns on a null value). The sample's LMUNativeTelemetry is a default SharedMemoryObjectOut,
        /// so its scalars are published and its arrays (scoringInfo.mSectorFlag, vehScoringInfo) are not;
        /// PlayerNativeScoring and PlayerNativeTelemetry are default structs, scalars only. The rFactor 2 plugin's
        /// Data is built with mSectorFlag = new sbyte[3], so Data.mSectorFlag01 to 03 are published, and
        /// CurrentPlayer is a default rF2VehicleScoring. Read from the decompiled RfactorReader of SimHub 9.12.6;
        /// a name added here has to be found there first.
        /// </summary>
        private static readonly HashSet<string> LmuPropertiesSimHubDeclares = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // ScoringInfoV01's scalars.
            Scoring + "mSession",
            Scoring + "mGamePhase",
            Scoring + "mMaxLaps",
            Scoring + "mSessionTimeRemaining",
            Scoring + "mInRealtime",
            // VehicleScoringInfoV01's scalars.
            Player + "mTotalLaps",
            Player + "mFinishStatus",
            Player + "mNumPenalties",
            Player + "mPitState",
            // TelemInfoV01's scalars.
            Raw + "PlayerNativeTelemetry.mSpeedLimiterAvailable",
            // rF2ScoringInfo with the sample's three sector flags, and rF2VehicleScoring.
            PluginScoring + "mSectorFlag01",
            PluginScoring + "mSectorFlag02",
            PluginScoring + "mSectorFlag03",
            PluginPlayer + "mSector",
        };

        [Fact]
        public void On_Le_Mans_Ultimate_the_plugin_reads_only_raw_names_SimHub_publishes()
        {
            // A frame built by hand can hold any name, but a name SimHub never declares reads null in the game, and
            // the answer it feeds is never given there: #1129's local yellow first read LMUNativeTelemetry's
            // mSectorFlag, which is not published. Every raw name the row asks for, on every branch, is held to the
            // names the sample declares.
            var asked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Func<string, object> Recording((string Name, object Value)[] properties)
            {
                var frame = Frame(properties);
                return name =>
                {
                    asked.Add(name);
                    return frame(name);
                };
            }
            var frames = new[]
            {
                LmuRace(),
                LmuRace((PluginPlayer + "mSector", (sbyte)0)),
                LmuRace((PluginPlayer + "mSector", (sbyte)2)),
                LmuRace((Player + "mTotalLaps", (short)19)),
                LmuRace((Scoring + "mMaxLaps", int.MaxValue)),
                LmuRace((Scoring + "mGamePhase", (byte)8)),
                LmuRace((Player + "mFinishStatus", (sbyte)1)),
                LmuRace((Scoring + "mSession", 5)),
                LmuRace((Raw + "PlayerNativeTelemetry.mSpeedLimiterAvailable", (byte)1), (Scoring + "mInRealtime", true)),
            };
            foreach (var frame in frames)
            {
                SimCapabilities.Read(SimCapabilities.LeMansUltimate, Recording(frame));
                SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Recording(frame));
            }
            var raw = asked.Where(name => name.StartsWith(Raw, StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.Contains(PluginScoring + "mSectorFlag01", raw);
            Assert.Contains(PluginScoring + "mSectorFlag02", raw);
            Assert.Contains(PluginScoring + "mSectorFlag03", raw);
            Assert.Contains(Player + "mPitState", raw);
            Assert.Empty(raw.Where(name => !LmuPropertiesSimHubDeclares.Contains(name)));
        }

        [Fact]
        public void On_Le_Mans_Ultimate_the_last_lap_raises_the_white_SimHub_never_does()
        {
            // LMUManager.GD_Flag_White is [NotAvailable]. Nineteen of twenty laps done is the last lap begun.
            Assert.True(Lmu((Player + "mTotalLaps", (short)19)).WhiteFlagShown);
            Assert.False(Lmu((Player + "mTotalLaps", (short)18)).WhiteFlagShown);
            // A timed race writes a huge lap count, and its last lap is the one begun after the clock ran out.
            var timed = new[] { (Scoring + "mMaxLaps", (object)int.MaxValue), (Player + "mTotalLaps", (object)(short)19) };
            Assert.False(Lmu(timed).WhiteFlagShown);
            Assert.True(Lmu(timed.Append((Scoring + "mSessionTimeRemaining", (object)0f)).ToArray()).WhiteFlagShown);
            // Not before a race, and not once the player has taken the chequer or stopped.
            Assert.False(Lmu((Player + "mTotalLaps", (short)19), (Scoring + "mSession", 5)).WhiteFlagShown);
            Assert.False(Lmu((Player + "mTotalLaps", (short)19), (Player + "mFinishStatus", (sbyte)1)).WhiteFlagShown);
            Assert.False(Lmu((Player + "mTotalLaps", (short)19), (Scoring + "mGamePhase", (byte)8)).WhiteFlagShown);
        }

        [Fact]
        public void With_no_scoring_page_Le_Mans_Ultimate_answers_no_flag_so_the_dashboards_read_SimHubs()
        {
            var empty = SimCapabilities.Read(SimCapabilities.LeMansUltimate, Frame());
            Assert.Null(empty.YellowFlagShown);
            Assert.Null(empty.BlackFlagShown);
            Assert.Null(empty.WhiteFlagShown);
            Assert.Null(empty.ChequeredFlagShown);
        }

        [Fact]
        public void On_iRacing_Assetto_Corsa_and_ACC_the_plugin_answers_no_flag()
        {
            // iRacing's bits are asked before any answer, and AC's and ACC's Flag_* are the one flag the game shows
            // the driver, so the dashboards read SimHub's own there; the plugin answering would only add a way to
            // be wrong. The LMU frame's properties are there too, to show they are not read.
            foreach (var game in new[] { SimCapabilities.IRacing, SimCapabilities.AssettoCorsa, SimCapabilities.AssettoCorsaCompetizione })
            {
                var report = SimCapabilities.Read(game, Frame(LmuRace((Scoring + "mGamePhase", (byte)8), (Player + "mNumPenalties", (short)2))));
                Assert.Null(report.YellowFlagShown);
                Assert.Null(report.BlackFlagShown);
                Assert.Null(report.WhiteFlagShown);
                Assert.Null(report.ChequeredFlagShown);
            }
        }

        [Fact]
        public void On_Assetto_Corsa_the_field_is_there_only_while_SimHubs_app_writes_it()
        {
            // Without SimHub's in-game app the page it writes counts no cars, and SimHub's opponents are the player
            // alone, named "Me": the leaderboard would be a board of one (#1131).
            var live = (Raw + "Graphics.Status", (object)AcStatus.AC_LIVE);
            var paused = (Raw + "Graphics.Status", (object)AcStatus.AC_PAUSE);
            Assert.True(SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame(live, (Raw + "Opponents.numVehicles", 24))).FieldReported);
            Assert.False(SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame(live, (Raw + "Opponents.numVehicles", 0))).FieldReported);
            Assert.False(SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame(paused, (Raw + "Opponents.numVehicles", 0))).FieldReported);
            Assert.Null(SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame()).FieldReported);
        }

        [Fact]
        public void On_Assetto_Corsa_a_game_still_loading_is_not_a_field_missing()
        {
            // While the game loads Graphics.Status is AC_OFF and SimHub's app has not started, so its page counts no
            // cars yet; Home works out its issues at that moment and would keep a warning the app then answers.
            var none = (Raw + "Opponents.numVehicles", (object)0);
            Assert.Null(SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame((Raw + "Graphics.Status", AcStatus.AC_OFF), none)).FieldReported);
            Assert.Null(SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame((Raw + "Graphics.Status", AcStatus.AC_REPLAY), none)).FieldReported);
            Assert.Null(SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame(none)).FieldReported);
        }

        [Fact]
        public void On_ACC_the_field_is_there_only_while_broadcasting_is_connected()
        {
            // ACCManager's opponents come from the broadcasting interface, whose realtime update is null until it
            // connects. With no frame at all the game is not running, which is not a field missing.
            var live = (Raw + "Graphics.Status", (object)AcStatus.AC_LIVE);
            Assert.True(SimCapabilities.Read(SimCapabilities.AssettoCorsaCompetizione, Frame(live, (Raw + "Realtime.SessionIndex", 0))).FieldReported);
            Assert.False(SimCapabilities.Read(SimCapabilities.AssettoCorsaCompetizione, Frame(live)).FieldReported);
            Assert.Null(SimCapabilities.Read(SimCapabilities.AssettoCorsaCompetizione, Frame()).FieldReported);
        }

        [Fact]
        public void On_iRacing_and_LMU_the_field_is_always_there()
        {
            // Both read the session's own list of cars, which the game writes on every frame.
            Assert.True(SimCapabilities.Read(SimCapabilities.IRacing, Frame()).FieldReported);
            Assert.True(SimCapabilities.Read(SimCapabilities.LeMansUltimate, Frame()).FieldReported);
        }

        [Fact]
        public void A_game_with_no_row_answers_nothing_so_the_dashboards_read_what_they_read_without_the_plugin()
        {
            var frame = Frame((GameData + "PitLimiterOn", 1), (Raw + "Telemetry.IsOnTrack", true));
            Assert.Same(SimReport.Unknown, SimCapabilities.Read("RFactor2", frame));
            Assert.Same(SimReport.Unknown, SimCapabilities.Read("", frame));
            Assert.Same(SimReport.Unknown, SimCapabilities.Read(null, frame));
            Assert.Same(SimReport.Unknown, SimCapabilities.Read(SimCapabilities.IRacing, null));
            foreach (var capability in Contract.Capabilities) Assert.Null(SimReport.Unknown.Read(capability));
        }

        [Fact]
        public void Every_claimed_sim_has_a_row_that_answers_every_question()
        {
            // 1.0 claims iRacing, Assetto Corsa, ACC and Le Mans Ultimate (#479). A row with a question left out
            // would publish null for it, which reads as iRacing's answer on a sim that is not iRacing.
            var claimed = new[] { "IRacing", "AssettoCorsa", "AssettoCorsaCompetizione", "LMU" };
            Assert.Equal(claimed.OrderBy(g => g), SimCapabilities.Games.OrderBy(g => g));
            var damage = claimed.ToDictionary(g => g, g => SimCapabilities.Read(g, Frame()).DamageReported);
            Assert.Equal(new bool?[] { false, true, true, false }, claimed.Select(g => damage[g]).ToArray());
            // A flag is the one question a row may leave unanswered on purpose: null there means "read SimHub's own
            // summary", which is right everywhere but Le Mans Ultimate, and only its row answers one (#1129).
            var shownOnLmu = SimCapabilities.Read(SimCapabilities.LeMansUltimate, Frame(
                (Raw + "LMUNativeTelemetry.scoring.scoringInfo.mGamePhase", (byte)8),
                (Raw + "PlayerNativeScoring.mNumPenalties", (short)0)));
            Assert.Equal(true, shownOnLmu.ChequeredFlagShown);
            Assert.Equal(false, shownOnLmu.BlackFlagShown);
        }

        [Fact]
        public void A_property_that_cannot_be_read_is_an_answer_not_given_and_never_an_exception()
        {
            Func<string, object> broken = name => throw new InvalidOperationException("SimHub could not evaluate " + name);
            var report = SimCapabilities.Read(SimCapabilities.AssettoCorsa, broken);
            // The constant answers stand; the two read from a property are unknown.
            Assert.True(report.CarHasPitLimiter);
            Assert.True(report.DamageReported);
            Assert.Null(report.PitLimiterOn);
            Assert.Null(report.DriverInCar);
            Assert.Null(report.FieldReported);
        }

        [Fact]
        public void A_value_reads_as_true_or_false_whatever_shape_SimHub_hands_it_in()
        {
            Assert.True(SimCapabilities.Truthy(true));
            Assert.False(SimCapabilities.Truthy(false));
            Assert.True(SimCapabilities.Truthy(1));
            Assert.False(SimCapabilities.Truthy(0));
            Assert.True(SimCapabilities.Truthy((byte)1));
            Assert.True(SimCapabilities.Truthy(0.5f));
            Assert.False(SimCapabilities.Truthy(0.0));
            Assert.True(SimCapabilities.Truthy("True"));
            Assert.False(SimCapabilities.Truthy("0"));
            Assert.True(SimCapabilities.Truthy(AcStatus.AC_REPLAY));
            Assert.Null(SimCapabilities.Truthy(null));
            Assert.Null(SimCapabilities.Truthy("on"));
            Assert.Null(SimCapabilities.Truthy(new object()));

            Assert.Equal(2L, SimCapabilities.Number(AcStatus.AC_LIVE));
            Assert.Equal(2L, SimCapabilities.Number("2"));
            Assert.Equal(2L, SimCapabilities.Number(2.0f));
            Assert.Null(SimCapabilities.Number(true));
            Assert.Null(SimCapabilities.Number("AC_LIVE"));
        }

        [Fact]
        public void A_report_is_one_interned_instance_per_set_of_answers_and_reads_by_name()
        {
            var a = SimReport.Of(true, null, false, true);
            Assert.Same(a, SimReport.Of(true, null, false, true));
            Assert.NotSame(a, SimReport.Of(true, null, false, false));
            Assert.Equal(true, a.Read(Contract.CarHasPitLimiter));
            Assert.Null(a.Read(Contract.PitLimiterOn));
            Assert.Equal(false, a.Read(Contract.DamageReported));
            Assert.Equal(true, a.Read(Contract.DriverInCar));
            Assert.Null(a.Read("ShiftLights"));
            // An answer left off the end is unknown, so a report of the first four is the same instance as one
            // that says the flags are unknown.
            Assert.Null(a.Read(Contract.YellowFlagShown));
            Assert.Same(a, SimReport.Of(true, null, false, true, null, null, null, null, null));
            Assert.Equal("limiter yes, on unknown, damage no, in the car yes, yellow unknown, black unknown, white unknown, chequered unknown, field unknown", a.ToString());
            var flags = SimReport.Of(null, null, null, null, true, false, null, true);
            Assert.Same(flags, SimReport.Of(null, null, null, null, true, false, null, true));
            Assert.Equal(true, flags.YellowFlagShown);
            Assert.Equal(false, flags.BlackFlagShown);
            Assert.Null(flags.WhiteFlagShown);
            Assert.Equal(true, flags.ChequeredFlagShown);
            Assert.Equal(true, flags.Read(Contract.ChequeredFlagShown));
            var alone = SimReport.Of(null, null, null, null, null, null, null, null, false);
            Assert.Equal(false, alone.FieldReported);
            Assert.NotSame(alone, SimReport.Unknown);
        }

        [Fact]
        public void They_are_published_with_the_shared_group_after_the_live_order()
        {
            // Appended to the shared group, which is pinned in order on both sides of the contract, and named for
            // the question a dashboard asks rather than for a game.
            Assert.Equal(new[]
            {
                "CarHasPitLimiter", "PitLimiterOn", "DamageReported", "DriverInCar",
                "YellowFlagShown", "BlackFlagShown", "WhiteFlagShown", "ChequeredFlagShown",
                "FieldReported",
            }, Contract.Capabilities);
            var shared = Contract.SharedPropertyNames().ToList();
            Assert.Equal(Contract.Capabilities, shared.Skip(shared.IndexOf(Contract.CarHasPitLimiter)).Take(Contract.Capabilities.Length));
            Assert.Equal(shared.IndexOf("LiveClassOrder" + LivePosition.Slots) + 1, shared.IndexOf(Contract.CarHasPitLimiter));
        }
    }
}
