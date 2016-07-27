nuget pack "%~d0%~p0\Topshelf.Diagnostics\Topshelf.Diagnostics.csproj" -Build -Properties Configuration=Release
nuget push Topshelf.Diagnostics.*.nupkg
