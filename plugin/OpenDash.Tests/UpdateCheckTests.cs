// UpdateCheckTests.cs: the decisions an update check makes. Each of these is a commitment ADR 0012 makes in prose,
// so each is pinned here: the setting is read before anything is built, a candidate is offered only to someone
// already running one, and an answer that could not be read never reports "up to date".
using System;
using System.Collections.Generic;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class UpdateCheckTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);

        private static ReleaseInfo Release(string tag, bool preRelease = false, bool draft = false) =>
            new ReleaseInfo { Tag = tag, PreRelease = preRelease, Draft = draft, Notes = "## What's Changed\n* something by @someone", Url = "https://example.invalid/" + tag };

        // ShouldCheck

        [Fact]
        public void The_setting_is_read_before_anything_is_built()
        {
            // Off means nothing is fetched at all, which is the commitment. Even a button press yields false,
            // because the panel does not offer the button when the setting is off.
            Assert.False(UpdateCheck.ShouldCheck(false, 0, Now, manual: false));
            Assert.False(UpdateCheck.ShouldCheck(false, 0, Now, manual: true));
        }

        [Fact]
        public void A_first_run_checks_and_a_second_within_the_day_does_not()
        {
            Assert.True(UpdateCheck.ShouldCheck(true, 0, Now, manual: false));

            var anHourAgo = Now.AddHours(-1).Ticks;
            Assert.False(UpdateCheck.ShouldCheck(true, anHourAgo, Now, manual: false));

            var yesterday = Now.AddHours(-24).Ticks;
            Assert.True(UpdateCheck.ShouldCheck(true, yesterday, Now, manual: false));
        }

        [Fact]
        public void A_person_who_presses_the_button_is_not_made_to_wait_a_day()
        {
            Assert.True(UpdateCheck.ShouldCheck(true, Now.AddMinutes(-1).Ticks, Now, manual: true));
        }

        [Fact]
        public void A_clock_that_went_backwards_does_not_silence_the_check_for_ever()
        {
            // Without this, a machine whose clock was wrong once never checks again.
            var future = Now.AddYears(5).Ticks;
            Assert.True(UpdateCheck.ShouldCheck(true, future, Now, manual: false));
        }

        // ComparableInstalled

        [Fact]
        public void An_unreadable_installed_version_falls_back_rather_than_posing_as_stable()
        {
            // "(unknown version)" parses as 0.0.0 with no pre-release part, so left alone it looks like a stable
            // release and its owner would never be offered a candidate.
            Assert.Equal("0.1.0-rc.2", UpdateCheck.ComparableInstalled(Versioning.UnknownVersion, "0.1.0-rc.2"));
            Assert.Equal("0.1.0-rc.2", UpdateCheck.ComparableInstalled(null, "0.1.0-rc.2"));
            Assert.Equal("0.2.0", UpdateCheck.ComparableInstalled("0.2.0", "0.1.0-rc.2"));
            Assert.Null(UpdateCheck.ComparableInstalled(null, null));
        }

        // IsPreRelease

        [Theory]
        [InlineData("0.1.0-rc.2", true)]
        [InlineData("v0.1.0-rc.1", true)]
        [InlineData("0.1.0", false)]
        [InlineData("v1.0.0", false)]
        [InlineData("0.1.0-", false)] // an empty suffix is the release, which is what VersionCompare does too
        [InlineData("", false)]
        [InlineData(null, false)]
        public void A_pre_release_is_the_dash_and_nothing_else(string version, bool expected)
        {
            Assert.Equal(expected, UpdateCheck.IsPreRelease(version));
        }

        // ReleaseFor

        [Fact]
        public void A_user_on_a_stable_version_is_not_offered_a_candidate()
        {
            var releases = new[] { Release("v0.2.0-rc.1", preRelease: true), Release("v0.1.0") };
            Assert.Null(UpdateCheck.ReleaseFor(releases, "0.1.0"));
        }

        [Fact]
        public void A_user_already_on_a_candidate_is_offered_the_next_one()
        {
            var releases = new[] { Release("v0.2.0-rc.1", preRelease: true), Release("v0.1.0-rc.2", preRelease: true) };
            Assert.Equal("v0.2.0-rc.1", UpdateCheck.ReleaseFor(releases, "0.1.0-rc.1").Tag);
        }

        [Fact]
        public void A_stable_release_is_offered_to_everyone()
        {
            var releases = new[] { Release("v0.2.0"), Release("v0.1.0-rc.2", preRelease: true) };
            Assert.Equal("v0.2.0", UpdateCheck.ReleaseFor(releases, "0.1.0-rc.2").Tag);
            Assert.Equal("v0.2.0", UpdateCheck.ReleaseFor(releases, "0.1.0").Tag);
        }

        [Fact]
        public void A_draft_is_never_offered_and_neither_is_something_older()
        {
            Assert.Null(UpdateCheck.ReleaseFor(new[] { Release("v0.9.0", draft: true) }, "0.1.0"));
            Assert.Null(UpdateCheck.ReleaseFor(new[] { Release("v0.1.0") }, "0.2.0"));
            Assert.Null(UpdateCheck.ReleaseFor(new[] { Release("v0.1.0") }, "0.1.0"));
        }

        [Fact]
        public void The_newest_offer_wins_whatever_order_the_feed_is_in()
        {
            var releases = new[] { Release("v0.2.0"), Release("v0.4.0"), Release("v0.3.0") };
            Assert.Equal("v0.4.0", UpdateCheck.ReleaseFor(releases, "0.1.0").Tag);
        }

        // Settles, and the pages it asks for

        [Fact]
        public void The_first_page_is_the_address_the_record_names_and_the_rest_follow_it()
        {
            Assert.Equal(UpdateCheck.ReleasesUrl, UpdateCheck.ReleasesPage(1));
            Assert.Equal(UpdateCheck.ReleasesUrl, UpdateCheck.ReleasesPage(0));
            Assert.Equal(UpdateCheck.ReleasesUrl + "&page=2", UpdateCheck.ReleasesPage(2));
            Assert.EndsWith("per_page=" + UpdateCheck.PageSize, UpdateCheck.ReleasesUrl);
        }

        [Fact]
        public void A_page_of_nothing_but_candidates_answers_nobody_on_a_stable_version()
        {
            // The window is a window over the newest releases of every kind, so a run of candidates can fill it
            // and hide the stable release below. Such a page settles nothing and another has to be read.
            var candidates = new[] { Release("v0.4.0-rc.2", preRelease: true), Release("v0.4.0-rc.1", preRelease: true) };
            Assert.False(UpdateCheck.Settles(candidates, "0.2.0"));

            // Whereas a user already on a candidate is answered by any release at all, so for them one page is
            // always enough.
            Assert.True(UpdateCheck.Settles(candidates, "0.3.0-rc.1"));
        }

        [Fact]
        public void One_release_of_the_right_kind_settles_it_whether_or_not_it_is_newer()
        {
            // Newest first by creation date, so nothing below a stable release was cut later than it.
            Assert.True(UpdateCheck.Settles(new[] { Release("v0.1.0") }, "0.2.0"));
            Assert.True(UpdateCheck.Settles(new[] { Release("v0.9.0") }, "0.2.0"));

            Assert.False(UpdateCheck.Settles(new[] { Release("v0.9.0", draft: true) }, "0.2.0"));
            Assert.False(UpdateCheck.Settles(new ReleaseInfo[0], "0.2.0"));
            Assert.False(UpdateCheck.Settles(null, "0.2.0"));
        }

        // Conclude

        [Fact]
        public void An_answer_that_could_not_be_read_is_never_up_to_date()
        {
            // The one quiet wrong outcome this feature can produce. ReleaseFeed.TryParse turns GitHub's own error
            // body into no releases, so "nothing came back" must not read as "nothing newer exists".
            foreach (var empty in new IReadOnlyList<ReleaseInfo>[] { null, new ReleaseInfo[0] })
            {
                var status = UpdateCheck.Conclude("0.1.0", empty, manual: true);
                Assert.Equal(UpdateState.Unreachable, status.State);
                Assert.Equal("0.1.0", status.InstalledVersion);
            }
        }

        [Fact]
        public void Nothing_newer_is_up_to_date_and_something_newer_is_an_offer()
        {
            var current = UpdateCheck.Conclude("0.2.0", new[] { Release("v0.2.0") }, manual: true);
            Assert.Equal(UpdateState.UpToDate, current.State);
            Assert.Null(current.LatestVersion);

            var offer = UpdateCheck.Conclude("0.1.0", new[] { Release("v0.2.0") }, manual: false);
            Assert.Equal(UpdateState.UpdateAvailable, offer.State);
            Assert.Equal("0.2.0", offer.LatestVersion);
            Assert.Equal("https://example.invalid/v0.2.0", offer.Url);
        }

        // What the panel shows

        [Fact]
        public void A_background_check_that_finds_nothing_says_nothing()
        {
            Assert.False(UpdateCheck.Conclude("0.2.0", new[] { Release("v0.2.0") }, manual: false).IsVisible);
            Assert.False(UpdateCheck.Conclude("0.2.0", null, manual: false).IsVisible);

            // A person who asked is answered, whatever the answer is.
            Assert.True(UpdateCheck.Conclude("0.2.0", new[] { Release("v0.2.0") }, manual: true).IsVisible);
            Assert.True(UpdateCheck.Conclude("0.2.0", null, manual: true).IsVisible);

            // An offer is always worth showing, asked for or not.
            Assert.True(UpdateCheck.Conclude("0.1.0", new[] { Release("v0.2.0") }, manual: false).IsVisible);
        }

        /// <summary>
        /// The whole table the "This plugin" pill is read off, which is every install status against every update
        /// state. The two facts it exists for are that an offer from either source turns the pill amber, and that a
        /// check which is off, pending or unanswered never changes what the disk says.
        /// </summary>
        [Theory]
        // Nothing has been asked, so the disk answers alone.
        [InlineData(InstallStatus.UpToDate, UpdateState.Idle, InstallStatus.UpToDate)]
        [InlineData(InstallStatus.UpdateAvailable, UpdateState.Idle, InstallStatus.UpdateAvailable)]
        [InlineData(InstallStatus.NotInstalled, UpdateState.Idle, InstallStatus.NotInstalled)]
        [InlineData(InstallStatus.Failed, UpdateState.Idle, InstallStatus.Failed)]
        // The setting is off. Nothing was fetched, so the pill may not imply anything was.
        [InlineData(InstallStatus.UpToDate, UpdateState.Disabled, InstallStatus.UpToDate)]
        [InlineData(InstallStatus.UpdateAvailable, UpdateState.Disabled, InstallStatus.UpdateAvailable)]
        [InlineData(InstallStatus.NotInstalled, UpdateState.Disabled, InstallStatus.NotInstalled)]
        [InlineData(InstallStatus.Failed, UpdateState.Disabled, InstallStatus.Failed)]
        // The question is still open.
        [InlineData(InstallStatus.UpToDate, UpdateState.Checking, InstallStatus.UpToDate)]
        [InlineData(InstallStatus.UpdateAvailable, UpdateState.Checking, InstallStatus.UpdateAvailable)]
        [InlineData(InstallStatus.NotInstalled, UpdateState.Checking, InstallStatus.NotInstalled)]
        [InlineData(InstallStatus.Failed, UpdateState.Checking, InstallStatus.Failed)]
        // Asked and not answered, which is a state rather than a fault and therefore says nothing new.
        [InlineData(InstallStatus.UpToDate, UpdateState.Unreachable, InstallStatus.UpToDate)]
        [InlineData(InstallStatus.UpdateAvailable, UpdateState.Unreachable, InstallStatus.UpdateAvailable)]
        [InlineData(InstallStatus.NotInstalled, UpdateState.Unreachable, InstallStatus.NotInstalled)]
        [InlineData(InstallStatus.Failed, UpdateState.Unreachable, InstallStatus.Failed)]
        // GitHub has nothing newer, so again the disk answers alone.
        [InlineData(InstallStatus.UpToDate, UpdateState.UpToDate, InstallStatus.UpToDate)]
        [InlineData(InstallStatus.UpdateAvailable, UpdateState.UpToDate, InstallStatus.UpdateAvailable)]
        [InlineData(InstallStatus.NotInstalled, UpdateState.UpToDate, InstallStatus.NotInstalled)]
        [InlineData(InstallStatus.Failed, UpdateState.UpToDate, InstallStatus.Failed)]
        // A release is waiting. It turns an otherwise clean pill amber and is outranked by the two worse states,
        // because a dashboard that is missing or that failed to install is the more urgent of the two facts.
        [InlineData(InstallStatus.UpToDate, UpdateState.UpdateAvailable, InstallStatus.UpdateAvailable)]
        [InlineData(InstallStatus.UpdateAvailable, UpdateState.UpdateAvailable, InstallStatus.UpdateAvailable)]
        [InlineData(InstallStatus.NotInstalled, UpdateState.UpdateAvailable, InstallStatus.NotInstalled)]
        [InlineData(InstallStatus.Failed, UpdateState.UpdateAvailable, InstallStatus.Failed)]
        public void The_pill_takes_the_worse_of_the_disk_and_the_release(InstallStatus installed, UpdateState update, InstallStatus expected)
        {
            Assert.Equal(expected, DashboardInstaller.PillStatus(installed, update));
        }

        [Fact]
        public void An_offer_from_either_source_reaches_the_pill_as_the_amber_label()
        {
            // The label is what a person actually reads, so the two halves of the fix are said once in those terms:
            // a disk behind the embedded copy, and a disk that matches it while GitHub holds something newer.
            Assert.Equal("Update available", DashboardInstaller.PillStatus(InstallStatus.UpdateAvailable, UpdateState.UpToDate).Label());
            Assert.Equal("Update available", DashboardInstaller.PillStatus(InstallStatus.UpToDate, UpdateState.UpdateAvailable).Label());
        }
    }
}
