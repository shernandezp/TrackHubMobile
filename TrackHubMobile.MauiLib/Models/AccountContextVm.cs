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

// Minimal projection of the Manager accountContext read. StatusId mirrors
// Common.Domain.Enums.AccountStatus: Trial=1, Active=2 (operational); Suspended=3, Cancelled=4,
// Archived=5 (non-operational).
public readonly record struct AccountContextVm(
    short StatusId
    );
