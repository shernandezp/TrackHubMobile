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

/// <summary>
/// The Router class provides methods to interact with GraphQL APIs for retrieving device position data.
/// </summary>
public sealed class Router(IGraphQLReader reader) : IRouter
{
    /// <summary>
    /// Retrieves a list of device positions associated with the current user.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
    /// <returns>A collection of <see cref="PositionVm"/> objects representing device positions.</returns>
    public async Task<IEnumerable<PositionVm>> GetDevicePositionsByUserAsync(CancellationToken cancellationToken)
    {
        // GraphQL query to fetch device positions by user
        const string query = @"
        query {
          devicePositionsByUser {
            deviceName
            transporterType
            speed
            transporterId
            deviceDateTime
            longitude
            latitude
          }
        }";

        // Execute the query and return the result
        var response = await reader.ExecuteGraphQLQuery<IEnumerable<PositionVm>>(Constants.RouterUrl, query, "devicePositionsByUser", cancellationToken);
        return response ?? [];
    }

    /// <summary>
    /// Retrieves the position of a specific device by its transporter ID.
    /// </summary>
    /// <param name="transporterId">The unique identifier of the transporter.</param>
    /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
    /// <returns>A <see cref="PositionVm"/> object representing the device position.</returns>
    public async Task<PositionVm> GetDeviceAsync(Guid transporterId, CancellationToken cancellationToken)
    {
        // GraphQL query to fetch a device position by transporter ID
        string query = $@"
        query {{
          devicePositionByTransporter(query: {{ transporterId: ""{transporterId}"" }}) {{
            attributes {{
              temperature
              satellites
              mileage
              ignition
              hourmeter
            }}
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
          }}
        }}";

        // Execute the query and return the result
        var response = await reader.ExecuteGraphQLQuery<PositionVm>(Constants.RouterUrl, query, "devicePositionByTransporter", cancellationToken);
        return response;
    }

    /// <summary>
    /// Retrieves the trips of a transporter within a date range.
    /// </summary>
    /// <param name="transporterId">The unique identifier of the transporter.</param>
    /// <param name="from">Range start (inclusive).</param>
    /// <param name="to">Range end (inclusive).</param>
    /// <param name="source">Optional history source enum literal (STORED or PROVIDER). Omitted when null.</param>
    /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
    /// <returns>A <see cref="GraphQLResult{T}"/> wrapping the trips or the first GraphQL error.</returns>
    public async Task<GraphQLResult<IEnumerable<TripVm>>> GetTripsByTransporterAsync(
        Guid transporterId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? source,
        CancellationToken cancellationToken)
    {
        string query = $@"
        query {{
          tripsByTransporter(query: {{ transporterId: ""{transporterId}"", from: ""{from:o}"", to: ""{to:o}""{FormatSourceArgument(source)} }}) {{
            averageSpeed
            duration
            totalDistance
            tripId
            type
            from
            to
            points {{
              course
              deviceDateTime
              eventId
              latitude
              longitude
              speed
            }}
          }}
        }}";

        return await reader.ExecuteGraphQLQueryWithErrors<IEnumerable<TripVm>>(Constants.RouterUrl, query, "tripsByTransporter", cancellationToken);
    }

    /// <summary>
    /// Retrieves the raw positions of a transporter within a date range.
    /// </summary>
    /// <param name="transporterId">The unique identifier of the transporter.</param>
    /// <param name="from">Range start (inclusive).</param>
    /// <param name="to">Range end (inclusive).</param>
    /// <param name="source">Optional history source enum literal (STORED or PROVIDER). Omitted when null.</param>
    /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
    /// <returns>A <see cref="GraphQLResult{T}"/> wrapping the positions or the first GraphQL error.</returns>
    public async Task<GraphQLResult<IEnumerable<PositionVm>>> GetPositionsByTransporterAsync(
        Guid transporterId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? source,
        CancellationToken cancellationToken)
    {
        string query = $@"
        query {{
          positionsByTransporter(query: {{ transporterId: ""{transporterId}"", from: ""{from:o}"", to: ""{to:o}""{FormatSourceArgument(source)} }}) {{
            deviceName
            transporterType
            speed
            transporterId
            deviceDateTime
            longitude
            latitude
            course
            address
            city
            state
            country
          }}
        }}";

        return await reader.ExecuteGraphQLQueryWithErrors<IEnumerable<PositionVm>>(Constants.RouterUrl, query, "positionsByTransporter", cancellationToken);
    }

    // The source argument is a GraphQL enum literal (unquoted) and only included when provided
    private static string FormatSourceArgument(string? source)
        => string.IsNullOrEmpty(source) ? string.Empty : $", source: {source}";
}
