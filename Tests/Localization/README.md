Run from the repository root with the .NET 9 SDK:

```powershell
dotnet run --project Tests/Localization/LocalizationTests.csproj
```

The executable checks the actual CSV importer/exporter without requiring a Unity installation: Unicode and multiline round trips, BOM, escaped quotes, semicolon delimiters, merge behavior, empty translation fallback, invalid rows, duplicate keys/languages/sources, stable source protection, and format placeholders. It also validates and round-trips the game's complete localization table.

In Unity, open **Tools → Football → Localization**, then start the **Menu** scene. Select `ru` or `en` under **Play Mode language**. Check menu buttons, player input labels, mobile controls (with Mobile Input Simulation enabled), tutorial steps, matchmaking states, match HUD and the result modal. Change language while each screen is visible and verify formatted numbers and control hints are retained.

Translations are stored in `Assets/Resources/FootballLocalization.json`. CSV uses UTF-8 with BOM and the header `key,source,ru,en,...`. Import merges existing keys and adds languages; it preserves entries and languages absent from the file. Source text is immutable for existing keys. Edit translation columns and press **Save** to apply imported or edited values. An empty translation falls back to the default language, then the source text.

For new static TMP labels, add **FootballLocalizedText** and set **Key** to a table key. Existing scene labels are bound automatically by their source text. Dynamically instantiated UI should use `FootballLocalization.BindHierarchy(root)` or have the component on its prefab. Dynamic scripts can use `FootballLocalization.SetText(label, source, args)` to retain formatting across language changes. `FootballLocalization.SetLanguage("en")` changes and saves the player's language preference.
