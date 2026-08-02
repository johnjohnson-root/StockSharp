# Local NuGet mirror

This folder is a local NuGet feed,
registered as a package source in the repository's `NuGet.config`.
It ships empty;
populate it with:

```
./tools/mirror-packages.sh
```

The script copies the complete `.nupkg` closure of both solutions here.
With the folder populated,
the build survives any upstream package being unpublished or delisted,
and can restore fully offline:

```
dotnet restore StockSharp.slnx --source ./nuget-mirror
```

`.gitignore` keeps the `.nupkg` files out of the repository by design,
so keep a populated mirror somewhere durable instead:
the `mirror.yml` workflow produces one as a downloadable CI artifact,
or attach the folder to a GitHub release.
