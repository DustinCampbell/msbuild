# Localizing MSBuild

## Localizable resource structure

- Neutral resources: [*resx](https://github.com/search?utf8=%E2%9C%93&q=repo%3AMicrosoft%2Fmsbuild+extension%3Aresx&type=Code&ref=advsearch&l=&l=)
- shared resources live in `Microsoft.Build.Framework`'s `SR.resx` and are consumed by the other MSBuild assemblies through `SR.ResourceManager` (exposed via `AssemblyResources.SharedResources`)
- each neutral resource has a directory named `xlf` besides it which contains its localized strings in .xlf format
- there is one language per xlf
- the logical name for a resource is derived from its generated resource type. For example, the `Microsoft.Build` assembly's [`Resources/SR.resx`](../../src/Build/Resources/SR.resx) generates `Microsoft.Build.Resources.SR`, whose resource manager uses the base name `Microsoft.Build.Resources.SR` and embeds `Microsoft.Build.Resources.SR.resources`.

## How to edit a resource

- if you need to add / remove / update a resource, only do so in the neutral resx files. xlf files get automatically updated during localized builds.

## What a localized build does

- converts xlf files to localized resx files
- the localized resx files are generated into the `$(IntermediaryOutputPath)`
- produces satellite assemblies for each language
- satellite assemblies are used even on English machines. This is for testing purposes, to ensure that English builds are not different than non English builds

## Process for interacting with the localization team

- 3 weeks cadence for main, initiated by loc team
- on demand for main / release branches, initiated by msbuild team

## Contributing a better translation

- send a PR with an updated `<target>` element of the xlf resource (do not include other non-localization changes)
- we will notify the localization team, which will then take over and review the PR

## Localizing XSD "IntelliSense"

Code completion ("IntelliSense") for MSBuild project files is provided minimally in Visual Studio by XML Schema files like [`Microsoft.Build.CommonTypes.xsd`](https://github.com/dotnet/msbuild/blob/ba9a1d64a7abf15a8505827c00413156a3eb7f62/src/MSBuild/MSBuild/Microsoft.Build.CommonTypes.xsd). These files are English-only in the GitHub repo; their localization is managed in the Microsoft-internal `VS` repo.

### If there is a bug in XSD localization

File XSD localization bugs in this repo. The MSBuild team will coordinate with the Visual Studio localization team to redirect it appropriately.

### When an XSD has been updated

After updating an XSD in the GitHub repo, the MSBuild-to-VS-repo insertion process automatically updates the canonical Visual Studio copy of the XSD.
