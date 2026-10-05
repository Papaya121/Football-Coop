Run from the project root:

```powershell
dotnet run --project Tests/PlayerProfile/PlayerProfileTests.csproj
```

Uses the actual nickname, profile, service and repository code with minimal Unity stubs. Checks validation, persistence calls, change events and rating preservation, including renaming during a rated match. Network transport and Mirror weaving require Unity.
