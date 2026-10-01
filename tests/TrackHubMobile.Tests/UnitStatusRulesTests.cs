// Copyright (c) 2026 Sergio Hernandez. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using TrackHubMobile.Models;

namespace TrackHubMobile.Tests;

[TestFixture]
public class UnitStatusRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void TheOnlineWindow_IsTheAccountsIntervalInMinutes()
        => Assert.That(UnitStatusRules.FromOnlineInterval(15).OnlineWindow, Is.EqualTo(TimeSpan.FromMinutes(15)));

    [TestCase(0)]
    [TestCase(-5)]
    public void AnUnsetInterval_FallsBackToAnHour(int minutes)
        => Assert.That(UnitStatusRules.FromOnlineInterval(minutes).OnlineWindow, Is.EqualTo(TimeSpan.FromHours(1)));

    [Test]
    public void AUnitSilentPastTheWindow_IsOffline_EvenWhenItsLastSpeedWasHigh()
    {
        var rules = UnitStatusRules.FromOnlineInterval(10);

        Assert.Multiple(() =>
        {
            Assert.That(rules.StatusOf(Unit(minutesAgo: 11, speed: 90), Now), Is.EqualTo(UnitStatus.Offline));
            Assert.That(rules.StatusOf(Unit(minutesAgo: 9, speed: 90), Now), Is.EqualTo(UnitStatus.Moving));
            Assert.That(rules.StatusOf(Unit(minutesAgo: 9, speed: 0), Now), Is.EqualTo(UnitStatus.Stopped));
        });
    }

    [Test]
    public void Speeding_IsAboveTheSingleThreshold()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UnitStatusRules.IsSpeeding(Unit(0, UnitStatusRules.SpeedingKmh)), Is.False);
            Assert.That(UnitStatusRules.IsSpeeding(Unit(0, UnitStatusRules.SpeedingKmh + 1)), Is.True);
        });
    }

    private static PositionVm Unit(int minutesAgo, double speed)
        => new(Guid.NewGuid(), "unit", "TRUCK", 0, 0, null, Now.AddMinutes(-minutesAgo), speed,
            null, null, null, null, null, null, null);
}
