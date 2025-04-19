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
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Models;
using TrackHubMobile.Utils;

namespace TrackHubMobile.Services;

public sealed class Router(IGraphQLReader reader) : IRouter
{
    public async Task<IEnumerable<PositionVm>> GetDevicePositionsByUserAsync(CancellationToken cancellationToken)
    {
        const string query = @"
        query {
          devicePositionsByUser {
            attributes {
              temperature
              satellites
              mileage
              ignition
              hobbsMeter
            }
            altitude
            address
            deviceName
            transporterType
            state
            speed
            longitude
            latitude
            eventId
            transporterId
            deviceDateTime
            course
            country
            city
          }
        }";

        var response = await reader.ExecuteGraphQLQuery<IEnumerable<PositionVm>>(Constants.RouterUrl, query, "devicePositionsByUser", cancellationToken);
        return response ?? [];
    }
}
