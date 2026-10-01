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

namespace TrackHubMobile.Models;

public enum UnitStatus
{
    Moving,
    Stopped,
    Offline
}

// The account's onlineInterval is in minutes, read exactly as the portal reads it.
public sealed record UnitStatusRules(TimeSpan OnlineWindow)
{
    // The backend has no speed-limit setting, so the threshold lives here only.
    public const double SpeedingKmh = 80;

    private const int DefaultOnlineMinutes = 60;

    public static UnitStatusRules Default { get; } = FromOnlineInterval(DefaultOnlineMinutes);

    public static UnitStatusRules FromOnlineInterval(int minutes)
        => new(TimeSpan.FromMinutes(minutes > 0 ? minutes : DefaultOnlineMinutes));

    public bool IsOnline(PositionVm unit, DateTimeOffset now) => now - unit.DeviceDateTime <= OnlineWindow;

    public static bool IsSpeeding(PositionVm unit) => unit.Speed > SpeedingKmh;

    public UnitStatus StatusOf(PositionVm unit, DateTimeOffset now)
        => !IsOnline(unit, now) ? UnitStatus.Offline
            : unit.Speed > 0 ? UnitStatus.Moving
            : UnitStatus.Stopped;
}
