// Copyright (c) 2025 Sergio Hernandez. All rights reserved.
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

using TrackHubMobile.Interfaces.Helpers;

namespace TrackHubMobile.Helpers;

public class TransporterHelper(ILocalizationResourceManager localization) : ITransporterHelper
{
    public string GetTimeDifference(DateTimeOffset inputTime)
    {
        // Both operands are absolute instants (UTC), so the elapsed time is
        // timezone-independent — no need to shift into local time here.
        var timeDifference = DateTimeOffset.UtcNow - inputTime;

        if (timeDifference.TotalMinutes < 1)
            return localization["JustNow"];
        if (timeDifference.TotalMinutes < 60)
            return $"{(int)timeDifference.TotalMinutes} {localization["Minute"]}{(timeDifference.TotalMinutes >= 2 ? "s" : "")}";
        if (timeDifference.TotalHours < 24)
            return $"{(int)timeDifference.TotalHours} {localization["Hour"]}{(timeDifference.TotalHours >= 2 ? "s" : "")}";
        if (timeDifference.TotalDays < 30)
            return $"{(int)timeDifference.TotalDays} {localization["Day"]}{(timeDifference.TotalDays >= 2 ? "s" : "")}";

        return $"30+ {localization["Day"]}s";
    }

    public string GetAccStatus(bool? accStatus)
    {
        if (accStatus is null)
            return localization["AccUnknown"];
        
        return accStatus.Value ? localization["AccOn"] : localization["AccOff"];
    }
}
