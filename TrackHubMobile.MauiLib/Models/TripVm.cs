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

namespace TrackHubMobile.Models;

/// <summary>
/// A trip segment returned by the Router tripsByTransporter query.
/// TripId is a Guid and Duration an ISO 8601 duration string (e.g. "PT1H5M"),
/// matching the Router GraphQL serialization.
/// </summary>
public readonly record struct TripVm(
    Guid TripId,
    short Type,
    DateTimeOffset From,
    DateTimeOffset To,
    double TotalDistance,
    string? Duration,
    double AverageSpeed,
    List<TripPointVm>? Points
    );
