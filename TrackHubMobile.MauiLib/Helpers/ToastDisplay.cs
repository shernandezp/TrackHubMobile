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

using CommunityToolkit.Maui.Alerts;
using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Messages;

namespace TrackHubMobile.Helpers;

public class ToastDisplay : IToastDisplay
{
    public void Initialize()
    {
        WeakReferenceMessenger.Default.Register<ToastMessage>(this, async (r, msg) =>
        {
            string text = msg.Value.Message;
            bool isError = msg.Value.IsError;

            var toast = Toast.Make(text, CommunityToolkit.Maui.Core.ToastDuration.Short);
            await toast.Show();
        });
    }
}
