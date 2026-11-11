namespace GcToolkit.Core.Tests.Geocaches.Listing;

/// <summary>Realistic listing snippets (English and Czech) shared by the scanner tests.</summary>
internal static class ListingFixtures
{
    public const string EnglishMultiIntro =
        "Welcome to my multi! Start at the church, N 49° 12.345 E 016° 34.567. " +
        "At stage 2 (D3.5/T2) count the windows and call me on +420 777 123 456 if lost. " +
        "Published 12.11.2026 at 14:30, the box costs $5.99 and was hidden in 2019.";

    public const string CzechMystery =
        "Vítejte u další mystery! Finální souřadnice jsou N 49° 1A.BCD E 016° 3E.FGH, " +
        "kde A = počet oken, B = rok postavení (poslední cifra). Cache je velikosti Small, " +
        "obtížnost 3 a terén 2,5. Zveřejněno 1. 5. 2024 v 9:00. Cena 150 Kč.";

    public const string CzechDecimalComma =
        "Parkovat můžete na N 50° 04,321' E 014° 25,678'. Pozor, cesta je blátivá.";

    public const string DmsWaypoint =
        "Waypoint 2: N 49° 12' 20.70\" E 016° 34' 34.02\" - look under the bench.";

    public const string DecimalDegreesPair =
        "Parking: 49.205750, 16.576117 (also N49.20575 E16.576117).";

    public const string UtmAndMgrs =
        "Grid refs: 33U 705083 5644673 and 33U VR 05083 44673.";

    public const string BritishGridRef =
        "Start at TG 51409 13177 on the OS map.";

    public const string ExpressionFormula =
        "Final: N 49° 1(A+B).C(D*2)E E 016° 3(F-1).GH(I*J)";

    public const string OrdinaryText =
        "Stage 2: Find the cache D3.5/T2. Call 777 123 456 or 603-555-1234. " +
        "Meeting 12.11.2026, 14:30-16:45. Price 12.50 EUR, v1.2.3 of the app. " +
        "Hint: look at N, E, S and W sides. Rating 4.5/5. Time 10.30.";
}
