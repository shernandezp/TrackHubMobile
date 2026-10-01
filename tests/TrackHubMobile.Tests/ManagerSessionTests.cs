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

using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Models;
using TrackHubMobile.Services;

namespace TrackHubMobile.Tests;

[TestFixture]
public class ManagerSessionTests
{
    private sealed class CountingReader : IGraphQLReader
    {
        public int Calls { get; private set; }

        public AccountSettingsVm Settings { get; set; }

        public Task<T?> ExecuteGraphQLQuery<T>(string url, string query, string rootFieldName, CancellationToken cancellationToken)
        {
            Calls++;
            object? answer = rootFieldName switch
            {
                "accountSettingsByUser" => (AccountSettingsVm?)Settings,
                "accountContext" => (AccountContextVm?)new AccountContextVm(2, "America/Bogota"),
                _ => null,
            };
            return Task.FromResult((T?)answer);
        }

        public Task<GraphQLResult<T>> ExecuteGraphQLQueryWithErrors<T>(string url, string query, string rootFieldName, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    [Test]
    public async Task SigningOut_ForgetsTheAccountsSettings()
    {
        var reader = new CountingReader { Settings = new AccountSettingsVm(Guid.NewGuid(), "OpenStreetMap", "key-a", 30, true, 10) };
        var manager = new Manager(reader);

        await manager.GetAccountSettingsAsync(CancellationToken.None);
        await manager.GetAccountSettingsAsync(CancellationToken.None);
        Assert.That(reader.Calls, Is.EqualTo(1), "cached for the session");

        manager.ResetSession();
        reader.Settings = new AccountSettingsVm(Guid.NewGuid(), "OpenStreetMap", "key-b", 60, true, 20);
        var next = await manager.GetAccountSettingsAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(reader.Calls, Is.EqualTo(2));
            Assert.That(next!.Value.MapsKey, Is.EqualTo("key-b"));
        });
    }

    [Test]
    public async Task TheAccountZone_IsReadFromTheAccount()
    {
        var zone = await new Manager(new CountingReader()).GetAccountTimeZoneAsync(CancellationToken.None);

        Assert.That(zone.Id, Is.EqualTo("America/Bogota"));
    }
}
