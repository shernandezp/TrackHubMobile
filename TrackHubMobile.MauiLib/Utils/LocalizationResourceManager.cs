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

using System.Globalization;
using System.Resources;
using TrackHubMobile.Interfaces;
using TrackHubMobile.Resources;

namespace TrackHubMobile.Utils;

public class LocalizationResourceManager : ILocalizationResourceManager
{
    private readonly ResourceManager _resourceManager;

    public LocalizationResourceManager()
    {
        _resourceManager = new ResourceManager(typeof(AppResources));
    }

    public string this[string key] => _resourceManager.GetString(key, CultureInfo.CurrentCulture) ?? string.Empty;
}
