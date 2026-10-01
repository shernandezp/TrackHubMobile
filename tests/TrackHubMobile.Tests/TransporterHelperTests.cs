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

using System.Globalization;
using TrackHubMobile.Helpers;
using TrackHubMobile.Interfaces.Helpers;

namespace TrackHubMobile.Tests;

[TestFixture]
public class TransporterHelperTests
{
    private sealed class Spanish : ILocalizationResourceManager
    {
        public string this[string key] => key switch
        {
            "TransporterTypeCargoContainer" => "Contenedor de carga",
            "TransporterTypeTruck" => "Camión",
            _ => string.Empty,
        };
    }

    private readonly TransporterHelper helper = new(new Spanish());

    [TestCase("CARGO_CONTAINER")]
    [TestCase("CargoContainer")]
    public void TheTransporterType_IsShownByItsLocalizedName(string raw)
        => Assert.That(helper.GetTransporterTypeName(raw), Is.EqualTo("Contenedor de carga"));

    [Test]
    public void AnUnknownType_IsShownAsReceived()
        => Assert.That(helper.GetTransporterTypeName("HOVERCRAFT"), Is.EqualTo("HOVERCRAFT"));

    [Test]
    public void AKnownTypeWithoutATranslation_IsShownAsReceived()
        => Assert.That(helper.GetTransporterTypeName("BOAT"), Is.EqualTo("BOAT"));

    [TestCase("en", "Truck")]
    [TestCase("es", "Camión")]
    public void EveryTransporterType_IsTranslatedInEveryShippedLanguage(string culture, string truck)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            var shipped = new TransporterHelper(new LocalizationResourceManager());
            Assert.Multiple(() =>
            {
                Assert.That(shipped.GetTransporterTypeName("TRUCK"), Is.EqualTo(truck));
                foreach (var type in AllTypes)
                {
                    Assert.That(shipped.GetTransporterTypeName(type), Is.Not.EqualTo(type), type);
                }
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static readonly string[] AllTypes =
    [
        "AIRCRAFT", "ASSET", "BICYCLE", "BOAT", "CAR", "CARGO_CONTAINER", "CONSTRUCTION_VEHICLE", "CHILD",
        "DELIVERY_VAN", "DRONE", "ELDERLY_PERSON", "FLEET_VEHICLE", "HEAVY_EQUIPMENT", "LIVESTOCK", "MOTORCYCLE",
        "PACKAGE", "PERSON", "PET", "SCHOOL_BUS", "SCOOTER", "TAXI", "TOOL", "TRUCK", "TRACTOR"
    ];
}
